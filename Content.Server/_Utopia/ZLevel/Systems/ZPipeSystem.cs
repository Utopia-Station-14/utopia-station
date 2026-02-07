using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server._Utopia.ZLevels.Pipes.Nodes;
using Content.Shared._Utopia.ZLevels.Pipes.Components;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Pipes.Systems;

public sealed class ZPipeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroupSystem = default!;

    private readonly Dictionary<ZPipeNode, HashSet<ZPipeNode>> _nodeConnections = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ZPipeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZPipeComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<ZLevelEntityLinkComponent, ComponentStartup>(OnLinkStartup);
    }

    private void OnStartup(EntityUid uid, ZPipeComponent comp, ComponentStartup args)
    {
        TryLink(uid);
    }

    private void OnMove(EntityUid uid, ZPipeComponent comp, ref MoveEvent args)
    {
        TryLink(uid);
    }

    private void OnLinkStartup(EntityUid uid, ZLevelEntityLinkComponent comp, ComponentStartup args)
    {
        if (HasComp<ZPipeComponent>(uid))
        {
            TryLink(uid);
        }
    }

    private IEnumerable<ZPipeNode> GetZNodes(NodeContainerComponent cont)
    {
        foreach (var node in cont.Nodes.Values)
        {
            if (node is ZPipeNode z)
                yield return z;
        }
    }

    public void TryLink(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        if (!TryComp(uid, out ZLevelEntityLinkComponent? link))
            return;

        if (!TryComp(uid, out NodeContainerComponent? cont))
            return;

        if (xform.GridUid is not EntityUid gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        foreach (var zNode in GetZNodes(cont))
        {
            ClearConnections(zNode);
        }

        var tileSize = grid.TileSize;

        var gridPos = xform.Coordinates.Position;
        var tileX = (int)MathF.Floor(gridPos.X / tileSize);
        var tileY = (int)MathF.Floor(gridPos.Y / tileSize);

        foreach (var zNode in GetZNodes(cont))
        {
            if (zNode.ZDirection == ZPipeDirection.Up)
            {
                TryConnectAt(uid, zNode, tileX, tileY, link.AboveMap, ZPipeDirection.Down);
            }

            if (zNode.ZDirection == ZPipeDirection.Down)
            {
                TryConnectAt(uid, zNode, tileX, tileY, link.BelowMap, ZPipeDirection.Up);
            }
        }
    }

    private void ClearConnections(ZPipeNode node)
    {
        if (!_nodeConnections.TryGetValue(node, out var connections))
            return;

        foreach (var connectedNode in connections)
        {
            node.RemoveAlwaysReachable(connectedNode);
            connectedNode.RemoveAlwaysReachable(node);
            
            if (_nodeConnections.TryGetValue(connectedNode, out var otherConnections))
            {
                otherConnections.Remove(node);
                if (otherConnections.Count == 0)
                {
                    _nodeConnections.Remove(connectedNode);
                }
            }
        }

        _nodeConnections.Remove(node);
    }

    private void TryConnectAt(
        EntityUid uid,
        ZPipeNode selfNode,
        int tileX,
        int tileY,
        EntityUid? targetMap,
        ZPipeDirection requiredDir)
    {
        if (targetMap is not EntityUid targetMapUid)
            return;

        if (!TryComp(targetMapUid, out MapGridComponent? targetGrid))
            return;

        if (!TryComp(targetMapUid, out TransformComponent? targetXform))
            return;

        if (targetXform.MapUid is not EntityUid mapUid)
            return;

        var tileSize = targetGrid.TileSize;
        var min = new Vector2(tileX * tileSize, tileY * tileSize);
        var max = min + new Vector2(tileSize, tileSize);
        var tileBox = new Box2(min, max);

        foreach (var ent in _lookup.GetEntitiesIntersecting(
                    mapUid,
                    tileBox,
                    LookupFlags.All))
        {
            if (ent == uid)
                continue;

            if (!TryComp(ent, out NodeContainerComponent? otherCont))
                continue;

            foreach (var node in otherCont.Nodes.Values)
            {
                if (node is not ZPipeNode otherZ)
                    continue;

                if (otherZ.ZDirection != requiredDir)
                    continue;

                if (_nodeConnections.TryGetValue(selfNode, out var selfConnections) &&
                    selfConnections.Contains(otherZ))
                {
                    continue;
                }

                selfNode.AddAlwaysReachable(otherZ);
                otherZ.AddAlwaysReachable(selfNode);

                if (!_nodeConnections.TryGetValue(selfNode, out selfConnections))
                {
                    selfConnections = new HashSet<ZPipeNode>();
                    _nodeConnections[selfNode] = selfConnections;
                }
                selfConnections.Add(otherZ);

                if (!_nodeConnections.TryGetValue(otherZ, out var otherConnections))
                {
                    otherConnections = new HashSet<ZPipeNode>();
                    _nodeConnections[otherZ] = otherConnections;
                }
                otherConnections.Add(selfNode);

                Logger.Debug($"[ZPipe] Linked {uid} <-> {ent}");
            }
        }
    }
}