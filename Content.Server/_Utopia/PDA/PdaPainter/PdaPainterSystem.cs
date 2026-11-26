using Content.Shared._Utopia.PDA.PdaPainter;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.PDA.PdaPainter;

public sealed partial class PdaPainterSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<PdaPainterComponent>(PdaPainterUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<PdaPainterPaintPdaMessage>(OnPaintPda);
            subs.Event<PdaPainterApplyCardMessage>(OnApplyCard);
            subs.Event<PdaPainterEjectMessage>(OnEject);
        });
    }

    [SubscribeLocalEvent]
    private void OnCompInit(Entity<PdaPainterComponent> ent, ref ComponentInit args)
    {
        UpdateCachedPrototypes(ent);

        _itemSlots.AddItemSlot(ent.Owner, PdaPainterComponent.PdaSlotId, ent.Comp.PdaSlot);
        _itemSlots.AddItemSlot(ent.Owner, PdaPainterComponent.CardSlotId, ent.Comp.CardSlot);
    }

    [SubscribeLocalEvent]
    private void OnCompRemove(Entity<PdaPainterComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.PdaSlot);
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.CardSlot);
    }

    [SubscribeLocalEvent]
    private void OnEntInserted(Entity<PdaPainterComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnEntRemoved(Entity<PdaPainterComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnOpened(Entity<PdaPainterComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPaintPda(Entity<PdaPainterComponent> ent, ref PdaPainterPaintPdaMessage args)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (ent.Comp.PdaSlot.Item is not { } pda)
            return;

        if (!_proto.TryIndex<EntityPrototype>(args.ProtoId, out var proto))
            return;

        if (!ent.Comp.AllowedPdaPrototypes.Contains(proto.ID))
            return;

        var appearance = EnsureComp<AppearanceComponent>(pda);
        _appearance.SetData(pda, PdaPainterVisuals.PdaPrototype, proto.ID, appearance);

        if (proto.TryComp<AppearanceComponent>(out var component, EntityManager.ComponentFactory)
        && component.AppearanceDataInit?.TryGetValue(PdaVisuals.PdaType, out var value) == true
        && value is string pdaType)
        {
            _appearance.SetData(
                pda,
                PdaVisuals.PdaType,
                pdaType,
                appearance
            );
        }

        _audio.PlayPvs(ent.Comp.PaintSound, ent);
        _meta.SetEntityName(pda, proto.Name);
        _meta.SetEntityDescription(pda, proto.Description);
    }

    private void OnApplyCard(Entity<PdaPainterComponent> ent, ref PdaPainterApplyCardMessage args)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (ent.Comp.CardSlot.Item is not { } card)
            return;

        if (!_proto.TryIndex<EntityPrototype>(args.ProtoId, out var proto))
            return;

        if (!ent.Comp.AllowedIdCardPrototypes.Contains(proto.ID))
            return;

        _audio.PlayPvs(ent.Comp.PaintSound, ent);
        var appearance = EnsureComp<AppearanceComponent>(card);
        _appearance.SetData(card, PdaPainterVisuals.IdCardPrototype, proto.ID, appearance);
    }

    private void OnEject(Entity<PdaPainterComponent> ent, ref PdaPainterEjectMessage args)
    {
        var slotId = args.Pda ? ent.Comp.PdaSlot : ent.Comp.CardSlot;
        _itemSlots.TryEject(ent, slotId, args.Actor, out _);
    }

    private void UpdateUi(Entity<PdaPainterComponent> ent)
    {
        var pda = ent.Comp.PdaSlot.Item;
        var card = ent.Comp.CardSlot.Item;

        var state = new PdaPainterBoundUserInterfaceState(
            pda != null ? Name(pda.Value) : null,
            card != null ? Name(card.Value) : null,
            ent.Comp.AllowedPdaPrototypes,
            ent.Comp.AllowedIdCardPrototypes
        );

        _ui.SetUiState(ent.Owner, PdaPainterUiKey.Key, state);
    }

    private void UpdateCachedPrototypes(Entity<PdaPainterComponent> ent)
    {
        ent.Comp.AllowedIdCardPrototypes.Clear();
        ent.Comp.AllowedPdaPrototypes.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryComp<TagComponent>(out var tags, EntityManager.ComponentFactory))
                continue;

            var tagsCopy = tags.Tags;

            if (!tagsCopy.Contains(PdaPainterComponent.WhitelistTag))
                continue;

            if (proto.HasComp<PdaComponent>(EntityManager.ComponentFactory))
            {
                ent.Comp.AllowedPdaPrototypes.Add(proto.ID);
            }

            if (proto.HasComp<IdCardComponent>(EntityManager.ComponentFactory))
            {
                ent.Comp.AllowedIdCardPrototypes.Add(proto.ID);
            }
        }
    }
}
