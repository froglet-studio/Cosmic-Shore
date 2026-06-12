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
    }
}
