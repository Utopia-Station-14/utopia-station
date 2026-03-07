using Content.Shared.Radio;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Components;

[RegisterComponent]
public sealed partial class ResearchConsoleComponent : Component
{
    /// <summary>
    /// The radio channel that the unlock announcements are broadcast to.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<RadioChannelPrototype> AnnouncementChannel = "Science";

    // Utopia-Tweak : Research
    [ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<TechDisciplinePrototype> CurrentDiscipline;
    // Utopia-Tweak : Research
}
