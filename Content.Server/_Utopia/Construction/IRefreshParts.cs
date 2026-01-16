using Content.Shared.Construction.Components;

namespace Content.Server.Utopia.Construction.Components
{
    [RequiresExplicitImplementation]
    public interface IRefreshParts
    {
        void RefreshParts(IEnumerable<MachinePartComponent> parts);
    }
}
