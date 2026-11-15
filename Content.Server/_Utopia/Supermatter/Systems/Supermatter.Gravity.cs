using Content.Shared._Utopia.Supermatter.Components;
using Content.Server.Singularity.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    private void ProcessGravity(Entity<SupermatterComponent> sm)
    {
        if (TryComp<GravityWellComponent>(sm, out var gravityWell))
            gravityWell.MaxRange = Math.Clamp(sm.Comp.InternalEnergy / 850f, 0.5f, 3f);
    }
}
