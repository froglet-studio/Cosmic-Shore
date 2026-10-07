// ─────────────────────────────────────────────────────────────────────────────
// UgsRequestPolicy.cs
// The ONE place a UGS (Lobby / Sessions / Relay) failure is classified, and the one
// place a retry of a UGS call is decided.
//
// WHY this class exists (2026-10-06, Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §3.2):
//   Before it, four files each carried their own IsRateLimitException - one structured
//   (PartySessionService), three matching the string "Too Many Requests" (PresenceLobbyService,
//   HostConnectionService, MultiplayerSetup) - and three retry loops disagreed on everything a
//   retry can disagree on: fixed 2000 ms (LobbyPropertyWriter.SaveWithRetryAsync) vs
//   exponential (PartySessionService) vs none at all (the "host conflict" retry, whose classifier
//   matched ANY message containing the word "host"). None jittered, so four clients that failed
//   together retried together. None had a budget. And one failure was retried at TWO layers:
//   SaveWithRetryAsync retried a 429 three times and HostConnectionService.RefreshAsync then
//   counted the same failure toward ForceReset.
//
//   The industry shape (AWS "Exponential Backoff and Jitter"; Google SRE "Handling Overload"):
//   classify -> exponential back-off with jitter -> single-flight per operation -> a retry
//   budget -> retry at ONE layer. That is all this file does.
//
// WHAT IT IS NOT:
//   NetworkDiagnostics.ClassifyException decides what to LOG and keeps its own label set on
//   purpose (Docs/NetworkDiagnostics/ARCHITECTURE.md, "Not a retry-control predicate"). This
//   class decides what to DO. A catch that only needs the decision calls the static Classify;
//   a call site that is about to spend the result wraps it in ExecuteAsync.
//
// THREAD SAFETY:
//   Main-thread only (UniTask PlayerLoop continuations; the caller's own .AsMainThread() stays
//   inside the delegate it hands us). The clock, the delay and the random source are injectable
//   so the edit-mode tests run every path synchronously with no PlayerLoop.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using Random = System.Random;

namespace CosmicShore.Utility
{
    /// <summary>
    /// What a failed UGS call means for the caller. Every catch in the party / presence / match
    /// layers maps a thrown exception to exactly one of these through
    /// <see cref="UgsRequestPolicy.Classify"/>; the retry policy, the refresh loop's
    /// error counter and the UI's error message all key off the same value.
    /// </summary>
    public enum UgsFailureClass
    {
        /// <summary>
        /// SDK noise that self-heals on the next read: the LobbyPatcher stale-index
        /// <c>ArgumentOutOfRangeException</c> and the <c>SessionError.Unknown</c> family it also
        /// surfaces through (Docs/PresenceSystem/BUGS.md B1 / B6). Never counts toward a reset;
        /// a one-shot call retries it like <see cref="Transient"/>.
        /// </summary>
        Benign      = 0,
        /// <summary>HTTP 429 - the service throttled us. Back off; never a reason to lose state.</summary>
        RateLimited = 1,
        /// <summary>A failure that a retry can fix: 5xx, a network drop, the SDK's own NRE on a lobby-events subscription.</summary>
        Transient   = 2,
        /// <summary>
        /// The call collided with state this client owns - the NetworkManager still shutting down
        /// from a prior host, a lobby that already exists. One retry after a short pause.
        /// </summary>
        Conflict    = 3,
        /// <summary>The session has no seat for us. Not retried; the UI says so.</summary>
        Full        = 4,
        /// <summary>The session or lobby is definitely gone server-side (404 / deleted / not a member). Not retried; recover.</summary>
        Gone        = 5,
        /// <summary>The caller's own token cancelled the call. Never retried, never counted as a fault.</summary>
        Cancelled   = 6,
        /// <summary>Everything else - an auth or permission failure, a bad parameter, our own bug. Not retried.</summary>
        Fatal       = 7,
    }

    /// <summary>
    /// The tunables of <see cref="UgsRequestPolicy"/>. Config, not code: lives on
    /// <see cref="HostConnectionDataSO"/> so a designer can retune a back-off without a build,
    /// per CLAUDE.md ("anti-spam / cooldown patterns belong in the SO config").
    /// </summary>
    [Serializable]
    public sealed class UgsRequestPolicySettings
    {
        [Tooltip("Retries after the first attempt for RateLimited / Transient / Benign failures. A Conflict retries once regardless of this value.")]
        [Min(0)] public int maxRetries = 3;

        [Tooltip("First back-off (ms) after a 429. Doubles per attempt up to rateLimitMaxDelayMs, then jittered down to jitterFloor of itself.")]
        [Min(0)] public int rateLimitBaseDelayMs = 1000;

        [Tooltip("Ceiling (ms) on the 429 back-off.")]
        [Min(0)] public int rateLimitMaxDelayMs = 8000;

        [Tooltip("First back-off (ms) after a Transient or Benign failure. Doubles per attempt up to transientMaxDelayMs.")]
        [Min(0)] public int transientBaseDelayMs = 500;

        [Tooltip("Ceiling (ms) on the Transient / Benign back-off.")]
        [Min(0)] public int transientMaxDelayMs = 4000;

        [Tooltip("Pause (ms) before the single Conflict retry - the NetworkManager / host binding usually releases within a frame or two.")]
        [Min(0)] public int conflictDelayMs = 250;

        [Tooltip("Lowest fraction of the computed delay a jittered retry may use: 0.5 = anywhere between half and the full delay. 1 disables jitter. Jitter is what stops four clients that failed together from retrying together.")]
        [Range(0.1f, 1f)] public float jitterFloor = 0.5f;

        [Tooltip("Retries this client may spend per rolling minute across ALL UGS calls. When the budget is spent a failure is thrown on its first occurrence instead of retried - the layer degrades, it never hammers. 0 disables retries entirely.")]
        [Min(0)] public int retryBudgetPerMinute = 10;
    }

    /// <summary>
    /// Classifies UGS failures (<see cref="Classify"/>) and runs UGS calls under one retry
    /// policy (<see cref="ExecuteAsync{T}"/>): exponential back-off with jitter per
    /// <see cref="UgsFailureClass"/>, single-flight coalescing by operation key, and a per-minute
    /// retry budget. Register one instance per client (Reflex singleton); the static
    /// <see cref="Classify"/> needs no instance.
    /// </summary>
    public sealed class UgsRequestPolicy
    {
        /// <summary>Length of the rolling retry-budget window.</summary>
        public const int BudgetWindowSeconds = 60;

        private readonly UgsRequestPolicySettings _settings;
        private readonly Func<double> _now;
        private readonly Func<int, CancellationToken, UniTask> _delay;
        private readonly Random _random;
        private readonly Queue<double> _retryTimes = new();
        private readonly Dictionary<string, object> _inFlight = new();

        /// <param name="settings">Tunables; <c>null</c> takes the defaults.</param>
        /// <param name="clock">Seconds source for the retry budget. Defaults to <see cref="Time.realtimeSinceStartupAsDouble"/>.</param>
        /// <param name="delay">The back-off wait. Defaults to an unscaled <see cref="UniTask.Delay(int, bool, PlayerLoopTiming, CancellationToken, bool)"/>; tests substitute a recorder.</param>
        /// <param name="random">Jitter source. Defaults to an unseeded <see cref="Random"/>; tests pass a seeded one.</param>
        public UgsRequestPolicy(
            UgsRequestPolicySettings settings,
            Func<double> clock = null,
            Func<int, CancellationToken, UniTask> delay = null,
            Random random = null)
        {
            _settings = settings ?? new UgsRequestPolicySettings();
            _now      = clock ?? (() => Time.realtimeSinceStartupAsDouble);
            _delay    = delay ?? ((ms, ct) => UniTask.Delay(ms, ignoreTimeScale: true, cancellationToken: ct));
            _random   = random ?? new Random();
        }

        /// <summary>A policy on the default settings - the fallback when DI did not supply one.</summary>
        public static UgsRequestPolicy CreateDefault() => new UgsRequestPolicy(new UgsRequestPolicySettings());

        public UgsRequestPolicySettings Settings => _settings;

        /// <summary>Retries spent inside the current <see cref="BudgetWindowSeconds"/> window.</summary>
        public int RetriesInBudgetWindow
        {
            get { PruneBudgetWindow(); return _retryTimes.Count; }
        }

        /// <summary>Operations currently coalescing under a key (diagnostic; tests assert it drains).</summary>
        public int InFlightCount => _inFlight.Count;

        // ─────────────────────────────────────────────────────────────────────
        // Classification
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Whether the policy may retry a failure of this class.</summary>
        public static bool IsRetryable(UgsFailureClass cls) =>
            cls is UgsFailureClass.Benign
                or UgsFailureClass.RateLimited
                or UgsFailureClass.Transient
                or UgsFailureClass.Conflict;

        /// <summary>
        /// Maps a thrown exception to a <see cref="UgsFailureClass"/>. Structured first - the
        /// SDK's own <see cref="SessionError"/>, then <see cref="RequestFailedException.ErrorCode"/>,
        /// then the exception's type - walking the <see cref="Exception.InnerException"/> chain
        /// because UGS, UniTask and <see cref="AggregateException"/> all wrap. Only when nothing
        /// structured matches does it fall back to the message text, which is the floor for SDK
        /// paths that lose the typed error (<c>LobbyConverter.ToSessionException</c> builds its
        /// exception with a null inner, so the original 429 lineage survives only as words).
        /// </summary>
        public static UgsFailureClass Classify(Exception e)
        {
            if (e == null) return UgsFailureClass.Fatal;

            int depth = 0;
            for (var current = e; current != null; current = current.InnerException, depth++)
            {
                switch (current)
                {
                    case OperationCanceledException:
                        return UgsFailureClass.Cancelled;

                    case SessionException se:
                    {
                        // The SDK's own NRE (its lobby-events subscription) arrives wrapped in whatever
                        // error the converter chose, and a retry fixes it whatever the label says
                        // (PartySessionService's old IsTransientSessionException) - unless the label is
                        // the stale-index catch-all, which the refresh loop must keep ignoring as Benign.
                        if (se.Error != SessionError.Unknown && se.InnerException is NullReferenceException)
                            return UgsFailureClass.Transient;
                        var byError = ClassifySessionError(se.Error, se.Message);
                        if (byError.HasValue) return byError.Value;
                        break; // an error member this table does not know - keep walking, then read the words
                    }

                    case RequestFailedException rfe:
                        if (rfe.ErrorCode == 429) return UgsFailureClass.RateLimited;
                        if (rfe.ErrorCode == 404) return UgsFailureClass.Gone;
                        if (rfe.ErrorCode == 401 || rfe.ErrorCode == 403) return UgsFailureClass.Fatal;
                        if (rfe.ErrorCode >= 500 && rfe.ErrorCode < 600) return UgsFailureClass.Transient;
                        if (rfe.ErrorCode <= 0) return UgsFailureClass.Transient; // no HTTP status: the request never reached the service
                        break;

                    // The UGS Lobby SDK's LobbyPatcher.ApplyPatchesToLobby throws this when a
                    // WebSocket delta names a stale player index. Harmless; the next read heals it.
                    case ArgumentOutOfRangeException aoore when HasLobbyPatcherFrame(aoore):
                        return UgsFailureClass.Benign;

                    // The SDK's own NRE on a lobby-events subscription, always delivered WRAPPED
                    // (PartySessionService's old IsTransientSessionException). A bare NRE at the
                    // top of the chain is our bug, and a retry would only hide it - and its message
                    // ("Object reference not set...") is one the floor below would read as the
                    // SDK's, so it is decided here, structurally.
                    case NullReferenceException:
                        return depth > 0 ? UgsFailureClass.Transient : UgsFailureClass.Fatal;

                    case WebException:
                    case SocketException:
                    case HttpRequestException:
                        return UgsFailureClass.Transient;
                }

                if (TryClassifyLobbyServiceException(current, out var lobbyClass))
                    return lobbyClass;
            }

            return ClassifyByMessage(FlattenMessages(e));
        }

        /// <summary>
        /// True when <paramref name="e"/>, or anything it wraps, is the UGS Lobby SDK's
        /// <c>LobbyPatcher.ApplyPatchesToLobby</c> stale-index <see cref="ArgumentOutOfRangeException"/>
        /// (Docs/PresenceSystem/BUGS.md B1). The one rule for that shape: <see cref="Classify"/> reads
        /// it as <see cref="UgsFailureClass.Benign"/>, and <c>BenignLobbyLogFilter</c> uses it to drop
        /// the copy the SDK logs on its own event task. Our code never calls LobbyPatcher, so the
        /// false-positive risk is nil.
        /// </summary>
        public static bool IsLobbyPatcherStaleIndex(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
                if (current is ArgumentOutOfRangeException aoore && HasLobbyPatcherFrame(aoore))
                    return true;
            return false;
        }

        private static bool HasLobbyPatcherFrame(Exception e) =>
            e.StackTrace != null && e.StackTrace.Contains("LobbyPatcher");

        private static UgsFailureClass? ClassifySessionError(SessionError error, string message)
        {
            switch (error)
            {
                case SessionError.RateLimitExceeded:
                    return UgsFailureClass.RateLimited;

                case SessionError.SessionNotFound:
                case SessionError.SessionDeleted:
                case SessionError.NotInLobby:
                case SessionError.AllocationNotFound:
                    return UgsFailureClass.Gone;

                case SessionError.NetworkManagerNotInitialized:
                case SessionError.NetworkManagerStartFailed:
                case SessionError.NetworkSetupFailed:
                case SessionError.SessionConflict:
                case SessionError.LobbyAlreadyExists:
                case SessionError.AllocationAlreadyExists:
                case SessionError.AlreadySubscribedToLobby:
                    return UgsFailureClass.Conflict;

                case SessionError.NotAuthorized:
                case SessionError.Forbidden:
                case SessionError.InvalidParameter:
                case SessionError.InvalidOperation:
                case SessionError.InvalidNetworkConfig:
                case SessionError.InvalidSessionMetadata:
                case SessionError.InvalidCreateSessionOptions:
                case SessionError.InvalidSessionIdentifier:
                case SessionError.MissingAssembly:
                case SessionError.TransportComponentMissing:
                case SessionError.TransportInvalid:
                    return UgsFailureClass.Fatal;

                case SessionError.Unknown:
                {
                    // The SDK's catch-all. Every observed stale-index variant lands here
                    // (HostConnectionService's old IsBenignSdkStaleIndexError: "Object reference
                    // not set", "Index was out of range", "Index must be within the bounds") - but
                    // so would a full lobby or a vanished one the converter could not type, so
                    // read the words before calling it noise.
                    var byMessage = ClassifyByMessage(message);
                    return byMessage is UgsFailureClass.Full or UgsFailureClass.Gone or UgsFailureClass.RateLimited
                        ? byMessage
                        : UgsFailureClass.Benign;
                }

                default:
                    return null;
            }
        }

        /// <summary>
        /// <c>Unity.Services.Lobbies.LobbyServiceException</c> is matched by name and read by
        /// reflection: the Sessions SDK normally converts it, so this assembly need not depend on
        /// the Lobbies types to recognise the rare unconverted one.
        /// </summary>
        private static bool TryClassifyLobbyServiceException(Exception e, out UgsFailureClass cls)
        {
            cls = UgsFailureClass.Fatal;
            if (e.GetType().FullName != "Unity.Services.Lobbies.LobbyServiceException") return false;
            var reasonProp = e.GetType().GetProperty("Reason");
            string reason = reasonProp?.GetValue(e)?.ToString() ?? string.Empty;
            if (Has(reason, "RateLimited"))  { cls = UgsFailureClass.RateLimited; return true; }
            if (Has(reason, "LobbyNotFound")) { cls = UgsFailureClass.Gone;        return true; }
            if (Has(reason, "LobbyFull"))     { cls = UgsFailureClass.Full;        return true; }
            if (Has(reason, "Forbidden") || Has(reason, "Unauthorized") || Has(reason, "ValidationError"))
            {
                cls = UgsFailureClass.Fatal;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The message floor. Every probe here corresponds to a shape one of the four retired
        /// classifiers matched by text; none of them is reached while a structured match exists.
        /// Note what is deliberately absent: the bare word "host", which the old host-conflict
        /// probe matched and which appears in "the host left", "Session host changed" and every
        /// NetworkManager log line.
        /// </summary>
        private static UgsFailureClass ClassifyByMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return UgsFailureClass.Fatal;

            if (Has(msg, "Too Many Requests") || Has(msg, "rate limit") || Has(msg, "RateLimit"))
                return UgsFailureClass.RateLimited;

            if (Has(msg, "is full") || Has(msg, "lobby full") || Has(msg, "LobbyFull") || Has(msg, "no available slots"))
                return UgsFailureClass.Full;

            bool namesASession = Has(msg, "session") || Has(msg, "lobby");
            if (namesASession &&
                (Has(msg, "not found") || Has(msg, "deleted") || Has(msg, "does not exist") ||
                 Has(msg, "NotInLobby") || Has(msg, "LobbyNotFound")))
                return UgsFailureClass.Gone;

            if (Has(msg, "Object reference") ||
                Has(msg, "Index was out of range") ||
                Has(msg, "Index must be within the bounds") ||
                Has(msg, "lobby service for events") ||
                Has(msg, "Error Code[23006]") ||
                Has(msg, "valid Lobby ID"))
                return UgsFailureClass.Transient;

            if (Has(msg, "NetworkManager"))
                return UgsFailureClass.Conflict;

            return UgsFailureClass.Fatal;
        }

        private static string FlattenMessages(Exception e)
        {
            var sb = new StringBuilder();
            for (var current = e; current != null; current = current.InnerException)
            {
                if (string.IsNullOrEmpty(current.Message)) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(current.Message);
            }
            return sb.ToString();
        }

        private static bool Has(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        // ─────────────────────────────────────────────────────────────────────
        // Back-off
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The wait before retry number <paramref name="attempt"/> (0-based) of a
        /// <paramref name="cls"/> failure: <c>min(cap, base · 2^attempt)</c>, then jittered down
        /// to <see cref="UgsRequestPolicySettings.jitterFloor"/> of that value. A Conflict waits
        /// the flat <see cref="UgsRequestPolicySettings.conflictDelayMs"/>.
        /// </summary>
        public int ComputeDelayMs(UgsFailureClass cls, int attempt)
        {
            int baseMs, capMs;
            switch (cls)
            {
                case UgsFailureClass.RateLimited:
                    baseMs = _settings.rateLimitBaseDelayMs;
                    capMs  = _settings.rateLimitMaxDelayMs;
                    break;
                case UgsFailureClass.Conflict:
                    return Math.Max(0, _settings.conflictDelayMs);
                default: // Transient, Benign
                    baseMs = _settings.transientBaseDelayMs;
                    capMs  = _settings.transientMaxDelayMs;
                    break;
            }
            if (baseMs <= 0) return 0;

            long full = Math.Min((long)Math.Max(0, capMs), (long)baseMs << Math.Clamp(attempt, 0, 20));
            double floor = Math.Clamp((double)_settings.jitterFloor, 0.1, 1.0);
            double fraction = floor + (1.0 - floor) * _random.NextDouble();
            return (int)Math.Round(full * fraction);
        }

        private bool TryConsumeRetryBudget()
        {
            if (_settings.retryBudgetPerMinute <= 0) return false;
            PruneBudgetWindow();
            if (_retryTimes.Count >= _settings.retryBudgetPerMinute) return false;
            _retryTimes.Enqueue(_now());
            return true;
        }

        private void PruneBudgetWindow()
        {
            double cutoff = _now() - BudgetWindowSeconds;
            while (_retryTimes.Count > 0 && _retryTimes.Peek() < cutoff)
                _retryTimes.Dequeue();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Execution
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs <paramref name="call"/> under the policy and returns its result.
        /// <para>
        /// <paramref name="operationKey"/> names the operation for the log line and, when
        /// non-empty, makes it <b>single-flight</b>: a second caller with the same key while
        /// the first is still running gets the first call's task instead of a second request.
        /// Use a key only where coalescing is the right answer (<c>party:create</c>,
        /// <c>party:join:{sessionId}</c>); pass <c>null</c> where two calls carry different
        /// payloads (a property save). One result type per key.
        /// </para>
        /// <para>
        /// Retries only what <see cref="IsRetryable"/> allows, up to
        /// <see cref="UgsRequestPolicySettings.maxRetries"/> (one for a Conflict), each retry
        /// spending one unit of the per-minute budget. A failure that is not retried is rethrown
        /// as-is so the caller's own catch sees the real exception. Cancellation propagates
        /// untouched.
        /// </para>
        /// </summary>
        public UniTask<T> ExecuteAsync<T>(string operationKey, Func<UniTask<T>> call, CancellationToken ct = default)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));

            bool coalesce = !string.IsNullOrEmpty(operationKey);
            if (coalesce && _inFlight.TryGetValue(operationKey, out var existing) && existing is UniTask<T> inFlight)
            {
                UgsRequestTelemetry.Count(UgsRequestCounter.Coalesced);
                return inFlight;
            }

            var task = RunAsync(operationKey, call, ct).Preserve();
            // A call that completed synchronously has already run its finally; registering it
            // now would leave a key that nothing ever removes.
            if (coalesce && task.Status == UniTaskStatus.Pending)
                _inFlight[operationKey] = task;
            return task;
        }

        /// <summary>Result-less form of <see cref="ExecuteAsync{T}"/>.</summary>
        public async UniTask ExecuteAsync(string operationKey, Func<UniTask> call, CancellationToken ct = default)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            await ExecuteAsync<bool>(operationKey, async () => { await call(); return true; }, ct);
        }

        private async UniTask<T> RunAsync<T>(string key, Func<UniTask<T>> call, CancellationToken ct)
        {
            string label = string.IsNullOrEmpty(key) ? "ugs" : key;
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    UgsRequestTelemetry.Count(UgsRequestCounter.Requests);
                    try
                    {
                        return await call();
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        var cls = Classify(e);
                        UgsRequestTelemetry.CountFailure(cls);

                        int retriesForClass = cls == UgsFailureClass.Conflict ? 1 : Math.Max(0, _settings.maxRetries);
                        if (!IsRetryable(cls) || attempt >= retriesForClass)
                            throw;

                        if (!TryConsumeRetryBudget())
                        {
                            UgsRequestTelemetry.Count(UgsRequestCounter.BudgetExhausted);
                            CSDebug.LogWarning(
                                $"[UgsRequestPolicy] {label}: retry budget spent ({_settings.retryBudgetPerMinute}/min) - " +
                                $"not retrying a {cls} failure ({e.GetType().Name}: {e.Message}).");
                            throw;
                        }

                        int delayMs = ComputeDelayMs(cls, attempt);
                        UgsRequestTelemetry.Count(UgsRequestCounter.Retries);
                        if (CSDebug.IsVerbose(CSLogChannel.Party))
                            CSDebug.LogVerbose(CSLogChannel.Party,
                                $"[UgsRequestPolicy] {label}: attempt {attempt + 1} failed ({cls}: {e.GetType().Name}: {e.Message}) - " +
                                $"retry {attempt + 1}/{retriesForClass} in {delayMs} ms");
                        await _delay(delayMs, ct);
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(key))
                    _inFlight.Remove(key);
            }
        }
    }
}
