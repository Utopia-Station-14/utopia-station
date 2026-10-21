using Content.Shared.Gravity;
using Content.Shared.Inventory;

namespace Content.Shared._Utopia.ModSuits.Modules.Magnet;

public sealed class SharedModSuitMagnetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitMagnetActiveComponent, InventoryRelayedEvent<IsWeightlessEvent>>(OnIsWeightless);
    }

    private void OnIsWeightless(Entity<ModSuitMagnetActiveComponent> ent, ref InventoryRelayedEvent<IsWeightlessEvent> args)
    {
        if (args.Args.Handled)
            return;

        args.Args.IsWeightless = false;
        args.Args.Handled = true;
    }
}
