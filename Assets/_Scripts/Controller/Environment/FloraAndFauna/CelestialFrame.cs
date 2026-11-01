using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A celestial lifeform has no sense of up (Garrett, 2026-11-01; the /fauna-creation skill, law L6). It steers with an
    /// up it CARRIES: each turn rotates the up by the smallest rotation that takes the old heading to the new one (parallel
    /// transport), so the body never rights itself to the world's +y and never flips when it heads straight up or down -
    /// both of which a <c>LookRotation(forward, Vector3.up)</c> does. Engine-free (plain doubles) so the math is held by
    /// edit-mode tests outside Unity; a species converts at its edge (<see cref="BeeFauna"/>).
    /// </summary>
    public static class CelestialFrame
    {
        /// <summary><paramref name="up"/> carried from heading <paramref name="from"/> to heading <paramref name="to"/>:
        /// rotated by the minimal rotation between the two headings, then made exactly square to <paramref name="to"/>
        /// and unit. Inputs need not be unit. A reversal (the headings opposite) keeps the up: any half-turn about an
        /// axis square to the heading is minimal, and the one about the up itself leaves it where it is.</summary>
        public static void CarryUp(double ux, double uy, double uz, double fx, double fy, double fz,
            double tx, double ty, double tz, out double ox, out double oy, out double oz)
        {
            Normalize(ref fx, ref fy, ref fz);
            Normalize(ref tx, ref ty, ref tz);
            double c = fx * tx + fy * ty + fz * tz;
            ox = ux; oy = uy; oz = uz;
            if (c > -0.999999)
            {
                // Rodrigues for the rotation taking f to t: u' = u c + (v x u) + v (v . u) / (1 + c), v = f x t
                double vx = fy * tz - fz * ty, vy = fz * tx - fx * tz, vz = fx * ty - fy * tx;
                double vu = vx * ux + vy * uy + vz * uz, k = vu / (1.0 + c);
                ox = ux * c + (vy * uz - vz * uy) + vx * k;
                oy = uy * c + (vz * ux - vx * uz) + vy * k;
                oz = uz * c + (vx * uy - vy * ux) + vz * k;
            }
            // square to the new heading (drift, and the reversal case), then unit
            double d = ox * tx + oy * ty + oz * tz;
            ox -= tx * d; oy -= ty * d; oz -= tz * d;
            if (!Normalize(ref ox, ref oy, ref oz))
            {
                // the carried up lay along the heading (only a degenerate input does this): any square direction
                if (Math.Abs(tx) < 0.9) { ox = 0; oy = -tz; oz = ty; }
                else { ox = tz; oy = 0; oz = -tx; }
                Normalize(ref ox, ref oy, ref oz);
            }
        }

        static bool Normalize(ref double x, ref double y, ref double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            if (l < 1e-12) return false;
            x /= l; y /= l; z /= l;
            return true;
        }
    }
}
