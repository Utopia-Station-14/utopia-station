using Content.Server._CE.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Light.EntitySystems;

/// <summary>
/// Applies `starlight` to space maps while preserving explicit per-map light setups.
/// </summary>
public sealed partial class SpaceLightSystem : EntitySystem
{
    // This really just exists to avoid doing mapping changes for all of them.

    private Color _spaceLightColor;
    private readonly HashSet<MapId> _spaceLightMaps = new();

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!; // Utopia-Tweak : ZLevels
    [Dependency] private CEZLevelsSystem _zLevel = default!; // Utopia-Tweak : ZLevels

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.SpaceLightColor, OnSpaceLightColorChanged, true);

        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<MapRemovedEvent>(OnMapRemoved);
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        if (!_map.TryGetMap(ev.Map, out var mapUid))
            return;

        // Utopia-Tweak : ZLevels
        if (!HasComp<MapLightComponent>(mapUid.Value))
        {
            _spaceLightMaps.Add(ev.Map);
            _map.SetAmbientLight(ev.Map, _spaceLightColor);
        }

        ZLevelLightApply(ev.Map);
        // Utopia-Tweak : ZLevels
    }

    private void OnMapRemoved(MapRemovedEvent ev)
    {
        _spaceLightMaps.Remove(ev.MapId);
    }

    private void OnSpaceLightColorChanged(string value)
    {
        _spaceLightColor = ParseSpaceLightColor(value);

        foreach (var mapId in _spaceLightMaps)
        {
            if (!_map.MapExists(mapId))
                continue;

            _map.SetAmbientLight(mapId, _spaceLightColor);
        }
    }

    private static Color ParseSpaceLightColor(string value)
    {
        if (!Color.TryFromHex(value, out var color))
            color = Color.FromHex(CCVars.DefaultSpaceLightColor);

        return Color.FromSrgb(color);
    }

    // Utopia-Tweak : ZLevels
    private void ZLevelLightApply(MapId mapId)
    {
        if (!_map.TryGetMap(mapId, out var mapUid))
            return;

        if (!_zLevel.TryGetMapNetwork(mapUid.Value, out var zLevels))
            return;

        foreach (var zMapUid in zLevels.Comp.SortedZLevels)
        {
            var zMapId = _transform.GetMapId(zMapUid);
            if (zMapId == mapId || !_map.MapExists(zMapId))
                continue;

            if (HasComp<MapLightComponent>(zMapUid))
                continue;

            _spaceLightMaps.Add(zMapId);
            _map.SetAmbientLight(zMapId, _spaceLightColor);
        }
    }
    // Utopia-Tweak : ZLevels

}
