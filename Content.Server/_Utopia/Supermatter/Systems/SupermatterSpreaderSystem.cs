using Content.Server.Spreader;
using Content.Server._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSpreaderSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterSpreaderComponent, SpreadNeighborsEvent>(OnSpread);
    }

    private void OnSpread(Entity<SupermatterSpreaderComponent> ent, ref SpreadNeighborsEvent args)
    {
        if (args.NeighborFreeTiles.Count == 0)
            return;

        var proto = MetaData(ent.Owner).EntityPrototype?.ID;
        if (proto == null)
            return;

        foreach (var (grid, tileRef) in args.NeighborFreeTiles)
        {
            if (args.Updates < 1)
                break;

            var coords = _map.GridTileToLocal(tileRef.GridUid, grid, tileRef.GridIndices);
            Spawn(proto, coords);

            args.Updates--;
        }
    }
}
