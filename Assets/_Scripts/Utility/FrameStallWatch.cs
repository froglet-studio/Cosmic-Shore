// ─────────────────────────────────────────────────────────────────────────────
// FrameStallWatch.cs
// Notices a frame that took seconds while the game is online, records it in the session record
// (NetSessionRecorder "stall"), and warns when it was long enough to lose the Relay connection.
//
// WHY (B36, Docs/PartySystem/BUGS.md):
//   Unity Relay drops a player it has heard nothing from for about 10 s ("player timed out due to
//   inactivity", then "allocation is invalid", then the host shuts down on a transport failure).
//   The transport pings the Relay every 0.5 s, but only from the main thread: a frame that lasts
//   10 s sends nothing. The owner's 20 Oct test lost its host three times in 20 minutes, each time
//   ~3 s after a Bloomrush scene was torn down, in a mode that ran at 190-680 ms a frame on that
//   machine. Nothing in the log said a frame had frozen, so the drops read as a network fault. This
//   records the freeze; the drop then names it (MultiplayerSetup.OnTransportFailure, "relayDrop").
//
// THREAD SAFETY: main-thread only. The clock is a settable delegate so tests drive it.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    public static class FrameStallWatch
    {
        /// <summary>A frame at least this long (seconds) is recorded as a stall while online.</summary>
        public const double STALL_SECONDS = 3.0;

        /// <summary>
        /// Unity Relay drops a connection after about 10 s without traffic. A stall this long (seconds)
        /// gets a warning, since the session may end because of it.
        /// </summary>
        public const double RELAY_RISK_SECONDS = 9.0;

        /// <summary>How long (seconds) after a stall a transport failure is still blamed on it.</summary>
        public const double BLAME_WINDOW_SECONDS = 30.0;

        /// <summary>Seconds source. Defaults to <see cref="Time.realtimeSinceStartupAsDouble"/>; tests substitute a manual clock.</summary>
        public static Func<double> Now { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>Whether a stall matters now: the NetworkManager is listening. Tests substitute it.</summary>
        public static Func<bool> OnlineProvider { get; set; } = () =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        static double s_lastFrameAt = double.NaN;
        static string s_lastScene;
        static double s_lastStallSeconds;
        static double s_lastStallEndedAt = double.NegativeInfinity;

        /// <summary>The longest stall recorded this Play, in seconds (0 when none).</summary>
        public static double LongestStallSeconds { get; private set; }

        /// <summary>
        /// Feeds one frame boundary. Returns the stall's length in seconds when the frame that just
        /// ended was a stall worth recording, else 0.
        /// </summary>
        public static double ObserveFrame(string sceneName)
        {
            double now = Now();
            double frame = double.IsNaN(s_lastFrameAt) ? 0 : now - s_lastFrameAt;
            s_lastFrameAt = now;
            // The frame began in the scene the previous boundary saw: a teardown freeze belongs to the scene
            // being torn down, not to the one active when the frame finally ends.
            string startedIn = s_lastScene ?? sceneName;
            s_lastScene = sceneName;
            if (frame < STALL_SECONDS || !SafeOnline()) return 0;
            string where = startedIn == sceneName ? sceneName : $"{startedIn}, ending in {sceneName}";

            s_lastStallSeconds = frame;
            s_lastStallEndedAt = now;
            if (frame > LongestStallSeconds) LongestStallSeconds = frame;

            string seconds = frame.ToString("0.0", CultureInfo.InvariantCulture);
            NetSessionRecorder.Mark("stall", $"{seconds}s in {where}");
            if (frame >= RELAY_RISK_SECONDS)
                CSDebug.LogWarning($"[Net] The game froze for {seconds} s in {where} while online. Unity Relay drops a " +
                                   "connection after about 10 s without traffic, so this session may end with 'player timed out " +
                                   "due to inactivity' (Docs/PartySystem/BUGS.md B36).");
            return frame;
        }

        /// <summary>
        /// The <c>relayDrop</c> detail for a transport failure now: the stall it most likely followed, or
        /// that none was seen in the last <see cref="BLAME_WINDOW_SECONDS"/>.
        /// </summary>
        public static string DescribeRecentStall()
        {
            double since = Now() - s_lastStallEndedAt;
            if (since > BLAME_WINDOW_SECONDS || s_lastStallSeconds <= 0)
                return "no freeze in the last 30 s";
            return $"after a {s_lastStallSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s freeze " +
                   $"{since.ToString("0.0", CultureInfo.InvariantCulture)} s earlier";
        }

        static bool SafeOnline()
        {
            try { return OnlineProvider(); }
            catch { return false; }
        }

        /// <summary>A new Play starts with no frame and no stall on the clock (no domain reload on Play).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            s_lastFrameAt = double.NaN;
            s_lastScene = null;
            s_lastStallSeconds = 0;
            s_lastStallEndedAt = double.NegativeInfinity;
            LongestStallSeconds = 0;
        }

        static int s_generation;

        /// <summary>One frame boundary per frame, for the whole Play, whatever scene is loaded.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => WatchAsync(++s_generation).Forget();

        // Ends when Play stops, or when a later Play's Install starts its own loop (no domain reload).
        static async UniTaskVoid WatchAsync(int generation)
        {
            while (generation == s_generation && Application.isPlaying)
            {
                ObserveFrame(SceneManager.GetActiveScene().name);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
    }
}
