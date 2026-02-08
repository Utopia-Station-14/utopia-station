using Content.Server.NodeContainer.Nodes;
using Content.Server._Utopia.ZLevels.Pipes.Nodes;
using Content.Shared._Utopia.ZLevels.Pipes.Components;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Pipes.Systems;

public sealed class ZPipeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<ZPipeNode, HashSet<ZPipeNode>> _nodeConnections = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ZPipeComponent, ComponentStartup>(OnZPipeStartup);
        SubscribeLocalEvent<ZPipeComponent, MoveEvent>(OnZPipeMove);
        SubscribeLocalEvent<ZLevelEntityLinkComponent, ComponentStartup>(OnLinkStartup);
    }

    private void OnZPipeStartup(EntityUid uid, ZPipeComponent comp, ComponentStartup args) => TryLink(uid);
    private void OnZPipeMove(EntityUid uid, ZPipeComponent comp, ref MoveEvent args) => TryLink(uid);

    private void OnLinkStartup(EntityUid uid, ZLevelEntityLinkComponent comp, ComponentStartup args)
    {
        if (HasComp<ZPipeComponent>(uid))
            TryLink(uid);
    }

    public void TryLink(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform)
            || !TryComp(uid, out ZLevelEntityLinkComponent? link)
            || !TryComp(uid, out NodeContainerComponent? cont)
            || xform.GridUid is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid))
        {
            return;
        }

        foreach (var zNode in GetZNodes(cont))
            ClearConnections(zNode);

        var searchBox = GetFullTileWorldBox(gridUid, grid, xform.Coordinates.Position);

        foreach (var zNode in GetZNodes(cont))
        {
            (EntityUid? targetMap, ZPipeDirection? requiredDir) = zNode.ZDirection switch
            {
                ZPipeDirection.Up => (link.AboveMap, ZPipeDirection.Down),
                ZPipeDirection.Down => (link.BelowMap, ZPipeDirection.Up),
                _ => ((EntityUid?) null, (ZPipeDirection?) null)
            };

            if (targetMap is { } map && requiredDir is { } dir)
                TryConnectAt(uid, zNode, searchBox, map, dir);
        }
    }

    /// <summary>
    /// Бокс в мировых координатах на весь тайл, в котором лежит позиция (grid local).
    /// </summary>
    private Box2 GetFullTileWorldBox(EntityUid gridUid, MapGridComponent grid, Vector2 localPos)
    {
        var tileSize = grid.TileSize;
        var tileX = (int) MathF.Floor(localPos.X / tileSize);
        var tileY = (int) MathF.Floor(localPos.Y / tileSize);
        var gridWorld = _transform.GetWorldPosition(gridUid);
        var origin = gridWorld + new Vector2(tileX * tileSize, tileY * tileSize);
        return new Box2(origin, origin + new Vector2(tileSize, tileSize));
    }

    private void ClearConnections(ZPipeNode node)
    {
        if (!_nodeConnections.Remove(node, out var connections))
            return;

        foreach (var other in connections)
        {
            node.RemoveAlwaysReachable(other);
            other.RemoveAlwaysReachable(node);
            if (_nodeConnections.TryGetValue(other, out var otherSet))
            {
                otherSet.Remove(node);
                if (otherSet.Count == 0)
                    _nodeConnections.Remove(other);
            }
        }
    }

    private void TryConnectAt(
        EntityUid uid,
        ZPipeNode selfNode,
        Box2 worldBox,
        EntityUid targetMapUid,
        ZPipeDirection requiredDir)
    {
        if (!TryComp(targetMapUid, out MapComponent? mapComp) || mapComp.MapId == MapId.Nullspace)
            return;

        foreach (var ent in _lookup.GetEntitiesIntersecting(mapComp.MapId, worldBox, LookupFlags.All))
        {
            if (ent == uid || !TryComp(ent, out NodeContainerComponent? otherCont))
                continue;

            foreach (var node in otherCont.Nodes.Values)
            {
                if (node is not ZPipeNode otherZ || otherZ.ZDirection != requiredDir)
                    continue;
                if (_nodeConnections.TryGetValue(selfNode, out var selfSet) && selfSet.Contains(otherZ))
                    continue;

                AddConnection(selfNode, otherZ);
            }
        }
    }

    private void AddConnection(ZPipeNode a, ZPipeNode b)
    {
        a.AddAlwaysReachable(b);
        b.AddAlwaysReachable(a);
        GetOrAddConnections(a).Add(b);
        GetOrAddConnections(b).Add(a);
    }

    private HashSet<ZPipeNode> GetOrAddConnections(ZPipeNode node)
    {
        if (!_nodeConnections.TryGetValue(node, out var set))
        {
            set = [];
            _nodeConnections[node] = set;
        }
        return set;
    }

    private static IEnumerable<ZPipeNode> GetZNodes(NodeContainerComponent cont)
    {
        foreach (var node in cont.Nodes.Values)
        {
            if (node is ZPipeNode z)
                yield return z;
        }
    }
}
