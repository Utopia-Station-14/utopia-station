using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Helpers;

public static partial class UtopiaHelper
{
    [Dependency] private static readonly IPrototypeManager PrototypeManager = default!;

    public static bool IsPrototypeOrParentInList(string entityProtoId, IReadOnlyList<string> list)
    {
        if (list.Contains(entityProtoId))
            return true;

        if (!PrototypeManager.TryIndex<EntityPrototype>(entityProtoId, out var proto) || proto.Parents == null)
            return false;

        return proto.Parents.Any(parent => IsPrototypeOrParentInList(parent, list));
    }
}
