using Content.Server._Utopia.Chat;
using Content.Shared._Utopia.Language;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Language;

[DataDefinition]
public sealed partial class CanHear : ILanguageCondition
{
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField]
    public bool RaiseOnListener { get; set; } = true;

    public bool Condition(EntityUid targetEntity, EntityUid? source, IEntityManager entMan)
    {
        if (!source.HasValue)
            return false;

        var ev = new CanHearVoiceEvent(source.Value, false);
        entMan.EventBus.RaiseLocalEvent(targetEntity, ref ev);

        return !ev.Cancelled;
    }
}

