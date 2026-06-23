using Content.Client.Effects;
using Content.Client.Popups;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Client._Utopia.Pulling.Systems;

public sealed partial class ClientPullingSystem : PullingSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;

    public override bool TryIncreaseGrabStage(Entity<PullerComponent> puller, Entity<PullableComponent> pullable)
    {
        if (!base.TryIncreaseGrabStage(puller, pullable))
            return false;

        var targetStage = puller.Comp.Stage + 1;

        var popupType = targetStage switch
        {
            GrabStage.Soft => PopupType.Small,
            GrabStage.Hard => PopupType.SmallCaution,
            GrabStage.Choke => PopupType.MediumCaution,
            _ => PopupType.Small,
        };

        var stageName = targetStage.ToString().ToLower();
        var targetName = Identity.Entity(pullable, EntityManager);

        _popup.PopupPredicted(
            Loc.GetString($"grab-increase-{stageName}-popup-self", ("target", targetName)),
            Loc.GetString($"grab-increase-{stageName}-popup-others", ("target", targetName), ("puller", targetName)),
            pullable,
            puller,
            popupType
        );

        _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg"), pullable, puller);
        _color.RaiseEffect(Color.Yellow, new List<EntityUid> { pullable.Owner }, Filter.Pvs(pullable.Owner));

        return true;
    }

    public override bool TryLowerGrabStage(Entity<PullerComponent> puller, Entity<PullableComponent> pullable, EntityUid user)
    {
        if (!base.TryLowerGrabStage(puller, pullable, user))
            return false;

        var targetStage = puller.Comp.Stage - 1;

        if (user == pullable.Owner)
            return true;

        var stageName = targetStage.ToString().ToLower();
        var targetName = Identity.Entity(pullable, EntityManager);

        _popup.PopupPredicted(
            Loc.GetString($"grab-lower-{stageName}-popup-self", ("target", targetName)),
            Loc.GetString($"grab-lower-{stageName}-popup-others", ("target", targetName), ("puller", targetName)),
            pullable,
            puller,
            PopupType.Small
        );

        _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg"), pullable, puller);

        return true;
    }
}
