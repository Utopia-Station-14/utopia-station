using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class MothAccentSystem : RelayAccentSystem<MothAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!; // Utopia-Tweak : Localization

    private static readonly Regex RegexLowerBuzz = new("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new("Z{1,3}");

    // Utopia-Tweak : Localization
    private static readonly Regex RegexLowerZh = new Regex("ж+");
    private static readonly Regex RegexUpperZh = new Regex("Ж+");
    private static readonly Regex RegexLowerZ = new Regex("з+");
    private static readonly Regex RegexUpperZ = new Regex("З+");

    private static readonly List<string> ReplacementsZh = new List<string> { "жж", "жжж" };
    private static readonly List<string> ReplacementsZhUpper = new List<string> { "ЖЖ", "ЖЖЖ" };
    private static readonly List<string> ReplacementsZ = new List<string> { "зз", "ззз" };
    private static readonly List<string> ReplacementsZUpper = new List<string> { "ЗЗ", "ЗЗЗ" };
    // Utopia-Tweak : Localization

    public override string Accentuate(string message, Entity<MothAccentComponent>? ent = null)
    {
        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

        // Utopia-Tweak : Localization
        message = RegexLowerZh.Replace(message, _random.Pick(ReplacementsZh));
        message = RegexUpperZh.Replace(message, _random.Pick(ReplacementsZhUpper));
        message = RegexLowerZ.Replace(message, _random.Pick(ReplacementsZ));
        message = RegexUpperZ.Replace(message, _random.Pick(ReplacementsZUpper));
        // Utopia-Tweak : Localization

        return message;
    }
}
