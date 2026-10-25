#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Multiplayer;

namespace CosmicShore.Tests
{
    /// <summary>
    /// LobbyPropertyWriter: the lobby read before a write is retried like the save.
    ///
    /// WHY: Shombith's and Gradi's 25 Oct multiplayer logs (one each, just after their party started) show
    /// "[LobbyPropertyWriter] RepublishLocalIdentity error (SessionException): RateLimitExceeded". The save ran under
    /// UgsRequestPolicy, but the read before it did not, so one 429 on that read dropped the whole write. The
    /// per-tick reconciler re-sends the identity later, but every other one-shot write (joined party, invite clear)
    /// has no such fallback.
    /// </summary>
    [TestFixture]
    public class LobbyPropertyWriterRetryTests
    {
        /// <summary>An ISession nobody calls: the writer reaches the lobby only through its substituted delegates.</summary>
        public class NullSession : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                throw new InvalidOperationException($"the test session was called: {targetMethod?.Name}");
        }

        List<int> _delays;

        [SetUp]
        public void SetUp()
        {
            _delays = new List<int>();
            UgsRequestTelemetry.Reset();
        }

        [TearDown]
        public void TearDown() => UgsRequestTelemetry.Reset();

        LobbyPropertyWriter NewWriter() =>
            new LobbyPropertyWriter(new UgsRequestPolicy(
                new UgsRequestPolicySettings { jitterFloor = 1f },
                clock: () => 10_000,
                delay: (ms, ct) => { _delays.Add(ms); return UniTask.CompletedTask; },
                random: new Random(7)));

        static ISession Session() => DispatchProxy.Create<ISession, NullSession>();

        static SessionException Error(SessionError error) => new SessionException("test", error, null);

        [Test]
        public void RateLimitedReadBeforeWrite_IsRetried_AndTheWriteLands()
        {
            var writer = NewWriter();
            int reads = 0, saves = 0, sets = 0;
            writer.Refresh = _ =>
            {
                reads++;
                if (reads == 1) throw Error(SessionError.RateLimitExceeded);
                return UniTask.CompletedTask;
            };
            writer.Save = _ => { saves++; return UniTask.CompletedTask; };

            writer.WriteAsync(Session(), () => sets++, "test").GetAwaiter().GetResult();

            Assert.AreEqual(1, sets, "the property is set once the read succeeds on retry");
            Assert.AreEqual(1, saves, "the save goes out");
            Assert.AreEqual(1, _delays.Count, "one back-off before the retried read");
        }

        [Test]
        public void FatalReadBeforeWrite_IsNotRetried_AndNothingIsWritten()
        {
            var writer = NewWriter();
            int reads = 0, saves = 0, sets = 0;
            writer.Refresh = _ => { reads++; throw Error(SessionError.NotAuthorized); };
            writer.Save = _ => { saves++; return UniTask.CompletedTask; };

            Assert.DoesNotThrow(() => writer.WriteAsync(Session(), () => sets++, "test").GetAwaiter().GetResult(),
                "WriteAsync swallows its failures (it can only be slow, never throw)");
            Assert.AreEqual(1, reads, "a fatal failure is not retried");
            Assert.AreEqual(0, sets);
            Assert.AreEqual(0, saves);
        }

        [Test]
        public void NullLobby_DoesNothing()
        {
            var writer = NewWriter();
            int reads = 0;
            writer.Refresh = _ => { reads++; return UniTask.CompletedTask; };
            writer.WriteAsync(null, () => { }, "test").GetAwaiter().GetResult();
            Assert.AreEqual(0, reads);
        }
    }
}
#endif
