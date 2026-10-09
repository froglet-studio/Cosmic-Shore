// ─────────────────────────────────────────────────────────────────────────────
// NetSessionRecorder.cs
// Owns the networking session's TIMELINE, and builds the NetSessionRecord that gets written
// to disk. It does not count anything itself.
//
// WHY it counts nothing (the "one cross-cutting decision, one owner" rule):
//   UgsRequestTelemetry already owns every counter and is already called from the real sites -
//   UgsRequestPolicy.ExecuteAsync, LobbyPropertyWriter, PresenceLobbyService,
//   PartySessionService, HostConnectionService. A second counter here would be a second place
//   to forget, and the two would disagree the first time somebody added a call site. This
//   class READS that owner and adds the one thing it has no opinion about: when things
//   happened, in order.
//
// WHY A TIMELINE AT ALL:
//   A counter says a 429 happened; it cannot say it happened 300 ms after the second guest
//   pressed Accept. Every bug in Docs/PartySystem/BUGS.md was diagnosed by reconstructing an
//   order of events from console scrollback. This records that order on purpose, bounded, so
//   it survives the session and can be attached to a report.
//
// DEPENDENCY DIRECTION:
//   Nothing in the party layer is referenced. Role and offline state arrive through two
//   assignable providers, so this file compiles and tests with no NetworkManager, no UGS and
//   no SOAP. Whoever knows the answer sets the provider; the default is honest ignorance.
//
// COST:
//   One struct appended to a List per marked event, capped at MaxEvents. No per-frame work, no
//   allocation while idle, no I/O until something asks for the file.
//
// THREAD SAFETY:
//   Main-thread only, like the rest of the party layer. Mark() from a background continuation
//   would race the list; every caller is already behind .AsMainThread().
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Records the networking session timeline and writes a <see cref="NetSessionRecord"/>.
    /// Static because the call sites are scattered across the party, presence and match layers
    /// and a one-line <c>NetSessionRecorder.Mark(...)</c> is the only shape that gets used -
    /// the same reasoning as <see cref="UgsRequestTelemetry"/>, whose style this follows.
    /// </summary>
    public static class NetSessionRecorder
    {
        /// <summary>
        /// Timeline cap. A 30-minute soak marking an event a second would hold 1,800; this
        /// leaves room for a burst without letting a pathological loop eat memory. When the cap
        /// is hit the OLDEST events are dropped and the count is recorded, because the end of a
        /// session is where the failure is.
        /// </summary>
        public const int MaxEvents = 4000;

        /// <summary>Subfolder of <see cref="Application.persistentDataPath"/>, beside PerfRuns.</summary>
        public const string Folder = "NetRuns";

        /// <summary>Seconds source. Assignable so tests run the whole class with no PlayerLoop.</summary>
        public static Func<double> Now { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>What this peer currently is: <c>host</c>, <c>client</c>, <c>spectator</c>, <c>offline</c>.</summary>
        public static Func<string> RoleProvider { get; set; } = () => "unknown";

        /// <summary>Whether the session is an offline loopback one.</summary>
        public static Func<bool> OfflineProvider { get; set; } = () => false;

        /// <summary>
        /// Whether the DEVICE had a network when an offline fallback was taken. This is what
        /// separates "the player has no wifi" (correct) from B24's shape, "we were online and
        /// gave up anyway" (a defect) - and only the latter belongs in the verdict.
        /// </summary>
        public static Func<bool> DeviceOnlineProvider { get; set; } = () => true;

        static readonly System.Collections.Generic.List<NetSessionEvent> s_events = new();
        static double s_startedAt = -1;
        static string s_startedUtc;
        static int s_dropped;
        static long s_offlineFallbacksWhileOnline;

        /// <summary>Seconds since the first mark, or 0 before anything was recorded.</summary>
        public static float Elapsed => s_startedAt < 0 ? 0f : (float)(Now() - s_startedAt);

        /// <summary>How many events the timeline currently holds.</summary>
        public static int EventCount => s_events.Count;

        /// <summary>
        /// Appends one event. <paramref name="e"/> should be a short stable verb so a diff
        /// between two runs lines up; <paramref name="detail"/> carries the identifying part.
        /// </summary>
        public static void Mark(string e, string detail = null)
        {
            if (string.IsNullOrEmpty(e)) return;

            if (s_startedAt < 0)
            {
                s_startedAt = Now();
                s_startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            }

            if (s_events.Count >= MaxEvents)
            {
                s_events.RemoveAt(0);
                s_dropped++;
            }

            s_events.Add(new NetSessionEvent { t = Elapsed, e = e, detail = detail ?? string.Empty });
        }

        /// <summary>
        /// Records that an offline session was entered, and whether the device was reachable at
        /// the time. Call this from the one place that sets <c>IsOfflineSession</c>; the verdict
        /// counts only the reachable case.
        /// </summary>
        public static void MarkOfflineFallback(bool deviceWasOnline)
        {
            if (deviceWasOnline) s_offlineFallbacksWhileOnline++;
            Mark("offlineFallback", deviceWasOnline ? "deviceWasOnline" : "deviceOffline");
        }

        /// <summary>Builds the record from the timeline plus <see cref="UgsRequestTelemetry"/>.</summary>
        public static NetSessionRecord BuildRecord()
        {
            long T(UgsRequestCounter c) => UgsRequestTelemetry.Total(c);
            float seconds = Elapsed;

            var record = new NetSessionRecord
            {
                startedUtc       = s_startedUtc ?? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                durationSeconds  = seconds,
                buildGuid        = Application.buildGUID,
                platform         = Application.platform.ToString(),
                isOfflineSession = SafeBool(OfflineProvider),
                role             = SafeString(RoleProvider),
                lifecycleDropped = s_dropped,
                counters = new NetSessionCounters
                {
                    requests            = T(UgsRequestCounter.Requests),
                    lobbyReads          = T(UgsRequestCounter.LobbyReads),
                    retries             = T(UgsRequestCounter.Retries),
                    coalesced           = T(UgsRequestCounter.Coalesced),
                    budgetExhausted     = T(UgsRequestCounter.BudgetExhausted),
                    rateLimited         = T(UgsRequestCounter.RateLimited),
                    transient           = T(UgsRequestCounter.Transient),
                    benign              = T(UgsRequestCounter.Benign),
                    conflict            = T(UgsRequestCounter.Conflict),
                    full                = T(UgsRequestCounter.Full),
                    gone                = T(UgsRequestCounter.Gone),
                    fatal               = T(UgsRequestCounter.Fatal),
                    presenceForceReset  = T(UgsRequestCounter.PresenceForceReset),
                    offlineFallback     = T(UgsRequestCounter.OfflineFallback),
                    // Guarded: a record built before the first mark would divide by zero and
                    // report Infinity, which serialises and then poisons every later average.
                    lobbyReadsPerSecond = seconds > 0.01f ? T(UgsRequestCounter.LobbyReads) / seconds : 0f,
                },
                verdict = new NetSessionVerdict
                {
                    offlineFallbacksWhileOnline = s_offlineFallbacksWhileOnline,
                    forceResets                 = T(UgsRequestCounter.PresenceForceReset),
                    budgetExhaustions           = T(UgsRequestCounter.BudgetExhausted),
                    rateLimited                 = T(UgsRequestCounter.RateLimited),
                },
            };

            record.lifecycle.AddRange(s_events);
            return record;
        }

        /// <summary>
        /// Writes the record as JSON and returns the full path, or null on failure.
        /// </summary>
        /// <remarks>
        /// Not called automatically. A session that never asks for the file does no I/O at all,
        /// which is what lets the timeline stay on in a shipped build.
        /// </remarks>
        public static string WriteJson()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, Folder);
                Directory.CreateDirectory(dir);

                string name = $"net_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                string path = Path.Combine(dir, name);
                File.WriteAllText(path, JsonUtility.ToJson(BuildRecord(), prettyPrint: true));
                return path;
            }
            catch (Exception e)
            {
                // A real fault: the caller asked for a file and there is none. Never a channel.
                CSDebug.LogWarning($"[NetSessionRecorder] Could not write the session record: {e.Message}");
                return null;
            }
        }

        /// <summary>Clears the timeline and the derived verdict. Does not touch the counters.</summary>
        /// <remarks>
        /// <see cref="UgsRequestTelemetry.Reset"/> owns the counters; resetting them from here
        /// would make two owners of one piece of state. A full reset is both calls.
        /// </remarks>
        public static void Reset()
        {
            s_events.Clear();
            s_startedAt = -1;
            s_startedUtc = null;
            s_dropped = 0;
            s_offlineFallbacksWhileOnline = 0;
        }

        // A provider is assigned by another system, so it can throw or be left dangling after a
        // scene unload. A diagnostic that takes the session down with it is worse than useless.
        static string SafeString(Func<string> f)
        {
            try { return f?.Invoke() ?? "unknown"; } catch { return "unknown"; }
        }

        static bool SafeBool(Func<bool> f)
        {
            try { return f?.Invoke() ?? false; } catch { return false; }
        }
    }
}
