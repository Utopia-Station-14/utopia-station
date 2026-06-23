using Content.Shared._Utopia.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Components
{
    [NetSerializable, Serializable]
    public enum ResearchConsoleUiKey : byte
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleUnlockTechnologyMessage : BoundUserInterfaceMessage
    {
        public string Id;

        public ConsoleUnlockTechnologyMessage(string id)
        {
            Id = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleServerSelectionMessage : BoundUserInterfaceMessage
    {

    }

    [Serializable, NetSerializable]
    public sealed class ResearchConsoleBoundInterfaceState : BoundUserInterfaceState
    {
        public int Points;
        public Dictionary<string, ResearchAvailablity> Researches;
        public ProtoId<TechDisciplinePrototype> CurrentDiscipline;

        public ResearchConsoleBoundInterfaceState(int points, Dictionary<string, ResearchAvailablity> list, ProtoId<TechDisciplinePrototype> currentDiscipline) // Utopia-Tweak : Research
        {
            Points = points;
            // Utopia-Tweak : Research
            Researches = list;
            CurrentDiscipline = currentDiscipline;
            // Utopia-Tweak : Research
        }
    }

    // Utopia-Tweak : Research
    [Serializable, NetSerializable]
    public sealed class ResearchConsoleSelectDisciplineMessage(ProtoId<TechDisciplinePrototype> protoId) : BoundUserInterfaceMessage
    {
        public readonly ProtoId<TechDisciplinePrototype> ProtoId = protoId;
    }
    // Utopia-Tweak : Research
}
