using Content.Shared.ActionBlocker;
using Content.Shared._Utopia.Language;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Language;

[DataDefinition]
public sealed partial class CanSpeak : ILanguageCondition
{
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField]
    public bool RaiseOnListener { get; set; } = false;

    public bool Condition(EntityUid targetEntity, EntityUid? source, IEntityManager entMan)
    {
        var blocker = entMan.System<ActionBlockerSystem>();
        return blocker.CanSpeak(targetEntity);
    }
}
