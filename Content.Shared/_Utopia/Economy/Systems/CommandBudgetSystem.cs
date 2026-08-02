using Content.Shared.Paper;
using Content.Shared.Station;
using Content.Shared.Cargo.Components;

namespace Content.Shared._Utopia.Economy;

public sealed class CommandBudgetSystem : SharedEconomySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommandBudgetPinPaperComponent, MapInitEvent>(OnMapInit);
    }

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
