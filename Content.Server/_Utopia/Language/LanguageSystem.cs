using System.Linq;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Shared._Utopia.Language;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Language;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public int Seed { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);

        SubscribeNetworkEvent<LanguageChosenMessage>(OnLanguageSwitch);
    }

    private void OnMapInit(EntityUid uid, LanguageSpeakerComponent component, MapInitEvent args)
    {
        component.CurrentLanguage ??= component.Languages.Keys
            .Where(x => (int)component.Languages[x] > 0)
            .FirstOrDefault(Universal);

        UpdateUi(uid);
    }

    private void OnRoundStart(RoundStartingEvent args)
    {
        Seed = _random.Next();
    }

    private void OnLanguageSwitch(LanguageChosenMessage args)
    {
        var uid = GetEntity(args.Uid);

        if (!TryComp<LanguageSpeakerComponent>(uid, out var component))
            return;

        if (!GetLanguagesKnowledged(uid, LanguageKnowledge.BadSpeak, out var langs)
        || !langs.ContainsKey(args.SelectedLanguage))
        {
            return;
        }

        component.CurrentLanguage = args.SelectedLanguage;
        UpdateUi(uid);
    }

    public string ObfuscateMessage(EntityUid uid, string originalMessage, List<string> replacements, bool obfiscateSyllables)
    {
        var builder = new StringBuilder();

        if (obfiscateSyllables)
        {
            ObfuscateSyllables(builder, originalMessage, replacements);
        }
        else
        {
            ObfuscatePhrases(builder, originalMessage, replacements);
        }

        var result = builder.ToString();
        result = _chat.SanitizeInGameICMessageLanguages(uid, result, out _);

        return result;
    }

    private void ObfuscateSyllables(StringBuilder builder, string message, List<string> replacements)
    {
        var wordBeginIndex = 0;
        var hashCode = 0;
        var newSentence = true;

        for (var i = 0; i < message.Length; i++)
        {
            var currentChar = char.ToLower(message[i]);
            var isEndOfWord = char.IsWhiteSpace(currentChar)
                || currentChar is '.' or '!' or '?' or '~' or '-' or ','
                || i == message.Length - 1;

            if (isEndOfWord)
            {
                var wordLength = i - wordBeginIndex;

                if (wordLength > 0)
                {
                    var syllablesCount = PseudoRandomNumber(hashCode, 1, 4);

                    for (var j = 0; j < syllablesCount; j++)
                    {
                        var index = PseudoRandomNumber(hashCode + j, 0, replacements.Count);
                        var replacement = replacements[index];

                        if (newSentence)
                        {
                            var replacementBuilder = new StringBuilder(replacement);
                            replacementBuilder[0] = char.ToUpper(replacement[0]);
                            replacement = replacementBuilder.ToString();
                            newSentence = false;
                        }

                        builder.Append(replacement);
                    }
                }

                if (char.IsWhiteSpace(currentChar) || currentChar is '.' or '!' or '?' or '~' or '-' or ',')
                {
                    builder.Append(currentChar);
                }

                var hasNextChar = message.Length >= i + 2;
                var nextChar = hasNextChar ? char.ToLower(message[i + 1]) : '\0';
                var isPunctuation = currentChar is '.' or '!' or '?' or '~' or ',';
                var isNextNotPunctuation = nextChar is not ('.' or '!' or '?' or '~' or ',');

                if (isPunctuation && hasNextChar && isNextNotPunctuation)
                {
                    builder.Append(' ');
                }

                if (currentChar is '.' or '!' or '?')
                {
                    newSentence = true;
                }

                hashCode = 0;
                wordBeginIndex = i + 1;
            }
            else
            {
                hashCode = hashCode * 31 + currentChar;
            }
        }
    }

    private void ObfuscatePhrases(StringBuilder builder, string message, List<string> replacements)
    {
        var sentenceBeginIndex = 0;

        for (var i = 0; i < message.Length; i++)
        {
            var currentChar = char.ToLower(message[i]);
            var isEndOfSentence = currentChar is '.' or '!' or '?' or '~' or '-' or ',' ||
                i == message.Length - 1;

            if (isEndOfSentence)
            {
                var sentenceLength = i + 1 - sentenceBeginIndex;

                if (sentenceLength > 0)
                {
                    var phraseCount = (int)Math.Clamp(Math.Cbrt(sentenceLength) - 1, 1, 4);

                    for (var j = 0; j < phraseCount; j++)
                    {
                        var phrase = _random.Pick(replacements);
                        builder.Append(phrase);
                    }
                }

                sentenceBeginIndex = i + 1;

                if (currentChar is '.' or '!' or '?')
                {
                    builder.Append(currentChar).Append(' ');
                }
            }
        }
    }

    private int PseudoRandomNumber(int seed, int min, int max)
    {
        seed += Seed;
        var random = (seed * 1103515245 + 12345) & 0x7fffffff;
        return random % (max - min) + min;
    }

    public string AccentuateMessage(EntityUid uid, string lang, string message)
    {
        if (!GetLanguagesKnowledged(uid, LanguageKnowledge.BadSpeak, out var langs))
            return message;

        if (!langs.TryGetValue(lang, out var knowledgeLevel))
            return message;

        if ((int)knowledgeLevel > (int)LanguageKnowledge.BadSpeak)
            return message;

        var sb = new StringBuilder();

        foreach (var character in message)
        {
            if (_random.Prob(0.2f / 3f))
            {
                var lower = char.ToLowerInvariant(character);
                var newString = lower switch
                {
                    'o' => "u",
                    's' => "ch",
                    'a' => "ah",
                    'u' => "oo",
                    'c' => "k",
                    'о' => "а",
                    'к' => "кх",
                    'щ' => "шч",
                    'ц' => "тс",
                    _ => $"{character}",
                };

                sb.Append(newString);
            }

            if (_random.Prob(0.5f * 3 / 20))
            {
                var next = _random.Next(1, 3) switch
                {
                    1 => "'",
                    2 => $"{character}{character}",
                    _ => $"{character}{character}{character}",
                };

                sb.Append(next);
            }
            else
            {
                sb.Append(character);
            }
        }

        return sb.ToString();
    }

    public override void UpdateUi(EntityUid uid, LanguageSpeakerComponent? comp = null)
    {
        base.UpdateUi(uid, comp);

        if (!Resolve(uid, ref comp, false))
            return;

        Dirty(uid, comp);

        if (!GetLanguagesKnowledged(uid, LanguageKnowledge.Understand, out var langs))
            return;

        if (!GetLanguages(uid, out _, out var translator, out var current))
            return;

        if (!_mind.TryGetMind(uid, out _, out var mind) || mind == null)
            return;

        if (!_player.TryGetSessionById(mind.UserId, out var session))
            return;

        foreach (var item in langs.ToList())
        {
            var proto = _proto.Index<LanguagePrototype>(item.Key);
            if (!proto.ShowUnderstood && item.Value < LanguageKnowledge.BadSpeak)
            {
                langs.Remove(item.Key);
            }
        }

        var state = new LanguageMenuStateMessage(GetNetEntity(uid), current, langs, translator);
        RaiseNetworkEvent(state, session);
    }
}
