using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.CollectiveMind;

[RegisterComponent, NetworkedComponent]
public sealed partial class CollectiveMindRankComponent : Component
{
    [DataField]
    public string RankName = "???";
}
