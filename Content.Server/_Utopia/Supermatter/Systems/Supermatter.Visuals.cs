using Content.Shared._Utopia.Supermatter.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private SharedPointLightSystem _light = default!;

    private SupermatterVisualState GetVisualState(Entity<SupermatterComponent> sm)
    {
        if (!sm.Comp.Active)
            return SupermatterVisualState.Inactive;

        return GetStatusType(sm) switch
        {
            SupermatterStatus.Stable => SupermatterVisualState.Stable,
            _ => SupermatterVisualState.Destabilization
        };
    }

    private void ProcessVisual(Entity<SupermatterComponent> sm)
    {
        ProcessShining(sm);
        ProcessSprite(sm);
    }

    private void ProcessShining(Entity<SupermatterComponent> sm)
    {
        if (TryComp<PointLightComponent>(sm, out var light))
        {
            var energy = Math.Clamp(sm.Comp.ExternalEnergy / 100f, 2, 10);
            _light.SetEnergy(sm, energy, light);
            _light.SetRadius(sm, energy, light);
        }
    }

    private void ProcessSprite(Entity<SupermatterComponent> sm)
    {
        var state = GetVisualState(sm);
        if (sm.Comp.VisualState == state)
            return;

        sm.Comp.VisualState = state;
        _appearance.SetData(sm, SupermatterVisuals.Status, state);
    }
}
