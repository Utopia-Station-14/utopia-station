using Content.Server._Utopia.Supermatter.Consoles;
using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

/// <summary>
/// Часть кода Суперматерии, которая отвечает за обработку урона.
/// </summary>
public sealed partial class SupermatterSystem
{
    public void ProcessHealing(Entity<SupermatterComponent> sm)
    {
        var baseHealing = BaseHealingPerTick * sm.Comp.HealingModifier;
        sm.Comp.CurrentDamage -= baseHealing;
    }

    private void UpdateIntegrity(Entity<SupermatterComponent> sm)
    {
        var integrity = sm.Comp.Integrity;
        integrity = MathHelper.Clamp(integrity - (sm.Comp.CurrentDamage / 10f), MinIntegrity, MaxIntegrity);

        sm.Comp.Integrity = integrity;
        sm.Comp.Status = GetStatusType(sm);

        var console = EntityManager.System<SupermatterConsoleSystem>();
        console.PlayAudio(sm.Comp.Status);
    }

    private void ProcessDamage(Entity<SupermatterComponent> sm)
    {
        float tempDamage = ProcessTemperatureDamage(sm);
        float powerDamage = ProcessEnergyDamage(sm);
        float moleDamage = ProcessMoleDamage(sm);
        float cascadeDamage = 0f;

        if (sm.Comp.CascadeModifier > 0)
        {
            sm.Comp.DamageType = SupermatterDamageType.Cascade;
            cascadeDamage = ProcessCascadeDamage(sm);
        }
        else if (tempDamage >= powerDamage && tempDamage >= moleDamage)
            sm.Comp.DamageType = SupermatterDamageType.Heat;
        else if (powerDamage >= moleDamage)
            sm.Comp.DamageType = SupermatterDamageType.Energy;
        else
            sm.Comp.DamageType = SupermatterDamageType.Mole;

        var totalDamage = tempDamage + powerDamage + moleDamage + cascadeDamage;
        if (totalDamage <= 0f)
        {
            ProcessHealing(sm);
            sm.Comp.CurrentDamage = 0f;
            UpdateIntegrity(sm);
            return;
        }

        sm.Comp.ArchivedDamage += totalDamage;
        sm.Comp.CurrentDamage = totalDamage;

        UpdateIntegrity(sm);
    }

    private float ProcessTemperatureDamage(Entity<SupermatterComponent> sm)
    {
        var temperature = sm.Comp.CurrentTemperature;

        if (temperature > sm.Comp.MaxTemperature)
            return (temperature - sm.Comp.MaxTemperature) / 500f;

        else if (temperature < sm.Comp.MinTemperature)
            return (temperature - sm.Comp.MinTemperature) / 500f;


        return 0f;
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

        var moleDamage = (mole - ToMuchGas) / 500f;
        return Math.Max(0f, moleDamage);
    }

    private float ProcessCascadeDamage(Entity<SupermatterComponent> sm)
    => sm.Comp.Integrity > 25f ? 1f : 2f;
}
