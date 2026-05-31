using Content.Server._Utopia.Genetics.Mutations.Components;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

using Content.Shared.Temperature;

public sealed class MutationThermalResistanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationThermalResistanceComponent, GetThermalInsulationEvent>(OnGetInsulation);
        SubscribeLocalEvent<MutationThermalResistanceComponent, ModifyChangedTemperatureEvent>(OnModifyTemperature);
    }

    private void OnGetInsulation(Entity<MutationThermalResistanceComponent> ent, ref GetThermalInsulationEvent args)
    {
        var coefficient = args.TemperatureDelta < 0
            ? ent.Comp.CoolingCoefficient
            : ent.Comp.HeatingCoefficient;

        args.Coefficient *= coefficient;
    }

    private void OnModifyTemperature(Entity<MutationThermalResistanceComponent> ent, ref ModifyChangedTemperatureEvent args)
    {
        var ev = new GetThermalInsulationEvent(1f)
        {
            TemperatureDelta = args.TemperatureDelta
        };

        RaiseLocalEvent(ent.Owner, ref ev);
        args.TemperatureDelta *= ev.Coefficient;
    }
}

[ByRefEvent]
public struct GetThermalInsulationEvent(float coefficient)
{
    public float Coefficient = coefficient;
    public float TemperatureDelta = 0f;
}
