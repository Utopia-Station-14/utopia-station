using Content.Server.Stack;
using Content.Server.Teleportation;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Throwing;

namespace Content.Server._Utopia.Bluespace;

public sealed class BluespaceCrystalSystem : EntitySystem
{
    [Dependency] private readonly StackSystem _stacks = default!;
    [Dependency] private readonly TeleportSystem _teleport = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<BluespaceCrystalComponent, ThrowDoHitEvent>(OnThrowInMob);
    }

    private void OnThrowInMob(Entity<BluespaceCrystalComponent> ent, ref ThrowDoHitEvent args)
    {
        if (ent.Comp.MobOnly && !HasComp<MobStateComponent>(args.Target))
            return;

        EnsureComp<RandomTeleportComponent>(ent, out var bcomp);
        _teleport.RandomTeleport(args.Target, ent.Comp.TeleportRadiusThrow, bcomp.DepartureSound, 1);

        if (ent.Comp.ConsumeOnThrow)
        {
            if (TryComp<StackComponent>(ent, out var stackComp))
            {
                _stacks.SetCount((ent.Owner, stackComp), stackComp.Count - 1);
                return;
            }

            QueueDel(ent.Owner);
        }
    }
}
