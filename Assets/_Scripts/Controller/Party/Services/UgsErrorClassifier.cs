// ─────────────────────────────────────────────────────────────────────────────
// UgsErrorClassifier.cs
// Shared, chain-walking classifiers for UGS exceptions that drive RETRY and
// BACKOFF decisions.
//
// WHY this class exists:
//   Three classes each carried their own private IsRateLimitException, and no
//   two agreed:
//
//     HostConnectionService  e.Message.Contains("Too Many Requests")
//     PresenceLobbyService   e.Message.Contains("Too Many Requests")
//     PartySessionService    e is RequestFailedException rfe && rfe.ErrorCode == 429
//
//   Each caught a case the others missed, and none walked InnerException - even
//   though the two sibling classifiers in the same file as the first one
//   (IsDefiniteSessionGoneException, IsBenignLobbyPatcherError) both do, because
//   UGS and UniTask wrap. A 429 delivered wrapped, or with a message the SDK
//   phrased differently, was simply not seen: no backoff was armed, and in
//   HostConnectionService.RefreshAsync it instead fell through to the generic
//   branch and incremented the counter toward MAX_REFRESH_ERRORS_BEFORE_RECONNECT
//   - so being throttled could escalate into ForceReset and a throwaway presence
//   lobby, which is the opposite of backing off.
//
// SCOPE:
//   Retry control only. NetworkDiagnostics.ClassifyException is deliberately
//   NOT reused here - it is documented as "for LOGS, not RETRY CONTROL", and
//   collapsing the two would make a logging tweak silently change retry
//   behavior. The two are allowed to agree; they are not allowed to be the
//   same code.
//
// FUTURE:
//   This is the seed of the `RefreshErrorPolicy` extraction tracked as D2 in
//   Docs/PartySystem/REFACTOR.md - the backoff state (_rateLimitBackoffUntil,
//   _consecutiveRefreshErrors) moves here once the classifiers are shared.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Chain-walking classifiers for UGS exceptions used to drive retry and
    /// backoff decisions. Shared by <see cref="HostConnectionService"/>,
    /// <see cref="PresenceLobbyService"/> and <see cref="PartySessionService"/>
    /// so all three agree on what "rate limited" means.
    /// </summary>
    public static class UgsErrorClassifier
    {
        /// <summary>
        /// True when the exception - or anything in its
        /// <see cref="Exception.InnerException"/> chain - is a UGS rate-limit
        /// (HTTP 429) response.
        ///
        /// <para>
        /// Matches the union of the three signals the SDK surfaces this through:
        /// a structured <c>SessionException</c> whose <c>Error</c> is
        /// <c>RateLimited</c>, a <c>RequestFailedException</c> with
        /// <c>ErrorCode == 429</c>, and the plain-text "Too Many Requests" that
        /// lobby-layer paths carry. Any single one of these on its own missed
        /// cases the others caught.
        /// </para>
        ///
        /// <para>
        /// The structured match compares <c>Error.ToString()</c> rather than the
        /// enum member directly, matching the convention already used by
        /// <c>HostConnectionService.IsBenignSdkStaleIndexError</c>: it avoids
        /// pinning the exact enum spelling across SDK versions.
        /// </para>
        ///
        /// <para>
        /// A bare "429" substring is deliberately NOT matched - it appears in
        /// session ids and player ids often enough to false-positive.
        /// </para>
        /// </summary>
        public static bool IsRateLimit(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
            {
                if (current is SessionException se &&
                    string.Equals(se.Error.ToString(), "RateLimited", StringComparison.Ordinal))
                    return true;

                if (current is Unity.Services.Core.RequestFailedException rfe && rfe.ErrorCode == 429)
                    return true;

                var msg = current.Message;
                if (!string.IsNullOrEmpty(msg) &&
                    msg.IndexOf("Too Many Requests", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
