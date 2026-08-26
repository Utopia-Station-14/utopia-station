using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Atmos.EntitySystems
{
    public sealed partial class GasTankMixerSystem : EntitySystem
    {
        [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private GasTankSystem _gasTankSystem = default!;
        [Dependency] private UserInterfaceSystem _ui = default!;
        [Dependency] private ItemSlotsSystem _itemSlots = default!;

        public override void Initialize()
        {
            base.Initialize();

            // SubscribeLocalEvent<GasTankMixerComponent, InteractHandEvent>(OnInteractHand);

            SubscribeLocalEvent<GasTankMixerComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
            SubscribeLocalEvent<GasTankMixerComponent, EntRemovedFromContainerMessage>(OnContainerChanged);

            Subs.BuiEvents<GasTankMixerComponent>(GasTankMixerUiKey.Key, subs =>
            {
                subs.Event<GasTankMixerStartMessage>(OnStartMessage);
                subs.Event<GasTankMixerSetTimeMessage>(OnSetTimeMessage);
                subs.Event<GasTankMixerEjectMessage>(OnEjectMessage);
            });
        }

        private void OnContainerChanged(EntityUid uid, GasTankMixerComponent comp, ContainerModifiedMessage args)
        {
            UpdateUi(uid, comp);
        }

        // private void OnInteractHand(EntityUid uid, GasTankMixerComponent comp, InteractHandEvent args)
        // {
        //     if (args.Handled)
        //         return;

        //     _ui.OpenUi(uid, GasTankMixerUiKey.Key, args.User);
        //     UpdateUi(uid, comp);

        //     args.Handled = true;
        // }

        private void OnStartMessage(EntityUid uid, GasTankMixerComponent comp, GasTankMixerStartMessage args)
        {
            if (comp.IsActive)
                return;

            if (_itemSlots.TryGetSlot(uid, GasTankMixerComponent.SlotAName, out var slotA) &&
                _itemSlots.TryGetSlot(uid, GasTankMixerComponent.SlotBName, out var slotB))
            {
                if (slotA.HasItem && slotB.HasItem)
                {
                    comp.IsActive = true;
                    UpdateUi(uid, comp);
                }
            }
        }

        private void OnSetTimeMessage(EntityUid uid, GasTankMixerComponent comp, GasTankMixerSetTimeMessage args)
        {
            if (comp.IsActive)
                return;

            comp.Timer = Math.Clamp(args.Time, 1f, 300f);
            UpdateUi(uid, comp);
        }

        private void OnEjectMessage(EntityUid uid, GasTankMixerComponent comp, GasTankMixerEjectMessage args)
        {
            if (comp.IsActive)
                return;

            if (_itemSlots.TryGetSlot(uid, args.SlotId, out var slot))
                _itemSlots.TryEject(uid, slot, args.Actor, out _);

            UpdateUi(uid, comp);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<GasTankMixerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (!comp.IsActive) continue;

                comp.Timer -= frameTime;

                if (comp.Timer <= 0)
                {
                    comp.IsActive = false;
                    Process(uid, comp);
                }
                else
                {
                    UpdateUi(uid, comp);
                }
            }
        }

        private void Process(EntityUid uid, GasTankMixerComponent comp)
        {
            if (!_itemSlots.TryGetSlot(uid, GasTankMixerComponent.SlotAName, out var slotA) ||
                !_itemSlots.TryGetSlot(uid, GasTankMixerComponent.SlotBName, out var slotB))
                return;

            var entA = slotA.Item;
            var entB = slotB.Item;

            if (entA == null || entB == null)
                return;

            if (!TryComp<GasTankComponent>(entA, out var tankA) ||
                !TryComp<GasTankComponent>(entB, out var tankB))
                return;

            if (tankA.Air == null || tankB.Air == null)
                return;

            _atmosphereSystem.Merge(tankA.Air, tankB.Air);

            for (var i = 0; i < 10; i++)
            {
                _atmosphereSystem.React(tankA.Air, tankA);
            }
        }

        private void UpdateUi(EntityUid uid, GasTankMixerComponent comp)
        {
            var hasA = _itemSlots.TryGetSlot(uid, GasTankMixerComponent.SlotAName, out var slotA) && slotA.HasItem;
            var hasB = _itemSlots.TryGetSlot(uid, GasTankMixerComponent.SlotBName, out var slotB) && slotB.HasItem;

            var state = new GasTankMixerBoundUserInterfaceState(
                hasA,
                hasB,
                comp.Timer,
                comp.IsActive
            );
            _ui.SetUiState(uid, GasTankMixerUiKey.Key, state);

            if (TryComp<GasTankMixerVisualsComponent>(uid, out var visuals))
            {
                visuals.HasTankA = hasA;
                visuals.HasTankB = hasB;
                Dirty(uid, visuals);
            }
        }
    }
}
