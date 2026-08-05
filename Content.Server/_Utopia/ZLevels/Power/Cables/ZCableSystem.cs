using System.Collections.Generic;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server._Utopia.ZLevels.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.GameObjects;

namespace Content.Server._Utopia.ZLevels.Power;

public sealed class ZCableSystem : EntitySystem
{
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;

    private readonly Dictionary<ZCableNode, HashSet<ZCableNode>> _connections = new();

    public IEnumerable<Node> GetZReachable(ZCableNode node)
    {
        if (_connections.TryGetValue(node, out var set))
            return set;

        return new List<Node>();
    }
    
    public void AddZConnection(ZCableNode a, ZCableNode b)
    {
        var addedToA = GetOrAdd(a).Add(b);
        var addedToB = GetOrAdd(b).Add(a);
        
        if (!addedToA && !addedToB)
            return;

        _nodeGroup.QueueReflood(a);
        _nodeGroup.QueueReflood(b);
    }

    public void ClearConnections(ZCableNode node)
    {
        if (!_connections.Remove(node, out var set))
            return;

        _nodeGroup.QueueReflood(node);

        foreach (var other in set)
        {
            if (_connections.TryGetValue(other, out var otherSet))
                otherSet.Remove(node);

            _nodeGroup.QueueReflood(other);
        }
    }

    public void ClearAll(NodeContainerComponent container)
    {
        foreach (var node in container.Nodes.Values)
        {
            if (node is ZCableNode z)
                ClearConnections(z);
        }
    }

    private HashSet<ZCableNode> GetOrAdd(ZCableNode node)
    {
        if (!_connections.TryGetValue(node, out var set))
        {
            set = new HashSet<ZCableNode>();
            _connections[node] = set;
        }

        return set;
    }
}