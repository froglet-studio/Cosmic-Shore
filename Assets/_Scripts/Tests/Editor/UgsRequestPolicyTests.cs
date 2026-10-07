#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace CosmicShore.Tests
{
    /// <summary>
    /// UgsRequestPolicy Tests - the one UGS failure classifier and the one retry policy.
    ///
    /// WHY THIS MATTERS:
    /// Four divergent IsRateLimitException copies and three retry loops were collapsed into this
    /// class on 2026-10-06 (Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §3.2).
    /// A classifier that is wrong by one shape fails silently in production - a 429 that reads
    /// as Fatal is not retried and bounces the guest; a "host" that reads as Conflict loops with
    /// no delay. Every shape the retired classifiers matched is pinned here, with the negative
    /// controls that document what they wrongly matched before.
    ///
    /// The policy is driven synchronously: the clock, the delay and the random source are all
    /// injected, so every retry path runs to completion inside one NUnit call with no PlayerLoop.
    /// </summary>
    [TestFixture]
    public class UgsRequestPolicyTests
    {
        private double _now;
        private List<int> _delays;
        private UgsRequestPolicySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _now = 10_000;
            _delays = new List<int>();
            _settings = new UgsRequestPolicySettings { jitterFloor = 1f }; // deterministic delays unless a test lowers it
            UgsRequestTelemetry.Reset();
            UgsRequestTelemetry.Now = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            UgsRequestTelemetry.Reset();
            UgsRequestTelemetry.Now = () => UnityEngine.Time.realtimeSinceStartupAsDouble;
        }

        private UgsRequestPolicy NewPolicy(UgsRequestPolicySettings settings = null, int seed = 7) =>
            new UgsRequestPolicy(
                settings ?? _settings,
                clock: () => _now,
                delay: (ms, ct) => { _delays.Add(ms); return UniTask.CompletedTask; },
                random: new Random(seed));

        private static SessionException Session(SessionError error, string message = "session error") =>
            new SessionException(message, error, null);

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        /// <summary>A frame named like the SDK type the Benign rule looks for in the stack trace.</summary>
        private static class LobbyPatcher
        {
            public static ArgumentOutOfRangeException Throw()
            {
                try { ApplyPatchesToLobby(); }
                catch (ArgumentOutOfRangeException e) { return e; }
                throw new InvalidOperationException("unreachable");
            }

            private static void ApplyPatchesToLobby() => throw new ArgumentOutOfRangeException("index");
        }

        #region Classify - structured

        [Test]
        public void Classify_StructuredRateLimit_IsRateLimited() =>
            Assert.AreEqual(UgsFailureClass.RateLimited, UgsRequestPolicy.Classify(Session(SessionError.RateLimitExceeded)));

        [Test]
        public void Classify_RequestFailed429_IsRateLimited() =>
            Assert.AreEqual(UgsFailureClass.RateLimited, UgsRequestPolicy.Classify(new RequestFailedException(429, "throttled")));

        [Test]
        public void Classify_RateLimitWrappedInAggregate_IsRateLimited()
        {
            var wrapped = new AggregateException(Session(SessionError.RateLimitExceeded));
            Assert.AreEqual(UgsFailureClass.RateLimited, UgsRequestPolicy.Classify(wrapped));
        }

        [TestCase(SessionError.SessionNotFound)]
        [TestCase(SessionError.SessionDeleted)]
        [TestCase(SessionError.NotInLobby)]
        [TestCase(SessionError.AllocationNotFound)]
        public void Classify_GoneErrors_AreGone(SessionError error) =>
            Assert.AreEqual(UgsFailureClass.Gone, UgsRequestPolicy.Classify(Session(error)));

        [Test]
        public void Classify_RequestFailed404_IsGone() =>
            Assert.AreEqual(UgsFailureClass.Gone, UgsRequestPolicy.Classify(new RequestFailedException(404, "nope")));

        [TestCase(SessionError.NetworkManagerNotInitialized)]
        [TestCase(SessionError.NetworkManagerStartFailed)]
        [TestCase(SessionError.NetworkSetupFailed)]
        [TestCase(SessionError.SessionConflict)]
        [TestCase(SessionError.LobbyAlreadyExists)]
        public void Classify_ConflictErrors_AreConflict(SessionError error) =>
            Assert.AreEqual(UgsFailureClass.Conflict, UgsRequestPolicy.Classify(Session(error)));

        [TestCase(SessionError.NotAuthorized)]
        [TestCase(SessionError.Forbidden)]
        [TestCase(SessionError.InvalidParameter)]
        [TestCase(SessionError.InvalidOperation)]
        public void Classify_PermissionAndParameterErrors_AreFatal(SessionError error) =>
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(Session(error)));

        [TestCase(401)]
        [TestCase(403)]
        public void Classify_RequestFailedAuth_IsFatal(int code) =>
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(new RequestFailedException(code, "denied")));

        [TestCase(500)]
        [TestCase(503)]
        [TestCase(0)]
        [TestCase(-1)]
        public void Classify_ServerAndNetworkFailures_AreTransient(int code) =>
            Assert.AreEqual(UgsFailureClass.Transient, UgsRequestPolicy.Classify(new RequestFailedException(code, "boom")));

        [Test]
        public void Classify_SocketException_IsTransient() =>
            Assert.AreEqual(UgsFailureClass.Transient, UgsRequestPolicy.Classify(new SocketException()));

        [Test]
        public void Classify_LobbyPatcherStaleIndex_IsBenign() =>
            Assert.AreEqual(UgsFailureClass.Benign, UgsRequestPolicy.Classify(LobbyPatcher.Throw()));

        [Test]
        public void Classify_ArgumentOutOfRangeElsewhere_IsNotBenign()
        {
            // Negative control for the stack-trace probe: the same exception type thrown from
            // our own code is a bug, not SDK noise.
            ArgumentOutOfRangeException ours;
            try { throw new ArgumentOutOfRangeException("i"); }
            catch (ArgumentOutOfRangeException e) { ours = e; }
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(ours));
        }

        [Test]
        public void Classify_UnknownWithStaleIndexMessage_IsBenign()
        {
            // HostConnectionService's old IsBenignSdkStaleIndexError: SessionError.Unknown wrapping
            // the SDK's moving set of stale-index messages.
            Assert.AreEqual(UgsFailureClass.Benign,
                UgsRequestPolicy.Classify(Session(SessionError.Unknown, "Index was out of range. Must be non-negative")));
            Assert.AreEqual(UgsFailureClass.Benign,
                UgsRequestPolicy.Classify(Session(SessionError.Unknown, "Object reference not set to an instance of an object")));
            Assert.AreEqual(UgsFailureClass.Benign,
                UgsRequestPolicy.Classify(Session(SessionError.Unknown, "something the SDK never typed")));
        }

        [Test]
        public void Classify_UnknownWithFullOrGoneMessage_IsNotBenign()
        {
            Assert.AreEqual(UgsFailureClass.Full, UgsRequestPolicy.Classify(Session(SessionError.Unknown, "lobby is full")));
            Assert.AreEqual(UgsFailureClass.Gone, UgsRequestPolicy.Classify(Session(SessionError.Unknown, "lobby not found")));
            Assert.AreEqual(UgsFailureClass.RateLimited, UgsRequestPolicy.Classify(Session(SessionError.Unknown, "Too Many Requests")));
        }

        [Test]
        public void Classify_SdkNreInsideSessionException_IsTransient()
        {
            // PartySessionService's old IsTransientSessionException: the SDK's own NRE on a
            // lobby-events subscription, delivered wrapped.
            var wrapped = new SessionException("subscribe failed", SessionError.InvalidOperation, new NullReferenceException());
            Assert.AreEqual(UgsFailureClass.Transient, UgsRequestPolicy.Classify(wrapped));
        }

        [Test]
        public void Classify_BareNre_IsFatal() =>
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(new NullReferenceException()));

        [Test]
        public void Classify_Cancellation_IsCancelled()
        {
            Assert.AreEqual(UgsFailureClass.Cancelled, UgsRequestPolicy.Classify(new OperationCanceledException()));
            Assert.AreEqual(UgsFailureClass.Cancelled, UgsRequestPolicy.Classify(new AggregateException(new OperationCanceledException())));
        }

        [Test]
        public void Classify_Null_IsFatal() =>
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(null));

        #endregion

        #region Classify - message floor

        [Test]
        public void Classify_TooManyRequestsMessage_IsRateLimited() =>
            Assert.AreEqual(UgsFailureClass.RateLimited, UgsRequestPolicy.Classify(new Exception("HTTP 429 Too Many Requests")));

        [Test]
        public void Classify_SessionNotFoundMessage_IsGone() =>
            Assert.AreEqual(UgsFailureClass.Gone, UgsRequestPolicy.Classify(new Exception("The session was not found")));

        [Test]
        public void Classify_NotFoundWithoutSessionWord_IsFatal() =>
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(new Exception("texture not found")));

        [TestCase("Error Code[23006] - Failed to subscribe to lobby service for events")]
        [TestCase("Index was out of range")]
        [TestCase("Object reference not set to an instance of an object")]
        [TestCase("provide a valid Lobby ID")]
        public void Classify_RetiredTransientProbes_AreTransient(string message) =>
            Assert.AreEqual(UgsFailureClass.Transient, UgsRequestPolicy.Classify(new Exception(message)));

        [Test]
        public void Classify_NetworkManagerMessage_IsConflict() =>
            Assert.AreEqual(UgsFailureClass.Conflict,
                UgsRequestPolicy.Classify(new InvalidOperationException("NetworkManager is already listening")));

        [Test]
        public void Classify_BareHostWord_IsNotConflict()
        {
            // Negative control for the retired IsHostConflictException, which matched any message
            // containing "host" and retried it twice with no back-off.
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(new Exception("the host left the match")));
        }

        [Test]
        public void Classify_FullMessage_IsFull() =>
            Assert.AreEqual(UgsFailureClass.Full, UgsRequestPolicy.Classify(new Exception("Lobby is full")));

        [Test]
        public void IsRetryable_Table()
        {
            Assert.IsTrue(UgsRequestPolicy.IsRetryable(UgsFailureClass.Benign));
            Assert.IsTrue(UgsRequestPolicy.IsRetryable(UgsFailureClass.RateLimited));
            Assert.IsTrue(UgsRequestPolicy.IsRetryable(UgsFailureClass.Transient));
            Assert.IsTrue(UgsRequestPolicy.IsRetryable(UgsFailureClass.Conflict));
            Assert.IsFalse(UgsRequestPolicy.IsRetryable(UgsFailureClass.Full));
            Assert.IsFalse(UgsRequestPolicy.IsRetryable(UgsFailureClass.Gone));
            Assert.IsFalse(UgsRequestPolicy.IsRetryable(UgsFailureClass.Cancelled));
            Assert.IsFalse(UgsRequestPolicy.IsRetryable(UgsFailureClass.Fatal));
        }

        #endregion

        #region Back-off

        [Test]
        public void ComputeDelay_RateLimited_DoublesUpToCap()
        {
            var policy = NewPolicy();
            Assert.AreEqual(1000, policy.ComputeDelayMs(UgsFailureClass.RateLimited, 0));
            Assert.AreEqual(2000, policy.ComputeDelayMs(UgsFailureClass.RateLimited, 1));
            Assert.AreEqual(4000, policy.ComputeDelayMs(UgsFailureClass.RateLimited, 2));
            Assert.AreEqual(8000, policy.ComputeDelayMs(UgsFailureClass.RateLimited, 3));
            Assert.AreEqual(8000, policy.ComputeDelayMs(UgsFailureClass.RateLimited, 10), "capped");
        }

        [Test]
        public void ComputeDelay_Transient_UsesItsOwnCurve()
        {
            var policy = NewPolicy();
            Assert.AreEqual(500,  policy.ComputeDelayMs(UgsFailureClass.Transient, 0));
            Assert.AreEqual(1000, policy.ComputeDelayMs(UgsFailureClass.Transient, 1));
            Assert.AreEqual(4000, policy.ComputeDelayMs(UgsFailureClass.Transient, 9), "capped");
            Assert.AreEqual(500,  policy.ComputeDelayMs(UgsFailureClass.Benign, 0), "Benign rides the Transient curve");
        }

        [Test]
        public void ComputeDelay_Conflict_IsFlat()
        {
            var policy = NewPolicy();
            Assert.AreEqual(250, policy.ComputeDelayMs(UgsFailureClass.Conflict, 0));
            Assert.AreEqual(250, policy.ComputeDelayMs(UgsFailureClass.Conflict, 5));
        }

        [Test]
        public void ComputeDelay_Jitter_StaysBetweenFloorAndFull()
        {
            var settings = new UgsRequestPolicySettings { jitterFloor = 0.5f };
            var policy = NewPolicy(settings, seed: 1234);
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < 500; i++)
            {
                int d = policy.ComputeDelayMs(UgsFailureClass.RateLimited, 2); // full = 4000
                min = Math.Min(min, d);
                max = Math.Max(max, d);
                Assert.That(d, Is.InRange(2000, 4000));
            }
            Assert.Less(min, 2600, "jitter must actually spread downward");
            Assert.Greater(max, 3400, "jitter must actually reach toward the full delay");
        }

        [Test]
        public void ComputeDelay_ZeroBase_IsZero()
        {
            var policy = NewPolicy(new UgsRequestPolicySettings { rateLimitBaseDelayMs = 0, jitterFloor = 1f });
            Assert.AreEqual(0, policy.ComputeDelayMs(UgsFailureClass.RateLimited, 3));
        }

        #endregion

        #region ExecuteAsync - retry behaviour

        [Test]
        public void Execute_SuccessFirstTry_NoDelayOneRequest()
        {
            var policy = NewPolicy();
            int calls = 0;
            int result = Run(policy.ExecuteAsync("t:ok", () => { calls++; return UniTask.FromResult(42); }));
            Assert.AreEqual(42, result);
            Assert.AreEqual(1, calls);
            Assert.IsEmpty(_delays);
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Requests));
            Assert.AreEqual(0, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Retries));
        }

        [Test]
        public void Execute_RateLimitedTwiceThenOk_RetriesWithRateLimitCurve()
        {
            var policy = NewPolicy();
            int calls = 0;
            int result = Run(policy.ExecuteAsync("t:429", () =>
            {
                calls++;
                if (calls <= 2) throw Session(SessionError.RateLimitExceeded);
                return UniTask.FromResult(7);
            }));
            Assert.AreEqual(7, result);
            Assert.AreEqual(3, calls);
            CollectionAssert.AreEqual(new[] { 1000, 2000 }, _delays);
            Assert.AreEqual(2, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Retries));
            Assert.AreEqual(2, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.RateLimited));
            Assert.AreEqual(3, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Requests));
        }

        [Test]
        public void Execute_ExhaustsRetries_ThrowsTheRealException()
        {
            var policy = NewPolicy();
            int calls = 0;
            var ex = Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("t:exhaust", () => { calls++; throw Session(SessionError.RateLimitExceeded, "still 429"); })));
            Assert.AreEqual(SessionError.RateLimitExceeded, ex.Error);
            Assert.AreEqual(_settings.maxRetries + 1, calls, "first attempt + maxRetries");
            Assert.AreEqual(_settings.maxRetries, _delays.Count);
        }

        [Test]
        public void Execute_TransientFailure_UsesTransientCurve()
        {
            var policy = NewPolicy();
            int calls = 0;
            Run(policy.ExecuteAsync("t:5xx", () =>
            {
                calls++;
                if (calls == 1) throw new RequestFailedException(503, "unavailable");
                return UniTask.FromResult(true);
            }));
            CollectionAssert.AreEqual(new[] { 500 }, _delays);
        }

        [Test]
        public void Execute_BenignFailure_IsRetriedAtAOneShotSite()
        {
            // A stale-index fault that the refresh loop ignores is still worth one more try when
            // the caller is about to spend the result.
            var policy = NewPolicy();
            int calls = 0;
            Run(policy.ExecuteAsync("t:benign", () =>
            {
                calls++;
                if (calls == 1) throw Session(SessionError.Unknown, "Index was out of range");
                return UniTask.FromResult(true);
            }));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Benign));
        }

        [TestCase(SessionError.SessionNotFound)]
        [TestCase(SessionError.Forbidden)]
        public void Execute_GoneAndFatal_AreNotRetried(SessionError error)
        {
            var policy = NewPolicy();
            int calls = 0;
            Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("t:noretry", () => { calls++; throw Session(error); })));
            Assert.AreEqual(1, calls);
            Assert.IsEmpty(_delays);
        }

        [Test]
        public void Execute_FullIsNotRetried()
        {
            var policy = NewPolicy();
            int calls = 0;
            Assert.Throws<InvalidOperationException>(() =>
                Run(policy.ExecuteAsync<int>("t:full", () => { calls++; throw new InvalidOperationException("Lobby is full"); })));
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Full));
        }

        [Test]
        public void Execute_Cancellation_PropagatesWithoutRetry()
        {
            var policy = NewPolicy();
            int calls = 0;
            Assert.Throws<OperationCanceledException>(() =>
                Run(policy.ExecuteAsync<int>("t:cancel", () => { calls++; throw new OperationCanceledException(); })));
            Assert.AreEqual(1, calls);
            Assert.IsEmpty(_delays);
            Assert.AreEqual(0, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Fatal), "a cancel is not a fault");
        }

        [Test]
        public void Execute_AlreadyCancelledToken_DoesNotCall()
        {
            var policy = NewPolicy();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            int calls = 0;
            Assert.Throws<OperationCanceledException>(() =>
                Run(policy.ExecuteAsync("t:pre-cancelled", () => { calls++; return UniTask.FromResult(1); }, cts.Token)));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Execute_Conflict_RetriesExactlyOnce()
        {
            var policy = NewPolicy();
            int calls = 0;
            Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("t:conflict", () => { calls++; throw Session(SessionError.NetworkManagerStartFailed); })));
            Assert.AreEqual(2, calls, "one retry regardless of maxRetries");
            CollectionAssert.AreEqual(new[] { 250 }, _delays);
        }

        [Test]
        public void Execute_ZeroMaxRetries_ThrowsOnFirstFailure()
        {
            var policy = NewPolicy(new UgsRequestPolicySettings { maxRetries = 0, jitterFloor = 1f });
            int calls = 0;
            Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("t:zero", () => { calls++; throw Session(SessionError.RateLimitExceeded); })));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Execute_NullCall_Throws()
        {
            var policy = NewPolicy();
            Assert.Throws<ArgumentNullException>(() => policy.ExecuteAsync<int>("t:null", null));
        }

        [Test]
        public void Execute_ResultlessForm_RetriesToo()
        {
            var policy = NewPolicy();
            int calls = 0;
            policy.ExecuteAsync("t:void", () =>
            {
                calls++;
                if (calls == 1) throw Session(SessionError.RateLimitExceeded);
                return UniTask.CompletedTask;
            }).GetAwaiter().GetResult();
            Assert.AreEqual(2, calls);
            CollectionAssert.AreEqual(new[] { 1000 }, _delays);
        }

        #endregion

        #region ExecuteAsync - budget

        [Test]
        public void Budget_SpentBudget_StopsRetryingUntilTheWindowRolls()
        {
            var settings = new UgsRequestPolicySettings { retryBudgetPerMinute = 2, jitterFloor = 1f };
            var policy = NewPolicy(settings);

            // Two retries spend the budget (third call succeeds).
            int calls = 0;
            Run(policy.ExecuteAsync("t:budget-1", () =>
            {
                calls++;
                if (calls <= 2) throw Session(SessionError.RateLimitExceeded);
                return UniTask.FromResult(1);
            }));
            Assert.AreEqual(2, policy.RetriesInBudgetWindow);

            // Budget spent: the next failure is thrown on its first occurrence.
            int calls2 = 0;
            Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("t:budget-2", () => { calls2++; throw Session(SessionError.RateLimitExceeded); })));
            Assert.AreEqual(1, calls2);
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.BudgetExhausted));

            // Roll the window: retries are allowed again.
            _now += UgsRequestPolicy.BudgetWindowSeconds + 1;
            Assert.AreEqual(0, policy.RetriesInBudgetWindow);
            int calls3 = 0;
            Run(policy.ExecuteAsync("t:budget-3", () =>
            {
                calls3++;
                if (calls3 == 1) throw Session(SessionError.RateLimitExceeded);
                return UniTask.FromResult(1);
            }));
            Assert.AreEqual(2, calls3);
        }

        [Test]
        public void Budget_Zero_DisablesRetries()
        {
            var policy = NewPolicy(new UgsRequestPolicySettings { retryBudgetPerMinute = 0, jitterFloor = 1f });
            int calls = 0;
            Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("t:nobudget", () => { calls++; throw Session(SessionError.RateLimitExceeded); })));
            Assert.AreEqual(1, calls);
        }

        #endregion

        #region ExecuteAsync - single-flight

        [Test]
        public void SingleFlight_SameKeyWhilePending_CoalescesIntoOneCall()
        {
            var policy = NewPolicy();
            var gate = new UniTaskCompletionSource<int>();
            int calls = 0;

            var first  = policy.ExecuteAsync("party:create", () => { calls++; return gate.Task; });
            var second = policy.ExecuteAsync("party:create", () => { calls++; return gate.Task; });

            Assert.AreEqual(1, calls, "the second caller must not issue a second request");
            Assert.AreEqual(1, policy.InFlightCount);
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Coalesced));

            gate.TrySetResult(99);
            Assert.AreEqual(99, Run(first));
            Assert.AreEqual(99, Run(second));
            Assert.AreEqual(0, policy.InFlightCount, "the key must drain once the call completes");
        }

        private static async UniTask<int> Consume(UniTask<int> task) => await task;

        [Test]
        public void SingleFlight_TwoConsumersAwaitingWhilePending_BothComplete()
        {
            // The case a Preserve()d UniTask cannot serve: both callers AWAIT the shared operation
            // before it completes. A memoized UniTask forwards the second awaiter to the
            // one-continuation core and throws; the completion source behind each key must not.
            var policy = NewPolicy();
            var gate = new UniTaskCompletionSource<int>();
            int calls = 0;
            var a = Consume(policy.ExecuteAsync("party:create", () => { calls++; return gate.Task; }));
            var b = Consume(policy.ExecuteAsync("party:create", () => { calls++; return gate.Task; }));
            Assert.AreEqual(1, calls);
            Assert.AreEqual(UniTaskStatus.Pending, a.Status);
            Assert.AreEqual(UniTaskStatus.Pending, b.Status);
            gate.TrySetResult(5);
            Assert.AreEqual(5, Run(a));
            Assert.AreEqual(5, Run(b));
            Assert.AreEqual(0, policy.InFlightCount);
        }

        [Test]
        public void SingleFlight_AwaiterSeesTheRealException()
        {
            var policy = NewPolicy();
            var gate = new UniTaskCompletionSource<int>();
            var a = Consume(policy.ExecuteAsync("party:join:Z", () => gate.Task));
            gate.TrySetException(new SessionException("gone", SessionError.SessionNotFound, null));
            var ex = Assert.Throws<SessionException>(() => Run(a));
            Assert.AreEqual(SessionError.SessionNotFound, ex.Error);
            Assert.AreEqual(0, policy.InFlightCount);
        }

        [Test]
        public void SingleFlight_DifferentKeys_DoNotCoalesce()
        {
            var policy = NewPolicy();
            var gate = new UniTaskCompletionSource<int>();
            int calls = 0;
            var a = policy.ExecuteAsync("party:join:A", () => { calls++; return gate.Task; });
            var b = policy.ExecuteAsync("party:join:B", () => { calls++; return gate.Task; });
            Assert.AreEqual(2, calls);
            Assert.AreEqual(2, policy.InFlightCount);
            gate.TrySetResult(1);
            Run(a); Run(b);
            Assert.AreEqual(0, policy.InFlightCount);
        }

        [Test]
        public void SingleFlight_NullKey_NeverCoalesces()
        {
            var policy = NewPolicy();
            var gate = new UniTaskCompletionSource<int>();
            int calls = 0;
            var a = policy.ExecuteAsync(null, () => { calls++; return gate.Task; });
            var b = policy.ExecuteAsync(null, () => { calls++; return gate.Task; });
            Assert.AreEqual(2, calls);
            Assert.AreEqual(0, policy.InFlightCount);
            gate.TrySetResult(1);
            Run(a); Run(b);
        }

        [Test]
        public void SingleFlight_SynchronousCompletion_LeavesNoKeyBehind()
        {
            // Regression guard for the stale-key hazard: a call that completes before ExecuteAsync
            // returns has already run its finally, so it must not be registered afterwards.
            var policy = NewPolicy();
            Run(policy.ExecuteAsync("party:create", () => UniTask.FromResult(1)));
            Assert.AreEqual(0, policy.InFlightCount);
            int calls = 0;
            Run(policy.ExecuteAsync("party:create", () => { calls++; return UniTask.FromResult(2); }));
            Assert.AreEqual(1, calls, "a stale key would have returned the first (completed) task and skipped this call");
        }

        [Test]
        public void SingleFlight_FailedCall_DrainsTheKey()
        {
            var policy = NewPolicy();
            Assert.Throws<SessionException>(() =>
                Run(policy.ExecuteAsync<int>("party:join:X", () => throw Session(SessionError.SessionNotFound))));
            Assert.AreEqual(0, policy.InFlightCount);
        }

        #endregion

        #region Defaults

        [Test]
        public void NullSettings_TakeDefaults()
        {
            var policy = new UgsRequestPolicy(null, clock: () => _now, delay: (ms, ct) => UniTask.CompletedTask, random: new Random(1));
            Assert.AreEqual(3, policy.Settings.maxRetries);
            Assert.AreEqual(1000, policy.Settings.rateLimitBaseDelayMs);
            Assert.AreEqual(8000, policy.Settings.rateLimitMaxDelayMs);
            Assert.AreEqual(500, policy.Settings.transientBaseDelayMs);
            Assert.AreEqual(4000, policy.Settings.transientMaxDelayMs);
            Assert.AreEqual(250, policy.Settings.conflictDelayMs);
            Assert.AreEqual(0.5f, policy.Settings.jitterFloor);
            Assert.AreEqual(10, policy.Settings.retryBudgetPerMinute);
        }

        #endregion
    }

    /// <summary>
    /// UgsRequestTelemetry Tests - the rolling counters the Phase 1 acceptance numbers are read from.
    /// </summary>
    [TestFixture]
    public class UgsRequestTelemetryTests
    {
        private double _now;

        [SetUp]
        public void SetUp()
        {
            _now = 500;
            UgsRequestTelemetry.Reset();
            UgsRequestTelemetry.Now = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            UgsRequestTelemetry.Reset();
            UgsRequestTelemetry.Now = () => UnityEngine.Time.realtimeSinceStartupAsDouble;
        }

        [Test]
        public void Count_IsVisibleInWindowAndTotal()
        {
            UgsRequestTelemetry.Count(UgsRequestCounter.RateLimited);
            UgsRequestTelemetry.Count(UgsRequestCounter.RateLimited);
            Assert.AreEqual(2, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.RateLimited));
            Assert.AreEqual(2, UgsRequestTelemetry.Total(UgsRequestCounter.RateLimited));
            Assert.AreEqual(0, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Retries));
        }

        [Test]
        public void Window_PrunesAfterSixtySeconds_TotalDoesNot()
        {
            UgsRequestTelemetry.Count(UgsRequestCounter.Requests);
            _now += 30;
            UgsRequestTelemetry.Count(UgsRequestCounter.Requests);
            Assert.AreEqual(2, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Requests));
            _now += 31; // first sample is now 61 s old
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Requests));
            _now += 60;
            Assert.AreEqual(0, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Requests));
            Assert.AreEqual(2, UgsRequestTelemetry.Total(UgsRequestCounter.Requests));
        }

        [Test]
        public void CountFailure_MapsEveryFaultClass_NotCancelled()
        {
            UgsRequestTelemetry.CountFailure(UgsFailureClass.RateLimited);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Transient);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Benign);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Conflict);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Full);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Gone);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Fatal);
            UgsRequestTelemetry.CountFailure(UgsFailureClass.Cancelled);
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.RateLimited));
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Transient));
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Benign));
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Conflict));
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Full));
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Gone));
            Assert.AreEqual(1, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Fatal));
            Assert.AreEqual(7, UgsRequestTelemetry.Total(UgsRequestCounter.RateLimited)
                             + UgsRequestTelemetry.Total(UgsRequestCounter.Transient)
                             + UgsRequestTelemetry.Total(UgsRequestCounter.Benign)
                             + UgsRequestTelemetry.Total(UgsRequestCounter.Conflict)
                             + UgsRequestTelemetry.Total(UgsRequestCounter.Full)
                             + UgsRequestTelemetry.Total(UgsRequestCounter.Gone)
                             + UgsRequestTelemetry.Total(UgsRequestCounter.Fatal), "Cancelled is deliberately uncounted");
        }

        [Test]
        public void Describe_CarriesEveryHeadlineFigure()
        {
            UgsRequestTelemetry.Count(UgsRequestCounter.Requests);
            UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);
            UgsRequestTelemetry.Count(UgsRequestCounter.RateLimited);
            UgsRequestTelemetry.Count(UgsRequestCounter.PresenceForceReset);
            UgsRequestTelemetry.Count(UgsRequestCounter.OfflineFallback);
            string s = UgsRequestTelemetry.Describe();
            StringAssert.Contains("req/min=1", s);
            StringAssert.Contains("reads/min=1", s);
            StringAssert.Contains("429/min=1", s);
            StringAssert.Contains("reset=1", s);
            StringAssert.Contains("offline=1", s);
        }

        [Test]
        public void Reset_ClearsWindowsAndTotals()
        {
            UgsRequestTelemetry.Count(UgsRequestCounter.Gone);
            UgsRequestTelemetry.Reset();
            Assert.AreEqual(0, UgsRequestTelemetry.InLastMinute(UgsRequestCounter.Gone));
            Assert.AreEqual(0, UgsRequestTelemetry.Total(UgsRequestCounter.Gone));
        }
    }
}
#endif
