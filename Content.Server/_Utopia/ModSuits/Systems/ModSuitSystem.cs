using Content.Shared._Utopia.ModSuits;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem : SharedModSuitSystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private static readonly TimeSpan UiUpdateDelay = TimeSpan.FromSeconds(1);
    private readonly HashSet<EntityUid> _pendingReturn = new();
    private readonly List<EntityUid> _returnBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ModSuitComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ModSuitComponent, PowerCellSlotEmptyEvent>(OnCellEmpty);

        SubscribeLocalEvent<ModSuitComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<ModSuitComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);

        InitializeDeploy();
        InitializeModuleActivity();
        InitializeSpeed();
        InitializePanelDoAfter();
        InitializeUi();
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

            var ent = new Entity<ModSuitComponent>(uid, comp);

            TryUnlockAfterRecharge(ent);
            UpdateUi(ent);
        }
    }

    private void TryUnlockAfterRecharge(Entity<ModSuitComponent> ent)
    {
        if (!ent.Comp.Locked || !HasEnergy(ent, ent.Comp.PartSwapCost))
            return;

        ent.Comp.Locked = false;
        Dirty(ent);
        RefreshSpeed(ent);
        UpdateDraw(ent);

        if (ent.Comp.Wearer is { } wearer)
            _popup.PopupEntity(Loc.GetString("modsuit-recharged"), wearer, wearer);
    }

    private void ProcessPendingReturns()
    {
        if (_pendingReturn.Count == 0)
            return;

        _returnBuffer.Clear();
        _returnBuffer.AddRange(_pendingReturn);
        _pendingReturn.Clear();

        foreach (var partUid in _returnBuffer)
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

        _returnBuffer.Clear();
    }

    #endregion

    #region Lifecycle

    private void OnMapInit(Entity<ModSuitComponent> ent, ref MapInitEvent args)
    {
        SpawnPart(ent, ModSuitPart.Helmet, ent.Comp.HelmetPrototype);
        SpawnPart(ent, ModSuitPart.Body, ent.Comp.BodyPrototype);
        SpawnPart(ent, ModSuitPart.Gloves, ent.Comp.GlovesPrototype);
        SpawnPart(ent, ModSuitPart.Boots, ent.Comp.BootsPrototype);

        UpdateDraw(ent);
    }

    private void SpawnPart(Entity<ModSuitComponent> ent, ModSuitPart part, string? proto)
    {
        if (proto == null)
            return;

        var spawned = Spawn(proto, MapCoordinates.Nullspace);
        var partComp = EnsureComp<ModSuitPartComponent>(spawned);
        partComp.Suit = ent;
        partComp.Part = part;
        Dirty(spawned, partComp);

        ent.Comp.Parts[part] = spawned;
        Dirty(ent);
        _container.Insert(spawned, ent.Comp.PartsContainer);
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

    private void OnContainerInserted(Entity<ModSuitComponent> ent, ref EntInsertedIntoContainerMessage args)
        => OnContainerModified(ent, args.Container.ID);

    private void OnContainerRemoved(Entity<ModSuitComponent> ent, ref EntRemovedFromContainerMessage args)
        => OnContainerModified(ent, args.Container.ID);

    private void OnContainerModified(Entity<ModSuitComponent> ent, string containerId)
    {
        if (containerId != ent.Comp.ModulesContainerId)
            return;

        UpdateDraw(ent);
        UpdateUi(ent);
    }

    #endregion

    #region Hooks

    protected override void OnModuleToggled(Entity<ModSuitComponent> suit)
    {
        UpdateDraw(suit);
        UpdateUi(suit);
    }

    protected override void OnPartDetached(EntityUid part)
    {
        _pendingReturn.Remove(part);
    }

    #endregion

    #region Status

    public ModSuitStatus GetModSuitStatus(Entity<ModSuitComponent> ent)
        => GetModSuitStatus(ent, GetCharge(ent));

    private ModSuitStatus GetModSuitStatus(Entity<ModSuitComponent> ent, float? charge)
    {
        var chargePercent = (charge ?? 0f) * 100f;

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

    #endregion
}
