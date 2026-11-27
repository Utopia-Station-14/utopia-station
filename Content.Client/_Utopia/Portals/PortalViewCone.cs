using System.Numerics;

namespace Content.Client._Utopia.Portals;

public static class PortalViewCone
{
    public static bool TryBuild(Vector2 eye, Vector2 center, float halfSize,
        Angle rotation, float range, out Vector2[] verts)
    {
        verts = Array.Empty<Vector2>();

        var local = (-rotation).RotateVec(eye - center);
        if (Math.Abs(local.X) < halfSize && Math.Abs(local.Y) < halfSize)
            return false;

        var dir = (center - eye).ToAngle();
        var min = double.MaxValue;
        var max = double.MinValue;
        var cMin = center;
        var cMax = center;

        for (var i = 0; i < 4; i++)
        {
            var corner = center + rotation.RotateVec(new Vector2(
                i == 0 || i == 3 ? -halfSize : halfSize,
                i < 2 ? -halfSize : halfSize));

            var a = Angle.ShortestDistance(dir, (corner - eye).ToAngle()).Theta;
            if (a < min) { min = a; cMin = corner; }
            if (a > max) { max = a; cMax = corner; }
        }

        var toMin = cMin - eye;
        var toMax = cMax - eye;
        if (toMin.LengthSquared() < 1e-6f || toMax.LengthSquared() < 1e-6f)
            return false;

        verts = new[]
        {
            cMin,
            cMax,
            eye + Vector2.Normalize(toMax) * range,
            eye + Vector2.Normalize(toMin) * range,
        };
        return true;
    }
}
