using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationBloodRegenerationSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    private float _accum = 0f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accum += frameTime;
        var elapsedSeconds = (int)_accum;
        if (elapsedSeconds <= 0)
            return;

        _accum -= elapsedSeconds;

        var query = EntityQueryEnumerator<MutationBloodRegenerationComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var regen, out var bloodstream))
        {
            if (_mobState.IsDead(uid) || _mobState.IsCritical(uid))
                continue;

            var currentPercentage = _bloodstream.GetBloodLevel(uid);
            var targetPercentage = MathF.Min(MathF.Max(regen.TargetPercentage, 0f), 1f) * 2f;
            var regenRate = MathF.Max(regen.RegenRatePerSecond, 0f);

            if (regenRate <= 0f)
                continue;

            if (currentPercentage >= targetPercentage)
                continue;

            if (!_solutionContainer.ResolveSolution(uid, bloodstream.BloodSolutionName,
            ref bloodstream.BloodSolution, out var bloodSolution))
                continue;

            var deficitPercentage = targetPercentage - currentPercentage;
            var maxVolume = bloodSolution.MaxVolume.Float();
            var regenThisTick = MathF.Min(regenRate * elapsedSeconds, deficitPercentage * maxVolume);

            _bloodstream.TryModifyBloodLevel((uid, bloodstream), regenThisTick);
        }
    }
}
