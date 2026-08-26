using Content.Server.Kitchen.Components;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared.Verbs;

namespace Content.Server.Kitchen.EntitySystems;

public sealed partial class MicrowaveBatterySystem : EntitySystem
{
    [Dependency] private PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MicrowaveBatteryComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<MicrowaveBatteryComponent, GetVerbsEvent<AlternativeVerb>>(OnGetBatteryVerbs);
    }

    private void OnComponentRemove(Entity<MicrowaveBatteryComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<ApcPowerReceiverComponent>(ent, out var apc))
        {
            apc.NeedsPower = true;
        }
    }

    private void OnGetBatteryVerbs(Entity<MicrowaveBatteryComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.BatterySwitch),
            Act = () => TogglePowerMode(ent, ent.Comp)
        });
    }

    private void TogglePowerMode(EntityUid uid, MicrowaveBatteryComponent battComp)
    {
        battComp.NetworkPower = !battComp.NetworkPower;
        if (TryComp<ApcPowerReceiverComponent>(uid, out var apc))
        {
            apc.NeedsPower = battComp.NetworkPower;
        }

        _popupSystem.PopupEntity(Loc.GetString(battComp.NetworkPower ?
            "microwave-switched-to-network" : "microwave-switched-to-battery"), uid);
    }
}
