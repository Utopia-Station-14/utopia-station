using Content.Shared._Utopia.Power.Components;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;

namespace Content.Client._Utopia.Power;

public sealed partial class BatterySlotVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatterySlotVisualsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(Entity<BatterySlotVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite == null)
            return;

        args.AppearanceData.TryGetValue(BatterySlotVisuals.Battery, out var rawHas);

        var hasBattery = rawHas is true;

        if (!args.AppearanceData.TryGetValue(BatterySlotVisuals.MaxCharge, out var maxCharge))
        {
            maxCharge = ent.Comp.Steps;
        }

        if (!args.AppearanceData.TryGetValue(BatterySlotVisuals.CurrentCharge, out var current))
        {
            current = ent.Comp.Steps;
        }

        var step = ContentHelpers.RoundToLevels((int)current, (int)maxCharge, ent.Comp.Steps);

        if (!_sprite.LayerMapTryGet((ent, sprite), BatterySlotVisualsLayers.Charge, out _, false))
            return;

        if (!hasBattery || step == 0 && !ent.Comp.ZeroVisible)
        {
            _sprite.LayerSetVisible((ent, sprite), BatterySlotVisualsLayers.Charge, false);
            return;
        }

        _sprite.LayerSetVisible((ent, sprite), BatterySlotVisualsLayers.Charge, true);
        _sprite.LayerSetRsiState((ent, sprite), BatterySlotVisualsLayers.Charge, $"{ent.Comp.BaseState}-{step}");
    }
}
