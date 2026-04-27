using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Content.Server.Chat.Managers;
using Robust.Server.GameObjects;

namespace Content.Server._Utopia.ZLevels;

public sealed class ZTileEventSystem : EntitySystem
{
    private const string ZAtmosEntity = "UtopiaZLevelGasTransfer";
    private const string ZTileID = "UtopiaSpace";

    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapGridComponent, TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(Entity<MapGridComponent> ent, ref TileChangedEvent args)
    {
        var gridUid = ent.Owner;

        if (!TryGetZMap(gridUid, out var mapEntity))
            return;

        foreach (var change in args.Changes)
        {
            if (!_tileDef.TryGetDefinition(change.NewTile.TypeId, out var newDef))
                continue;

            if (!_tileDef.TryGetDefinition(change.OldTile.TypeId, out var oldDef))
                continue;

            if (oldDef.ID != ZTileID && newDef.ID == ZTileID)
                SpawnSpace(ent, change.GridIndices, mapEntity);
            else if (oldDef.ID == ZTileID && newDef.ID != ZTileID)
                DeleteSpace(ent, change.GridIndices, mapEntity);
        }
    }

    private bool TryGetZMap(EntityUid gridUid, out Entity<CEZLevelMapComponent?> mapEntity)
    {
        mapEntity = default;

        if (!TryComp(gridUid, out TransformComponent? gridXform))
            return false;

        if (gridXform.MapUid is not { } mapUid)
            return false;

        if (!TryComp(mapUid, out CEZLevelMapComponent? zMap))
            return false;

        mapEntity = new(mapUid, zMap);
        return true;
    }

    private EntityCoordinates GetMapCoordinates(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var local = _mapSystem.GridTileToLocal(gridUid, grid, tile);
        var mapCoords = _transform.ToMapCoordinates(local);
        var mapUid = Transform(gridUid).MapUid!.Value;

        return new EntityCoordinates(mapUid, mapCoords.Position);
    }

    private void SpawnSpace(Entity<MapGridComponent> ent, Vector2i tile, Entity<CEZLevelMapComponent?> mapEntity)
    {
        var grid = ent.Comp;
        var gridUid = ent.Owner;

        if (!_zLevels.TryMapOffset(mapEntity, -1, out var below) || below == null)
            return;

        var belowMap = below.Value.Owner;

        var mapCoords = GetMapCoordinates(gridUid, grid, tile);
        var belowCoords = new EntityCoordinates(belowMap, mapCoords.Position);

        var ents = _lookup.GetEntitiesInRange(belowCoords, 0.25f);
        foreach (var e in ents)
        {
            if (MetaData(e).EntityPrototype?.ID == ZAtmosEntity)
                return;
        }

        Spawn(ZAtmosEntity, mapCoords);
        Spawn(ZAtmosEntity, belowCoords);
    }

    private void DeleteSpace(Entity<MapGridComponent> ent, Vector2i tile, Entity<CEZLevelMapComponent?> mapEntity)
    {
        var grid = ent.Comp;
        var gridUid = ent.Owner;

        if (!_zLevels.TryMapOffset(mapEntity, -1, out var below) || below == null)
            return;

        var belowMap = below.Value.Owner;

        var mapCoords = GetMapCoordinates(gridUid, grid, tile);
        var belowCoords = new EntityCoordinates(belowMap, mapCoords.Position);

        var ents = _lookup.GetEntitiesInRange(belowCoords, 0.25f);
        foreach (var e in ents)
        {
            if (MetaData(e).EntityPrototype?.ID == ZAtmosEntity)
                Del(e);
        }
    }
}