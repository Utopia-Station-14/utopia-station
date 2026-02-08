using Content.Shared.Construction.Components;

namespace Content.Server._Utopia.Construction.Components
{
    [RequiresExplicitImplementation]
    public interface IRefreshParts
    {
        void RefreshParts(IEnumerable<MachinePartComponent> parts);
    }
}
