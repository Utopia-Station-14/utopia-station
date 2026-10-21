using Content.Shared.Inventory;

namespace Content.Shared._Utopia.ModSuits;
public static class ModSuitSlots
{
    public static readonly ModSuitPart[] All =
    {
        ModSuitPart.Helmet,
        ModSuitPart.Body,
        ModSuitPart.Gloves,
        ModSuitPart.Boots,
    };
    public static string GetSlotName(ModSuitPart part)
    {
        return part switch
        {
            ModSuitPart.Helmet => "head",
            ModSuitPart.Body => "outerClothing",
            ModSuitPart.Gloves => "gloves",
            ModSuitPart.Boots => "shoes",
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null),
        };
    }
    public static SlotFlags GetSlotFlags(ModSuitPart part)
    {
        return part switch
        {
            ModSuitPart.Helmet => SlotFlags.HEAD,
            ModSuitPart.Body => SlotFlags.OUTERCLOTHING,
            ModSuitPart.Gloves => SlotFlags.GLOVES,
            ModSuitPart.Boots => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };
    }
    public static bool TryGetPart(SlotFlags flags, out ModSuitPart part)
    {
        foreach (var candidate in All)
        {
            if ((flags & GetSlotFlags(candidate)) == 0)
                continue;

            part = candidate;
            return true;
        }

        part = default;
        return false;
    }
}
