using Robust.Shared.Timing;

namespace Content.Shared._Utopia.Combat;

public sealed class ComboCounterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public void AddToCounter(ComboCounterComponent comp, int input)
    {
        if ((_timing.CurTime - comp.LastCombo).TotalSeconds > comp.Duration)
        {
            comp.ComboCounter = 0;
        }

        if (comp.ComboCounter < comp.MaxCombo)
        {
            comp.ComboCounter = Math.Min(comp.ComboCounter + input, comp.MaxCombo);
        }

        comp.LastCombo = _timing.CurTime;
    }
}
