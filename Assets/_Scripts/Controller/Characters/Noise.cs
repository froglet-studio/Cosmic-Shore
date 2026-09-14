using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Deterministic value noise + fBm for textures and hair clumping. Integer-lattice hashing,
    /// so the same coordinates give the same value on every machine.
    /// </summary>
    public static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            uint h = unchecked((uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1013904223));
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) * (1f / 16777216f);
        }

        /// <summary>Smooth value noise in [0,1].</summary>
        public static float Value(float x, float y, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float sx = GeometryKit.Smooth(fx), sy = GeometryKit.Smooth(fy);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>Fractal sum, roughly [0,1].</summary>
        public static float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f, int seed = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, seed + i * 31) * amp;
                norm += amp;
                x *= lacunarity; y *= lacunarity; amp *= gain;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Tileable-in-x value noise: x wraps at <paramref name="period"/>.</summary>
        public static float Periodic(float x, float y, int period, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float sx = GeometryKit.Smooth(fx), sy = GeometryKit.Smooth(fy);
            int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period;
            float a = Hash(x0, yi, seed), b = Hash(x1, yi, seed);
            float c = Hash(x0, yi + 1, seed), d = Hash(x1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>Distance to the nearest jittered lattice point (Worley F1) and its cell id, scaled so a cell is ~1 unit.</summary>
        public static float Worley(float x, float y, int seed, out float cellId)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 9f; cellId = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j;
                    float px = cx + Hash(cx, cy, seed), py = cy + Hash(cx, cy, seed + 7);
                    float dx = px - x, dy = py - y;
                    float d = dx * dx + dy * dy;
                    if (d < best) { best = d; cellId = Hash(cx, cy, seed + 13); }
                }
            return GeometryKit.SafeSqrt(best);
        }
    }
}
