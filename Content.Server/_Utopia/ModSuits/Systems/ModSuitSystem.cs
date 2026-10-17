using System.Linq;
using Content.Shared._Utopia.ModSuits;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private static readonly TimeSpan UiUpdateDelay = TimeSpan.FromSeconds(1);

    private static readonly Dictionary<ModSuitPart, string> Slots = new()
    {
        [ModSuitPart.Helmet] = "head",
        [ModSuitPart.Body] = "outerClothing",
        [ModSuitPart.Gloves] = "gloves",
        [ModSuitPart.Boots] = "shoes",
    };

    private readonly HashSet<EntityUid> _pendingReturn = new();
    private readonly HashSet<EntityUid> _deploying = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ModSuitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ModSuitComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<ModSuitComponent, GotEquippedEvent>(OnSuitEquipped);
        SubscribeLocalEvent<ModSuitComponent, GotUnequippedEvent>(OnSuitUnequipped);
        SubscribeLocalEvent<ModSuitComponent, BeingUnequippedAttemptEvent>(OnSuitUnequipAttempt);
        SubscribeLocalEvent<ModSuitComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ModSuitComponent, PowerCellSlotEmptyEvent>(OnCellEmpty);

        SubscribeLocalEvent<ModSuitComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<ModSuitComponent, EntRemovedFromContainerMessage>(OnContainerModified);

        SubscribeLocalEvent<ModSuitComponent, ToggleModSuitUiEvent>(OnToggleUiAction);
        SubscribeLocalEvent<ModSuitComponent, ModSuitTogglePartMessage>(OnTogglePartMessage);
        SubscribeLocalEvent<ModSuitComponent, ModSuitToggleAllMessage>(OnToggleAllMessage);
        SubscribeLocalEvent<ModSuitComponent, ModSuitToggleSpeedMessage>(OnToggleSpeedMessage);
        SubscribeLocalEvent<ModSuitComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);

        SubscribeLocalEvent<ModSuitComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ModSuitComponent, ModSuitPanelDoAfterEvent>(OnPanelDoAfter);
        SubscribeLocalEvent<ModSuitComponent, ModSuitDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<ModSuitComponent, ModSuitManualRemoveDoAfterEvent>(OnManualRemoveDoAfter);
        SubscribeLocalEvent<ModSuitComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ModSuitComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<ModSuitPartComponent, ComponentInit>(OnPartInit);
        SubscribeLocalEvent<ModSuitPartComponent, BeingUnequippedAttemptEvent>(OnPartUnequipAttempt);
        SubscribeLocalEvent<ModSuitPartComponent, GotUnequippedEvent>(OnPartUnequipped);
    }

    #region Update
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessPendingReturns();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ModSuitComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.EquippedParts <= 0 || now < comp.NextUiUpdate)
                continue;

            comp.NextUiUpdate = now + UiUpdateDelay;

            if (comp.Locked && HasEnergy((uid, comp), comp.PartSwapCost))
            {
                comp.Locked = false;
                RefreshSpeed((uid, comp));
                UpdateDraw((uid, comp));

                if (comp.Wearer is { } wearer)
                    _popup.PopupEntity(Loc.GetString("modsuit-recharged"), wearer, wearer);
            }

            UpdateUi((uid, comp));
        }
    }
    #endregion

    #region Base
    private void ProcessPendingReturns()
    {
        if (_pendingReturn.Count == 0)
            return;

        var pending = _pendingReturn.ToArray();
        _pendingReturn.Clear();

        foreach (var partUid in pending)
        {
            if (TerminatingOrDeleted(partUid) || !TryComp<ModSuitPartComponent>(partUid, out var partComp))
                continue;

            if (TerminatingOrDeleted(partComp.Suit) || !TryComp<ModSuitComponent>(partComp.Suit, out var suit))
                continue;

            var ent = new Entity<ModSuitComponent>(partComp.Suit, suit);

            if (!suit.PartsContainer.Contains(partUid))
                _container.Insert(partUid, suit.PartsContainer);

            RefreshCount(ent);
            UpdateUi(ent);
        }
    }

    private void OnInit(Entity<ModSuitComponent> ent, ref ComponentInit args)
    {
        ent.Comp.PartsContainer = _container.EnsureContainer<Container>(ent, ent.Comp.PartsContainerId);
        ent.Comp.ModulesContainer = _container.EnsureContainer<Container>(ent, ent.Comp.ModulesContainerId);
    }

    private void OnMapInit(Entity<ModSuitComponent> ent, ref MapInitEvent args)
    {
        SpawnPart(ent, ModSuitPart.Helmet, ent.Comp.HelmetPrototype);
        SpawnPart(ent, ModSuitPart.Body, ent.Comp.BodyPrototype);
        SpawnPart(ent, ModSuitPart.Gloves, ent.Comp.GlovesPrototype);
        SpawnPart(ent, ModSuitPart.Boots, ent.Comp.BootsPrototype);

        UpdateDraw(ent);
    }

    public ModSuitStatus GetModSuitStatus(Entity<ModSuitComponent> ent)
    {
        var chargePercent = (GetCharge(ent) ?? 0f) * 100f;

        if (chargePercent <= 0f && ent.Comp.Locked)
            return ModSuitStatus.SPRINGTRAPPED;

        if (chargePercent <= 0f)
            return ModSuitStatus.Dead;

        if (ent.Comp.EquippedParts > 0 && chargePercent >= 35f)
            return ModSuitStatus.Active;

        if (chargePercent < 35f)
            return ModSuitStatus.NeedCharging;

        return ModSuitStatus.Inactive;
    }

    private void SpawnPart(Entity<ModSuitComponent> ent, ModSuitPart part, string? proto)
    {
        if (proto == null)
            return;

        var spawned = Spawn(proto, MapCoordinates.Nullspace);
        var partComp = EnsureComp<ModSuitPartComponent>(spawned);
        partComp.Suit = ent;
        partComp.Part = part;

        ent.Comp.Parts[part] = spawned;
        _container.Insert(spawned, ent.Comp.PartsContainer);
    }

    private void OnPartInit(Entity<ModSuitPartComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Replaced = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ReplacedContainerId);
    }

    private void OnShutdown(Entity<ModSuitComponent> ent, ref ComponentShutdown args)
    {
        foreach (var part in ent.Comp.Parts.Values)
        {
            _pendingReturn.Remove(part);

            if (!Exists(part))
                continue;

            if (TryComp<ModSuitPartComponent>(part, out var partComp))
                _container.EmptyContainer(partComp.Replaced);

            QueueDel(part);
        }

        ent.Comp.Parts.Clear();
    }

    private void OnContainerModified(Entity<ModSuitComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ModulesContainerId)
        {
            UpdateDraw(ent);
            UpdateUi(ent);
        }
    }

    private void OnContainerModified(Entity<ModSuitComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ModulesContainerId)
        {
            UpdateDraw(ent);
            UpdateUi(ent);
        }
    }
    #endregion

    #region UI
    private void OnToggleUiAction(Entity<ModSuitComponent> ent, ref ToggleModSuitUiEvent args)
    {
        if (args.Handled || ent.Comp.Wearer != args.Performer)
            return;

        args.Handled = true;
        UpdateUi(ent);
        _ui.TryToggleUi(ent.Owner, ModSuitUiKey.Key, args.Performer);
    }

    private void UpdateUi(Entity<ModSuitComponent> ent)
    {
        var parts = new Dictionary<ModSuitPart, ModSuitPartUiState>();
        foreach (var (part, uid) in ent.Comp.Parts)
            parts[part] = new ModSuitPartUiState(GetNetEntity(uid), IsDeployed(ent, part));

        var modules = new List<ModSuitModuleUiState>();
        foreach (var moduleUid in ent.Comp.ModulesContainer.ContainedEntities)
        {
            var expenditure = 0f;
            var active = true;

            if (TryComp<ModSuitModuleComponent>(moduleUid, out var moduleComp))
            {
                expenditure = moduleComp.Expenditure;
                active = moduleComp.Active;
            }

            modules.Add(new ModSuitModuleUiState(
                GetNetEntity(moduleUid),
                Name(moduleUid),
                expenditure,
                active));
        }

        var (totalDraw, modulesDraw, speedDraw) = CalculateDraw(ent);

        string? wearerName = null;
        string? wearerJob = null;

        if (ent.Comp.Wearer is { } owner
            && !TerminatingOrDeleted(owner)
            && _idCard.TryFindIdCard(owner, out var idCard))
        {
            if (!string.IsNullOrWhiteSpace(idCard.Comp.FullName))
                wearerName = idCard.Comp.FullName;

            if (!string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle))
                wearerJob = idCard.Comp.LocalizedJobTitle;
        }

        _ui.SetUiState(ent.Owner, ModSuitUiKey.Key, new ModSuitBoundUiState(
            ent.Comp.Wearer is { } wearer ? GetNetEntity(wearer) : null,
            GetCharge(ent),
            totalDraw,
            speedDraw,
            modulesDraw,
            ent.Comp.PartSwapCost,
            ent.Comp.SpeedEnabled,
            ent.Comp.EquippedParts,
            ent.Comp.Parts.Count,
            ent.Comp.Locked,
            GetModSuitStatus(ent),
            parts,
            modules,
            wearerName,
            wearerJob));
    }
    #endregion
}
