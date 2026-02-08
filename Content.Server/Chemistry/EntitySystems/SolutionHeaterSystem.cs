using Content.Server.Chemistry.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Construction.Components;
using Content.Shared.Placeable;
using Content.Shared.Power;

namespace Content.Server.Chemistry.EntitySystems;

public sealed class SolutionHeaterSystem : EntitySystem
{
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionHeaterComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SolutionHeaterComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<SolutionHeaterComponent, ItemRemovedEvent>(OnItemRemoved);

        // Utopia-Tweak : Machine Parts
        SubscribeLocalEvent<SolutionHeaterComponent, RefreshPartsEvent>(OnRefreshParts);
        SubscribeLocalEvent<SolutionHeaterComponent, UpgradeExamineEvent>(OnUpgradeExamine);
        // Utopia-Tweak : Machine Parts
    }

    private void TurnOn(EntityUid uid)
    {
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, true);
        EnsureComp<ActiveSolutionHeaterComponent>(uid);
    }

    public bool TryTurnOn(EntityUid uid, ItemPlacerComponent? placer = null)
    {
        if (!Resolve(uid, ref placer))
            return false;

        if (placer.PlacedEntities.Count <= 0 || !_powerReceiver.IsPowered(uid))
            return false;

        TurnOn(uid);
        return true;
    }

    public void TurnOff(EntityUid uid)
    {
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, false);
        RemComp<ActiveSolutionHeaterComponent>(uid);
    }

    private void OnPowerChanged(Entity<SolutionHeaterComponent> entity, ref PowerChangedEvent args)
    {
        var placer = Comp<ItemPlacerComponent>(entity);
        if (args.Powered && placer.PlacedEntities.Count > 0)
        {
            TurnOn(entity);
        }
        else
        {
            TurnOff(entity);
        }
    }

    private void OnItemPlaced(Entity<SolutionHeaterComponent> entity, ref ItemPlacedEvent args)
    {
        TryTurnOn(entity);
    }

    private void OnItemRemoved(Entity<SolutionHeaterComponent> entity, ref ItemRemovedEvent args)
    {
        var placer = Comp<ItemPlacerComponent>(entity);
        if (placer.PlacedEntities.Count == 0) // Last entity was removed
            TurnOff(entity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveSolutionHeaterComponent, SolutionHeaterComponent, ItemPlacerComponent>();
        while (query.MoveNext(out _, out _, out var heater, out var placer))
        {
            foreach (var heatingEntity in placer.PlacedEntities)
            {
                if (!TryComp<SolutionContainerManagerComponent>(heatingEntity, out var container))
                    continue;

                var energy = heater.HeatPerSecond * heater.HeatMultiplier * frameTime; // Utopia-Tweak : Machine Parts
                foreach (var (_, soln) in _solutionContainer.EnumerateSolutions((heatingEntity, container)))
                {
                    _solutionContainer.AddThermalEnergy(soln, energy);
                }
            }
        }
    }

    // Utopia-Tweak : Machine Parts
    private void OnRefreshParts(EntityUid uid, SolutionHeaterComponent component, RefreshPartsEvent args)
    {
        var heatTier = args.PartTiers[component.MachinePartHeatPerSecond] - 1;

        component.HeatMultiplier = MathF.Pow(component.PartTierHeatMultiplier, heatTier);
    }

    private void OnUpgradeExamine(EntityUid uid, SolutionHeaterComponent component, UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("solution-heater-upgrade-heat", component.HeatMultiplier);
    }
    // Utopia-Tweak : Machine Parts
}
