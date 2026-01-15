using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer;
using Content.Server.Power.Components;
using Content.Server.Power.Turbines.Nodes;
using Content.Shared.Atmos;
using Content.Shared.NodeContainer;
using Content.Shared.Power.Turbines.Components;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.Power.Turbines;

public sealed class TurbineSystem : EntitySystem
{
    private const string NodeNameTurbine = "turbine";

    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private EntityQuery<NodeContainerComponent> _nodeContainerQuery;

    public override void Initialize()
    {
        base.Initialize();
        _nodeContainerQuery = GetEntityQuery<NodeContainerComponent>();
    }

    public override void Update(float frameTime)
    {
        foreach (var rotor in EntityQuery<TurbineRotorComponent>())
        {
            var uid = rotor.Owner;
            var group = GetNodeGroup(uid);
            if (group == null || !group.IsFullyBuilt)
                continue;

            if (rotor.IsActive)
                ProcessTurbine(uid, rotor);
        }

        // Обновляем состояние всех консолей на сервере
        foreach (var console in EntityQuery<TurbineConsoleComponent>())
        {
            UpdateConsoleState(console);
        }
    }

    [Access(typeof(TurbineNodeGroup))]
    public void UpdateRotorConnectivity(EntityUid uid, TurbineNodeGroup group)
    {
        if (!TryComp(uid, out PowerSupplierComponent? supplier))
            return;

        supplier.Enabled = group.IsFullyBuilt;
    }

    private TurbineNodeGroup? GetNodeGroup(EntityUid uid)
    {
        if (!_nodeContainerQuery.TryGetComponent(uid, out var container))
            return null;

        if (!container.Nodes.TryGetValue(NodeNameTurbine, out var node))
            return null;

        return node.NodeGroup as TurbineNodeGroup;
    }

    private void ProcessTurbine(EntityUid uid, TurbineRotorComponent rotor)
    {
        if (!rotor.IsActive)
            return;

        if (!TryComp(uid, out TurbineInletComponent? inlet) ||
            !TryComp(uid, out TurbineOutletComponent? outlet))
            return;

        var inletNode = inlet.Owner;
        var outletNode = outlet.Owner;

        var inGas = CollectGas(inletNode, inlet);
        if (inGas.TotalMoles <= 0f)
            return;

        var (outGas, energy, rpm) = ProcessRotor(inGas, rotor);
        rotor.CurrentRPM = rpm;

        DumpGas(outletNode, outlet, outGas);

        if (TryComp(uid, out PowerSupplierComponent? supplier))
            supplier.MaxSupply = energy;
    }

    private GasMixture CollectGas(EntityUid inletUid, TurbineInletComponent comp)
    {
        var xform = Transform(inletUid);
        if (xform.GridUid == null)
            return new GasMixture();

        var gridUid = xform.GridUid.Value;
        var tilePos = _transformSystem.GetGridTilePositionOrDefault(inletUid);
        var backDir = xform.LocalRotation.GetDir().GetOpposite();
        var targetPos = tilePos + backDir.ToIntVec();

        var mix = _atmos.GetTileMixture(gridUid, null, targetPos, false);
        if (mix == null || mix.TotalMoles <= 0f)
            return new GasMixture();

        var taken = mix.Remove(comp.GasIntake);
        _atmos.InvalidateTile(gridUid, targetPos);

        return taken;
    }

    private (GasMixture gas, float energy, float rpm) ProcessRotor(GasMixture gas, TurbineRotorComponent rotor)
    {
        var pressure = gas.Pressure;
        var temperature = gas.Temperature;

        if (pressure < rotor.MinPressure)
            return (gas, 0f, 0f);

        var pressureDrop = MathF.Min(pressure - rotor.MinPressure, 500f);

        var flow = pressureDrop * 0.5f;
        var energy = flow * rotor.Efficiency;
        var rpm = MathF.Sqrt(flow) * 120f / rotor.RpmFactor;

        ProcessDamage(rotor, rpm, temperature);

        var heatCapacity = _atmos.GetHeatCapacity(gas, true);
        if (heatCapacity > 0f)
        {
            var tempDrop = energy / heatCapacity;
            gas.Temperature = MathF.Max(Atmospherics.TCMB, gas.Temperature - tempDrop);
        }

        return (gas, energy, rpm);
    }

    private void DumpGas(EntityUid outletUid, TurbineOutletComponent comp, GasMixture gas)
    {
        if (gas.TotalMoles <= 0f)
            return;

        var xform = Transform(outletUid);
        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;
        var tilePos = _transformSystem.GetGridTilePositionOrDefault(outletUid);
        var dir = xform.LocalRotation.GetDir();
        var targetPos = tilePos + dir.ToIntVec();

        var mix = _atmos.GetTileMixture(gridUid, null, targetPos, false);
        if (mix == null)
            return;

        _atmos.Merge(mix, gas);
        _atmos.InvalidateTile(gridUid, targetPos);
    }

    private void ProcessDamage(TurbineRotorComponent rotor, float rpm, float temperature)
    {
        var damage = 0f;
        if (temperature > rotor.MaxTemperature)
            damage += temperature - rotor.MaxTemperature;
        if (rpm > rotor.MaxRPM)
            damage += rpm - rotor.MaxRPM;

        rotor.Integrity -= damage;
    }

    /// <summary>
    /// Обновляем состояние консолей напрямую в UI
    /// </summary>
    private void UpdateConsoleState(TurbineConsoleComponent comp)
    {
        var turbines = new List<TurbineConsoleEntry>();

        // Собираем данные о всех турбинах
        foreach (var rotor in EntityQuery<TurbineRotorComponent>())
        {
            var status = TurbineStatusType.Nominal;
            if (rotor.Integrity <= 0)
                status = TurbineStatusType.Critical;
            else if (rotor.CurrentRPM > rotor.MaxRPM * 0.9f)
                status = TurbineStatusType.Warning;

            var netEntity = GetNetEntity(rotor.Owner);
            turbines.Add(new TurbineConsoleEntry(netEntity, $"Turbine {rotor.Owner}", status));
        }

        TurbineFocusData? focusData = null;
        if (comp.FocusTurbine != null)
        {
            var entity = GetEntity(comp.FocusTurbine.Value);
            if (TryComp<TurbineRotorComponent>(entity, out var focusedRotor))
            {
                focusData = new TurbineFocusData(
                    comp.FocusTurbine.Value,
                    focusedRotor.CurrentRPM,
                    focusedRotor.MaxRPM,
                    focusedRotor.MaxTemperature,
                    focusedRotor.Integrity
                );
            }
        }

        comp.Turbines = turbines.ToArray();
        comp.FocusData = focusData;
    }
}