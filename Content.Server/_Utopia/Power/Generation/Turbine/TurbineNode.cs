using System.Collections.Generic;
using System.Linq;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Turbines;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Power.Turbines.Nodes;

/// <summary>
/// Нод-система турбины.
/// <seealso cref="TurbineSystem"/>
/// <seealso cref="TurbineRotorComponent"/>
/// <seealso cref="TurbineInletComponent"/>
/// <seealso cref="TurbineOutletComponent"/>
/// </summary>
[NodeGroup(NodeGroupID.Turbine)]
public sealed class TurbineNodeGroup : BaseNodeGroup
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsFullyBuilt { get; private set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public TurbineNodeInlet? Inlet { get; private set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public TurbineNodeRotor? Rotor { get; private set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public TurbineNodeOutlet? Outlet { get; private set; }

    private IEntityManager? _entMan;

    public override void Initialize(Node sourceNode, IEntityManager entMan)
    {
        base.Initialize(sourceNode, entMan);
        _entMan = entMan;
    }

    public override void LoadNodes(List<Node> groupNodes)
    {
        DebugTools.Assert(_entMan != null);
        base.LoadNodes(groupNodes);

        Inlet = groupNodes.OfType<TurbineNodeInlet>().SingleOrDefault();
        Rotor = groupNodes.OfType<TurbineNodeRotor>().SingleOrDefault();
        Outlet = groupNodes.OfType<TurbineNodeOutlet>().SingleOrDefault();

        IsFullyBuilt = Inlet != null && Rotor != null && Outlet != null;

        if (Rotor != null)
        {
            var system = _entMan.EntitySysManager.GetEntitySystem<TurbineSystem>();
            system.UpdateRotorConnectivity(Rotor.Owner, this);
        }
    }
}


/// <summary>
/// Первая часть турбины, которая занимается поглощением газов. Ищет ротор турбины спереди себя.
/// </summary>
[DataDefinition]
public sealed partial class TurbineNodeInlet : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();

        var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
        var forwardDir = xform.Comp.LocalRotation.GetDir();
        var targetIdx = gridIndex.Offset(forwardDir);

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, targetIdx))
        {
            if (node is not TurbineNodeRotor rotor)
                continue;

            var rotorXform = xformQuery.GetComponent(rotor.Owner);
            if (rotorXform.LocalRotation.GetDir() == forwardDir)
                yield return rotor;
        }
    }
}

/// <summary>
/// Основная часть турбины. Ищет первую и третью часть спереди/сзади.
/// </summary>
[DataDefinition]
public sealed partial class TurbineNodeRotor : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();

        var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
        var forwardDir = xform.Comp.LocalRotation.GetDir();
        var backDir = forwardDir.GetOpposite();

        var backIdx = gridIndex.Offset(backDir);
        var forwardIdx = gridIndex.Offset(forwardDir);

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, backIdx))
        {
            if (node is TurbineNodeInlet Inlet)
            {
                var InletXform = xformQuery.GetComponent(Inlet.Owner);
                if (InletXform.LocalRotation.GetDir() == forwardDir)
                    yield return Inlet;
            }
        }

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, forwardIdx))
        {
            if (node is TurbineNodeOutlet Outlet)
            {
                var OutletXform = xformQuery.GetComponent(Outlet.Owner);
                if (OutletXform.LocalRotation.GetDir() == forwardDir)
                    yield return Outlet;
            }
        }
    }
}

/// <summary>
/// Третья часть турбины. Ищет ротор сзади себя.
/// </summary>
[DataDefinition]
public sealed partial class TurbineNodeOutlet : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var mapSystem = entMan.System<SharedMapSystem>();

        var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
        var forwardDir = xform.Comp.LocalRotation.GetDir();
        var backDir = forwardDir.GetOpposite();
        var targetIdx = gridIndex.Offset(backDir);

        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, targetIdx))
        {
            if (node is not TurbineNodeRotor rotor)
                continue;

            var rotorXform = xformQuery.GetComponent(rotor.Owner);
            if (rotorXform.LocalRotation.GetDir() == forwardDir)
                yield return rotor;
        }
    }
}
