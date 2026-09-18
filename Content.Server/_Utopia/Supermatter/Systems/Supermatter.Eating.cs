using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Interaction;
using Content.Shared.Damage.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Projectiles;
using Content.Server._Utopia.Supermatter.Components;
using Content.Server._Utopia.Supermatter.Systems;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterEatingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SupermatterSystem _superMatter = default!;

    private const float MinMass = 0f;
    private EntProtoId _collisionResultPrototype = "Ash";
    private SoundSpecifier _collisionResultSound = new SoundPathSpecifier("/Audio/_Utopia/Supermatter/supermatter.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterEatingComponent, StartCollideEvent>(OnCollideEvent);
        SubscribeLocalEvent<SupermatterEatingComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<SupermatterEatingComponent, InteractUsingEvent>(OnItemInteract);
    }

    private void OnCollideEvent(Entity<SupermatterEatingComponent> ent, ref StartCollideEvent args)
    {
        var targetUid = args.OtherEntity;
        ProcessConsumption(ent, targetUid);
    }

    private void OnHandInteract(Entity<SupermatterEatingComponent> ent, ref InteractHandEvent args)
    {
        var targetUid = args.User;
        ProcessConsumption(ent, targetUid);
    }

    private void OnItemInteract(Entity<SupermatterEatingComponent> ent, ref InteractUsingEvent args)
    {
        var targetUid = args.Used;
        ProcessConsumption(ent, targetUid);
    }

    private void ProcessConsumption(Entity<SupermatterEatingComponent> eater, EntityUid targetUid)
    {
        var entityEnergy = GetEntityMass(targetUid);
        var entityReagents = GetEntityReagents(targetUid);

        if (entityEnergy <= MinMass)
            return;

        ReachForTheSunAndBurnBurnBurn(eater.Owner, targetUid, entityReagents, entityEnergy);
    }

    private void ReachForTheSunAndBurnBurnBurn(EntityUid uid, EntityUid targetEntity, IReadOnlyList<ReagentQuantity>? reagents, float energy)
    {
        if (TryComp<SupermatterComponent>(uid, out var matter))
        {
            if (!matter.Active)
                _superMatter.OffOn((uid, matter));

            _superMatter.ConsumeExternalEnergy((uid, matter), energy);
            _superMatter.AddReagents(uid, matter, reagents);
        }

        _audio.PlayPvs(_collisionResultSound, uid);
        if (!HasComp<ProjectileComponent>(targetEntity))
            SpawnAtPosition(_collisionResultPrototype, Transform(targetEntity).Coordinates);

        QueueDel(targetEntity);
    }

    private float GetEntityMass(EntityUid uid)
    {
        var energy = 0f;

        if (HasComp<SupermatterProtectionComponent>(uid) || HasComp<GodmodeComponent>(uid))
            return energy;

        if (TryComp<PhysicsComponent>(uid, out var physic))
            energy += physic.Mass;

        if (TryComp<SupermatterFoodComponent>(uid, out var smFood))
            energy += smFood.Energy;

        return energy;
    }

    private IReadOnlyList<ReagentQuantity>? GetEntityReagents(EntityUid uid, string solutionName = "default")
    {
        Entity<SolutionComponent>? solutionEntity = null;

        if (_solutionContainer.ResolveSolution(uid, solutionName, ref solutionEntity, out var solution) && solution != null)
            return solution.Contents;

        return null;
    }
}
