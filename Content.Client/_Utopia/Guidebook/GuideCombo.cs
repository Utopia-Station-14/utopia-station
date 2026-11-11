using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Content.Client.Guidebook.Richtext;
using Content.Shared._Utopia.Combat;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Utopia.Guidebook;

[UsedImplicitly]
public sealed partial class GuideCombo : UtopiaGuideEntry, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly ResPath IntentsRsi = new("/Textures/_Utopia/Interface/Misc/intents.rsi");

    public GuideCombo()
    {
        IoCManager.InjectDependencies(this);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!args.TryGetValue("Combo", out var comboId))
            return false;

        if (_prototypes.TryIndex<CombatMovePrototype>(comboId, out var combo))
        {
            AddActions(combo.ActionsNeeds);
            AddTitle(comboId, GetNote(args));
        }
        else if (_prototypes.TryIndex<CombatWeaponMovePrototype>(comboId, out var weaponCombo))
        {
            AddActions(weaponCombo.ActionsNeeds);
            AddTitle(comboId, GetNote(args));
        }
        else
        {
            return false;
        }

        control = this;
        return true;
    }

    private void AddActions<TAction>(IEnumerable<TAction> actions) where TAction : struct, Enum
    {
        var rsi = _resources.GetResource<RSIResource>(IntentsRsi);

        foreach (var action in actions)
        {
            var stateName = ToStateName(action);
            if (!rsi.RSI.TryGetState(stateName, out var state))
                throw new InvalidOperationException($"Missing combo action icon state '{stateName}' in {IntentsRsi}");

            AddIcon(state.Frame0, Loc.GetString($"combo-intent-{stateName}"));
        }
    }

    private static string ToStateName<TAction>(TAction action) where TAction : struct, Enum
    {
        var name = action.ToString();
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var chars = name.ToCharArray();
        chars[0] = char.ToLowerInvariant(chars[0]);
        return new string(chars);
    }

    private static string? GetNote(Dictionary<string, string> args)
    {
        return args.TryGetValue("Note", out var note) ? note : null;
    }
}