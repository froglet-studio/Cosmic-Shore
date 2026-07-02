namespace CosmicShore.Engine
{
    /// <summary>
    /// Frame clock for the simulation loop. The game loop (or a test harness) drives it
    /// via <see cref="Advance"/>; everything else reads it like the original engine API.
    /// </summary>
    public static class Time
    {
        static float _frameDeltaTime;
        static bool _inFixedPhase;

        /// <summary>Scaled frame delta; reports <see cref="fixedDeltaTime"/> during FixedUpdate (original contract).</summary>
        public static float deltaTime => _inFixedPhase ? fixedDeltaTime : _frameDeltaTime;
        public static float unscaledDeltaTime { get; private set; }
        public static float time { get; private set; }

        /// <summary>
        /// Double-precision view of <see cref="time"/> (engine addition for SA1:
        /// TimePlayedScoring's offline clock path). Backed by the same float
        /// accumulator until a long-session use case demands a true double track.
        /// </summary>
        public static double timeAsDouble => time;
        public static float unscaledTime { get; private set; }

        /// <summary>
        /// Wall-clock seconds since startup in the original engine. Backed by the
        /// harness-driven unscaled clock here so headless runs stay deterministic
        /// (readers: AdaptiveAnimationManager's frame-interval throttle).
        /// </summary>
        public static float realtimeSinceStartup => unscaledTime;
        public static float fixedDeltaTime { get; set; } = 0.02f;
        public static float timeScale { get; set; } = 1f;
        public static int frameCount { get; private set; }
        public static bool inFixedTimeStep => _inFixedPhase;

        /// <summary>Advance the clock by one frame of <paramref name="unscaledDelta"/> seconds.</summary>
        public static void Advance(float unscaledDelta)
        {
            unscaledDeltaTime = unscaledDelta;
            _frameDeltaTime = unscaledDelta * timeScale;
            unscaledTime += unscaledDeltaTime;
            time += _frameDeltaTime;
            frameCount++;
        }

        internal static void EnterFixedPhase() => _inFixedPhase = true;
        internal static void ExitFixedPhase() => _inFixedPhase = false;

        /// <summary>Reset the clock to zero (test isolation / scene reload).</summary>
        public static void Reset()
        {
            _frameDeltaTime = 0f;
            _inFixedPhase = false;
            unscaledDeltaTime = 0f;
            time = 0f;
            unscaledTime = 0f;
            timeScale = 1f;
            frameCount = 0;
        }
    }
}
