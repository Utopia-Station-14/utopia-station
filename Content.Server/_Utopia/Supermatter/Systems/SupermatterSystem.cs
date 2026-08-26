using Content.Shared.Atmos;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IRobustRandom _random = default!;

    private SupermatterGasDataPrototype?[] _gasDataCache = Array.Empty<SupermatterGasDataPrototype>();
    private readonly List<SupermatterReactionPrototype> _reactionsCache = new();

    private readonly Dictionary<string, SupermatterReagentDataPrototype> _reagentDataCache = new();
    private readonly List<SupermatterReagentReactionPrototype> _reagentReactionsCache = new();

    private static readonly (Vector2i Offset, float Ratio)[] TileCollectionRatios =
    {
        (new(0, 0), 0.30f),
        (new(1, 0), 0.15f),  (new(-1, 0), 0.15f), (new(0, 1), 0.15f),  (new(0, -1), 0.15f),
        (new(1, 1), 0.075f), (new(-1, 1), 0.075f), (new(1, -1), 0.075f), (new(-1, -1), 0.075f)
    };

    public override void Initialize()
    {
        base.Initialize();

        CacheAllData();
        _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;

        SubscribeLocalEvent<SupermatterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SupermatterComponent, AtmosDeviceUpdateEvent>(OnUpdate);
        SubscribeLocalEvent<SupermatterComponent, ExaminedEvent>(RandomizeDescription);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prototypeManager.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SupermatterGasDataPrototype>() ||
            args.WasModified<SupermatterReactionPrototype>() ||
            args.WasModified<SupermatterReagentDataPrototype>() ||
            args.WasModified<SupermatterReagentReactionPrototype>())
        {
            CacheAllData();
        }
    }

    private void CacheAllData()
    {
        _gasDataCache = new SupermatterGasDataPrototype[Atmospherics.AdjustedNumberOfGases];
        _reactionsCache.Clear();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterGasDataPrototype>())
        {
            var gasIndex = (int)proto.Gas;
            if (gasIndex < _gasDataCache.Length)
                _gasDataCache[gasIndex] = proto;
        }

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterReactionPrototype>())
        {
            _reactionsCache.Add(proto);
        }

        _reagentDataCache.Clear();
        _reagentReactionsCache.Clear();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterReagentDataPrototype>())
        {
            _reagentDataCache[proto.Reagent] = proto;
        }

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterReagentReactionPrototype>())
        {
            _reagentReactionsCache.Add(proto);
        }
    }

    private void OnMapInit(Entity<SupermatterComponent> sm, ref MapInitEvent args)
    {
        SetWasteGases(sm);
    }

    public void SetWasteGases(Entity<SupermatterComponent> sm)
    {
        sm.Comp.WasteGas.SetMoles(Gas.Oxygen, 0.8f);
        sm.Comp.WasteGas.SetMoles(Gas.Plasma, 0.2f);
    }

    private void OnUpdate(Entity<SupermatterComponent> sm, ref AtmosDeviceUpdateEvent args)
    {
        if (!sm.Comp.Active)
            return;

        ProcessGases(sm, args.dt);
        ProcessReagents(sm, args.dt);
        ProcessEnergy(sm);
        ProcessRadiation(sm);
        ProcessLightning(sm);
        // ProcessLight(sm);
        // ProcessGravity(sm);
        ProcessDamage(sm);
        ProcessSpeaking(sm);
    }

    private void RandomizeDescription(Entity<SupermatterComponent> sm, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var number = _random.Next(3);
        args.PushMarkup(
            Loc.GetString($"supermatter-examine-{number}")
        );
    }

    public SupermatterStatus GetStatusType(Entity<SupermatterComponent> sm)
    {
        if (!sm.Comp.Active)
            return SupermatterStatus.Inactive;

        if (sm.Comp.Integrity <= IntegrityForDelamination)
            return SupermatterStatus.Delamination;

        if (sm.Comp.Integrity <= IntegrityForCatastropheStatus)
            return SupermatterStatus.Catastrophe;

        if (sm.Comp.Integrity <= IntegrityForDestabilizationStatus)
            return SupermatterStatus.Destabilization;

        return SupermatterStatus.Stable;
    }
}
