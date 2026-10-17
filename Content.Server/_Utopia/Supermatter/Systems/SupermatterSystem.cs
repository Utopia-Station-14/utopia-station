using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Interaction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Popups;
using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared._Utopia.Supermatter.Prototypes;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;
using Content.Server._Utopia.Supermatter.Components;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chatManager = default!;

    private SupermatterGasDataPrototype?[] _gasDataCache = Array.Empty<SupermatterGasDataPrototype>();
    private readonly List<SupermatterReactionPrototype> _reactionsCache = new();

    private readonly Dictionary<string, SupermatterReagentDataPrototype> _reagentDataCache = new();
    private readonly List<SupermatterReagentReactionPrototype> _reagentReactionsCache = new();

    private EntProtoId SupermatterShard = "SupermatterShard";
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
        SubscribeLocalEvent<SupermatterComponent, SupermatterDoAfterEvent>(DoAfter);
        SubscribeLocalEvent<SupermatterComponent, InteractUsingEvent>(OnItemInteract);
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
            var gasIndex = (int) proto.TargetGas;
            if (gasIndex >= 0 && gasIndex < _gasDataCache.Length)
                _gasDataCache[gasIndex] = proto;
        }

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterReactionPrototype>())
            _reactionsCache.Add(proto);

        _reagentDataCache.Clear();
        _reagentReactionsCache.Clear();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterReagentDataPrototype>())
            _reagentDataCache[proto.TargetReagent] = proto;

        foreach (var proto in _prototypeManager.EnumeratePrototypes<SupermatterReagentReactionPrototype>())
            _reagentReactionsCache.Add(proto);
    }

    private void OnMapInit(Entity<SupermatterComponent> sm, ref MapInitEvent args)
    {
        SetInitGases(sm);
        SetWasteGases(sm);

        sm.Comp.VisualState = GetVisualState(sm);
        _appearance.SetData(sm, SupermatterVisuals.Status, sm.Comp.VisualState);
    }

    public void OffOn(Entity<SupermatterComponent> sm)
    {
        if (!sm.Comp.Active)
            SendMessage(sm, Loc.GetString("supermatter-on"), true);
        else
            SendMessage(sm, Loc.GetString("supermatter-off"), true);

        sm.Comp.Active = !sm.Comp.Active;
        Dirty(sm);
    }

    public float GetIntegrityModifier(Entity<SupermatterComponent> sm)
        => 100f / sm.Comp.Integrity;

    private void SetInitGases(Entity<SupermatterComponent> sm)
    {
        sm.Comp.AtmosGas.SetMoles(Gas.Oxygen, 5f);
        sm.Comp.AtmosGas.SetMoles(Gas.Nitrogen, 15f);
    }

    private void SetWasteGases(Entity<SupermatterComponent> sm)
    {
        sm.Comp.WasteGas.SetMoles(Gas.Oxygen, 0.8f);
        sm.Comp.WasteGas.SetMoles(Gas.Plasma, 0.2f);
    }

    private void OnUpdate(Entity<SupermatterComponent> sm, ref AtmosDeviceUpdateEvent args)
    {
        if (!sm.Comp.Active)
            return;

        ProcessAudio(sm);
        ProcessGases(sm, args.dt);
        ProcessReagents(sm, args.dt);
        ProcessEnergy(sm, args.dt);
        ProcessRadiation(sm, args.dt);
        ProcessLightning(sm);
        ProcessGravity(sm);
        ProcessDamage(sm);
        ProcessSpeaking(sm);
        ProcessAnomaly(sm);
        ProcessDelamination(sm);
        ProcessVisual(sm);
    }

    private void OnItemInteract(Entity<SupermatterComponent> sm, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<SupermatterKnifeComponent>(args.Used) || HasComp<SupermatterInertCoreComponent>(args.Used))
        {
            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 10f, new SupermatterDoAfterEvent(), args.Target)
            {
                BreakOnDamage = true,
                BreakOnHandChange = true,
                BreakOnWeightlessMove = false,
                NeedHand = true,
                RequireCanInteract = true,
                Used = args.Used,
            };

            _doAfter.TryStartDoAfter(doAfterArgs);
        }
        args.Handled = !args.Handled;
    }

    private void DoAfter(Entity<SupermatterComponent> sm, ref SupermatterDoAfterEvent args)
    {
        if (args.Cancelled || !args.Used.HasValue)
            return;

        if (HasComp<SupermatterInertCoreComponent>(args.Used))
            InsertInertCore(sm);
        else
            GetShard(sm);

        QueueDel(args.Used.Value);
    }

    private void GetShard(Entity<SupermatterComponent> sm)
    {
        Spawn(SupermatterShard);
        sm.Comp.Integrity -= DamagePerSlicing;

        SendAnnouncement(sm, Loc.GetString("supermatter-get-shard"), DelaminationCanceled);
    }

    private void InsertInertCore(Entity<SupermatterComponent> sm)
    {
        sm.Comp.StabilizerInside += 1;

        SendAnnouncement(sm, Loc.GetString("supermatter-insert-inert-core"), DelaminationCanceled);
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

        if (sm.Comp.Integrity <= IntegrityForWarningStatus)
            return SupermatterStatus.Warning;

        return SupermatterStatus.Stable;
    }

    private static void ApplyModifiersLerp(SupermatterComponent comp, Vector4 target, float rate)
    {
        var step = MathF.Max(0f, MathF.Min(rate, 1f));

        comp.TemperatureScaleModifier = MathHelper.CloseTo(comp.TemperatureScaleModifier, target.X, 0.001f)
            ? target.X : MathHelper.Lerp(comp.TemperatureScaleModifier, target.X, step);

        comp.TemperatureProtectionModifier = MathHelper.CloseTo(comp.TemperatureProtectionModifier, target.Y, 0.001f)
            ? target.Y : MathHelper.Lerp(comp.TemperatureProtectionModifier, target.Y, step);

        comp.EnergyScaleModifier = MathHelper.CloseTo(comp.EnergyScaleModifier, target.Z, 0.001f)
            ? target.Z : MathHelper.Lerp(comp.EnergyScaleModifier, target.Z, step);

        comp.WasteOutputModifier = MathHelper.CloseTo(comp.WasteOutputModifier, target.W, 0.001f)
            ? target.W : MathHelper.Lerp(comp.WasteOutputModifier, target.W, step);
    }

    private static void DecayModifiers(SupermatterComponent comp, float frameTime)
        => ApplyModifiersLerp(comp, Vector4.One * comp.BaseModifier, comp.ModifierDecayRate * frameTime);
}
