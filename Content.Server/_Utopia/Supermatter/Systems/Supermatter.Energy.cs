using Content.Shared._Utopia.Supermatter.Components;


namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    public void ChangeExternalEnergy(SupermatterComponent sm, float energy)
        => sm.ExternalEnergy += energy;

    private void ChangeInternalEnergy(SupermatterComponent sm, float energy)
        => sm.InternalEnergy += energy;

    private bool CheckBalance(Entity<SupermatterComponent> sm)
    {
        if (Math.Abs(sm.Comp.ExternalEnergy / sm.Comp.InternalEnergy - 2f) < 0.001f)
            return true;

        return false;
    }
    private void RebalanceEnergy(Entity<SupermatterComponent> sm)
    {
        if (sm.Comp.ExternalEnergy > sm.Comp.InternalEnergy)
        {
            sm.Comp.InternalEnergy += EnergyPerCheck;
            sm.Comp.ExternalEnergy -= EnergyPerCheck;
        }
        else
        {
            sm.Comp.ExternalEnergy += EnergyPerCheck;
            sm.Comp.InternalEnergy -= EnergyPerCheck;
        }
    }

    private void ProcessEnergy(Entity<SupermatterComponent> sm)
    {
        sm.Comp.TotalEnergy = sm.Comp.ExternalEnergy + sm.Comp.InternalEnergy;

        if (!CheckBalance(sm))
            RebalanceEnergy(sm);
    }
}