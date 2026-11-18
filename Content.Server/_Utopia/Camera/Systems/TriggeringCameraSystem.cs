using Robust.Shared.Prototypes;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.SurveillanceCamera;
using Content.Server._Utopia.Camera.Components;
using Content.Server._Utopia.Camera;
using Content.Shared.Trigger;
using Content.Shared.AlertLevel;

namespace Content.Server._Utopia.Camera.Systems;

public sealed partial class TriggeringCameraSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggeringCameraComponent, VoiceTriggeredEvent>(VoiceTriggered);
        SubscribeLocalEvent<TriggeringCameraComponent, AlertLevelChangedEvent>(AlertLevelChanged);
    }

    private void AlertLevelChanged(Entity<TriggeringCameraComponent> ent, ref AlertLevelChangedEvent args)
    {
        if (args.AlertLevel != ent.Comp.Green)
            ent.Comp.Active = false;
        else
            ent.Comp.Active = true;
    }

    private void VoiceTriggered(Entity<TriggeringCameraComponent> ent, ref VoiceTriggeredEvent args)
    {
        if (!TryComp<SurveillanceCameraComponent>(ent.Owner, out var camera))
            return;

        if (!camera.Active || !ent.Comp.Active)
            return;

        if (ent.Comp.KeywordsPrototype == null ||
            !_prototypeManager.TryIndex(ent.Comp.KeywordsPrototype.Value, out TriggeringCameraPrototype? keywordsProto))
        {
            return;
        }

        string message = args.MessageWithoutPhrase ?? string.Empty;
        EntityUid sender = args.Source;

        bool matched = false;
        foreach (var keyword in keywordsProto.Keywords)
        {
            if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
                break;
            }
        }

        if (matched)
            OnKeywordMatched(ent, sender, message);
    }

    private void OnKeywordMatched(Entity<TriggeringCameraComponent> cameraEnt, EntityUid sender, string message)
    {
    }
}
