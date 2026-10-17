using Content.Shared.Chat;
using Content.Shared.Speech.Components;
using Content.Shared._Utopia.SpeechBarks;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class VoiceOverrideSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnTransformSpeakerBark(Entity<VoiceOverrideComponent> entity, ref TransformSpeakerBarkEvent args)
    {
        if (!entity.Comp.Enabled)
            return;

        args.Data = entity.Comp.Bark ?? args.Data;
    }
}
