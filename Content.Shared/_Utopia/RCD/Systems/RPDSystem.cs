using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Database;
using Content.Shared.RCD.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.RCD.Systems;

public sealed partial class RCDSystem : EntitySystem
{
    [Dependency] private readonly SharedAtmosPipeLayersSystem _pipeLayersSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly PipeRestrictOverlapSystem _pipeOverlap = default!;

    private void OnStartup(EntityUid uid, RCDComponent component, ComponentStartup args)
    {
        UpdateCachedPrototype(uid, component);
        Dirty(uid, component);

        return;
    }

    private void OnRPDEyeRotationEvent(RPDEyeRotationEvent ev, EntitySessionEventArgs session)
    {
        var uid = GetEntity(ev.NetEntity);

        if (session.SenderSession.AttachedEntity is not { } player)
            return;

        if (_hands.GetActiveItem(player) != uid)
            return;

        if (!TryComp<RCDComponent>(uid, out var rcd))
            return;

        if (rcd.LastKnownEyeRotation != ev.EyeRotation)
        {
            rcd.LastKnownEyeRotation = ev.EyeRotation;
        }
    }

    private void OnGetUtilityVerb(EntityUid uid, RCDComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !component.IsRpd)
            return;

        var verb = new UtilityVerb
        {
            Act = () => SwitchPipeMode(uid, component, args.User),
            Text = Loc.GetString("rcd-verb-switch-mode"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Impact = LogImpact.Low
        };

        args.Verbs.Add(verb);
    }

    private void OnGetAlternativeVerb(EntityUid uid, RCDComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !component.IsRpd || !args.Using.HasValue)
            return;

        if (args.Using.Value != uid)
            return;

        var verb = new AlternativeVerb
        {
            Act = () => SwitchPipeMode(uid, component, args.User),
            Text = Loc.GetString("rcd-verb-switch-mode"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Impact = LogImpact.Low
        };

        args.Verbs.Add(verb);
    }

    private void OnRCDConstructionGhostFlipEvent(RCDConstructionGhostFlipEvent ev, EntitySessionEventArgs session)
    {
        var uid = GetEntity(ev.NetEntity);

        if (session.SenderSession.AttachedEntity is not { } player)
            return;

        if (_hands.GetActiveItem(player) != uid)
            return;

        if (!TryComp<RCDComponent>(uid, out var rcd))
            return;

        rcd.UseMirrorPrototype = ev.UseMirrorPrototype;
        Dirty(uid, rcd);
    }

    private void SwitchPipeMode(EntityUid uid, RCDComponent component, EntityUid? user = null)
    {
        if (!component.IsRpd)
            return;

        component.CurrentMode = component.CurrentMode switch
        {
            RpdMode.Primary => RpdMode.Secondary,
            RpdMode.Secondary => RpdMode.Tertiary,
            RpdMode.Tertiary => RpdMode.Free,
            RpdMode.Free => RpdMode.Primary,
            _ => RpdMode.Free
        };

        Dirty(uid, component);

        if (user != null)
            _audio.PlayPredicted(component.SoundSwitchMode, uid, user.Value);
    }

    public void UpdateCachedPrototype(EntityUid uid, RCDComponent component)
    {
        if (component.ProtoId.Id != component.CachedPrototype?.Prototype ||
            component.CachedPrototype?.MirrorPrototype != null &&
            component.ProtoId.Id != component.CachedPrototype?.MirrorPrototype)
        {
            component.CachedPrototype = _protoManager.Index(component.ProtoId);
        }
    }

    public RpdMode GetCurrentRpdMode(EntityUid uid, RCDComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return RpdMode.Free;

        return component.CurrentMode;
    }
}
