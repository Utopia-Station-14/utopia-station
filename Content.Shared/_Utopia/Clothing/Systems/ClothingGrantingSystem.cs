using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Tag;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared.ADT.Clothing;

public sealed class ClothingGrantingSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingGrantComponentComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<ClothingGrantComponentComponent, GotUnequippedEvent>(OnCompUnequip);

        SubscribeLocalEvent<ClothingGrantTagComponent, GotEquippedEvent>(OnTagEquip);
        SubscribeLocalEvent<ClothingGrantTagComponent, GotUnequippedEvent>(OnTagUnequip);
    }

    private void OnCompEquip(Entity<ClothingGrantComponentComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(ent, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        foreach (var (name, data) in ent.Comp.Components)
        {
            var newComp = Factory.GetComponent(name);

            if (HasComp(args.Equipee, newComp.GetType()))
                continue;

            var temp = (object)newComp;

            _serializationManager.CopyTo(data.Component, ref temp);
            AddComp(args.Equipee, (Component)temp!);

            ent.Comp.IsActive = true;
        }
    }

    private void OnCompUnequip(Entity<ClothingGrantComponentComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.IsActive) return;

        foreach (var (name, _) in ent.Comp.Components)
        {
            var newComp = (Component)Factory.GetComponent(name);
            RemComp(args.Equipee, newComp.GetType());
        }

        ent.Comp.IsActive = false;
    }

    private void OnTagEquip(Entity<ClothingGrantTagComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(ent, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<TagComponent>(args.Equipee);
        _tagSystem.AddTag(args.Equipee, ent.Comp.Tag);

        ent.Comp.IsActive = true;
    }

    private void OnTagUnequip(Entity<ClothingGrantTagComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.IsActive)
            return;

        _tagSystem.RemoveTag(args.Equipee, ent.Comp.Tag);
        ent.Comp.IsActive = false;
    }
}
