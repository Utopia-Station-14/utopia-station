using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.NodeContainer.Nodes;
using Content.Server._Utopia.ZLevels.Pipes.Nodes;
using Content.Shared._Utopia.ZLevels.Pipes.Components;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Pipes.Systems;

public sealed class ZPipeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    private readonly Dictionary<ZPipeNode, HashSet<ZPipeNode>> _connections = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ZPipeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZPipeComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ZLevelEntityLinkComponent, ComponentStartup>(OnLinkStartup);

        SubscribeLocalEvent<ZPipeComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
    }

    private void OnAtmosUpdate(EntityUid uid, ZPipeComponent comp, ref AtmosDeviceUpdateEvent args)
    {
        if (!TryComp(uid, out NodeContainerComponent? cont))
            return;

        foreach (var node in GetZNodes(cont))
        {
            if (!_connections.TryGetValue(node, out var set))
                continue;

            foreach (var other in set)
            {
                if (node.Owner.Id < other.Owner.Id)
                    TransferGas(node, other, args.dt);
            }
        }
    }

    private void OnStartup(EntityUid uid, ZPipeComponent comp, ComponentStartup args)
    {
        if (TryComp(uid, out TransformComponent? xform) && xform.Anchored)
            TryLink(uid);
    }

    private void OnAnchorChanged(EntityUid uid, ZPipeComponent comp, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
        {
            TryLink(uid);
        }
        else
        {
            if (TryComp(uid, out NodeContainerComponent? cont))
            {
                foreach (var z in GetZNodes(cont))
                    ClearConnections(z);
            }
        }
    }

    private void OnLinkStartup(EntityUid uid, ZLevelEntityLinkComponent comp, ComponentStartup args)
    {
        if (HasComp<ZPipeComponent>(uid)
            && TryComp(uid, out TransformComponent? xform)
            && xform.Anchored)
        {
            TryLink(uid);
        }
    }

    public void TryLink(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform) ||
            !TryComp(uid, out ZLevelEntityLinkComponent? link) ||
            !TryComp(uid, out NodeContainerComponent? cont))
            return;

        if (!xform.Anchored)
            return;

        if (xform.GridUid is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
            return;

        foreach (var z in GetZNodes(cont))
            ClearConnections(z);

        var worldBox = GetTileWorldBox(gridUid, grid, xform.Coordinates.Position);

        foreach (var z in GetZNodes(cont))
        {
            EntityUid? targetMap = null;
            ZPipeDirection? requiredDir = null;

            switch (z.ZDirection)
            {
                case ZPipeDirection.Up:
                    targetMap = link.AboveMap;
                    requiredDir = ZPipeDirection.Down;
                    break;

                case ZPipeDirection.Down:
                    targetMap = link.BelowMap;
                    requiredDir = ZPipeDirection.Up;
                    break;
            }

            if (targetMap == null || requiredDir == null)
                continue;

            TryConnectAt(uid, z, worldBox, targetMap.Value, requiredDir.Value);
        }
    }

    private void TryConnectAt(
        EntityUid uid,
        ZPipeNode self,
        Box2 worldBox,
        EntityUid targetMap,
        ZPipeDirection requiredDir)
    {
        if (!TryComp(targetMap, out MapComponent? map) ||
            map.MapId == MapId.Nullspace)
            return;

        foreach (var ent in _lookup.GetEntitiesIntersecting(map.MapId, worldBox, LookupFlags.All))
        {
            if (ent == uid ||
                !TryComp(ent, out NodeContainerComponent? cont) ||
                !TryComp(ent, out TransformComponent? xform) ||
                !xform.Anchored)
                continue;

            foreach (var node in cont.Nodes.Values)
            {
                if (node is not ZPipeNode other)
                    continue;

                if (other.ZDirection != requiredDir)
                    continue;

                AddConnection(self, other);
            }
        }
    }

    private void TransferGas(ZPipeNode a, ZPipeNode b, float dt)
    {
        var airA = a.Air;
        var airB = b.Air;

        var deltaP = airA.Pressure - airB.Pressure;
        if (MathF.Abs(deltaP) < 0.01f)
            return;

        var src = deltaP > 0 ? airA : airB;
        var dst = deltaP > 0 ? airB : airA;

        var transferPressure = MathF.Abs(deltaP) * 0.5f;

        var T = src.Temperature;
        var V = src.Volume;
        if (T <= 0f || V <= 0f)
            return;

        var dn = (transferPressure * V) / (Atmospherics.R * T);
        dn = MathF.Min(dn, src.TotalMoles);

        if (dn <= 0f)
            return;

        var removed = src.Remove(dn);
        _atmosphere.Merge(dst, removed);
    }

    private void AddConnection(ZPipeNode a, ZPipeNode b)
    {
        GetOrAdd(a).Add(b);
        GetOrAdd(b).Add(a);
    }

    private HashSet<ZPipeNode> GetOrAdd(ZPipeNode node)
    {
        if (!_connections.TryGetValue(node, out var set))
        {
            set = new HashSet<ZPipeNode>();
            _connections[node] = set;
        }

        return set;
    }

    private void ClearConnections(ZPipeNode node)
    {
        if (!_connections.Remove(node, out var set))
            return;

        foreach (var other in set)
        {
            if (_connections.TryGetValue(other, out var otherSet))
            {
                otherSet.Remove(node);
                if (otherSet.Count == 0)
                    _connections.Remove(other);
            }
        }
    }

    private static IEnumerable<ZPipeNode> GetZNodes(NodeContainerComponent cont)
    {
        foreach (var node in cont.Nodes.Values)
            if (node is ZPipeNode z)
                yield return z;
    }

    private Box2 GetTileWorldBox(EntityUid gridUid, MapGridComponent grid, Vector2 localPos)
    {
        var size = grid.TileSize;
        var x = (int)(localPos.X / size);
        var y = (int)(localPos.Y / size);

        var origin =
            _transform.GetWorldPosition(gridUid) +
            new Vector2(x * size, y * size);

        return new Box2(origin, origin + new Vector2(size, size));
    }
}
