using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Utopia.ModSuits;

public abstract partial class SharedModSuitSystem : EntitySystem
{
    [Dependency] protected SharedContainerSystem _container = default!;
    [Dependency] protected SharedUserInterfaceSystem _ui = default!;
    [Dependency] protected SharedPopupSystem _popup = default!;
    [Dependency] protected SharedHandsSystem _hands = default!;
    [Dependency] protected SharedToolSystem _tool = default!;
    [Dependency] protected SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ModSuitPartComponent, ComponentInit>(OnPartInit);

        InitializePanel();
        InitializeUnequip();
        InitializeToggle();
    }
    private void OnInit(Entity<ModSuitComponent> ent, ref ComponentInit args)
    {
        ent.Comp.PartsContainer = _container.EnsureContainer<Container>(ent, ent.Comp.PartsContainerId);
        ent.Comp.ModulesContainer = _container.EnsureContainer<Container>(ent, ent.Comp.ModulesContainerId);
    }

    private void OnPartInit(Entity<ModSuitPartComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Replaced = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ReplacedContainerId);
    }

    // server-side part
    protected virtual void UpdateUi(Entity<ModSuitComponent> ent)
    {
    }

    protected virtual void OnModuleToggled(Entity<ModSuitComponent> suit)
    {
    }
    protected virtual void OnPartDetached(EntityUid part)
    {
    }
    protected static bool IsDeployed(Entity<ModSuitComponent> ent, ModSuitPart part)
    {
        return ent.Comp.Parts.TryGetValue(part, out var uid) && !ent.Comp.PartsContainer.Contains(uid);
    }
    protected bool TryGetSuit(Entity<ModSuitPartComponent> part, out Entity<ModSuitComponent> suit)
    {
        suit = default;

        if (TerminatingOrDeleted(part.Comp.Suit) || !TryComp<ModSuitComponent>(part.Comp.Suit, out var comp))
            return false;

        suit = new Entity<ModSuitComponent>(part.Comp.Suit, comp);
        return true;
    }
    protected void CollectModules(Entity<ModSuitComponent> suit, List<Entity<ModSuitModuleComponent>> into)
    {
        foreach (var uid in suit.Comp.ModulesContainer.ContainedEntities)
        {
            if (TryComp<ModSuitModuleComponent>(uid, out var module))
                into.Add(new Entity<ModSuitModuleComponent>(uid, module));
        }
    }
}
