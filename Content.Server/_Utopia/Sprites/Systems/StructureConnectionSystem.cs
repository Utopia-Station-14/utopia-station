using Content.Shared._Utopia.Walls;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Utopia.Walls;

public sealed partial class WallConnectSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WallConnectComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WallConnectComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<WallConnectComponent, MoveEvent>(OnMove);
    }

    private void OnStartup(EntityUid uid, WallConnectComponent component, ComponentStartup args)
    {
        UpdateTile(uid);
        UpdateNeighbours(uid);
    }

    private void OnTerminating(EntityUid uid, WallConnectComponent component, ref EntityTerminatingEvent args)
    {
        // At this point the entity's own WallConnectComponent/Transform may
        // still technically be present and anchored (component removal order
        // means Transform is usually torn down last), so neighbours would
        // still "see" this wall as connected if we didn't explicitly ignore
        // it. Pass uid as the entity to ignore when recomputing neighbours.
        UpdateNeighbours(uid, ignore: uid);
    }

    private void OnMove(EntityUid uid, WallConnectComponent component, ref MoveEvent args)
    {
        UpdateTile(uid);
        UpdateNeighbours(uid);
    }

    // All 8 directions - orthogonal neighbours need updating because their own
    // orthogonal flags changed, and diagonal neighbours need updating because
    // their diagonal flags (ne/se/sw/nw) depend on this tile.
    private static readonly Direction[] Directions =
    {
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
        Direction.NorthEast,
        Direction.SouthEast,
        Direction.SouthWest,
        Direction.NorthWest,
    };

    private void UpdateNeighbours(EntityUid uid, EntityUid? ignore = null)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } gridUid)
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var dir in Directions)
        {
            var pos = tile.Offset(dir);
            var enumerator =_map.GetAnchoredEntitiesEnumerator(gridUid, grid, pos);

            while (enumerator.MoveNext(out var other))
            {
                if (other == ignore)
                    continue;

                if (HasComp<WallConnectComponent>(other))
                    UpdateTile(other.Value, ignore);
            }
        }
    }

    private void UpdateTile(EntityUid uid, EntityUid? ignore = null)
    {
        if (!TryComp(uid, out WallConnectComponent? connect))
            return;

        var xform = Transform(uid);

        if (!xform.Anchored)
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var gridUid = xform.GridUid!.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        var n = HasConnection(connect, gridUid, grid, tile.Offset(Direction.North), ignore);
        var e = HasConnection(connect, gridUid, grid, tile.Offset(Direction.East), ignore);
        var s = HasConnection(connect, gridUid, grid, tile.Offset(Direction.South), ignore);
        var w = HasConnection(connect, gridUid, grid, tile.Offset(Direction.West), ignore);

        var ne = HasConnection(connect, gridUid, grid, tile.Offset(Direction.NorthEast), ignore);
        var se = HasConnection(connect, gridUid, grid, tile.Offset(Direction.SouthEast), ignore);
        var sw = HasConnection(connect, gridUid, grid, tile.Offset(Direction.SouthWest), ignore);
        var nw = HasConnection(connect, gridUid, grid, tile.Offset(Direction.NorthWest), ignore);

        // Don't allow a diagonal connection without both adjacent orthogonal sides
        if (!(n && e))
            ne = false;

        if (!(s && e))
            se = false;

        if (!(s && w))
            sw = false;

        if (!(n && w))
            nw = false;

        var mask = CalculateMask(n, e, s, w, ne, se, sw, nw);

        // Several shapes are just horizontal mirrors of each other (E-only vs
        // W-only, N+E vs N+W, S+E vs S+W, etc). Rather than requiring art for
        // both, we pick whichever of {mask, mirrored mask} is numerically
        // smaller as the "canonical" state, and tell the client to flip the
        // sprite horizontally when the real mask isn't the canonical one.
        var mirrored = MirrorMaskHorizontal(mask);
        var canonicalMask = Math.Min(mask, mirrored);
        var flip = canonicalMask != mask;

        // Just store the data - the client-side visualizer decides how to
        // turn this into an actual RSI state, since SpriteComponent/SpriteSystem
        // only exist on the client.
        _appearance.SetData(uid, WallVisuals.ConnectMask, canonicalMask);
        _appearance.SetData(uid, WallVisuals.FlipX, flip);
    }

    /// <summary>
    /// Mirrors a connection bitmask left-right: E &lt;-&gt; W, NE &lt;-&gt; NW,
    /// SE &lt;-&gt; SW. N and S are unaffected since they sit on the mirror axis.
    /// </summary>
    private static int MirrorMaskHorizontal(int mask)
    {
        var n = mask & 1;
        var e = mask & 2;
        var s = mask & 4;
        var w = mask & 8;
        var ne = mask & 16;
        var se = mask & 32;
        var sw = mask & 64;
        var nw = mask & 128;

        var result = 0;

        result |= n;
        result |= w != 0 ? 2 : 0;   // E <- W
        result |= s;
        result |= e != 0 ? 8 : 0;   // W <- E

        result |= nw != 0 ? 16 : 0; // NE <- NW
        result |= sw != 0 ? 32 : 0; // SE <- SW
        result |= se != 0 ? 64 : 0; // SW <- SE
        result |= ne != 0 ? 128 : 0; // NW <- NE

        return result;
    }

    private bool HasConnection(WallConnectComponent self, EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityUid? ignore = null)
    {
        var enumerator =_map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (enumerator.MoveNext(out var uid))
        {
            if (uid == ignore)
                continue;

            if (!TryComp(uid, out WallConnectComponent? other))
                continue;

            if (other.ConnectKey != self.ConnectKey)
                continue;

            return true;
        }

        return false;
    }

    private static int CalculateMask(bool n, bool e, bool s, bool w, bool ne, bool se, bool sw, bool nw)
    {
        var mask = 0;

        if (n) mask |= 1;
        if (e) mask |= 2;
        if (s) mask |= 4;
        if (w) mask |= 8;

        if (ne) mask |= 16;
        if (se) mask |= 32;
        if (sw) mask |= 64;
        if (nw) mask |= 128;

        return mask;
    }
}
