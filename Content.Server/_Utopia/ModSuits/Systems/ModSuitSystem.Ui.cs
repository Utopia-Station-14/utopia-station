using Content.Shared._Utopia.ModSuits;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private readonly List<Entity<ModSuitModuleComponent>> _uiModuleBuffer = new();

    private void InitializeUi()
    {
        SubscribeLocalEvent<ModSuitComponent, ToggleModSuitUiEvent>(OnToggleUiAction);
    }

    private void OnToggleUiAction(Entity<ModSuitComponent> ent, ref ToggleModSuitUiEvent args)
    {
        if (args.Handled || ent.Comp.Wearer != args.Performer)
            return;

        args.Handled = true;

        _ui.TryToggleUi(ent.Owner, ModSuitUiKey.Key, args.Performer);
        UpdateUi(ent);
    }

    protected override void UpdateUi(Entity<ModSuitComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ModSuitUiKey.Key))
            return;

        var parts = new Dictionary<ModSuitPart, ModSuitPartUiState>(ent.Comp.Parts.Count);
        foreach (var (part, uid) in ent.Comp.Parts)
        {
            parts[part] = new ModSuitPartUiState(GetNetEntity(uid), IsDeployed(ent, part));
        }

        _uiModuleBuffer.Clear();
        CollectModules(ent, _uiModuleBuffer);

        var modules = new List<ModSuitModuleUiState>(_uiModuleBuffer.Count);
        foreach (var module in _uiModuleBuffer)
        {
            modules.Add(new ModSuitModuleUiState(
                GetNetEntity(module.Owner),
                Name(module.Owner),
                module.Comp.Expenditure,
                module.Comp.Active,
                module.Comp.Enabled,
                module.Comp.Toggleable));
        }

        _uiModuleBuffer.Clear();

        var draw = CalculateDraw(ent);
        var charge = GetCharge(ent);
        var (wearerName, wearerJob) = GetWearerIdentity(ent);

        _ui.SetUiState(ent.Owner, ModSuitUiKey.Key, new ModSuitBoundUiState(
            ent.Comp.Wearer is { } wearer ? GetNetEntity(wearer) : null,
            charge,
            draw.Total,
            draw.Speed,
            draw.Modules,
            ent.Comp.PartSwapCost,
            ent.Comp.SpeedEnabled,
            ent.Comp.EquippedParts,
            ent.Comp.Parts.Count,
            ent.Comp.Locked,
            GetModSuitStatus(ent, charge),
            parts,
            modules,
            wearerName,
            wearerJob));
    }

    private (string? Name, string? Job) GetWearerIdentity(Entity<ModSuitComponent> ent)
    {
        if (ent.Comp.Wearer is not { } owner
            || TerminatingOrDeleted(owner)
            || !_idCard.TryFindIdCard(owner, out var idCard))
        {
            return (null, null);
        }

        var name = string.IsNullOrWhiteSpace(idCard.Comp.FullName) ? null : idCard.Comp.FullName;
        var job = string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle) ? null : idCard.Comp.LocalizedJobTitle;
        return (name, job);
    }
}
