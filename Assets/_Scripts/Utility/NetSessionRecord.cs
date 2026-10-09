// ─────────────────────────────────────────────────────────────────────────────
// NetSessionRecord.cs
// The serializable shape of one networking session, written as JSON by
// NetSessionRecorder and read by a human, a diff, or a soak-run assertion.
//
// WHY this file exists (Docs/MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md §6.3):
//   UgsRequestTelemetry already COUNTS the right things - requests, lobby reads, retries,
//   every failure class, budget exhaustions, force-resets, offline fallbacks - and
//   DiagnosticsHUD already has a JSON sink. What was missing was a record that survives the
//   session: a counter you can only read on an overlay cannot be attached to a bug report,
//   diffed against yesterday's run, or asserted on by a 30-minute soak.
//
//   The Verdict block exists so a soak run PASSES OR FAILS on four integers rather than on
//   somebody's reading of a console. That is the whole point of writing this to disk.
//
// WHY A DTO AND NOT A JObject:
//   Unity's JsonUtility serialises public fields of a [Serializable] type and nothing else, so
//   the schema is the type. A field added here appears in every later run's JSON and in the
//   diff; a field removed stops appearing. There is no stringly-typed key to misspell.
//
// THREAD SAFETY / ENGINE DEPENDENCE:
//   None. Plain data, no UnityEngine calls, no statics - so the whole schema is constructible
//   and assertable from an edit-mode test with no PlayerLoop and no UGS.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace CosmicShore.Utility
{
    /// <summary>One timestamped thing that happened to the networking layer.</summary>
    /// <remarks>
    /// <paramref name="detail"/> is deliberately one free-text field rather than a typed
    /// payload per event kind: the timeline's job is to make a failure REPRODUCIBLE, and the
    /// moment it needs a schema per event nobody adds events to it.
    /// </remarks>
    [Serializable]
    public struct NetSessionEvent
    {
        /// <summary>Seconds since the recorder started, not since level load.</summary>
        public float t;

        /// <summary>A short stable verb: <c>signedIn</c>, <c>partyCreated</c>, <c>inviteSent</c>, <c>disconnect</c>, <c>recovery</c>.</summary>
        public string e;

        /// <summary>Whatever makes this occurrence identifiable - an id, a verdict, a duration.</summary>
        public string detail;
    }

    /// <summary>The counters, sampled at write time. Names match <see cref="UgsRequestCounter"/>.</summary>
    [Serializable]
    public struct NetSessionCounters
    {
        public long requests, lobbyReads, retries, coalesced, budgetExhausted;
        public long rateLimited, transient, benign, conflict, full, gone, fatal;
        public long presenceForceReset, offlineFallback;

        /// <summary>Lobby reads per second across the whole session - the number §6.4 thresholds.</summary>
        public float lobbyReadsPerSecond;
    }

    /// <summary>
    /// The four integers a soak run is judged on. Every one of them should be zero on a healthy
    /// run; any non-zero value is a named defect with a bug entry behind it.
    /// </summary>
    [Serializable]
    public struct NetSessionVerdict
    {
        /// <summary>Entered an offline session while the device was genuinely online (B24's shape).</summary>
        public long offlineFallbacksWhileOnline;

        /// <summary>The presence layer was rebuilt from scratch (<c>ForceReset</c>).</summary>
        public long forceResets;

        /// <summary>The retry budget ran out, so a failure was thrown rather than retried.</summary>
        public long budgetExhaustions;

        /// <summary>Rate limits seen at all. Non-zero means the read rate is still too high.</summary>
        public long rateLimited;

        /// <summary>True only when all four are zero.</summary>
        public bool IsClean =>
            offlineFallbacksWhileOnline == 0 && forceResets == 0 &&
            budgetExhaustions == 0 && rateLimited == 0;
    }

    /// <summary>One session's networking record. Schema <c>cosmicshore.netsession.v1</c>.</summary>
    [Serializable]
    public sealed class NetSessionRecord
    {
        public string schema = "cosmicshore.netsession.v1";

        public string startedUtc;
        public float  durationSeconds;
        public string buildGuid;
        public string platform;
        public bool   isOfflineSession;

        /// <summary>host / client / spectator / offline - what this peer was for most of the session.</summary>
        public string role;

        public NetSessionCounters counters;
        public NetSessionVerdict  verdict;

        /// <summary>Oldest first. Bounded by the recorder so a long session cannot grow without limit.</summary>
        public List<NetSessionEvent> lifecycle = new();

        /// <summary>Set when the timeline hit its cap, so a truncated record never reads as a complete one.</summary>
        public int lifecycleDropped;
    }
}
