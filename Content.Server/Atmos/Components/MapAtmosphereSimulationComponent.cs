using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server.Atmos.Components;

/// <summary>
/// Per-tile atmosphere simulation for open map space (outside any grid).
/// Parent entity must have <see cref="MapAtmosphereComponent"/> and <see cref="MapComponent"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(AtmosphereSystem), typeof(AtmosDebugOverlaySystem))]
public sealed partial class MapAtmosphereSimulationComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Simulated { get; set; } = true;

    [ViewVariables]
    public bool ProcessingPaused { get; set; }

    [ViewVariables]
    public float Timer { get; set; }

    [ViewVariables]
    public int UpdateCounter { get; set; } = 1;

    [ViewVariables]
    public Dictionary<Vector2i, TileAtmosphere> Tiles = new(256);

    [ViewVariables]
    public readonly HashSet<TileAtmosphere> ActiveTiles = new(256);

    [ViewVariables]
    public readonly HashSet<ExcitedGroup> ExcitedGroups = new(256);

    [ViewVariables]
    public readonly HashSet<TileAtmosphere> HotspotTiles = new(64);

    [ViewVariables]
    public readonly HashSet<TileAtmosphere> SuperconductivityTiles = new(64);

    [ViewVariables]
    public HashSet<TileAtmosphere> HighPressureDelta = new(64);

    [ViewVariables]
    public readonly HashSet<Vector2i> InvalidatedCoords = new(256);

    [ViewVariables]
    public readonly Queue<TileAtmosphere> CurrentRunInvalidatedTiles = new();

    [ViewVariables]
    public readonly Queue<TileAtmosphere> CurrentRunTiles = new();

    [ViewVariables]
    public readonly Queue<ExcitedGroup> CurrentRunExcitedGroups = new();

    [ViewVariables]
    public AtmosphereProcessingState State { get; set; } = AtmosphereProcessingState.Revalidate;

    /// <summary>
    /// Overlay chunks keyed by world tile chunk indices (same layout as <see cref="GasTileOverlayComponent"/>).
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector2i, GasOverlayChunk> OverlayChunks = new(64);

    [ViewVariables]
    public readonly HashSet<Vector2i> InvalidOverlayTiles = new(256);
}
