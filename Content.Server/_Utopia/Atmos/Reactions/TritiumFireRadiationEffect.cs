using System.Numerics;
using Content.Server.Atmos.Reactions;
using Content.Server.Radiation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Atmos.Systems;

public sealed partial class TritiumFireRadiationSystem : EntitySystem
{
    [Dependency] private RadiationSystem _radiation = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string Proto = "TritiumFireRadiation";

    private const int SourceSpacing = 8;
    private const float FlushInterval = 1f;
    private static readonly TimeSpan TileTimeout = TimeSpan.FromSeconds(2);
    private const float IntensityPerMole = 40f;
    private const float MaxIntensityPerSource = 150f;

    private struct TileFire
    {
        public float Fuel;
        public TimeSpan LastSeen;
    }

    private sealed class GridState
    {
        public readonly Dictionary<Vector2i, TileFire> Tiles = new();
        public readonly List<EntityUid> Sources = new();
    }

    private static readonly Vector2i[] Neighbors =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    };

    private readonly Dictionary<EntityUid, GridState> _grids = new();
    private readonly List<EntityUid> _gridsToRemove = new();
    private readonly List<Vector2i> _expired = new();
    private readonly List<Vector2i> _perimeter = new();
    private readonly List<Vector2i> _chosen = new();
    private readonly List<float> _fuelPerSource = new();
    private float _accumulator;


    public void Pulse(EntityUid grid, Vector2i tile, float burnedFuel)
    {
        if (!_grids.TryGetValue(grid, out var state))
            _grids[grid] = state = new GridState();

        state.Tiles[tile] = new TileFire { Fuel = burnedFuel, LastSeen = _timing.CurTime };
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < FlushInterval)
            return;

        _accumulator = 0f;
        _gridsToRemove.Clear();

        foreach (var (grid, state) in _grids)
        {
            if (TerminatingOrDeleted(grid))
            {
                _gridsToRemove.Add(grid);
                continue;
            }

            Rebuild(grid, state);

            if (state.Tiles.Count == 0 && state.Sources.Count == 0)
                _gridsToRemove.Add(grid);
        }

        foreach (var grid in _gridsToRemove)
            _grids.Remove(grid);
    }

    private void Rebuild(EntityUid grid, GridState state)
    {
        var now = _timing.CurTime;

        _expired.Clear();
        foreach (var (pos, fire) in state.Tiles)
        {
            if (now - fire.LastSeen > TileTimeout)
                _expired.Add(pos);
        }
        foreach (var pos in _expired)
            state.Tiles.Remove(pos);

        _perimeter.Clear();
        foreach (var pos in state.Tiles.Keys)
        {
            foreach (var n in Neighbors)
            {
                if (!state.Tiles.ContainsKey(pos + n))
                {
                    _perimeter.Add(pos);
                    break;
                }
            }
        }

        _perimeter.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        _chosen.Clear();

        const int minDistSq = SourceSpacing * SourceSpacing;
        foreach (var pos in _perimeter)
        {
            var tooClose = false;
            foreach (var c in _chosen)
            {
                var d = pos - c;
                if (d.X * d.X + d.Y * d.Y < minDistSq)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                _chosen.Add(pos);
        }

        _fuelPerSource.Clear();
        for (var i = 0; i < _chosen.Count; i++)
            _fuelPerSource.Add(0f);

        if (_chosen.Count > 0)
        {
            foreach (var (pos, fire) in state.Tiles)
            {
                var best = 0;
                var bestDist = int.MaxValue;
                for (var i = 0; i < _chosen.Count; i++)
                {
                    var d = pos - _chosen[i];
                    var distSq = d.X * d.X + d.Y * d.Y;
                    if (distSq < bestDist)
                    {
                        bestDist = distSq;
                        best = i;
                    }
                }

                _fuelPerSource[best] += fire.Fuel;
            }
        }

        for (var i = 0; i < _chosen.Count; i++)
        {
            var tile = _chosen[i];
            var coords = new EntityCoordinates(grid, new Vector2(tile.X + 0.5f, tile.Y + 0.5f));
            var intensity = MathF.Min(_fuelPerSource[i] * IntensityPerMole, MaxIntensityPerSource);

            EntityUid src;
            if (i < state.Sources.Count && !TerminatingOrDeleted(state.Sources[i]))
            {
                src = state.Sources[i];
                _transform.SetCoordinates(src, coords);
            }
            else
            {
                src = Spawn(Proto, coords);
                if (i < state.Sources.Count)
                    state.Sources[i] = src;
                else
                    state.Sources.Add(src);
            }

            _radiation.SetIntensity(src, intensity);
            _radiation.SetSourceEnabled(src, true);
        }

        for (var i = state.Sources.Count - 1; i >= _chosen.Count; i--)
        {
            QueueDel(state.Sources[i]);
            state.Sources.RemoveAt(i);
        }
    }
}
