using Content.Shared._Utopia.Language;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Language;

[DataDefinition]
public sealed partial class OnlyToCanUnderstand : ILanguageCondition
{
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField]
    public bool RaiseOnListener { get; set; } = true;

    public bool Condition(EntityUid targetEntity, EntityUid? source, IEntityManager entMan)
    {
        var lang = entMan.System<LanguageSystem>();
        return lang.CanUnderstand(targetEntity, Language);
    }
}
