using JetBrains.Annotations;
using Content.Server._Utopia.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Construction.Components;
using Content.Shared.Power.Components;

namespace Content.Server._Utopia.Power.EntitySystems;

[UsedImplicitly]
public sealed class UpgradeBatterySystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _batterySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UpgradeBatteryComponent, RefreshPartsEvent>(OnRefreshParts);
        SubscribeLocalEvent<UpgradeBatteryComponent, UpgradeExamineEvent>(OnUpgradeExamine);
    }

    public void OnRefreshParts(EntityUid uid, UpgradeBatteryComponent component, RefreshPartsEvent args)
    {
        var powerCellTier = args.PartTiers[component.MachinePartPowerCapacity];

        if (TryComp<BatteryComponent>(uid, out var batteryComp))
        {
            _batterySystem.SetMaxCharge((uid, batteryComp), MathF.Pow(component.MaxChargeMultiplier, powerCellTier - 1) * component.BaseMaxCharge);
        }
    }

    private void OnUpgradeExamine(EntityUid uid, UpgradeBatteryComponent component, UpgradeExamineEvent args)
    {
        if (TryComp<BatteryComponent>(uid, out var batteryComp))
        {
            args.AddPercentageUpgrade("upgrade-max-charge", batteryComp.MaxCharge / component.BaseMaxCharge);
        }
    }
}
