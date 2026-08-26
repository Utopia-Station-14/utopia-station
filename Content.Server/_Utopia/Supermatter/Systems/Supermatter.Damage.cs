using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    public void ProcessHealing(Entity<SupermatterComponent> sm)
    {
        var baseHealing = BaseHealingPerTick * sm.Comp.HealingModificator; // TODO: Modificators
        sm.Comp.CurrentDamage -= baseHealing;
    }

    private void UpdateIntegrity(Entity<SupermatterComponent> sm)
    {
        var integrity = sm.Comp.Integrity;
        integrity = MathHelper.Clamp((integrity - sm.Comp.CurrentDamage) / 10f, MinIntegrity, MaxIntegrity);

        sm.Comp.Integrity = integrity;
    }

    private void ProcessDamage(Entity<SupermatterComponent> sm)
    {
        var tempDamage = ProcessTemperatureDamage(sm);
        var powerDamage = ProcessEnergyDamage(sm);
        var moleDamage = ProcessMoleDamage(sm);

        var totalDamage = tempDamage + powerDamage + moleDamage;
        if (totalDamage <= 0f)
        {
            ProcessHealing(sm);
            return;
        }

        sm.Comp.ArchivedDamage += totalDamage;
        sm.Comp.CurrentDamage = totalDamage;

        UpdateIntegrity(sm);
    }

    private float ProcessTemperatureDamage(Entity<SupermatterComponent> sm)
    {
        var temperature = sm.Comp.CurrentTemperature;
        var tempDamage = 0f;

        if (temperature > sm.Comp.MaxTemperature)
            tempDamage = (temperature - sm.Comp.MaxTemperature) / 150f;

        else if (temperature < sm.Comp.MinTemperature)
            tempDamage = (sm.Comp.MinTemperature - temperature) / 150f;

        return Math.Max(0f, tempDamage);
    }

    private float ProcessEnergyDamage(Entity<SupermatterComponent> sm)
    {
        var energy = sm.Comp.ExternalEnergy;

        if (energy <= ToMuchEnergy)
            return 0f;

        var energyDamage = (energy - ToMuchEnergy) / 500f;
        return Math.Max(0f, energyDamage);
    }

    private float ProcessMoleDamage(Entity<SupermatterComponent> sm)
    {
        var mole = sm.Comp.AtmosGas.TotalMoles;

        if (mole <= ToMuchGas)
            return 0f;

        var moleDamage = (mole - ToMuchGas) / 80f;
        return Math.Max(0f, moleDamage);
    }
}
