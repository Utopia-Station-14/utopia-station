using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Actions;
using Content.Shared._Utopia.Genetics.Events;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Forensics;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationInkGlandsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationInkGlandsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MutationInkGlandsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MutationInkGlandsComponent, InkSpurtActionEvent>(OnActionPerformed);
    }

    private void OnInit(Entity<MutationInkGlandsComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent, ref ent.Comp.GrantedAction, ent.Comp.ActionId);
    }

    private void OnShutdown(Entity<MutationInkGlandsComponent> uid, ref ComponentShutdown args)
    {
        if (uid.Comp.GrantedAction is { Valid: true } action)
        {
            _actions.RemoveAction(action);
        }
    }

    private void OnActionPerformed(Entity<MutationInkGlandsComponent> ent, ref InkSpurtActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        var amount = FixedPoint2.New(ent.Comp.Amount);

        var solution = new Solution();
        solution.AddReagent(ent.Comp.ReagentId, amount);

        if (!TryComp(ent.Owner, out TransformComponent? xform))
            return;

        var behindCoords = xform.Coordinates.Offset(xform.LocalRotation.GetCardinalDir().GetOpposite().ToVec());

        if (_puddle.TrySpillAt(behindCoords, solution, out var puddleUid))
        {
            _forensics.TransferDna(puddleUid, ent.Owner, false);
        }
        else
        {
            _puddle.TrySpillAt(xform.Coordinates, solution, out _);
        }

        _audio.PlayPredicted(ent.Comp.SpillSound, ent.Owner, ent.Owner);
    }
}
