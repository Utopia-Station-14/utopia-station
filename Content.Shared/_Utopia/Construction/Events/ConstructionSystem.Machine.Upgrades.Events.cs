using Content.Shared.Stacks;
using Robust.Shared.Utility;

namespace Content.Shared.Construction.Components;

public struct MachinePartState
{
    public MachinePartComponent Part;
    public StackComponent? Stack;

    public int Quantity()
    {
        return Stack?.Count ?? 1;
    }
}

public sealed class RefreshPartsEvent : EntityEventArgs
{
    public IReadOnlyList<MachinePartState> Parts = [];

    public Dictionary<string, float> PartTiers = [];
}

public sealed class UpgradeExamineEvent : EntityEventArgs
{
    private FormattedMessage _message;

    public UpgradeExamineEvent(ref FormattedMessage message)
    {
        _message = message;
    }

    public void AddPercentageUpgrade(string upgradedLocId, float multiplier)
    {
        var percent = Math.Round(100 * MathF.Abs(multiplier - 1), 2);
        var locId = multiplier switch
        {
            < 1 => "machine-upgrade-decreased-by-percentage",
            1 or float.NaN => "machine-upgrade-not-upgraded",
            > 1 => "machine-upgrade-increased-by-percentage",
        };
        var upgraded = Loc.GetString(upgradedLocId);
        _message.AddMarkupOrThrow(Loc.GetString(locId, ("upgraded", upgraded), ("percent", percent)) + '\n');
    }

    public void AddNumberUpgrade(string upgradedLocId, int number)
    {
        var difference = Math.Abs(number);
        var locId = number switch
        {
            < 0 => "machine-upgrade-decreased-by-amount",
            0 => "machine-upgrade-not-upgraded",
            > 0 => "machine-upgrade-increased-by-amount",
        };
        var upgraded = Loc.GetString(upgradedLocId);
        _message.AddMarkupOrThrow(Loc.GetString(locId, ("upgraded", upgraded), ("difference", difference)) + '\n');
    }
}
