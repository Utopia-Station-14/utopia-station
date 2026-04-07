using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Utopia.Combat;

[ImplicitDataDefinitionForInheritors]
public partial interface IComboEffect
{
    void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan);
}

[Serializable, NetSerializable]
public sealed partial class ComboDamageEffect : IComboEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var damageable = entMan.System<DamageableSystem>();
        damageable.TryChangeDamage(target, Damage);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboDamageToUserEffect : IComboEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var damageable = entMan.System<DamageableSystem>();
        damageable.TryChangeDamage(user, Damage);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboStaminaDamageEffect : IComboEffect
{
    [DataField(required: true)]
    public int StaminaDamage;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var stun = entMan.System<SharedStaminaSystem>();
        stun.TakeStaminaDamage(target, StaminaDamage);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboSpawnEffect : IComboEffect
{
    [DataField]
    public string? SpawnOnUser;

    [DataField]
    public string? SpawnOnTarget;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (SpawnOnTarget != null)
        {
            entMan.SpawnAtPosition(SpawnOnTarget, target.ToCoordinates());
        }

        if (SpawnOnUser != null)
        {
            entMan.SpawnAtPosition(SpawnOnUser, user.ToCoordinates());
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboFallEffect : IComboEffect
{
    [DataField]
    public bool DropItems;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var standing = entMan.System<StandingStateSystem>();
        if (!standing.IsDown(target))
        {
            var stun = entMan.System<SharedStunSystem>();
            stun.TryKnockdown(target, time: null, drop: DropItems, force: false);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboMoreDamageToDownedEffect : IComboEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage;

    [DataField]
    public bool IgnoreResistances;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var down = entMan.System<StandingStateSystem>();
        var damageable = entMan.System<DamageableSystem>();
        if (down.IsDown(target))
        {
            damageable.TryChangeDamage(target, Damage, IgnoreResistances);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboStunEffect : IComboEffect
{
    [DataField]
    public bool Fall = true;

    [DataField]
    public int StunTime;

    [DataField]
    public bool DropItems = true;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (!entMan.HasComponent<StatusEffectsComponent>(target))
            return;

        var down = entMan.System<SharedStunSystem>();
        down.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(StunTime));

        if (Fall)
        {
            down.TryKnockdown(target, time: TimeSpan.FromSeconds(StunTime), drop: DropItems, force: true);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboPopupEffect : IComboEffect
{
    [DataField(required: true)]
    public string LocaleText;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var popup = entMan.System<SharedPopupSystem>();
        popup.PopupPredicted(Loc.GetString(LocaleText, ("user", Identity.Entity(user, entMan)), ("target", target)),
            target, target, PopupType.LargeCaution);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboDropFromHandsEffect : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var hands = entMan.System<SharedHandsSystem>();
        if (!entMan.TryGetComponent<HandsComponent>(target, out var hand) || hand.ActiveHandId == null)
            return;

        hands.DoDrop(target, hand.ActiveHandId);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboHandsRetakeEffect : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var hands = entMan.System<SharedHandsSystem>();

        if (!hands.TryGetActiveItem(target, out var activeItem)
        || !hands.TryDrop(target, activeItem.Value))
            return;

        if (!hands.TryGetEmptyHand(user, out var emptyHand)
        || !hands.TryPickup(user, activeItem.Value, emptyHand))
            return;

        hands.SetActiveHand(user, emptyHand);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboAudioEffect : IComboEffect
{
    [DataField(required: true)]
    public SoundSpecifier Sound;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var audio = entMan.System<SharedAudioSystem>();
        var coords = user.ToCoordinates();

        audio.PlayPredicted(Sound, coords, null);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboAudioTargetEffect : IComboEffect
{
    [DataField(required: true)]
    public SoundSpecifier Sound;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var audio = entMan.System<SharedAudioSystem>();
        var coords = target.ToCoordinates();

        audio.PlayPredicted(Sound, coords, null);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboMuteEffect : IComboEffect
{
    [DataField]
    public int Time;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var status = entMan.System<StatusEffectNew.StatusEffectsSystem>();
        status.TryAddStatusEffectDuration(target, "StatusEffectMuted", out _, TimeSpan.FromSeconds(Time));
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboSlowdownEffect : IComboEffect
{
    [DataField]
    public int Time;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var status = entMan.System<StatusEffectsSystem>();
        status.TryAddStatusEffect<StunnedStatusEffectComponent>(target, "SlowedDown", TimeSpan.FromSeconds(Time), false);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboMoreStaminaDamageToDownedEffect : IComboEffect
{
    [DataField(required: true)]
    public float Damage;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var down = entMan.System<StandingStateSystem>();
        var stun = entMan.System<SharedStaminaSystem>();

        if (down.IsDown(target))
        {
            stun.TakeStaminaDamage(target, Damage);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboFlashEffect : IComboEffect
{
    [DataField]
    public float Duration;

    [DataField]
    public float SlowDown;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var status = entMan.System<StatusEffectsSystem>();
        var blind = entMan.System<BlindableSystem>();

        status.TryAddStatusEffect<FlashedComponent>(target, "Flashed", TimeSpan.FromSeconds(Duration), true);
        blind.AdjustEyeDamage(target, 1);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboStopGrabEffect : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var pull = entMan.System<PullingSystem>();

        if (!entMan.TryGetComponent<PullerComponent>(user, out var puller)
        || !entMan.TryGetComponent<PullableComponent>(target, out var pulled))
            return;

        for (var i = (int)puller.Stage; i > 0; i--)
        {
            pull.TryLowerGrabStageOrStopPulling((user, puller), (target, pulled));
        }

        pull.TryStopPull(target, pulled, user);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboStopTargetGrabEffect : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var pull = entMan.System<PullingSystem>();
        if (!entMan.TryGetComponent<PullerComponent>(target, out var puller)
        || !entMan.TryGetComponent<PullableComponent>(user, out var pulled))
            return;

        for (var i = puller.Stage; i > 0; i--)
        {
            pull.TryLowerGrabStageOrStopPulling((target, puller), (user, pulled));
        }

        pull.TryStopPull(user, pulled, target);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboThrowTargetEffect : IComboEffect
{
    [DataField]
    public float ThrownSpeed = 7f;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var pull = entMan.System<PullingSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var mapPos = transform.GetMapCoordinates(user).Position;
        var hitPos = transform.GetMapCoordinates(target).Position;
        var dir = hitPos - mapPos;

        pull.Throw(target, dir, ThrownSpeed);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboThrowOnUserEffect : IComboEffect
{
    [DataField]
    public float ThrownSpeed = 7f;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var pull = entMan.System<PullingSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var mapPos = transform.GetMapCoordinates(user).Position;
        var hitPos = transform.GetMapCoordinates(target).Position;
        var dir = mapPos - hitPos;

        pull.Throw(target, dir, ThrownSpeed);
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboEffectTeleportOnVictim : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var transform = entMan.System<SharedTransformSystem>();

        if (entMan.HasComponent<MobStateComponent>(target))
        {
            transform.SetCoordinates(user, transform.GetMoverCoordinates(target));
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboEffectSwapPostion : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var transform = entMan.System<SharedTransformSystem>();

        if (entMan.HasComponent<MobStateComponent>(target))
        {
            transform.SwapPositions(user, target);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboEffectSleep : IComboEffect
{
    [DataField]
    public int Time;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var status = entMan.System<StatusEffectNew.StatusEffectsSystem>();

        status.TryAddStatusEffectDuration(target, "StatusEffectForcedSleeping", out _, TimeSpan.FromSeconds(Time));
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboEffectAddToCounter : IComboEffect
{
    [DataField]
    public int Amount = 1;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var counter = entMan.System<ComboCounterSystem>();

        if (entMan.TryGetComponent<ComboCounterComponent>(user, out var comp))
        {
            counter.AddToCounter(comp, Amount);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboEffectClearCounter : IComboEffect
{
    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (entMan.TryGetComponent<ComboCounterComponent>(user, out var comp))
        {
            comp.ComboCounter = 0;
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class ComboEffectCounterDamageBonus : IComboEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (entMan.TryGetComponent<MobStateComponent>(target, out var state)
        && state.CurrentState != MobState.Dead)
        {
            if (entMan.TryGetComponent<ComboCounterComponent>(user, out var comp))
            {
                var damageable = entMan.System<DamageableSystem>();
                var newDamage = Damage * comp.ComboCounter;
                damageable.TryChangeDamage(target, newDamage);
            }
        }
    }
}

[Serializable]
public sealed partial class ComboDelayedEffect : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    [DataField(required: true)]
    public TimeSpan Delay;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        Timer.Spawn(Delay, () =>
        {
            foreach (var comboEvent in ComboEvents)
            {
                comboEvent.DoEffect(user, target, entMan);
            }
        });
    }
}

[Serializable]
public sealed partial class ComboEffectOnCounterDoCombo : IComboEffect
{
    [DataField]
    public int Amount = 1;

    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (entMan.TryGetComponent<ComboCounterComponent>(user, out var comp)
        && Amount == comp.ComboCounter)
        {
            foreach (var comboEvent in ComboEvents)
            {
                comboEvent.DoEffect(user, target, entMan);
            }
        }
    }
}

[Serializable]
public sealed partial class ComboEffectToDowned : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var down = entMan.System<StandingStateSystem>();
        if (down.IsDown(target))
        {
            foreach (var comboEvent in ComboEvents)
            {
                comboEvent.DoEffect(user, target, entMan);
            }
        }
    }
}

[Serializable]
public sealed partial class ComboEffectUserWalking : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (entMan.TryGetComponent<InputMoverComponent>(user, out var comp)
        && !comp.Sprinting)
        {
            foreach (var comboEvent in ComboEvents)
            {
                comboEvent.DoEffect(user, target, entMan);
            }
        }
    }
}

[Serializable]
public sealed partial class ComboEffectUserSprinting : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (entMan.TryGetComponent<InputMoverComponent>(user, out var comp)
        && comp.Sprinting)
        {
            foreach (var comboEvent in ComboEvents)
            {
                comboEvent.DoEffect(user, target, entMan);
            }
        }
    }
}

[Serializable]
public sealed partial class ComboEffectToStanding : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var down = entMan.System<StandingStateSystem>();
        if (!down.IsDown(target))
        {
            foreach (var comboEvent in ComboEvents)
            {
                comboEvent.DoEffect(user, target, entMan);
            }
        }
    }
}

[Serializable]
public sealed partial class ComboEffectToUserPuller : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<PullableComponent>(user, out var pullable))
            return;

        if (pullable.Puller == null || pullable.Puller != target)
            return;

        if (ComboEvents == null)
            return;

        foreach (var comboEvent in ComboEvents)
        {
            comboEvent?.DoEffect(user, target, entMan);
        }
    }
}

[Serializable]
public sealed partial class ComboEffectByUserPuller : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<PullableComponent>(user, out var pullable))
            return;

        if (pullable.Puller == null || pullable.Puller != target)
            return;

        if (ComboEvents == null)
            return;

        foreach (var comboEvent in ComboEvents)
        {
            comboEvent?.DoEffect(target, user, entMan);
        }
    }
}

[Serializable]
public sealed partial class ComboEffectToPulled : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<PullerComponent>(user, out var puller))
            return;

        if (puller.Pulling == null || puller.Pulling != target)
            return;

        if (ComboEvents == null)
            return;

        foreach (var comboEvent in ComboEvents)
        {
            comboEvent?.DoEffect(user, target, entMan);
        }
    }
}

[Serializable]
public sealed partial class ComboEffectToStuned : IComboEffect
{
    [DataField(required: true)]
    public List<IComboEffect> ComboEvents = new();

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        if (!entMan.HasComponent<StunnedComponent>(target))
            return;

        if (ComboEvents == null)
            return;

        foreach (var comboEvent in ComboEvents)
        {
            comboEvent?.DoEffect(user, target, entMan);
        }
    }
}
