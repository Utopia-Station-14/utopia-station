using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Genetics.Events;

[Serializable, NetSerializable]
public sealed class DnaScannerSequencerButtonPressedMessage(int buttonIndex, char newBase,
    string mutationId) : BoundUserInterfaceMessage
{
    public int ButtonIndex = buttonIndex;
    public char NewBase = newBase;
    public string MutationId = mutationId;
}

[Serializable, NetSerializable]
public sealed class DnaScannerSaveMutationToStorageMessage(string mutationId) : BoundUserInterfaceMessage
{
    public readonly string MutationId = mutationId;
}

[Serializable, NetSerializable]
public sealed class DnaScannerDeleteMutationFromStorageMessage(string mutationId) : BoundUserInterfaceMessage
{
    public readonly string MutationId = mutationId;
}

[Serializable, NetSerializable]
public sealed class DnaScannerPrintActivatorMessage(string mutationId) : BoundUserInterfaceMessage
{
    public readonly string MutationId = mutationId;
}

[Serializable, NetSerializable]
public sealed class DnaScannerPrintMutatorMessage(string mutationId) : BoundUserInterfaceMessage
{
    public readonly string MutationId = mutationId;
}

[Serializable, NetSerializable]
public sealed class DnaScannerScrambleDnaMessage : BoundUserInterfaceMessage
{
    public DnaScannerScrambleDnaMessage() { }
}

[Serializable, NetSerializable]
public sealed class DnaScannerDiscoveredMutationsUpdatedMessage(HashSet<string> discoveredMutationIds) : BoundUserInterfaceMessage
{
    public HashSet<string> DiscoveredMutationIds = discoveredMutationIds;
}

[Serializable, NetSerializable]
public sealed class DnaScannerToggleResearchMessage(string mutationId) : BoundUserInterfaceMessage
{
    public readonly string MutationId = mutationId;
}

[Serializable, NetSerializable]
public sealed class DnaScannerUseJokerMessage : BoundUserInterfaceMessage
{
    public DnaScannerUseJokerMessage() { }
}
