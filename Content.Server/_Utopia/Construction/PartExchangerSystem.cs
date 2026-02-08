using Content.Server._Utopia.Construction.Components;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Stack;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Exchanger;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Construction;

public sealed class PartExchangerSystem : EntitySystem
{
    [Dependency] private readonly ConstructionSystem _construction = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PartExchangerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PartExchangerComponent, ExchangerDoAfterEvent>(OnDoAfter);
    }

    private struct UpgradePartState
    {
        public MachinePartComponent Part;
        public StackComponent? Stack;
        public bool InContainer;
    }

    private void OnDoAfter(EntityUid uid, PartExchangerComponent component, DoAfterEvent args)
    {
        if (args.Cancelled)
        {
            component.AudioStream = _audio.Stop(component.AudioStream);
            return;
        }

        if (args.Handled || args.Args.Target == null)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage) || storage.Container == null)
            return;

        var partsByType = new Dictionary<ProtoId<MachinePartPrototype>, List<(EntityUid, UpgradePartState)>>();

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (_construction.GetMachinePartState(item, out var partState))
            {
                UpgradePartState upgrade;
                upgrade.Part = partState.Part;
                upgrade.Stack = partState.Stack;
                upgrade.InContainer = true;

                var partType = upgrade.Part.PartType;
                if (!partsByType.TryGetValue(partType, out var value))
                {
                    value = [];
                    partsByType[partType] = value;
                }

                value.Add((item, upgrade));
            }
        }

        if (TryComp<MachineComponent>(args.Args.Target.Value, out var machine))
        {
            TryExchangeMachineParts(machine, args.Args.Target.Value, uid, partsByType);
        }
        else if (TryComp<MachineFrameComponent>(args.Args.Target.Value, out var machineFrame))
        {
            TryConstructMachineParts(machineFrame, args.Args.Target.Value, uid, partsByType);
        }

        args.Handled = true;
    }

    private void TryExchangeMachineParts(MachineComponent machine, EntityUid uid, EntityUid storageUid, Dictionary<ProtoId<MachinePartPrototype>, List<(EntityUid part, UpgradePartState state)>> partsByType)
    {
        var board = machine.BoardContainer.ContainedEntities.FirstOrNull();

        if (board == null || !TryComp<MachineBoardComponent>(board, out var macBoardComp))
            return;

        foreach (var item in new ValueList<EntityUid>(machine.PartContainer.ContainedEntities))
        {
            if (_construction.GetMachinePartState(item, out var partState))
            {
                UpgradePartState upgrade;
                upgrade.Part = partState.Part;
                upgrade.Stack = partState.Stack;
                upgrade.InContainer = false;

                var partType = upgrade.Part.PartType;
                if (!partsByType.TryGetValue(partType, out var value))
                {
                    value = [];
                    partsByType[partType] = value;
                }

                value.Add((item, upgrade));

                _container.RemoveEntity(uid, item);
            }
        }

        foreach (var (partKey, partList) in partsByType)
        {
            partList.Sort((x, y) => y.state.Part.Tier.CompareTo(x.state.Part.Tier));
        }

        var updatedParts = new List<(EntityUid id, MachinePartState state, int index)>();
        foreach (var (type, amount) in macBoardComp.Requirements)
        {
            if (partsByType.ContainsKey(type))
            {
                var partsNeeded = amount;
                var index = 0;
                foreach ((var part, var state) in partsByType[type])
                {
                    if (partsNeeded <= 0)
                        break;

                    if (state.Stack is not null)
                    {
                        var count = state.Stack.Count;
                        if (count <= partsNeeded)
                        {
                            MachinePartState partState;
                            partState.Part = state.Part;
                            partState.Stack = state.Stack;

                            updatedParts.Add((part, partState, index));
                            partsNeeded -= count;
                        }
                        else
                        {
                            var splitStack = _stack.Split((part, state.Stack), partsNeeded, Transform(uid).Coordinates) ?? EntityUid.Invalid;

                            if (splitStack == EntityUid.Invalid)
                                continue;

                            if (_construction.GetMachinePartState(splitStack, out var splitState))
                            {
                                updatedParts.Add((splitStack, splitState, -1));
                                partsNeeded = 0;
                            }
                        }
                    }
                    else
                    {
                        MachinePartState partState;
                        partState.Part = state.Part;
                        partState.Stack = state.Stack;

                        updatedParts.Add((part, partState, index));
                        partsNeeded--;
                    }

                    index++;
                }
            }
        }

        for (var i = updatedParts.Count - 1; i >= 0; i--)
        {
            var (id, state, index) = updatedParts[i];
            var inserted = _container.Insert(id, machine.PartContainer);
            if (index >= 0)
            {
                partsByType[state.Part.PartType].RemoveAt(index);
            }
        }

        foreach (var (_, partSet) in partsByType)
        {
            foreach (var (part, state) in partSet)
            {
                if (!state.InContainer)
                {
                    _storage.Insert(storageUid, part, out _, playSound: false);
                }
            }
        }
        _construction.RefreshParts(uid, machine);
    }

    private void TryConstructMachineParts(MachineFrameComponent machine, EntityUid uid, EntityUid storageEnt, Dictionary<ProtoId<MachinePartPrototype>, List<(EntityUid part, UpgradePartState state)>> partsByType)
    {
        var board = machine.BoardContainer.ContainedEntities.FirstOrNull();

        if (!machine.HasBoard || !TryComp<MachineBoardComponent>(board, out var macBoardComp))
            return;

        foreach (var item in new ValueList<EntityUid>(machine.PartContainer.ContainedEntities))
        {
            if (_construction.GetMachinePartState(item, out var partState))
            {
                UpgradePartState upgrade;
                upgrade.Part = partState.Part;
                upgrade.Stack = partState.Stack;
                upgrade.InContainer = false;

                var partType = upgrade.Part.PartType;
                if (!partsByType.TryGetValue(partType, out var value))
                {
                    value = [];
                    partsByType[partType] = value;
                }

                value.Add((item, upgrade));
                machine.Progress[partType] -= partState.Quantity();
                machine.Progress[partType] = int.Max(0, machine.Progress[partType]);

                _container.RemoveEntity(uid, item);
            }
        }

        foreach (var partList in partsByType.Values)
        {
            partList.Sort((x, y) => y.state.Part.Tier.CompareTo(x.state.Part.Tier));
        }

        var updatedParts = new List<(EntityUid id, MachinePartState state, int index)>();
        foreach (var (type, amount) in macBoardComp.Requirements)
        {
            if (partsByType.TryGetValue(type, out var value))
            {
                var partsNeeded = amount;
                var index = 0;
                foreach ((var part, var state) in value)
                {
                    if (partsNeeded <= 0)
                        break;

                    if (state.Stack is not null)
                    {
                        var count = state.Stack.Count;
                        if (count <= partsNeeded)
                        {
                            MachinePartState partState;
                            partState.Part = state.Part;
                            partState.Stack = state.Stack;

                            updatedParts.Add((part, partState, index));
                            partsNeeded -= count;
                        }
                        else
                        {
                            var splitStack = _stack.Split((part, state.Stack), partsNeeded,
                                Transform(uid).Coordinates) ?? EntityUid.Invalid;

                            if (splitStack == EntityUid.Invalid)
                                continue;

                            if (_construction.GetMachinePartState(splitStack, out var splitState))
                            {
                                updatedParts.Add((splitStack, splitState, -1));
                                partsNeeded = 0;
                            }
                        }
                    }
                    else
                    {
                        MachinePartState partState;
                        partState.Part = state.Part;
                        partState.Stack = state.Stack;

                        updatedParts.Add((part, partState, index));
                        partsNeeded--;
                    }

                    index++;
                }
            }
        }

        for (var i = updatedParts.Count - 1; i >= 0; i--)
        {
            var (id, state, index) = updatedParts[i];
            _container.Insert(id, machine.PartContainer, force: true);
            if (index >= 0)
            {
                partsByType[state.Part.PartType].RemoveAt(index);
            }
            machine.Progress[state.Part.PartType] += state.Quantity();
        }

        foreach (var (_, partSet) in partsByType)
        {
            foreach (var (part, state) in partSet)
            {
                if (!state.InContainer)
                {
                    _storage.Insert(storageEnt, part, out _, playSound: false);
                }
            }
        }
    }

    private void OnAfterInteract(EntityUid uid, PartExchangerComponent component, AfterInteractEvent args)
    {
        if (component.DoDistanceCheck && !args.CanReach)
            return;

        if (args.Target == null)
            return;

        if (!HasComp<MachineComponent>(args.Target) && !HasComp<MachineFrameComponent>(args.Target))
            return;

        if (TryComp<WiresPanelComponent>(args.Target, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("construction-step-condition-wire-panel-open"), args.Target.Value);
            return;
        }

        var audioStream = _audio.PlayPvs(component.ExchangeSound, uid);
        if (audioStream != null)
        {
            component.AudioStream = audioStream.Value.Entity;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.ExchangeDuration,
            new ExchangerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true
        });
    }
}
