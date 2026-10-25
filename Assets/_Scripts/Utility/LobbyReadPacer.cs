// ─────────────────────────────────────────────────────────────────────────────
// LobbyReadPacer.cs
// The one gate every lobby / session GET of ours passes: no two reads less than
// MIN_SPACING_SECONDS apart, whichever lobby they read.
//
// WHY (B31, Docs/PartySystem/BUGS.md):
//   UGS Lobby allows one GET per second PER PLAYER, across every lobby that player reads.
//   The first B31 fix kept the AVERAGE under it (0.83 GETs/s) but the refresh tick still read
//   the presence lobby and then the party session back to back, milliseconds apart. Every party
//   read landed in the same second as the presence read and was refused: Caleb's and Gradi's
//   logs (21-23 Oct) show 1,061 party-read 429s against 12 presence-read ones, one per party
//   poll. A rate cap is about spacing, not the mean - so every read waits its turn here.
//
// THREAD SAFETY: main-thread only. The clock is a settable delegate so tests drive it.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Utility
{
    public static class LobbyReadPacer
    {
        /// <summary>Fewest seconds between two lobby GETs: UGS's one per second, plus a margin for clock skew.</summary>
        public const double MIN_SPACING_SECONDS = 1.05;

        /// <summary>Seconds source. Defaults to <see cref="Time.realtimeSinceStartupAsDouble"/>; tests substitute a manual clock.</summary>
        public static Func<double> Now { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        static double s_lastReadAt = double.NegativeInfinity;

        /// <summary>Seconds until a read may go out; 0 when it may go now.</summary>
        public static double SecondsUntilNextRead()
        {
            double wait = s_lastReadAt + MIN_SPACING_SECONDS - Now();
            return wait > 0 ? wait : 0;
        }

        /// <summary>
        /// Claims the read slot if it is free and returns true; returns false (claiming nothing) while
        /// the last read is under <see cref="MIN_SPACING_SECONDS"/> old.
        /// </summary>
        public static bool TryTakeTurn()
        {
            if (SecondsUntilNextRead() > 0) return false;
            s_lastReadAt = Now();
            return true;
        }

        /// <summary>Waits (real time) until the read slot is free, then claims it.</summary>
        public static async UniTask WaitTurnAsync()
        {
            // Re-checked after every wait: another caller on the main thread may have claimed the slot
            // while this one slept.
            while (!TryTakeTurn())
                await UniTask.Delay(TimeSpan.FromSeconds(SecondsUntilNextRead()), DelayType.Realtime);
        }

        /// <summary>
        /// Reads <paramref name="session"/> from UGS once its turn comes, and counts the read. Every
        /// lobby GET of ours goes through here; the SDK's own reads, if any, are outside it.
        /// </summary>
        public static async UniTask RefreshAsync(ISession session)
        {
            await WaitTurnAsync();
            UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);
            await session.RefreshAsync().AsMainThread();
        }

        /// <summary>A new Play starts with no read on the clock (the project enters Play without a domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() => s_lastReadAt = double.NegativeInfinity;
    }
}
