using System.Numerics;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.Radiation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class TritiumFireRadiationSystem : EntitySystem
{
    [Dependency] private RadiationSystem _radiation = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private const string Proto = "TritiumFireRadiation";

    private const int SourceSpacing = 6;
    private const float FlushInterval = 1f;
    private static readonly TimeSpan TileTimeout = TimeSpan.FromSeconds(2);
    private const float IntensityPerMole = 0.5f;
    private const float MaxIntensityPerSource = 10f;

    private const int MinDistSq = SourceSpacing * SourceSpacing;
    private const int CellSize = SourceSpacing - 1;
    private const int MaxRing = 3;

    private sealed class TileFire
    {
        public float Fuel;
        public TimeSpan LastSeen;
        public int Source = -1;
    }

    private struct SourceInfo
    {
        public EntityUid Uid;
        public Vector2i Tile;
        public float Intensity;
    }

    private sealed class GridState
    {
        public readonly Dictionary<Vector2i, TileFire> Tiles = new();
        public readonly List<Vector2i> Chosen = new();
        public readonly List<SourceInfo> Sources = new();
        public bool Dirty = true;
    }

    private static readonly Vector2i[] Neighbors =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    };

    private readonly Dictionary<EntityUid, GridState> _grids = new();
    private readonly List<EntityUid> _gridsToRemove = new();
    private readonly List<Vector2i> _expired = new();
    private readonly List<long> _perimeterKeys = new();
    private readonly Dictionary<Vector2i, int> _cells = new();
    private readonly Stack<TileFire> _pool = new();
    private float[] _fuel = new float[16];
    private float _accumulator;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        UpdatesAfter.Add(typeof(AtmosphereSystem));
    }

    #region Pulse

    public void Pulse(EntityUid grid, Vector2i tile, float burnedFuel)
    {
        PulseState(GetOrCreate(grid), tile, burnedFuel, _gameTiming.CurTime);
    }

    public void PulsePipeNet(PipeNet net, float burnedFuel)
    {
        var nodes = net.Nodes;
        var count = nodes.Count;
        if (count == 0)
            return;

        var perNode = burnedFuel / count;
        var now = _gameTiming.CurTime;

        EntityUid lastGrid = default;
        GridState? lastState = null;

        foreach (var node in nodes)
        {
            var owner = node.Owner;
            if (TerminatingOrDeleted(owner))
                continue;

            if (!_xformQuery.TryGetComponent(owner, out var xform) || xform.GridUid is not { } grid)
                continue;

            if (lastState == null || grid != lastGrid)
            {
                lastState = GetOrCreate(grid);
                lastGrid = grid;
            }

            var tile = _transform.GetGridTilePositionOrDefault((owner, xform));
            PulseState(lastState, tile, perNode, now);
        }
    }

    private GridState GetOrCreate(EntityUid grid)
    {
        if (!_grids.TryGetValue(grid, out var state))
        {
            state = new GridState();
            _grids[grid] = state;
        }

        return state;
    }

    private void PulseState(GridState state, Vector2i tile, float fuel, TimeSpan now)
    {
        if (state.Tiles.TryGetValue(tile, out var fire))
        {
            fire.Fuel = fuel;
            fire.LastSeen = now;
            return;
        }

        fire = _pool.Count > 0 ? _pool.Pop() : new TileFire();
        fire.Fuel = fuel;
        fire.LastSeen = now;
        fire.Source = -1;
        state.Tiles.Add(tile, fire);
        state.Dirty = true;
    }

    #endregion

    #region Update

    public override void Update(float frameTime)
    {
        if (_grids.Count == 0)
            return;

        _accumulator += frameTime;
        if (_accumulator < FlushInterval)
            return;

        _accumulator = 0f;
        _gridsToRemove.Clear();

        var now = _gameTiming.CurTime;

        foreach (var (grid, state) in _grids)
        {
            if (TerminatingOrDeleted(grid))
            {
                _gridsToRemove.Add(grid);
                continue;
            }

            Rebuild(grid, state, now);

            if (state.Tiles.Count == 0 && state.Sources.Count == 0)
                _gridsToRemove.Add(grid);
        }

        foreach (var grid in _gridsToRemove)
            _grids.Remove(grid);
    }

    private void Rebuild(EntityUid grid, GridState state, TimeSpan now)
    {
        var tiles = state.Tiles;
        ResetFuel(state.Chosen.Count);
        _expired.Clear();

        foreach (var (pos, fire) in tiles)
        {
            if (now - fire.LastSeen > TileTimeout)
                _expired.Add(pos);
            else if (!state.Dirty)
                _fuel[fire.Source] += fire.Fuel;
        }

        if (_expired.Count > 0)
        {
            foreach (var pos in _expired)
            {
                if (tiles.Remove(pos, out var dead))
                    _pool.Push(dead);
            }

            state.Dirty = true;
        }

        if (state.Dirty)
        {
            RebuildGroup(state);
            state.Dirty = false;

            ResetFuel(state.Chosen.Count);
            foreach (var fire in tiles.Values)
                _fuel[fire.Source] += fire.Fuel;
        }

        ApplySources(grid, state);
    }

    #endregion

    #region Group

    private void RebuildGroup(GridState state)
    {
        var tiles = state.Tiles;
        var chosen = state.Chosen;
        _perimeterKeys.Clear();

        foreach (var pos in tiles.Keys)
        {
            foreach (var n in Neighbors)
            {
                if (!tiles.ContainsKey(pos + n))
                {
                    _perimeterKeys.Add(Pack(pos));
                    break;
                }
            }
        }

        _perimeterKeys.Sort();
        _cells.Clear();
        chosen.Clear();

        foreach (var key in _perimeterKeys)
        {
            var pos = Unpack(key);
            if (IsTooClose(pos, chosen))
                continue;

            _cells[new Vector2i(Cell(pos.X), Cell(pos.Y))] = chosen.Count;
            chosen.Add(pos);
        }

        foreach (var (pos, fire) in tiles)
            fire.Source = FindNearest(pos, chosen);
    }

    private bool IsTooClose(Vector2i pos, List<Vector2i> chosen)
    {
        var cx = Cell(pos.X);
        var cy = Cell(pos.Y);

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (!_cells.TryGetValue(new Vector2i(cx + dx, cy + dy), out var idx))
                    continue;

                var d = pos - chosen[idx];
                if (d.X * d.X + d.Y * d.Y < MinDistSq)
                    return true;
            }
        }

        return false;
    }

    private int FindNearest(Vector2i pos, List<Vector2i> chosen)
    {
        var cx = Cell(pos.X);
        var cy = Cell(pos.Y);
        var best = -1;
        var bestDist = int.MaxValue;

        for (var r = 0; r <= MaxRing; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                var fullColumn = dx == -r || dx == r;
                var step = fullColumn ? 1 : 2 * r;

                for (var dy = -r; dy <= r; dy += step)
                {
                    if (!_cells.TryGetValue(new Vector2i(cx + dx, cy + dy), out var idx))
                        continue;

                    var d = pos - chosen[idx];
                    var distSq = d.X * d.X + d.Y * d.Y;
                    if (distSq < bestDist)
                    {
                        bestDist = distSq;
                        best = idx;
                    }
                }
            }

            var reach = r * CellSize;
            if (best >= 0 && bestDist <= reach * reach)
                return best;
        }

        best = 0;
        bestDist = int.MaxValue;
        for (var i = 0; i < chosen.Count; i++)
        {
            var d = pos - chosen[i];
            var distSq = d.X * d.X + d.Y * d.Y;
            if (distSq < bestDist)
            {
                bestDist = distSq;
                best = i;
            }
        }

        return best;
    }

    #endregion

    #region Sources

    private void ApplySources(EntityUid grid, GridState state)
    {
        var chosen = state.Chosen;
        var sources = state.Sources;

        for (var i = 0; i < chosen.Count; i++)
        {
            var tile = chosen[i];
            var intensity = MathF.Min(_fuel[i] * IntensityPerMole, MaxIntensityPerSource);

            if (i < sources.Count && !TerminatingOrDeleted(sources[i].Uid))
            {
                var info = sources[i];
                var changed = false;

                if (info.Tile != tile)
                {
                    _transform.SetCoordinates(info.Uid,
                        new EntityCoordinates(grid, new Vector2(tile.X + 0.5f, tile.Y + 0.5f)));
                    info.Tile = tile;
                    changed = true;
                }

                if (MathF.Abs(info.Intensity - intensity) > 0.01f || (intensity == 0f && info.Intensity != 0f))
                {
                    _radiation.SetIntensity(info.Uid, intensity);
                    info.Intensity = intensity;
                    changed = true;
                }

                if (changed)
                    sources[i] = info;

                continue;
            }

            var coords = new EntityCoordinates(grid, new Vector2(tile.X + 0.5f, tile.Y + 0.5f));
            var src = Spawn(Proto, coords);
            _radiation.SetIntensity(src, intensity);
            _radiation.SetSourceEnabled(src, true);

            var created = new SourceInfo { Uid = src, Tile = tile, Intensity = intensity };
            if (i < sources.Count)
                sources[i] = created;
            else
                sources.Add(created);
        }

        for (var i = sources.Count - 1; i >= chosen.Count; i--)
        {
            QueueDel(sources[i].Uid);
            sources.RemoveAt(i);
        }
    }

    #endregion

    #region Helpers

    private void ResetFuel(int count)
    {
        if (_fuel.Length < count)
            _fuel = new float[Math.Max(count, _fuel.Length * 2)];

        Array.Clear(_fuel, 0, count);
    }

    private static int Cell(int v) => v >= 0 ? v / CellSize : (v - CellSize + 1) / CellSize;

    private static long Pack(Vector2i p)
        => ((long) p.X << 32) | (uint) (p.Y ^ int.MinValue);

    private static Vector2i Unpack(long k)
        => new((int)(k >> 32), unchecked((int)(uint)k) ^ int.MinValue);

    #endregion
}
