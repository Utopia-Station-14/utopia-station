using System.Linq;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Materials;

namespace Content.Shared.Construction
{
    /// <summary>
    /// Deals with machine parts and machine boards.
    /// </summary>
    public sealed partial class MachinePartSystem : EntitySystem
    {
        [Dependency] private SharedLatheSystem _lathe = default!;
        [Dependency] private SharedConstructionSystem _construction = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MachineBoardComponent, ExaminedEvent>(OnMachineBoardExamined);
            SubscribeLocalEvent<MachinePartComponent, ExaminedEvent>(OnMachinePartExamined);
        }

        private void OnMachineBoardExamined(EntityUid uid, MachineBoardComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            using (args.PushGroup(nameof(MachineBoardComponent)))
            {
                args.PushMarkup(Loc.GetString("machine-board-component-on-examine-label"));
                foreach (var (material, amount) in component.StackRequirements)
                {
                    var stack = ProtoMan.Index(material);
                    var name = ProtoMan.Index(stack.Spawn).Name;

                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", amount),
                        ("requiredElement", Loc.GetString(name))));
                }

                foreach (var (_, info) in component.ComponentRequirements)
                {
                    var examineName = _construction.GetExamineName(info);
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", info.Amount),
                        ("requiredElement", examineName)));
                }

                foreach (var (_, info) in component.TagRequirements)
                {
                    var examineName = _construction.GetExamineName(info);
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", info.Amount),
                        ("requiredElement", examineName)));
                }

                // Utopia-Tweak : Machine Parts
                foreach (var (part, amount) in component.Requirements)
                {
                    var partProto = ProtoMan.Index(part);
                    var name = ProtoMan.Index(partProto.StockPartPrototype).Name;
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", amount),
                        ("requiredElement", Loc.GetString(name))));
                }
                // Utopia-Tweak : Machine Parts
            }
        }

        // Utopia-Tweak : Machine Parts
        private void OnMachinePartExamined(EntityUid uid, MachinePartComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            using (args.PushGroup(nameof(MachinePartComponent)))
            {
                args.PushMarkup(Loc.GetString("machine-part-component-on-examine-tier-text",
                    ("tier", component.Tier)));
                args.PushMarkup(Loc.GetString("machine-part-component-on-examine-type-text", ("type",
                    Loc.GetString(ProtoMan.Index<MachinePartPrototype>(component.PartType).Name))));
            }
        }
        // Utopia-Tweak : Machine Parts

        public bool TryGetMachineBoardMaterialCost(Entity<MachineBoardComponent> entity, out Dictionary<string, int> materials, int coefficient = 1)
        {
            var (_, comp) = entity;

            materials = new Dictionary<string, int>();

            foreach (var (stackId, amount) in comp.StackRequirements)
            {
                var stackProto = ProtoMan.Index(stackId);
                var defaultProto = ProtoMan.Index(stackProto.Spawn);

                if (defaultProto.TryComp<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in physComp.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (_lathe.TryGetRecipesFromEntity(stackProto.Spawn, out var recipes))
                {
                    var partRecipe = recipes[0];
                    if (recipes.Count > 1)
                        partRecipe = recipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    // The item has no material cost, so we cannot get the full cost.
                    return false;
                }
            }

            var genericPartInfo = comp.ComponentRequirements.Values.Concat(comp.TagRequirements.Values);
            foreach (var info in genericPartInfo)
            {
                var amount = info.Amount;
                var defaultProtoId = info.DefaultPrototype;

                if (_lathe.TryGetRecipesFromEntity(defaultProtoId, out var recipes))
                {
                    var partRecipe = recipes[0];
                    if (recipes.Count > 1)
                        partRecipe = recipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (ProtoMan.Resolve(defaultProtoId, out var defaultProto) &&
                         defaultProto.TryComp<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in physComp.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    // The item has no material cost, so we cannot get the full cost.
                    return false;
                }
            }

            // We were able to construct all elements of the recipe.
            return true;
        }
    }
}
