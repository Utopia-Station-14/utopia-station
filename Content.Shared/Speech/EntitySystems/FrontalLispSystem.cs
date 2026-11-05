using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Robust.Shared.Random;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class FrontalLispSystem : RelayAccentSystem<FrontalLispComponent>
{
    // @formatter:off
    private static readonly Regex RegexUpperTh = new("[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new("[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new("[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new("[e]+[x]+[c]*|[x]+");
    // @formatter:on

    // Utopia-Tweak : Localization
    private static readonly Regex RegexLowerC = new Regex("с");// для "с" на "ш"/"с"
    private static readonly Regex RegexUpperC = new Regex("С");// для "С" на "Ш"/"С"
    private static readonly Regex RegexLowerCh = new Regex("ч");// для "ч" на "ш"/"ч"
    private static readonly Regex RegexUpperCh = new Regex("Ч");// для "Ч" на "Ш"/"Ч"
    private static readonly Regex RegexLowerTs = new Regex("ц");// для "ц" на "ч"/"ц"
    private static readonly Regex RegexUpperTs = new Regex("Ц");// для "Ц" на "Ч"/"Ц"
    private static readonly Regex RegexLowerT = new Regex(@"\B[т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex RegexUpperT = new Regex(@"\B[Т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex RegexLowerZ = new Regex("з");// для "з" на "ж"/"з"
    private static readonly Regex RegexUpperZ = new Regex("З");// для "З" на "Ж"/"З"
    // Utopia-Tweak : Localization
    [Dependency] private IRobustRandom _random = default!; // Utopia-Tweak : Localization
    public override string Accentuate(string message, Entity<FrontalLispComponent>? ent = null)
    {
        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "TH");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "EKTH");
        message = RegexLowerEcks.Replace(message, "ekth");

        // Utopia-Tweak : Localization
        message = RegexLowerC.Replace(message, _random.Prob(0.90f) ? "ш" : "с");
        message = RegexUpperC.Replace(message, _random.Prob(0.90f) ? "Ш" : "С");
        message = RegexLowerCh.Replace(message, _random.Prob(0.90f) ? "ш" : "ч");
        message = RegexUpperCh.Replace(message, _random.Prob(0.90f) ? "Ш" : "Ч");
        message = RegexLowerTs.Replace(message, _random.Prob(0.90f) ? "ч" : "ц");
        message = RegexUpperTs.Replace(message, _random.Prob(0.90f) ? "Ч" : "Ц");
        message = RegexLowerT.Replace(message, _random.Prob(0.90f) ? "ч" : "т");
        message = RegexUpperT.Replace(message, _random.Prob(0.90f) ? "Ч" : "Т");
        message = RegexLowerZ.Replace(message, _random.Prob(0.90f) ? "ж" : "з");
        message = RegexUpperZ.Replace(message, _random.Prob(0.90f) ? "Ж" : "З");
        // Utopia-Tweak : Localization

        return message;
    }
}
