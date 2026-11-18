using Content.Shared._Utopia.Economy.Components;
using Content.Shared.Paper;
using Content.Shared.Station;
using Content.Shared.Cargo.Components;

namespace Content.Shared._Utopia.Economy.Systems;

public sealed partial class CommandBudgetSystem : SharedEconomySystem
{
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedStationSystem _station = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<CommandBudgetPinPaperComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(_station.GetOwningStation(ent), out StationBankAccountComponent? stationBank))
            return;

        if (ent.Comp.CommandBudgetType == null)
            return;

        stationBank.BankAccounts.TryGetValue(ent.Comp.CommandBudgetType.Value, out var account);

        if (account != null)
        {
            var pin = account.AccountPin;
            _paper.SetContent((ent, EnsureComp<PaperComponent>(ent)), Loc.GetString("command-budget-pin-message", ("pin", pin)));
        }
    }
}
