using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Language;

[ImplicitDataDefinitionForInheritors]
public partial interface ILanguageCondition
{
    ProtoId<LanguagePrototype> Language { get; set; }

    bool RaiseOnListener { get; set; }

    bool Condition(EntityUid targetEntity, EntityUid? source, IEntityManager entMan);
}
