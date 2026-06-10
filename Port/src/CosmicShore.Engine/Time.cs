namespace CosmicShore.Engine
{
    /// <summary>
    /// Frame clock for the simulation loop. The game loop (or a test harness) drives it
    /// via <see cref="Advance"/>; everything else reads it like the original engine API.
    /// </summary>
    public static class Time
    {
        public static float deltaTime { get; private set; }
        public static float unscaledDeltaTime { get; private set; }
        public static float time { get; private set; }
        public static float unscaledTime { get; private set; }
        public static float fixedDeltaTime { get; set; } = 0.02f;
        public static float timeScale { get; set; } = 1f;
        public static int frameCount { get; private set; }

        /// <summary>Advance the clock by one frame of <paramref name="unscaledDelta"/> seconds.</summary>
        public static void Advance(float unscaledDelta)
        {
            unscaledDeltaTime = unscaledDelta;
            deltaTime = unscaledDelta * timeScale;
            unscaledTime += unscaledDeltaTime;
            time += deltaTime;
            frameCount++;
        }

        /// <summary>Reset the clock to zero (test isolation / scene reload).</summary>
        public static void Reset()
        {
            deltaTime = 0f;
            unscaledDeltaTime = 0f;
            time = 0f;
            unscaledTime = 0f;
            timeScale = 1f;
            frameCount = 0;
        }
    }
}
