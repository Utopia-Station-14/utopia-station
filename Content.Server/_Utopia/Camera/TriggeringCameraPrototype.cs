using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Camera;

[Prototype]
public sealed partial class TriggeringCameraPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public HashSet<string> Keywords { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
