using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Shared._Utopia.ShiftSchedule;

public static class ShiftScheduleCode
{
    public const int MaxEntries = 30;
    public const int MaxTitleLength = 80;
    public const int MaxDescriptionLength = 500;
    public const int MaxTimeSeconds = 99 * 3600 + 59 * 60 + 59;
    public const int MaxCodeLength = 128 * 1024;

    public static string Encode(IEnumerable<ShiftScheduleEntryState> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append("- title: ").Append(Quote(entry.Title))
                .Append("\n  timeSeconds: ").Append(entry.Time.ToString(CultureInfo.InvariantCulture))
                .Append("\n  description: ").Append(Quote(entry.Description))
                .Append('\n');
        }

        return builder.Length == 0 ? "[]" : builder.ToString();
    }

    public static bool TryDecode(string code, out List<ShiftScheduleCodeEntry> entries)
    {
        entries = [];

        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
            return false;

        try
        {
            var documents = DataNodeParser.ParseYamlStream(new StringReader(code)).ToArray();
            if (documents.Length != 1 || documents[0].Root is not SequenceDataNode sequence || sequence.Count > MaxEntries)
                return false;

            var decoded = new List<ShiftScheduleCodeEntry>(sequence.Count);
            for (var index = 0; index < sequence.Count; index++)
            {
                if (sequence[index] is not MappingDataNode item || item.Count is < 2 or > 3
                || !item.TryGet<ValueDataNode>("title", out var title) || title.IsNull
                || !item.TryGet<ValueDataNode>("timeSeconds", out var time) || time.IsNull
                || !int.TryParse(time.Value, out var timeSeconds))
                    return false;

                var description = string.Empty;
                if (item.TryGet("description", out var descriptionNode))
                {
                    if (descriptionNode is not ValueDataNode value || value.IsNull)
                        return false;

                    description = value.Value;
                }

                decoded[index] = new ShiftScheduleCodeEntry(title.Value, timeSeconds, description);
            }

            entries = decoded;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\0': builder.Append("\\0"); break;
                case '\a': builder.Append("\\a"); break;
                case '\b': builder.Append("\\b"); break;
                case '\t': builder.Append("\\t"); break;
                case '\n': builder.Append("\\n"); break;
                case '\v': builder.Append("\\v"); break;
                case '\f': builder.Append("\\f"); break;
                case '\r': builder.Append("\\r"); break;
                case '\u001b': builder.Append("\\e"); break;

                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}

public sealed record ShiftScheduleCodeEntry(string Title, int TimeSeconds, string Description);
