using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
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
        var state = GetVisualState(sm);

        if (sm.Comp.VisualState == state)
            return;

        sm.Comp.VisualState = state;
        _appearance.SetData(sm, SupermatterVisuals.Status, state);
    }
}
