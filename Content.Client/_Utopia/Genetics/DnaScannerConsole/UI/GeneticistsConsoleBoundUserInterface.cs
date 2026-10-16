using Robust.Client.UserInterface;
using Content.Shared._Utopia.Genetics.Components;
using Content.Shared._Utopia.Genetics.Events;

namespace Content.Client._Utopia.Genetics.DnaScannerConsole.UI;

public sealed class GeneticistsConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GeneticistsConsoleWindow? _mainWindow;

    public GeneticistsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _mainWindow = this.CreateWindow<GeneticistsConsoleWindow>();

        if (EntMan.TryGetComponent(Owner, out MetaDataComponent? meta))
            _mainWindow.Title = meta.EntityName;

        _mainWindow.OnSequencerButtonPressed += (index, newBase, mutationId) =>
            SendMessage(new DnaScannerSequencerButtonPressedMessage(index, newBase, mutationId));

        _mainWindow.OnSaveMutationPressed += mutationId =>
            SendMessage(new DnaScannerSaveMutationToStorageMessage(mutationId));

        _mainWindow.OnDeleteMutationPressed += mutationId =>
            SendMessage(new DnaScannerDeleteMutationFromStorageMessage(mutationId));

        _mainWindow.OnPrintActivatorPressed += mutationId =>
            SendMessage(new DnaScannerPrintActivatorMessage(mutationId));

        _mainWindow.OnPrintMutatorPressed += mutationId =>
            SendMessage(new DnaScannerPrintMutatorMessage(mutationId));

        _mainWindow.OnScrambleDnaPressed += () =>
            SendMessage(new DnaScannerScrambleDnaMessage());

        _mainWindow.OnToggleResearchPressed += mutationId =>
            SendMessage(new DnaScannerToggleResearchMessage(mutationId));

        _mainWindow.OnJokerUsed += () =>
            SendMessage(new DnaScannerUseJokerMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GeneticistsConsoleBoundUserInterfaceState scannerState || _mainWindow == null)
            return;

        _mainWindow.UpdateSubjectInfo(
            scannerState.SubjectName,
            scannerState.HealthStatus,
            scannerState.RadiationDamage,
            scannerState.SubjectGeneticInstability,
            scannerState.ScrambleCooldownEnd
        );

        _mainWindow.UpdateResearchData(
            scannerState.ResearchRemaining,
            scannerState.ResearchOriginal,
            scannerState.ActiveResearchMutationIds ?? new HashSet<string>()
        );

        if (scannerState.IsFullUpdate)
        {
            _mainWindow.UpdateGeneticsTab(scannerState.Mutations, scannerState.BaseMutationIds);
            _mainWindow.UpdateDiscoveredMutations(scannerState.DiscoveredMutationIds);
            _mainWindow.UpdateSavedMutations(scannerState.SavedMutations);
        }

        _mainWindow.UpdateResearchLabel();
        _mainWindow.UpdateJokerCooldown(scannerState.JokerCooldownEnd);
    }
}
