using Content.Shared.Power.Turbines;
using Content.Shared.Power.Turbines.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client.Power.Turbines;

public sealed class TurbineVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TurbineOutletComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, TurbineOutletComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, TurbineVisuals.Spinning, out var spinning, args.Component) && spinning)
            args.Sprite.LayerSetState(1, "fan");
        else
            args.Sprite.LayerSetState(1, "fan_stoped");
    }
}