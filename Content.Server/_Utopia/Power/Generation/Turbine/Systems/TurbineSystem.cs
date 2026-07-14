using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer;
using Content.Server.Power.Components;
using Content.Server.Power.Turbines.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.NodeContainer;
using Content.Shared.Power.Turbines.Components;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.Power.Turbines;

public sealed class TurbineSystem : EntitySystem
{
    private const string NodeNameTurbine = "turbine";
    private const float UpdateInterval = 1f;

    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;

    private float _accumulator;
    private EntityQuery<NodeContainerComponent> _nodeContainerQuery;
    private static readonly ProtoId<RadioChannelPrototype> Channel = "Engineering";

    public override void Initialize()
    {
        base.Initialize();
        _nodeContainerQuery = GetEntityQuery<NodeContainerComponent>();

        Subs.BuiEvents<TurbineConsoleComponent>(TurbineConsoleUiKey.Key, subs =>
        {
            subs.Event<TurbineConsoleFocusChangeMessage>(OnSelectTurbine);
            subs.Event<TurbineConsoleToggleMessage>(OnToggleTurbine);
        });
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator -= UpdateInterval;

        var rotorQuery = EntityQueryEnumerator<TurbineRotorComponent>();
        while (rotorQuery.MoveNext(out var uid, out var rotor))
        {
            var group = GetNodeGroup(uid);
            if (group is not { IsFullyBuilt: true })
                continue;

            if (rotor.IsActive)
            {
                ProcessTurbine(uid, rotor, group);

                rotor.TalkingTimer += frameTime;
                if (rotor.TalkingTimer < UpdateInterval)
                    continue;
                
                rotor.TalkingTimer -= UpdateInterval;

                ProcessTalking(uid, rotor);
            }
            else
            {
                ResetRotor(uid, rotor);
            }
        }

        var consoleQuery = EntityQueryEnumerator<TurbineConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console))
        {
            UpdateConsoleState(consoleUid, console);
        }
    }

    [Access(typeof(TurbineNodeGroup))]
    public void UpdateRotorConnectivity(EntityUid uid, TurbineNodeGroup group)
    {
        if (TryComp<PowerSupplierComponent>(uid, out var supplier))
        {
            supplier.Enabled = group.IsFullyBuilt;
        }
    }

    private void ResetRotor(EntityUid uid, TurbineRotorComponent rotor)
    {
        rotor.CurrentRPM = 0f;
        rotor.CurrentPressure = 0f;
        rotor.CurrentTemperature = 0f;

        if (TryComp<PowerSupplierComponent>(uid, out var supplier))
            supplier.MaxSupply = 0f;
    }

    private TurbineNodeGroup? GetNodeGroup(EntityUid uid)
    {
        if (!_nodeContainerQuery.TryGetComponent(uid, out var container))
            return null;

        if (!container.Nodes.TryGetValue(NodeNameTurbine, out var node))
            return null;

        return node.NodeGroup as TurbineNodeGroup;
    }

    private void ProcessTurbine(EntityUid uid, TurbineRotorComponent rotor, TurbineNodeGroup group)
    {
        if (!rotor.IsActive || group.Inlet == null || group.Outlet == null)
            return;

        var inletNode = group.Inlet.Owner;
        var outletNode = group.Outlet.Owner;

        if (!TryComp<TurbineInletComponent>(inletNode, out var inlet) ||
            !TryComp<TurbineOutletComponent>(outletNode, out var outlet))
            return;

        var (inGas, sourcePressure) = CollectGas(inletNode, inlet);
        
        if (inGas.TotalMoles <= 0f)
        {
            ResetRotor(uid, rotor);
            return;
        }

        var (outGas, energy, rpm) = ProcessRotor(uid, inGas, sourcePressure, rotor);
        rotor.Energy = energy;
        rotor.CurrentRPM = rpm;

        DumpGas(outletNode, outlet, outGas);

        if (TryComp<PowerSupplierComponent>(uid, out var supplier))
            supplier.MaxSupply = energy * 1000f;
    }

    private (GasMixture gas, float sourcePressure) CollectGas(EntityUid inletUid, TurbineInletComponent comp)
    {
        var xform = Transform(inletUid);
        if (xform.GridUid == null)
            return (new GasMixture(), 0f);

        var gridUid = xform.GridUid.Value;
        var tilePos = _transformSystem.GetGridTilePositionOrDefault(inletUid);
        var backDir = xform.LocalRotation.GetDir().GetOpposite();
        var targetPos = tilePos + backDir.ToIntVec();

        var mix = _atmos.GetTileMixture(gridUid, null, targetPos, false);
        if (mix == null || mix.TotalMoles <= 0f)
            return (new GasMixture(), 0f);

        var sourcePressure = mix.Pressure;
        var taken = mix.Remove(comp.GasIntake);
        _atmos.InvalidateTile(gridUid, targetPos);

        return (taken, sourcePressure);
    }

    private (GasMixture gas, float energy, float rpm) ProcessRotor(EntityUid uid, GasMixture gas, float pressure, TurbineRotorComponent rotor)
    {
        var temperature = gas.Temperature;

        rotor.CurrentPressure = pressure;
        rotor.CurrentTemperature = temperature;

        if (pressure < rotor.MinPressure)
            return (gas, 0f, 0f);

        var pressureDrop = MathF.Min(pressure - rotor.MinPressure, 500f);
        var flow = pressureDrop * 0.5f;
        var energy = (flow * rotor.Efficiency) / 2f;
        var rpm = MathF.Sqrt(flow) * 120f / rotor.RpmFactor;

        ProcessDamage(uid, rotor, rpm, temperature, pressure);

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

    private void ProcessDamage(EntityUid uid, TurbineRotorComponent rotor, float rpm, float temperature, float pressure)
    {
        rotor.DamageArchived = rotor.Integrity;

        var temperatureDamage = MathF.Max(temperature - rotor.MaxTemperature, 0f) / 100f;
        var pressureDamage = MathF.Max(pressure - rotor.MaxPressure, 0f) / 100f;
        var energyDamage = MathF.Max(rpm - rotor.MaxRPM, 0f) / 100f;

        rotor.TemperatureDamage += temperatureDamage;
        rotor.PressureDamage += pressureDamage;
        rotor.EnergyDamage += energyDamage;

        var totalDamage = temperatureDamage + pressureDamage + energyDamage;

        const float maxDamagePerSecond = 1f; 
        if (totalDamage > maxDamagePerSecond)
        {
            totalDamage = maxDamagePerSecond;
        }

        var integrity = MathF.Max(0f, rotor.DamageArchived - totalDamage);
        rotor.Integrity = Math.Clamp(integrity, 0f, 100f);

        if (rotor.Integrity <= 0f)
            TriggerExplosion(uid, rotor);
    }

    private void TriggerExplosion(EntityUid uid, TurbineRotorComponent rotor)
    {
        var explosionType = GetExplosionType(rotor);
        switch (explosionType)
        {
            case ExplosionType.Heat:
                _explosion.QueueExplosion(uid, "FireBomb", totalIntensity: 100, slope: 2.5f, maxTileIntensity: 10f, canCreateVacuum: false);
                break;
            case ExplosionType.EMP:
                _explosion.QueueExplosion(uid, "PowerSink", totalIntensity: 250, slope: 2.5f, maxTileIntensity: 10f, canCreateVacuum: false);
                break;
            case ExplosionType.BIGSHOT:
                _explosion.QueueExplosion(uid, "HardBomb", totalIntensity: 500, slope: 3.0f, maxTileIntensity: 20f, canCreateVacuum: true);
                break;
        }
    }

    private void ProcessTalking(EntityUid uid, TurbineRotorComponent rotor)
    {
        if (rotor.Integrity >= rotor.DamageArchived)
            return;

        string message;
        
        if (rotor.PressureDamage > rotor.EnergyDamage && rotor.PressureDamage > rotor.TemperatureDamage)
            message = Loc.GetString("turbine-pressure-damage", ("integrity", rotor.Integrity));
        else if (rotor.EnergyDamage > rotor.PressureDamage && rotor.EnergyDamage > rotor.TemperatureDamage)
            message = Loc.GetString("turbine-energy-damage", ("integrity", rotor.Integrity));
        else
            message = Loc.GetString("turbine-heat-damage", ("integrity", rotor.Integrity));

        _radioSystem.SendRadioMessage(uid, message, Channel, uid);
    }

    private ExplosionType GetExplosionType(TurbineRotorComponent rotor)
    {
        if (rotor.PressureDamage > rotor.EnergyDamage && rotor.PressureDamage > rotor.TemperatureDamage)
            return ExplosionType.BIGSHOT;

        if (rotor.EnergyDamage > rotor.PressureDamage && rotor.EnergyDamage > rotor.TemperatureDamage)
            return ExplosionType.EMP;

        return ExplosionType.Heat;
    }

    private TurbineStatusType GetTurbineStatus(TurbineRotorComponent rotor)
    {
        return rotor switch
        {
            { IsActive: false } => TurbineStatusType.Off,
            { Integrity: <= 0f } => TurbineStatusType.Critical,
            var r when r.CurrentRPM > r.MaxRPM * 0.9f => TurbineStatusType.Warning,
            _ => TurbineStatusType.Nominal
        };
    }

    private void OnSelectTurbine(Entity<TurbineConsoleComponent> ent, ref TurbineConsoleFocusChangeMessage msg)
    {
        ent.Comp.FocusTurbine = msg.FocusTurbine;
        Dirty(ent);
        UpdateConsoleState(ent.Owner, ent.Comp);
    }

    private void OnToggleTurbine(Entity<TurbineConsoleComponent> ent, ref TurbineConsoleToggleMessage msg)
    {
        var entity = GetEntity(msg.Turbine);
        if (TryComp<TurbineRotorComponent>(entity, out var rotor))
        {
            rotor.IsActive = msg.IsActive;
            UpdateConsoleState(ent.Owner, ent.Comp);
        }
    }

    private void UpdateConsoleState(EntityUid consoleUid, TurbineConsoleComponent comp)
    {
        var turbines = new List<TurbineConsoleEntry>();

        var query = EntityQueryEnumerator<TurbineRotorComponent>();
        while (query.MoveNext(out var rotorUid, out var rotor))
        {
            var status = GetTurbineStatus(rotor);
            var netEntity = GetNetEntity(rotorUid);
            turbines.Add(new TurbineConsoleEntry(netEntity, $"Turbine {rotorUid}", status));
        }

        TurbineFocusData? focusData = null;
        if (comp.FocusTurbine != null)
        {
            var entity = GetEntity(comp.FocusTurbine.Value);
            if (TryComp<TurbineRotorComponent>(entity, out var focusedRotor))
            {
                focusData = new TurbineFocusData(
                    comp.FocusTurbine.Value,
                    GetTurbineStatus(focusedRotor),
                    focusedRotor.CurrentRPM,
                    focusedRotor.MaxRPM,
                    focusedRotor.CurrentPressure,
                    focusedRotor.MaxPressure,
                    focusedRotor.CurrentTemperature,
                    focusedRotor.MaxTemperature,
                    focusedRotor.Integrity,
                    focusedRotor.Energy,
                    focusedRotor.IsActive
                );
            }
        }

        comp.Turbines = turbines.ToArray();
        comp.FocusData = focusData;

        var state = new TurbineConsoleBoundInterfaceState(comp.Turbines, comp.FocusData);
        _ui.SetUiState(consoleUid, TurbineConsoleUiKey.Key, state);
    }
}