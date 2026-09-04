// ─────────────────────────────────────────────────────────────────────────────
// NetworkDiagnostics.cs
//
// Catch-block diagnostic helper for party / lobby / session / transition
// failures. Pure observability - adding a call site never changes behavior.
//
// Two outputs:
//   - GetSnapshot() - one-line string of current Unity reachability + NetworkMonitor
//     state for inclusion in the appended catch-block log line.
//   - ClassifyException(e) - human-readable category for the same log line,
//     one of: "Offline" | "RateLimit" | "SessionGone" | "AuthRequired"
//             | "Cancelled" | "Transient" | "Unknown".
//
// Initialize() must be called once at boot from AppManager so the snapshot
// can include the live NetworkMonitor state (IsOnline, last transition time).
// If never initialized, GetSnapshot() still returns reachability - just
// without the monitor mirror - so the helper degrades gracefully on test
// harnesses that don't run AppManager.
//
// NOT a retry-control predicate. PartySessionService.IsTransientSessionException
// is the source of truth for retry decisions; ClassifyException is purely for
// log-line categorization. Keeping them separate prevents coupling log format
// to retry policy.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    public static class NetworkDiagnostics
    {
        private static NetworkMonitorDataVariable s_netDataVar;

        /// <summary>
        /// Wires the helper to the live <see cref="NetworkMonitorDataVariable"/>
        /// so <see cref="GetSnapshot"/> can include the monitor's state mirror
        /// (<c>IsOnline</c>, <c>LastTransitionUnscaledTime</c>). Called once
        /// from <c>AppManager.StartNetworkMonitor()</c>. Safe to call again -
        /// last writer wins.
        /// </summary>
        public static void Initialize(NetworkMonitorDataVariable netDataVar)
        {
            s_netDataVar = netDataVar;
        }

        /// <summary>
        /// One-line catch-block snapshot of current network reachability and
        /// the monitor's most recent state transition.
        /// <para>Example: <c>"reach=NotReachable|monitor=Offline|sinceChange=12.3s"</c>.</para>
        /// <para>
        /// Reachability comes from <see cref="Application.internetReachability"/>.
        /// On real devices this is accurate. In the Unity Editor it can read
        /// <c>ReachableViaLocalAreaNetwork</c> even when the WiFi adapter is off
        /// - the helper still emits the value as-is; pair it with the
        /// <c>monitor=</c> field for cross-checking.
        /// </para>
        /// </summary>
        public static string GetSnapshot()
        {
            var reach = Application.internetReachability;
            var data = s_netDataVar != null ? s_netDataVar.Value : null;
            if (data != null)
            {
                string monitor = data.IsOnline ? "Online" : "Offline";
                float since = Time.unscaledTime - data.LastTransitionUnscaledTime;
                return $"reach={reach}|monitor={monitor}|sinceChange={since:F1}s";
            }
            return $"reach={reach}|monitor=Uninitialized|sinceChange=N/A";
        }

        /// <summary>
        /// Categorize a caught exception into one of seven labels suitable for
        /// inclusion in a one-line log: <c>Offline</c>, <c>RateLimit</c>,
        /// <c>SessionGone</c>, <c>AuthRequired</c>, <c>Cancelled</c>,
        /// <c>Transient</c>, or <c>Unknown</c>.
        /// <para>
        /// Matches by type and by full type name (string) so SDK-version-
        /// sensitive types compile cleanly when absent. Order matters: more
        /// specific cases first.
        /// </para>
        /// <para>
        /// This is for LOGS, not RETRY CONTROL.
        /// <c>PartySessionService.IsTransientSessionException</c> remains the
        /// source of truth for retry decisions.
        /// </para>
        /// </summary>
        public static string ClassifyException(Exception e)
        {
            if (e == null) return "Unknown";

            // Unwrap one layer of AggregateException for cleaner classification -
            // UGS Tasks occasionally wrap. Use the innermost for matching.
            var inner = e;
            if (e is AggregateException agg && agg.InnerException != null)
                inner = agg.InnerException;

            // Cancellation first - short-circuits other matches when a flow
            // was cooperatively canceled mid-await.
            if (inner is OperationCanceledException || inner is TaskCanceledException)
                return "Cancelled";

            string typeName = inner.GetType().FullName ?? string.Empty;

            // Authentication - UGS auth dropped the user mid-flow.
            if (typeName == "Unity.Services.Authentication.AuthenticationException")
                return "AuthRequired";

            // UGS session-layer failure. SessionException is in
            // Unity.Services.Multiplayer; SDK version sensitive enum values
            // are read defensively via reflection on the message.
            if (typeName == "Unity.Services.Multiplayer.SessionException")
            {
                // SessionException carries an error enum that distinguishes
                // not-found (host quit / stale invite) from transient. We
                // can't reference the enum directly here without an SDK pin,
                // so inspect the message text - defensive but version-stable.
                string msg = inner.Message ?? string.Empty;
                if (msg.IndexOf("NotFound", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("NotInLobby", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "SessionGone";
                return "Transient";
            }

            // UGS lobby-layer failure with HTTP status code.
            if (typeName == "Unity.Services.Lobbies.LobbyServiceException")
            {
                // Try to extract the Reason / HTTP-status field by reflection
                // without an SDK pin. If absent, fall back to message inspection.
                var reasonProp = inner.GetType().GetProperty("Reason");
                if (reasonProp != null)
                {
                    string reasonStr = reasonProp.GetValue(inner)?.ToString() ?? string.Empty;
                    if (reasonStr.IndexOf("RateLimited", StringComparison.OrdinalIgnoreCase) >= 0) return "RateLimit";
                    if (reasonStr.IndexOf("LobbyNotFound", StringComparison.OrdinalIgnoreCase) >= 0) return "SessionGone";
                }
                string msg = inner.Message ?? string.Empty;
                if (msg.IndexOf("429", StringComparison.Ordinal) >= 0) return "RateLimit";
                if (msg.IndexOf("404", StringComparison.Ordinal) >= 0) return "SessionGone";
                return "Transient";
            }

            // UGS request layer - wraps HTTP status code on ErrorCode.
            if (inner is Unity.Services.Core.RequestFailedException rfe)
            {
                if (rfe.ErrorCode == 429) return "RateLimit";
                if (rfe.ErrorCode == 404) return "SessionGone";
                if (rfe.ErrorCode >= 500 && rfe.ErrorCode < 600) return "Transient";
                if (rfe.ErrorCode == -1 || rfe.ErrorCode == 0) return "Offline";
                return "Transient";
            }

            // Transport-layer offline signals.
            if (inner is WebException || inner is SocketException || inner is HttpRequestException)
                return "Offline";

            return "Unknown";
        }
    }
}
