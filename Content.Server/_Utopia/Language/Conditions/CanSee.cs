using Content.Shared._Utopia.Language;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Language;

[DataDefinition]
public sealed partial class CanSee : ILanguageCondition
{
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField]
    public bool RaiseOnListener { get; set; } = true;

    public bool Condition(EntityUid targetEntity, EntityUid? source, IEntityManager entMan)
    {
        var ev = new CanSeeAttemptEvent();
        entMan.EventBus.RaiseLocalEvent(targetEntity, ev);

        return ev.Blind;
    }
}
