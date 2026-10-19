using System.Numerics;

namespace Content.Server._Utopia.Deflector;

[RegisterComponent]
public sealed partial class DeflectorComponent : Component
{
    [DataField("exitSide")]
    public List<Direction> ExitSide = new();

    [DataField("trinaryReflection")]
    public bool TrinaryReflection;

    [DataField("trinaryMirrorDirection")]
    public Direction? TrinaryMirrorDirection;

    [DataField("binaryReflection")]
    public bool BinaryReflection;

    public static readonly Dictionary<Direction, Vector2> DirectionToVector = new()
    {
        { Direction.South, new Vector2(0, 1) },
        { Direction.North, new Vector2(0, -1) },
        { Direction.West, new Vector2(1, 0) },
        { Direction.East, new Vector2(-1, 0) },
        { Direction.SouthWest, new Vector2(1, 1).Normalized() },
        { Direction.SouthEast, new Vector2(-1, 1).Normalized() },
        { Direction.NorthWest, new Vector2(1, -1).Normalized() },
        { Direction.NorthEast, new Vector2(-1, -1).Normalized() }
    };
}
