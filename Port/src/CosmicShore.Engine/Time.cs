using System;

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
        static float _levelLoadTime;

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
        /// Scaled seconds since the last Single scene load finished (original contract), marked by
        /// <see cref="SceneManagement.SceneManager.NotifySceneLoaded(SceneManagement.Scene, SceneManagement.LoadSceneMode)"/>.
        /// </summary>
        public static float timeSinceLevelLoad => time - _levelLoadTime;

        internal static void MarkLevelLoaded() => _levelLoadTime = time;

        /// <summary>
        /// Wall-clock seconds since startup in the original engine. Backed by the
        /// harness-driven unscaled clock here so headless runs stay deterministic
        /// (readers: AdaptiveAnimationManager's frame-interval throttle).
        /// </summary>
        public static float realtimeSinceStartup => unscaledTime;
        public static double realtimeSinceStartupAsDouble => unscaledTime;
        public static double unscaledTimeAsDouble => unscaledTime;
        /// <summary>The physics step (ProjectSettings/TimeManager "Fixed Timestep"; 0.02 until the project's is read).</summary>
        public static float fixedDeltaTime { get; set; } = 0.02f;

        /// <summary>
        /// The longest a frame's deltaTime may report (TimeManager "Maximum Allowed Timestep").
        /// Unbounded until a project's settings are read: a bare harness that ticks one long
        /// step means that step.
        /// </summary>
        public static float maximumDeltaTime { get; set; } = float.PositiveInfinity;
        public static float timeScale { get; set; } = 1f;
        public static int frameCount { get; private set; }
        public static bool inFixedTimeStep => _inFixedPhase;

        /// <summary>Advance the clock by one frame of <paramref name="unscaledDelta"/> seconds.</summary>
        public static void Advance(float unscaledDelta)
        {
            unscaledDeltaTime = unscaledDelta;
            // The engine clamps a hitch to maximumDeltaTime, so a long frame cannot run an
            // unbounded burst of fixed steps (Unity's contract: deltaTime <= maximumDeltaTime).
            _frameDeltaTime = MathF.Min(unscaledDelta, maximumDeltaTime) * timeScale;
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
            _levelLoadTime = 0f;
            unscaledTime = 0f;
            timeScale = 1f;
            frameCount = 0;
            // A fresh world starts from the engine defaults; a project's TimeManager is read
            // after the loop exists (ContentRuntime), so its step never leaks into the next world.
            fixedDeltaTime = 0.02f;
            maximumDeltaTime = float.PositiveInfinity;
        }
    }
}
