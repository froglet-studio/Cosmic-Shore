namespace CosmicShore.Engine
{
    /// <summary>
    /// Engine stand-in for <c>UnityEngine.Random</c> — a single global, seedable RNG
    /// (engine addition for V10: <c>GameDataSO.GetRandomSpawnPose</c> draws spawn points
    /// through it; V-layer follow-ups like <c>SegmentSpawner.Initialize</c> reseed it via
    /// <see cref="InitState"/> for deterministic cross-client tracks). Same contracts as
    /// the original: int <see cref="Range(int,int)"/> is max-exclusive, float
    /// <see cref="Range(float,float)"/> is max-inclusive. Main-thread only, like the
    /// original.
    /// </summary>
    public static class Random
    {
        static System.Random _rng = new System.Random();

        /// <summary>Reseeds the global state (deterministic sequence per seed).</summary>
        public static void InitState(int seed) => _rng = new System.Random(seed);

        /// <summary>Random int in [minInclusive, maxExclusive). Returns minInclusive when the range is empty.</summary>
        public static int Range(int minInclusive, int maxExclusive)
            => minInclusive >= maxExclusive ? minInclusive : _rng.Next(minInclusive, maxExclusive);

        /// <summary>Random float in [minInclusive, maxInclusive].</summary>
        public static float Range(float minInclusive, float maxInclusive)
            => minInclusive + (float)_rng.NextDouble() * (maxInclusive - minInclusive);

        /// <summary>Random float in [0, 1].</summary>
        public static float value => (float)_rng.NextDouble();

        /// <summary>Random point on the surface of a unit sphere (uniform — Marsaglia rejection).</summary>
        public static Vector3 onUnitSphere
        {
            get
            {
                while (true)
                {
                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                    float sqr = p.sqrMagnitude;
                    if (sqr > 1e-6f && sqr <= 1f) return p / Mathf.Sqrt(sqr);
                }
            }
        }

        /// <summary>Uniformly random rotation (axis from the unit sphere, angle in [0, 360)).</summary>
        public static Quaternion rotation
            => Quaternion.AngleAxis(Range(0f, 360f), onUnitSphere);

        /// <summary>Random point inside (or on) a unit sphere.</summary>
        public static Vector3 insideUnitSphere
        {
            get
            {
                while (true)
                {
                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                    if (p.sqrMagnitude <= 1f) return p;
                }
            }
        }
    }
}
