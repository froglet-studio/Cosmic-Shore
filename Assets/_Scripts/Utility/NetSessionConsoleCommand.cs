// ─────────────────────────────────────────────────────────────────────────────
// NetSessionConsoleCommand.cs
// The `net` console command: read the networking counters, and write the session record.
//
// WHY it self-registers:
//   DiagnosticsHUD's own doc says it - "Systems add commands via
//   DiagnosticsHUD.RegisterCommand". Registering from here instead of adding a line to
//   DiagnosticsHUD keeps a 1,400-line file out of the diff and keeps this feature's three
//   files together. RegisterCommand's body is already #if UNITY_EDITOR || DEVELOPMENT_BUILD,
//   so the call is a no-op in a release player and needs no guard of its own (the shell stays
//   unguarded - Docs/CONDITIONAL_COMPILATION.md Pattern 1).
//
// WHY A COMMAND AND NOT A WINDOW:
//   The point of the session record is that it is taken DURING a run, on whatever machine the
//   run is happening on, including a player build where no Editor window exists. A console
//   line works in MPPM, in a standalone build, and over a remote session; a window works in
//   one of those three.
//
// SUBCOMMANDS
//   net          one-line summary - the §6.4 numbers a run is judged on
//   net dump     write the JSON and return the path
//   net reset    clear the timeline AND the counters (both owners), to start a measurement
//   net mark <t> append a manual marker, so a human action lands in the timeline
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>Registers and handles the <c>net</c> diagnostics command.</summary>
    public static class NetSessionConsoleCommand
    {
        public const string CommandName = "net";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() =>
            PerformanceBenchmark.DiagnosticsHUD.RegisterCommand(CommandName, Handle);

        /// <summary>Handles <c>net [dump|reset|mark &lt;text&gt;]</c>. Returns the overlay line.</summary>
        public static string Handle(string[] args)
        {
            string sub = args is { Length: > 0 } ? args[0].ToLowerInvariant() : string.Empty;

            switch (sub)
            {
                case "":
                    return Summary();

                case "dump":
                {
                    string path = NetSessionRecorder.WriteJson();
                    return path == null
                        ? "net: could not write the record - see the warning above"
                        : $"net: wrote {path}";
                }

                case "reset":
                    // Both owners, because a measurement that starts with yesterday's counters
                    // and today's timeline is worse than no measurement.
                    NetSessionRecorder.Reset();
                    UgsRequestTelemetry.Reset();
                    return "net: timeline and counters reset";

                case "mark":
                {
                    string text = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "mark";
                    NetSessionRecorder.Mark("manual", text);
                    return $"net: marked '{text}' at t={NetSessionRecorder.Elapsed:F1}s";
                }

                default:
                    return $"net: unknown '{sub}' - try: net | net dump | net reset | net mark <text>";
            }
        }

        /// <summary>
        /// The verdict first, because that is the question. A clean run is one line; a dirty one
        /// names which of the four went non-zero so the next step is obvious.
        /// </summary>
        static string Summary()
        {
            var r = NetSessionRecorder.BuildRecord();
            var c = r.counters;
            var v = r.verdict;

            string verdict = v.IsClean
                ? "CLEAN"
                : $"DIRTY(429={v.rateLimited} reset={v.forceResets} " +
                  $"budget={v.budgetExhaustions} offlineWhileOnline={v.offlineFallbacksWhileOnline})";

            return $"net {verdict} | {r.role}{(r.isOfflineSession ? "/offline" : "")} " +
                   $"| {r.durationSeconds:F0}s | reads {c.lobbyReads} ({c.lobbyReadsPerSecond:F2}/s) " +
                   $"| req {c.requests} retry {c.retries} coalesced {c.coalesced} " +
                   $"| events {NetSessionRecorder.EventCount}" +
                   (r.lifecycleDropped > 0 ? $" (+{r.lifecycleDropped} dropped)" : "");
        }
    }
}
