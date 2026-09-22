using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared._Utopia.Supermatter.Components;
using Robust.Shared.Audio.Systems;
using Content.Server._Utopia.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Server.GameObjects;

namespace Content.Server._Utopia.Supermatter.Consoles;

public sealed partial class SupermatterConsoleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SupermatterConsoleComponent>(SupermatterConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<SupermatterConsoleFocusChangeMessage>(OnFocusChange);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SupermatterConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (!_ui.IsUiOpen(uid, SupermatterConsoleUiKey.Key))
                continue;

            UpdateUI(uid, console);
        }
    }

    public void PlayAudio(SupermatterStatus status)
    {
        var sound = status switch
        {
            SupermatterStatus.Warning =>
        };
    }

    private void OnUiOpened(Entity<SupermatterConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent, ent.Comp);
    }

    private void OnFocusChange(Entity<SupermatterConsoleComponent> ent, ref SupermatterConsoleFocusChangeMessage args)
    {
        ent.Comp.FocusSupermatter = args.NetEntity;
        Dirty(ent);
        UpdateUI(ent, ent.Comp);
    }

    private void UpdateUI(EntityUid uid, SupermatterConsoleComponent console)
    {
        if (!_ui.IsUiOpen(uid, SupermatterConsoleUiKey.Key))
            return;

        var entries = new List<SupermatterConsoleEntry>();

        var smQuery = EntityQueryEnumerator<SupermatterComponent, MetaDataComponent>();
        while (smQuery.MoveNext(out var smUid, out var sm, out var meta))
        {
            entries.Add(new SupermatterConsoleEntry(
                GetNetEntity(smUid),
                meta.EntityName,
                sm.Integrity,
                sm.Status));
        }

        SupermatterFocusData? focusData = null;

        if (console.FocusSupermatter is { } netFocus &&
            TryGetEntity(netFocus, out var focusUid) &&
            TryComp<SupermatterComponent>(focusUid, out var focusSm) &&
            TryComp<MetaDataComponent>(focusUid, out var focusMeta))
        {
            focusData = new SupermatterFocusData(
                netFocus,
                focusMeta.EntityName,
                focusMeta.EntityPrototype?.ID,
                focusSm.Integrity,
                focusSm.Status,
                focusSm.CurrentTemperature,
                focusSm.MinTemperature,
                focusSm.MaxTemperature,
                focusSm.TotalEnergy,
                focusSm.InternalEnergy,
                focusSm.ExternalEnergy,
                focusSm.Radiation,
                focusSm.AtmosGas.Clone(),
                focusSm.EnergyScaleModifier,
                focusSm.EnergyReductionModifier,
                focusSm.TemperatureScaleModifier,
                focusSm.WasteOutputModifier,
                focusSm.TemperatureProtectionModifier);
        }

        _ui.SetUiState(uid, SupermatterConsoleUiKey.Key, new SupermatterConsoleBoundInterfaceState(entries.ToArray(), focusData));
    }
}
