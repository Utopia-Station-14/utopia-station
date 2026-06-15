using Content.Server.Power.Generator;
using Content.Shared.Power.Generator;
using Content.Shared.Radiation.Components;
using Content.Shared._Utopia.Power.Generator;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Power.Generator;

public sealed class GeneratorRadiationSystem : EntitySystem
{
    private float _accumulator;

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(GeneratorSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;

        if (_accumulator < 1f)
            return;

        _accumulator -= 1f;

        var query = EntityQueryEnumerator<FuelGeneratorComponent, GeneratorRadiationComponent>();

        while (query.MoveNext(out var uid, out var gen, out var genRad))
        {
            if (gen.On && !genRad.Active)
                ProcessRadiation(uid, gen, genRad);

            if (genRad.Active)
                ReduceRadiation(uid, genRad);
        }
    }

    private void ProcessRadiation(EntityUid uid, FuelGeneratorComponent gen, GeneratorRadiationComponent genRad)
    {
        if (!_random.Prob(gen.TargetPower / gen.MaxTargetPower))
            return;

        if (TryComp<RadiationSourceComponent>(uid, out var rad))
        {
            var ratio = gen.TargetPower / gen.MaxTargetPower;
            rad.Intensity = ratio * genRad.RadiationMultiplier;
        }

        SetActive(uid, genRad, true);
    }

    private void ReduceRadiation(EntityUid uid, GeneratorRadiationComponent genRad)
    {
        if (TryComp<RadiationSourceComponent>(uid, out var rad))
        {
            if (rad.Intensity > 0)
            {
                rad.Intensity -= genRad.RadiationReduceAmount;
                return;
            }
        }

        SetActive(uid, genRad, false);
    }

    private void SetActive(EntityUid uid, GeneratorRadiationComponent genRad, bool active)
    {
        if (genRad.Active == active)
            return;

        genRad.Active = active;
        _appearance.SetData(uid, GeneratorVisuals.Radiating, active);
        Dirty(uid, genRad);
    }
}
