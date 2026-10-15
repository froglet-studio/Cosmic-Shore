using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class NetFaultsStaticsCollection { public const string Name = "NetFaults statics"; }

    /// <summary>
    /// <see cref="NetFaults"/>: each armed fault reaches the call it names with the SDK-shaped error,
    /// counted faults are consumed, and with nothing armed every call passes through (docs/MULTIPLAYER.md §6.3).
    /// </summary>
    [Collection(NetFaultsStaticsCollection.Name)]
    public class NetFaultsTests : IDisposable
    {
        sealed class FakeSession : IHostSession
        {
            public int Refreshes, Saves, Leaves, Deletes;
            public string Id => "s1";
            public string Code => "CODE";
            public bool IsHost => true;
            public int MaxPlayers => 4;
            public int PlayerCount => 1;
            public event Action Deleted { add { } remove { } }
            public event Action<string> PlayerLeaving { add { } remove { } }
            public IReadOnlyList<IReadOnlyPlayer> Players => Array.Empty<IReadOnlyPlayer>();
            public IPlayer CurrentPlayer => null;
            public Task RefreshAsync() { Refreshes++; return Task.CompletedTask; }
            public Task SaveCurrentPlayerDataAsync() { Saves++; return Task.CompletedTask; }
            public Task LeaveAsync() { Leaves++; return Task.CompletedTask; }
            public IHostSession AsHost() => this;
            public Task DeleteAsync() { Deletes++; return Task.CompletedTask; }
            public Task RemovePlayerAsync(string playerId) => Task.CompletedTask;
        }

        sealed class FakeService : IMultiplayerService
        {
            public readonly FakeSession Session = new();
            public int Creates, Joins, Queries;
            public Task<ISession> CreateSessionAsync(SessionOptions options) { Creates++; return Task.FromResult<ISession>(Session); }
            public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null) { Joins++; return Task.FromResult<ISession>(Session); }
            public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options) { Queries++; return Task.FromResult(new QuerySessionsResults(new List<ISessionInfo>())); }
        }

        readonly FakeService inner = new();
        readonly IMultiplayerService svc;

        public NetFaultsTests()
        {
            NetFaults.Clear();
            NetFaults.Raised.Clear();
            svc = new FaultInjectingMultiplayerService(inner);
        }

        public void Dispose()
        {
            NetFaults.Clear();
            NetFaults.Raised.Clear();
            MultiplayerService.Reset();
        }

        static SessionError ErrorOf(Func<Task> call)
        {
            var e = Assert.ThrowsAsync<SessionException>(call).GetAwaiter().GetResult();
            return e.Error;
        }

        [Fact]
        public async Task NothingArmed_EveryCallPassesThrough()
        {
            var s = await svc.CreateSessionAsync(new SessionOptions());
            await svc.JoinSessionByIdAsync("s1");
            await svc.QuerySessionsAsync(new QuerySessionsOptions());
            await s.RefreshAsync();
            await s.SaveCurrentPlayerDataAsync();
            Assert.Equal((1, 1, 1, 1, 1), (inner.Creates, inner.Joins, inner.Queries, inner.Session.Refreshes, inner.Session.Saves));
            Assert.Empty(NetFaults.Raised);
        }

        [Fact]
        public async Task Full_RefusesTheNextJoin_Once_WithTheFullSessionShape()
        {
            Assert.Equal("[netfault] full=1", NetFaults.Apply("full"));
            var e = await Assert.ThrowsAsync<SessionException>(() => svc.JoinSessionByIdAsync("s1"));
            Assert.Equal(SessionError.Unknown, e.Error);
            Assert.Equal("Session is full.", e.Message);
            Assert.Equal(0, inner.Joins);
            await svc.CreateSessionAsync(new SessionOptions()); // a create never meets "full"
            await svc.JoinSessionByIdAsync("s1");               // consumed: the next join passes
            Assert.Equal(1, inner.Joins);
        }

        [Fact]
        public async Task RateLimit_FailsTheNextNCalls_OfAnyKind_With429()
        {
            NetFaults.Apply("ratelimit=2");
            Assert.Equal(SessionError.RateLimitExceeded, ErrorOf(() => svc.QuerySessionsAsync(new QuerySessionsOptions())));
            Assert.Equal(SessionError.RateLimitExceeded, ErrorOf(() => svc.CreateSessionAsync(new SessionOptions())));
            await svc.QuerySessionsAsync(new QuerySessionsOptions());
            Assert.Equal(2, NetFaults.Raised["ratelimit"]);
        }

        [Fact]
        public async Task RelayFail_FailsCreatesAndJoins_NotQueries()
        {
            NetFaults.Apply("relayfail=2");
            await svc.QuerySessionsAsync(new QuerySessionsOptions());
            Assert.Equal(SessionError.NetworkSetupFailed, ErrorOf(() => svc.CreateSessionAsync(new SessionOptions())));
            Assert.Equal(SessionError.NetworkSetupFailed, ErrorOf(() => svc.JoinSessionByIdAsync("s1")));
            await svc.CreateSessionAsync(new SessionOptions());
        }

        [Fact]
        public async Task Down_FailsEveryServiceCall_ButLeavingStillWorks_UntilUp()
        {
            var s = await svc.CreateSessionAsync(new SessionOptions());
            NetFaults.Apply("down");
            ErrorOf(() => svc.CreateSessionAsync(new SessionOptions()));
            ErrorOf(() => svc.JoinSessionByIdAsync("s1"));
            ErrorOf(() => svc.QuerySessionsAsync(new QuerySessionsOptions()));
            ErrorOf(() => s.RefreshAsync());
            ErrorOf(() => s.SaveCurrentPlayerDataAsync());
            await s.LeaveAsync();
            await s.AsHost().DeleteAsync();
            Assert.Equal((1, 1), (inner.Session.Leaves, inner.Session.Deletes));
            NetFaults.Apply("up");
            await s.RefreshAsync();
            Assert.Equal(1, inner.Session.Refreshes);
        }

        [Fact]
        public async Task Slow_DelaysEveryCall()
        {
            NetFaults.Apply("slow=60");
            var sw = Stopwatch.StartNew();
            await svc.QuerySessionsAsync(new QuerySessionsOptions());
            Assert.True(sw.ElapsedMilliseconds >= 55, $"took {sw.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData("full=-1")]
        [InlineData("explode")]
        [InlineData("ratelimit=3 bogus")]
        public void BadSpec_ChangesNothing(string spec)
        {
            NetFaults.Apply("full=2");
            Assert.Contains("error", NetFaults.Apply(spec));
            Assert.Equal(2, NetFaults.FullJoins);
            Assert.Equal(0, NetFaults.RateLimited);
        }

        [Fact]
        public void Install_WrapsTheServiceOnce()
        {
            MultiplayerService.Instance = inner;
            NetFaults.Install();
            NetFaults.Install();
            var wrapped = Assert.IsType<FaultInjectingMultiplayerService>(MultiplayerService.Instance);
            Assert.Same(inner, wrapped.Inner);
        }
    }
}
