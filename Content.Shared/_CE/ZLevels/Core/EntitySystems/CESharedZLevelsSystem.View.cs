/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
*/

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Actions;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] protected readonly ITileDefinitionManager TilDefMan = default!;
    [Dependency] protected readonly IMapManager MapManager = default!; // Utopia-Tweak : ZLevels

    private void InitView()
    {
        SubscribeLocalEvent<CEZLevelViewerComponent, MoveEvent>(OnViewerMove);
        SubscribeLocalEvent<CEZLevelViewerComponent, CEToggleZLevelLookUpAction>(OnToggleLookUp);
    }

    protected virtual void OnViewerMove(Entity<CEZLevelViewerComponent> ent, ref MoveEvent args)
    {
        // Utopia-Tweak : ZLevels
        if (HasComp<GhostComponent>(ent.Owner))
            return;
        // Utopia-Tweak : ZLevels

        if (!ent.Comp.LookUp)
            return;

        if (!HasOpaqueAbove(ent))
            return;

        ent.Comp.LookUp = false;
        DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.LookUp));
    }

    private void OnToggleLookUp(Entity<CEZLevelViewerComponent> ent, ref CEToggleZLevelLookUpAction args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasOpaqueAbove(ent) && !HasComp<GhostComponent>(ent.Owner)) // Utopia-Tweak : ZLevels
        {
            _popup.PopupClient(Loc.GetString("ce-zlevel-look-up-fail"), ent, ent);
            return;
        }

        ent.Comp.LookUp = !ent.Comp.LookUp;
        DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.LookUp));
    }

    public bool HasOpaqueAbove(EntityUid ent, Entity<CEZLevelMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        var worldPos = _transform.GetWorldPosition(ent); // Utopia-Tweak : ZLevels

        if (!MapManager.TryFindGridAt(mapAboveUid.Value.Owner, worldPos, out var gridAboveUid, out var gridAboveComp)) // Utopia-Tweak : ZLevels
            return false;

        if (!MapSys.TryGetTileRef(gridAboveUid, gridAboveComp, worldPos, out var tileRef)) // Utopia-Tweak : ZLevels
            return false;

        var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId];

        return !tileDef.Transparent;
    }
}

public sealed partial class CEToggleZLevelLookUpAction : InstantActionEvent
{
}
