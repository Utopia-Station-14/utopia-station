using Content.Server.Chat.Systems;
using Content.Shared._Utopia.Combat;
using Content.Shared.Chat;
using Content.Shared.StatusEffectNew;

namespace Content.Server._Utopia.Combat;

[Serializable]
public sealed partial class ComboSpeechEffect : IComboEffect
{
    [DataField]
    public string Speech;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var chat = entMan.System<ChatSystem>();
        chat.TrySendInGameICMessage(user, Loc.GetString(Speech), InGameICChatType.Speak, true, true, checkRadioPrefix: false);
    }
}

[Serializable]
public sealed partial class ComboBlockLungsEffect : IComboEffect
{
    [DataField]
    public int Time;

    public void DoEffect(EntityUid user, EntityUid target, IEntityManager entMan)
    {
        var status = entMan.System<StatusEffectsSystem>();
        status.TryAddStatusEffectDuration(target, "StatusEffectBreathingBlocked", out _, TimeSpan.FromSeconds(Time));
    }
}
