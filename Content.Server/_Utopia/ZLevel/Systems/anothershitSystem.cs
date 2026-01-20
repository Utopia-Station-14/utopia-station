using System;
using Robust.Shared.Maths;

namespace Content.Server._Utopia.GridSync
{
    public static class AngleAveraging
    {
        public static Angle Average(params Angle[] angles)
        {
            if (angles.Length == 0)
                return Angle.Zero;

            double sin = 0;
            double cos = 0;

            foreach (var angle in angles)
            {
                sin += Math.Sin(angle);
                cos += Math.Cos(angle);
            }

            return new Angle(Math.Atan2(sin, cos));
        }
    }
}