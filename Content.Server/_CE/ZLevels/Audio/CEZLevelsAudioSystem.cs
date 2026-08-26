// Ported from ColonialMarinesUniverse Content.Server/_CMU14/ZLevels/Core/CMUZLevelsSystem.Audio.cs.
// Remaded for Utopia Station

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Audio;

public sealed partial class CEZLevelsAudioSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private CEZLevelOpeningCache _openingCache = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    private const float CrossZAudioOpeningRadius = 1.5f;

    private readonly HashSet<EntityUid> _processed = new();
    private readonly HashSet<EntityUid> _projections = new();
    private readonly Dictionary<EntityUid, List<EntityUid>> _projectionsBySource = new();
    private readonly List<Entity<MapGridComponent>> _openingGridScratch = new();
    private readonly List<(Vector2 Center, float Distance)> _openingCenters = new();

    [Dependency] private EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    private bool _crossZAudioEnabled = true;
    private bool _creatingProjection;
    private bool _debug;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, UCCVars.CEZLevelsCrossZAudio, OnCrossZAudioChanged, true);
        Subs.CVar(_config, UCCVars.CEZLevelsCrossZAudioDebug, v => _debug = v, true);

        SubscribeLocalEvent<AudioComponent, MoveEvent>(OnAudioMove);
        SubscribeLocalEvent<AudioComponent, MapInitEvent>(OnAudioMapInit);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
    }

    private void OnCrossZAudioChanged(bool enabled)
    {
        _crossZAudioEnabled = enabled;
        if (enabled)
            return;

        foreach (var projections in _projectionsBySource.Values)
        {
            foreach (var projection in projections)
            {
                _projections.Remove(projection);
                if (!TerminatingOrDeleted(projection))
                    QueueDel(projection);
            }
        }

        _projectionsBySource.Clear();
        _processed.Clear();
    }

    private void OnAudioMove(Entity<AudioComponent> ent, ref MoveEvent args)
    {
        if (_processed.Remove(ent) && _projectionsBySource.Remove(ent, out var stale))
        {
            foreach (var projection in stale)
            {
                _projections.Remove(projection);
                if (!TerminatingOrDeleted(projection))
                    QueueDel(projection);
            }
        }

        TryProject(ent, args.Component);
    }

    private void OnAudioMapInit(Entity<AudioComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out TransformComponent? xform))
            return;

        TryProject(ent, xform);
    }

    private void OnAudioShutdown(Entity<AudioComponent> ent, ref ComponentShutdown args)
    {
        _processed.Remove(ent);
        _projections.Remove(ent);

        if (_projectionsBySource.Remove(ent, out var projections))
        {
            foreach (var projection in projections)
            {
                _projections.Remove(projection);
                if (!TerminatingOrDeleted(projection))
                    QueueDel(projection);
            }
        }
    }

    private void TryProject(Entity<AudioComponent> ent, TransformComponent xform)
    {
        if (_creatingProjection || _projections.Contains(ent) || !_crossZAudioEnabled)
            return;

        if (ent.Comp.Global || ent.Comp.IncludedEntities != null || string.IsNullOrEmpty(ent.Comp.FileName))
            return;

        if (xform.MapUid is not { } sourceMap)
        {
            if (_debug)
                Log.Info($"[crossz-audio] {ToPrettyString(ent)} skipped: no MapUid (file={ent.Comp.FileName})");
            return;
        }

        if (!_zMapQuery.TryComp(sourceMap, out var sourceZMap) || !_mapQuery.TryComp(sourceMap, out var sourceMapComp))
            return;

        if (!_processed.Add(ent))
            return;

        var sourcePosition = _transform.GetWorldPosition(xform);
        if (_debug)
        {
            Log.Info($"[crossz-audio] {ToPrettyString(ent)} ENTER: file={ent.Comp.FileName} map={ToPrettyString(sourceMap)} grid={(xform.GridUid is { } g ? ToPrettyString(g) : "null")} pos={sourcePosition} MaxDistance={ent.Comp.Params.MaxDistance}");
        }

        ProjectCrossZAudio((ent.Owner, ent.Comp), sourceMapComp.MapId, sourcePosition);
    }

    private void ProjectCrossZAudio(
        Entity<AudioComponent> source,
        MapId sourceMapId,
        Vector2 sourcePosition)
    {
        if (source.Comp.Params.MaxDistance <= 0f)
        {
            if (_debug)
                Log.Info($"[crossz-audio] {ToPrettyString(source)} bail: MaxDistance<=0");
            return;
        }

        ResolvedSoundSpecifier? specifier = null;
        ProjectDirection(source, sourceMapId, sourcePosition, ref specifier, -1);
        ProjectDirection(source, sourceMapId, sourcePosition, ref specifier, +1);
    }

    private void ProjectDirection(
        Entity<AudioComponent> source,
        MapId sourceMapId,
        Vector2 sourcePosition,
        ref ResolvedSoundSpecifier? specifier,
        int step)
    {
        var currentMap = GetMapEntityForMapId(sourceMapId);
        if (currentMap == null)
            return;

        var currentMapId = sourceMapId;

        for (var depth = step; Math.Abs(depth) <= CESharedZLevelsSystem.MaxZLevelsBelowRendering; depth += step)
        {
            if (step < 0)
            {
                if (!HasOpeningNear(currentMapId, sourcePosition))
                {
                    if (_debug)
                        Log.Info($"[crossz-audio]   depth={depth}: no opening in floor of {ToPrettyString(currentMap)} near {sourcePosition}");
                    return;
                }
            }

            if (!TryResolveAdjacentMap(currentMap.Value, step, out var nextMap) ||
                !_mapQuery.TryComp(nextMap, out var nextMapComp))
            {
                if (_debug)
                    Log.Info($"[crossz-audio]   depth={depth}: no map at that offset");
                return;
            }

            if (step > 0)
            {
                if (!HasOpeningNear(nextMapComp.MapId, sourcePosition))
                {
                    if (_debug)
                        Log.Info($"[crossz-audio]   depth={depth}: no opening in floor of {ToPrettyString(nextMap)} near {sourcePosition}");
                    return;
                }
            }

            specifier ??= new ResolvedPathSpecifier(source.Comp.FileName!);
            CreateProjection(source, specifier, nextMap.Value, sourcePosition);
            if (_debug)
                Log.Info($"[crossz-audio]   depth={depth}: PROJECTED {source.Comp.FileName} to {ToPrettyString(nextMap)} @ {sourcePosition}");

            currentMap = nextMap;
            currentMapId = nextMapComp.MapId;
        }
    }

    private EntityUid? GetMapEntityForMapId(MapId mapId)
    {
        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var uid, out var mapComp))
        {
            if (mapComp.MapId == mapId)
                return uid;
        }
        return null;
    }

    private bool TryResolveAdjacentMap(EntityUid map, int offset, [NotNullWhen(true)] out EntityUid? nextMap)
    {
        nextMap = null;
        if (!_zMapQuery.TryComp(map, out var mapComp))
            return false;

        if (!_zLevels.TryMapOffset((map, mapComp), offset, out var targetMap))
            return false;

        nextMap = targetMap;
        return true;
    }

    private bool HasOpeningNear(MapId mapId, Vector2 sourcePosition)
    {
        _openingCenters.Clear();
        _openingGridScratch.Clear();

        _openingCache.FindOpeningCentersNear(
            mapId,
            sourcePosition,
            CrossZAudioOpeningRadius,
            _openingCenters,
            _openingGridScratch,
            _map,
            _transform,
            _tileDefinition,
            true);

        return _openingCenters.Count > 0;
    }

    private void CreateProjection(
        Entity<AudioComponent> source,
        ResolvedSoundSpecifier specifier,
        EntityUid targetMap,
        Vector2 sourcePosition)
    {
        _creatingProjection = true;
        try
        {
            var projectedAudio = _audio.PlayPvs(specifier, new EntityCoordinates(targetMap, sourcePosition), source.Comp.Params);
            if (projectedAudio is not { } projected)
                return;

            _projections.Add(projected.Entity);
            projected.Component.Flags = source.Comp.Flags;
            Dirty(projected.Entity, projected.Component);

            if (!_projectionsBySource.TryGetValue(source.Owner, out var list))
            {
                list = new List<EntityUid>();
                _projectionsBySource[source.Owner] = list;
            }
            list.Add(projected.Entity);
        }
        finally
        {
            _creatingProjection = false;
        }
    }
}
