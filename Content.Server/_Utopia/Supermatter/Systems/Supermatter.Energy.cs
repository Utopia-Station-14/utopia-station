using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    public void ChangeExternalEnergy(SupermatterComponent sm, float energy)
    {
        sm.ExternalEnergy = MathF.Max(0f, sm.ExternalEnergy + energy);
    }

    private void ChangeInternalEnergy(SupermatterComponent sm, float energy)
    {
        sm.InternalEnergy = MathF.Max(0f, sm.InternalEnergy + energy);
    }

    public void ConsumeExternalEnergy(Entity<SupermatterComponent> sm, float baseAmount)
    {
        var actualLoss = baseAmount * sm.Comp.EnergyReductionModifier;
        ChangeExternalEnergy(sm.Comp, -actualLoss);
    }

    public void ConsumeInternalEnergy(Entity<SupermatterComponent> sm, float baseAmount)
    {
        var actualLoss = baseAmount * sm.Comp.EnergyReductionModifier;
        ChangeInternalEnergy(sm.Comp, -actualLoss);
    }

    private bool CheckBalance(Entity<SupermatterComponent> sm)
    {
        if (MathHelper.CloseTo(sm.Comp.InternalEnergy, 0f))
            return false;

        var currentRatio = sm.Comp.ExternalEnergy / sm.Comp.InternalEnergy;
        return MathF.Abs(currentRatio - sm.Comp.TargetEnergyRatio) < 0.01f;
    }
    private void RebalanceEnergy(Entity<SupermatterComponent> sm, float frameTime)
    {
        var targetExternal = (sm.Comp.TotalEnergy * sm.Comp.TargetEnergyRatio) / (1f + sm.Comp.TargetEnergyRatio);
        var targetInternal = sm.Comp.TotalEnergy - targetExternal;

        var externalDelta = targetExternal - sm.Comp.ExternalEnergy;
        var internalDelta = targetInternal - sm.Comp.InternalEnergy;

        var transferRate = sm.Comp.EnergyScaleModificator * frameTime;

        ChangeExternalEnergy(sm.Comp, externalDelta * transferRate);
        ChangeInternalEnergy(sm.Comp, internalDelta * transferRate);
    }

    private void ProcessEnergy(Entity<SupermatterComponent> sm, float frameTime)
    {
        sm.Comp.TotalEnergy = sm.Comp.ExternalEnergy + sm.Comp.InternalEnergy;

        if (CheckBalance(sm))
            return;

        RebalanceEnergy(sm, frameTime);
    }
}
