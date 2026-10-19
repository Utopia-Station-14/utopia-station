using System.Linq;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Utopia.WashingMachine;

public abstract partial class SharedWashingMachineSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] protected SharedEntityStorageSystem Storage = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WashingMachineComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt);
        SubscribeLocalEvent<WashingMachineComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
    }

    private void OnStorageOpenAttempt(Entity<WashingMachineComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (ent.Comp.State != WashingMachineState.Idle)
        {
            args.Cancelled = true;
        }
    }

    private void OnGetVerbs(Entity<WashingMachineComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanComplexInteract)
            return;

        if (ent.Comp.State != WashingMachineState.Idle || !_power.IsPowered(ent.Owner) || Storage.IsOpen(ent.Owner)
        || TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            return;

        if (!TryComp<EntityStorageComponent>(ent, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("washing-machine-start"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Act = () =>
            {
                if (Timing.CurTime < ent.Comp.NextWashAllowed)
                {
                    _popup.PopupEntity(Loc.GetString("washing-machine-cooldown"), ent.Owner, user);
                    return;
                }

                TryStartWash(ent, user);
            }
        });
    }

    protected virtual bool TryStartWash(Entity<WashingMachineComponent> ent, EntityUid user)
    {
        if (Timing.CurTime < ent.Comp.NextWashAllowed)
            return false;

        if (!TryComp<EntityStorageComponent>(ent, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return false;

        ent.Comp.State = WashingMachineState.Washing;
        ent.Comp.WashFinishTime = Timing.CurTime + ent.Comp.WashTime;

        Dirty(ent.Owner, ent.Comp);
        Appearance.SetData(ent.Owner, WashingMachineVisuals.State, WashingMachineState.Washing);

        var items = storage.Contents.ContainedEntities.ToHashSet();

        var machineEv = new WashingMachineStartedWashingEvent(items);
        RaiseLocalEvent(ent.Owner, machineEv);

        var itemEv = new WashingMachineIsBeingWashed(ent.Owner, items);
        foreach (var item in items)
        {
            RaiseLocalEvent(item, itemEv);
        }

        return true;
    }

    protected virtual void UpdateForensics(Entity<WashingMachineComponent> ent, HashSet<EntityUid> items) { }
}
