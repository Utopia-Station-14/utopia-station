using System.Linq;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private void InitializeMachineUpgrades()
    {
        SubscribeLocalEvent<MachineComponent, GetVerbsEvent<ExamineVerb>>(OnMachineExaminableVerb);
    }

    private void OnMachineExaminableVerb(EntityUid uid, MachineComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var markup = new FormattedMessage();
        RaiseLocalEvent(uid, new UpgradeExamineEvent(ref markup));
        if (markup.IsEmpty)
            return;

        if (!FormattedMessage.TryFromMarkup(markup.ToMarkup().TrimEnd('\n'), out markup))
            markup = FormattedMessage.Empty;

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                _examineSystem.SendExamineTooltip(args.User, uid, markup, getVerbs: false, centerAtCursor: false);
            },
            Text = Loc.GetString("machine-upgrade-examinable-verb-text"),
            Message = Loc.GetString("machine-upgrade-examinable-verb-message"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    public List<MachinePartState> GetAllParts(EntityUid uid, MachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new List<MachinePartState>();

        return GetAllParts(component);
    }

    public List<MachinePartState> GetAllParts(MachineComponent component)
    {
        var parts = new List<MachinePartState>();

        foreach (var entity in component.PartContainer.ContainedEntities)
        {
            if (GetMachinePartState(entity, out var partState))
            {
                parts.Add(partState);
            }
        }

        return parts;
    }

    public bool GetMachinePartState(EntityUid uid, out MachinePartState state)
    {
        state = new MachinePartState();
        if (TryComp(uid, out MachinePartComponent? part) && part is not null)
        {
            state.Part = part;
            TryComp(uid, out state.Stack);
            return true;
        }

        return false;
    }

    public Dictionary<string, float> GetPartsTiers(List<MachinePartState> partStates)
    {
        var output = new Dictionary<string, float>();
        foreach (var type in _prototypeManager.EnumeratePrototypes<MachinePartPrototype>())
        {
            var amount = 0f;
            var sumTier = 0f;
            foreach (var state in partStates.Where(part => part.Part.PartType == type.ID))
            {
                amount += state.Quantity();
                sumTier += state.Part.Tier * state.Quantity();
            }

            var tier = amount != 0 ? sumTier / amount : 1.0f;
            output.Add(type.ID, tier);
        }

        return output;
    }

    public void RefreshParts(EntityUid uid, MachineComponent component)
    {
        var parts = GetAllParts(component);
        EntityManager.EventBus.RaiseLocalEvent(uid, new RefreshPartsEvent
        {
            Parts = parts,
            PartTiers = GetPartsTiers(parts),
        }, true);
    }
}
