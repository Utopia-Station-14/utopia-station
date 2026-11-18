using Robust.Shared.Prototypes;
using Content.Shared.AlertLevel;
using Content.Server._Utopia.Camera;

namespace Content.Server._Utopia.Camera.Components;

[RegisterComponent]
public sealed partial class TriggeringCameraComponent : Component
{
    [DataField]
    public bool Active = false;

    [DataField]
    public ProtoId<AlertLevelPrototype> Green = new ProtoId<AlertLevelPrototype>("Green");

    [DataField]
    public ProtoId<TriggeringCameraPrototype>? KeywordsPrototype;
}
