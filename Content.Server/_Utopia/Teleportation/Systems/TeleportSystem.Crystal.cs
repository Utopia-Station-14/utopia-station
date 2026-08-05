using Content.Server.Stack;
using Content.Shared._Utopia.Teleportation;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Sprite;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Teleportation;

public sealed class TeleportCrystalSystem : EntitySystem
{
    [Dependency] private readonly TeleportSystem _teleport = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TeleportCrystalComponent, ThrowDoHitEvent>(OnThrowIn);
    }

    private void OnThrowIn(Entity<TeleportCrystalComponent> ent, ref ThrowDoHitEvent args)
    {
        if (ent.Comp.MobOnly && !HasComp<MobStateComponent>(args.Target))
            return;

        var target = args.Target;

        switch (ent.Comp.CType)
        {
            case CrystalType.Bluespace:
                {
                    EnsureComp<RandomTeleportComponent>(ent, out var bcomp);
                    _teleport.RandomTeleport(target, ent.Comp.SpecialValue, bcomp.DepartureSound, 1);
                    break;
                }
            case CrystalType.Redspace:
                {
                    if (HasComp<RedspaceEffectComponent>(target))
                        return;

                    EnsureComp<RedspaceEffectComponent>(target);

                    var scale = _scaleVisuals.GetSpriteScale(target);
                    var newScale = scale * ent.Comp.SpecialValue;

                    EnsureComp<ItemComponent>(target);
                    _scaleVisuals.SetSpriteScale(target, newScale);
                    _physics.ScaleFixtures(target, ent.Comp.SpecialValue);

                    Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.Cooldown), () =>
                    {
                        _scaleVisuals.SetSpriteScale(target, scale);
                        _physics.ScaleFixtures(target, 1f / ent.Comp.SpecialValue);

                        RemComp<ItemComponent>(target);
                        RemComp<RedspaceEffectComponent>(target);
                    });

                    break;
                }
        }

        if (ent.Comp.ConsumeOnThrow)
        {
            if (TryComp<StackComponent>(ent, out var stackComp))
            {
                _stack.SetCount((ent.Owner, stackComp), stackComp.Count - 1);
                return;
            }

            QueueDel(ent.Owner);
        }
    }
}
