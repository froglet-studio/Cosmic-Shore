using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pins the nondeterminism a parity replay must not carry (<c>Port/parity/README.md</c>):
    /// <see cref="Begin"/> seeds <see cref="UnityEngine.Random"/> and fixes the frame clock at
    /// 1/60 s (<c>Time.captureFramerate</c>), and <see cref="NewRandom"/> hands the game's
    /// otherwise unseeded <see cref="System.Random"/> sites a seed derived from the session seed
    /// and the site's name. With no session running every site behaves exactly as before:
    /// <see cref="NewRandom"/> returns <c>new System.Random()</c>.
    ///
    /// <para>The environment hook (<see cref="ApplyEnvironment"/>) lets a player build or Prisma
    /// be driven with no editor and no engine change: <c>COSMIC_SHORE_REPLAY</c> names a replay
    /// file (its seed starts the session and its status stream starts <see cref="ReplayPlayer"/>)
    /// and <c>COSMIC_SHORE_PARITY_OUT</c> names the directory <see cref="ParityProbe"/> writes
    /// the channels into. Both unset: nothing happens.</para>
    /// </summary>
    public static class DeterministicSession
    {
        public const string ReplayEnvironmentVariable = "COSMIC_SHORE_REPLAY";
        public const string ParityOutEnvironmentVariable = "COSMIC_SHORE_PARITY_OUT";
        public const int FixedFramerate = 60;

        static readonly Dictionary<string, int> s_siteCounters = new();

        /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>The session seed given to <see cref="Begin"/>.</summary>
        public static int Seed { get; private set; }

        /// <summary>Seeds UnityEngine.Random, fixes the frame clock at 1/60 s and resets the per-site counters.</summary>
        public static void Begin(int seed)
        {
            Seed = seed;
            IsActive = true;
            s_siteCounters.Clear();
            UnityEngine.Random.InitState(seed);
            Time.captureFramerate = FixedFramerate;
            CSDebug.LogVerbose(CSLogChannel.Parity, $"[DeterministicSession] begun with seed {seed}");
        }

        /// <summary>Ends the session and releases the frame clock. UnityEngine.Random is left as it is.</summary>
        public static void End()
        {
            if (!IsActive) return;
            IsActive = false;
            s_siteCounters.Clear();
            Time.captureFramerate = 0;
            CSDebug.LogVerbose(CSLogChannel.Parity, "[DeterministicSession] ended");
        }

        /// <summary>
        /// A <see cref="System.Random"/> for <paramref name="site"/>. Outside a session it is the
        /// time-seeded one the site always made. Inside one, the n-th request from a site gets a
        /// seed derived from the session seed, the site name and n, so two pilots asking from the
        /// same site do not share a stream and the same run order gives the same streams.
        /// </summary>
        public static System.Random NewRandom(string site)
        {
            if (!IsActive) return new System.Random();
            site ??= "";
            s_siteCounters.TryGetValue(site, out int n);
            s_siteCounters[site] = n + 1;
            return new System.Random(SiteSeed(Seed, site, n));
        }

        /// <summary>
        /// FNV-1a over the site name folded with the seed and the request index. Not
        /// <c>string.GetHashCode</c>, which .NET randomizes per process.
        /// </summary>
        public static int SiteSeed(int seed, string site, int index)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                foreach (char c in site ?? "") h = (h ^ c) * 16777619u;
                h = (h ^ (uint)index) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// The opt-in environment hook. Runs before the first scene loads in a player build and
        /// in Prisma (which invokes the same attribute), so the seed lands before any scene's
        /// Random draws and the probe sees the first scene load. Inert without the variables.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyEnvironment()
        {
            string replayPath = Environment.GetEnvironmentVariable(ReplayEnvironmentVariable);
            string parityOut = Environment.GetEnvironmentVariable(ParityOutEnvironmentVariable);
            if (string.IsNullOrEmpty(replayPath) && string.IsNullOrEmpty(parityOut)) return;

            int checkpointEvery = 30;
            if (!string.IsNullOrEmpty(replayPath))
            {
                if (!File.Exists(replayPath))
                {
                    CSDebug.LogError($"[DeterministicSession] {ReplayEnvironmentVariable} names '{replayPath}', which does not exist; no replay");
                }
                else
                {
                    var file = ReplayFile.Load(replayPath);
                    checkpointEvery = Math.Max(1, file.checkpointEvery);
                    Begin(file.seed);
                    // A replay with no status frames is driven by its do stream through the
                    // device strategies; the player would otherwise own the slot and eat that input.
                    if (file.Status.Length > 0) ReplayPlayer.Start(file);
                    else CSDebug.LogVerbose(CSLogChannel.Parity, $"[DeterministicSession] replay '{replayPath}' carries no status frames; device input stays in charge");
                }
            }

            if (!string.IsNullOrEmpty(parityOut))
                ParityProbe.Begin(parityOut, checkpointEvery);
        }
    }
}
