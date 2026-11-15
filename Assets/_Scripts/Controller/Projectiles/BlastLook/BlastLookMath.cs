using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One piece of blast debris (a casing shard or an ember), simulated on the CPU by
    /// <see cref="BlastLookSystem"/>.</summary>
    public struct BlastDebris
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Quaternion Rotation;
        public Vector3 SpinAxis;
        public float Spin;
        /// <summary>Per-axis size, world units (x, y across; z along the shard).</summary>
        public Vector3 Size;
        public float Age;
        public float Life;
        public float Cool;
        public float Drag;
        public float SpinDrag;
        public bool Streaks;
    }

    /// <summary>
    /// The curves of the blast look (BLAST_LOOK.md), pure and deterministic so the edit-mode tests can hold them:
    /// how a piece of debris is seeded, how it flies (exponential drag: it slows and HANGS), how it cools and
    /// shrinks, and the core's and shell's beats. A blast is seeded from its own position, so a replay of the
    /// match throws the same shards the match did.
    /// </summary>
    public static class BlastLookMath
    {
        /// <summary>A hash in [0, 1) of (seed, index, channel): the debris' dice. No global Random.</summary>
        public static float Hash01(uint seed, int index, int channel)
        {
            uint h = seed ^ (uint)(index * 0x27D4EB2D) ^ (uint)(channel * 0x165667B1);
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            h *= 0xC2B2AE3Du;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777216f;
        }

        /// <summary>The seed a blast at <paramref name="position"/> rolls its debris with.</summary>
        public static uint SeedFor(Vector3 position)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)Mathf.RoundToInt(position.x * 8f)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(position.y * 8f)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(position.z * 8f)) * 16777619u;
                return h == 0 ? 1u : h;
            }
        }

        /// <summary>A direction uniform on the unit sphere from two dice.</summary>
        public static Vector3 SphereDirection(float u, float v)
        {
            float z = 2f * u - 1f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float a = 2f * Mathf.PI * v;
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }

        /// <summary>
        /// The speed at which a piece must leave so that drag stops it at <paramref name="reach"/>: under
        /// <c>dv/dt = -k v</c> a piece travels <c>v0 / k</c> in all, so <c>v0 = reach x k</c>.
        /// </summary>
        public static float LaunchSpeedForReach(float reach, float drag) => reach * drag;

        /// <summary>Distance travelled by <paramref name="seconds"/> under exponential drag.</summary>
        public static float DistanceAt(float launchSpeed, float drag, float seconds) =>
            launchSpeed / drag * (1f - Mathf.Exp(-drag * seconds));

        /// <summary>Seed piece <paramref name="index"/> of a blast of radius <paramref name="radius"/> at
        /// <paramref name="origin"/>. Sizes are drawn on a cube curve, so most pieces are small and a few are
        /// big, as in any real break.</summary>
        public static BlastDebris Seed(uint seed, int index, Vector3 origin, float radius, in BlastDebrisSpec spec)
        {
            float h(int c) => Hash01(seed, index, c);

            var dir = SphereDirection(h(0), h(1));
            float reach = radius * Mathf.Lerp(spec.ReachShare.x, spec.ReachShare.y, Mathf.Sqrt(h(2)));
            float size = Mathf.Clamp(radius * Mathf.Lerp(spec.SizeShare.x, spec.SizeShare.y, h(3) * h(3) * h(3)),
                spec.SizeClamp.x, spec.SizeClamp.y);

            var debris = new BlastDebris
            {
                // Born just inside the core so the first frame is a burst, not a cloud already standing.
                Position = origin + dir * (radius * 0.01f * h(4)),
                Velocity = dir * LaunchSpeedForReach(reach, spec.Drag),
                Rotation = Quaternion.LookRotation(SphereDirection(h(5), h(6)), SphereDirection(h(7), h(8))),
                SpinAxis = SphereDirection(h(9), h(10)),
                Spin = spec.Spin * Mathf.Lerp(0.25f, 1f, h(11)),
                // Irregular: a little flatter and narrower than long, each piece its own.
                Size = new Vector3(size * Mathf.Lerp(0.45f, 0.8f, h(12)), size * Mathf.Lerp(0.3f, 0.7f, h(13)), size),
                Age = 0f,
                Life = Mathf.Lerp(spec.Life.x, spec.Life.y, h(14)),
                Cool = Mathf.Lerp(spec.Cool.x, spec.Cool.y, h(15)),
                Drag = spec.Drag,
                SpinDrag = spec.SpinDrag,
                Streaks = spec.Streaks,
            };
            return debris;
        }

        /// <summary>Advance one piece by <paramref name="dt"/>. Exact under exponential drag, so a slow-motion
        /// step and a full-speed one land the piece on the same path.</summary>
        public static void Step(ref BlastDebris d, float dt)
        {
            if (dt <= 0f) return;
            float decay = Mathf.Exp(-d.Drag * dt);
            d.Position += d.Velocity * ((1f - decay) / d.Drag);
            d.Velocity *= decay;
            if (d.Spin > 0f)
            {
                d.Rotation = Quaternion.AngleAxis(d.Spin * dt * Mathf.Rad2Deg, d.SpinAxis) * d.Rotation;
                d.Spin *= Mathf.Exp(-d.SpinDrag * dt);
            }
            d.Age += dt;
        }

        public static bool Alive(in BlastDebris d) => d.Age < d.Life;

        /// <summary>Heat, 1 white-hot at the release falling by e every Cool seconds.</summary>
        public static float Heat(float age, float cool) => cool <= 0f ? 0f : Mathf.Exp(-Mathf.Max(0f, age) / cool);

        /// <summary>Scale, 1 for most of the life and shrinking smoothly to 0 over the last
        /// <paramref name="shrinkShare"/> of it.</summary>
        public static float Shrink(float age, float life, float shrinkShare)
        {
            if (life <= 0f) return 0f;
            float start = life * (1f - Mathf.Clamp01(shrinkShare));
            if (age <= start) return 1f;
            float t = Mathf.Clamp01((age - start) / Mathf.Max(1e-4f, life - start));
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>The core's radius as a share of its widest at <paramref name="age"/>: a flash out
        /// (ease-out), a hold, then a collapse as it cools.</summary>
        public static float CoreScale(float age, float grow, float hold, float fade)
        {
            if (age < 0f) return 0f;
            if (age < grow)
            {
                float t = age / Mathf.Max(1e-4f, grow);
                return 1f - (1f - t) * (1f - t);
            }
            float after = age - grow - hold;
            if (after <= 0f) return 1f;
            float f = Mathf.Clamp01(after / Mathf.Max(1e-4f, fade));
            return 1f - f * f * (3f - 2f * f);
        }

        /// <summary>The core's heat: white-hot through the flash and the hold, then cooling.</summary>
        public static float CoreHeat(float age, float grow, float hold, float fade)
        {
            float after = age - grow - hold;
            return after <= 0f ? 1f : Mathf.Exp(-after / Mathf.Max(1e-4f, fade * 0.45f));
        }

        /// <summary>The shock front's growth, 0 to 1 over the blast's duration: the blast's own
        /// <c>sin(t pi / 2)</c> ease (AOEExplosion.ExplodeAsync), so the front is where the sweep is.</summary>
        public static float ShellGrowth(float age, float duration) =>
            duration <= 0f ? 1f : Mathf.Sin(Mathf.Clamp01(age / duration) * Mathf.PI * 0.5f);
    }
}
