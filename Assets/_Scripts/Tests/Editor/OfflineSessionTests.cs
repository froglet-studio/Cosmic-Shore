#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Obvious.Soap;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The seven OFFLINE cases (Docs/MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md §4.1)
    /// and the invariant worth a test name of its own (§4.2): a late online success must never
    /// tear down a live offline host.
    ///
    /// WHY THIS MATTERS:
    /// Offline is the Steam case and it ships, and until this file it had no tests. Every case
    /// below is a decision some code makes from two or three facts - the device's reachability,
    /// the player's choice, the session's flag. These tests pin each decision where it is made,
    /// with no Editor, no NetworkManager and no UGS: the boot gate's plan, the flag around
    /// StartHost, the monitor's poll, the party layer's stand-downs and the reconnect's order.
    /// </summary>
    [TestFixture]
    public class OfflineSessionTests
    {
        const string OfflinePreferenceKey = "CosmicShore.OfflinePreferred";

        readonly List<UnityEngine.Object> _made = new();
        int _savedPreference;

        T Track<T>(T o) where T : UnityEngine.Object { _made.Add(o); return o; }

        [SetUp]
        public void SetUp()
        {
            // AsMainThread() marshals to the thread MainThreadDispatcher recorded at boot. Record
            // this one, so a continuation completed from the test resumes inline instead of being
            // posted to a player loop that is not running.
            typeof(MainThreadDispatcher).GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null);
            _savedPreference = PlayerPrefs.GetInt(OfflinePreferenceKey, 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made)
                if (o) UnityEngine.Object.DestroyImmediate(o);
            _made.Clear();
            PlayerPrefs.SetInt(OfflinePreferenceKey, _savedPreference);
        }

        // ── Cases 1-3: the boot gate's plan ──────────────────────────────────────────────

        [Test]
        public void Case1_NoNetworkAtBoot_MakesNoRelayAttempts_AndExplainsTheOfflineStart()
        {
            var plan = AuthenticationSceneController.PlanBootNetwork(offlinePreferred: false, deviceOffline: true, offlineSessionLive: false);
            Assert.IsFalse(plan.AttemptRelay, "with no network the attempts cannot succeed - none may be made (no 45 s burn)");
            Assert.IsTrue(plan.UnwantedFallback, "the player did not ask for offline: it is counted and explained");
        }

        [Test]
        public void Case2_NetworkUpButUgsUnreachable_WalksTheRelayAttempts()
        {
            var plan = AuthenticationSceneController.PlanBootNetwork(offlinePreferred: false, deviceOffline: false, offlineSessionLive: false);
            Assert.IsTrue(plan.AttemptRelay, "a reachable device whose UGS calls fail deserves the attempts");
            Assert.IsTrue(plan.UnwantedFallback);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Case3_PlayerChoseOffline_NoAttempts_NoNotice_NoFallbackCount(bool deviceOffline)
        {
            var plan = AuthenticationSceneController.PlanBootNetwork(offlinePreferred: true, deviceOffline: deviceOffline, offlineSessionLive: false);
            Assert.IsFalse(plan.AttemptRelay, "a deliberate choice must not cost the player attempts they asked not to make");
            Assert.IsFalse(plan.UnwantedFallback, "a chosen offline start is neither an 'unwanted offline' notice nor an OfflineFallback count");
        }

        [Test]
        public void AnOfflineSessionAlreadyLive_IsNeverOfferedRelayAttempts()
        {
            var plan = AuthenticationSceneController.PlanBootNetwork(offlinePreferred: false, deviceOffline: false, offlineSessionLive: true);
            Assert.IsFalse(plan.AttemptRelay);
        }

        // ── Cases 2 and 7: the flag around StartHost ─────────────────────────────────────

        [Test]
        public void Case2_TheOfflineFlagIsSetBeforeStartHost()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            bool flagWhenHostStarted = false;

            bool started = OfflineModeService.TryStartOfflineHost(gameData, () =>
            {
                flagWhenHostStarted = gameData.IsOfflineSession;
                return true;
            });

            Assert.IsTrue(started);
            Assert.IsTrue(flagWhenHostStarted,
                "every callback host bring-up fires (connection approval, Player.OnNetworkSpawn) must already see an offline session");
            Assert.IsTrue(gameData.IsOfflineSession);
        }

        [Test]
        public void Case7_StartHostRefuses_TheFlagIsClearedAgain()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            LogAssert.Expect(LogType.Error, new Regex(@"StartHost failed"));

            bool started = OfflineModeService.TryStartOfflineHost(gameData, () => false);

            Assert.IsFalse(started);
            Assert.IsFalse(gameData.IsOfflineSession,
                "a flag left set with no host running stands the party layer down for a session that is not offline");
        }

        [Test]
        public void Case7_StartHostThrows_TheFlagIsClearedAgain()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            LogAssert.Expect(LogType.Error, new Regex(@"StartHost threw"));
            LogAssert.Expect(LogType.Error, new Regex(@"StartHost failed"));

            bool started = OfflineModeService.TryStartOfflineHost(gameData, () => throw new InvalidOperationException("port in use"));

            Assert.IsFalse(started);
            Assert.IsFalse(gameData.IsOfflineSession);
        }

        // ── Case 4: the network dies mid-session ─────────────────────────────────────────

        [Test]
        public void Case4_NetworkLostMidSession_RaisesTheNoticeEventOnce()
        {
            var lost = Track(ScriptableObject.CreateInstance<ScriptableEventNoParam>());
            var found = Track(ScriptableObject.CreateInstance<ScriptableEventNoParam>());
            var variable = Track(ScriptableObject.CreateInstance<NetworkMonitorDataVariable>());
            var data = new NetworkMonitorData { OnNetworkLost = lost, OnNetworkFound = found };
            variable.Value = data;
            int lostCount = 0;
            lost.OnRaised += () => lostCount++;

            bool reachable = true;
            var monitor = new NetworkMonitor(variable, () => reachable);
            monitor.Poll();
            Assert.AreEqual(0, lostCount, "no transition, no event");

            reachable = false;
            monitor.Poll();
            monitor.Poll();
            Assert.AreEqual(1, lostCount, "one loss is one notice, however many polls see it");
            Assert.IsFalse(data.IsOnline);
        }

        [Test]
        public void Case4_OnlyTheBootGateEverEntersAnOfflineSession()
        {
            // Structural, like DisconnectNoticeTests' install-site check. A network that dies
            // mid-session gets a notice; it must never promote the live host to a loopback one in
            // place - that is a re-boot through the Authentication scene (case 6). So the boot gate
            // is the one caller of EnterOfflineSessionAsync.
            var root = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Scripts");
            Assert.IsTrue(Directory.Exists(root), $"{root} not found");
            var callers = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")) continue;
                if (file.EndsWith("OfflineModeService.cs")) continue; // the definition
                if (File.ReadAllText(file).Contains("EnterOfflineSessionAsync(")) callers.Add(Path.GetFileName(file));
            }
            CollectionAssert.AreEqual(new[] { "AuthenticationSceneController.cs" }, callers);
        }

        // ── Case 5: offline, then the network returns ────────────────────────────────────

        [Test]
        public void Case5_OfflineSession_EnsurePartySession_TouchesNothing()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.IsOfflineSession = true;
            var transition = new FakeTransition();
            var session = new FakePartySession();
            var hcs = NewService(gameData, transition, session, new FakePresenceLobby());

            var ensure = hcs.EnsurePartySessionAsync();

            Assert.AreEqual(UniTaskStatus.Succeeded, ensure.Status);
            Assert.AreEqual(0, transition.ShutdownCalls, "the loopback host is the session - it must not be shut down");
            Assert.AreEqual(0, session.CreateCalls);
        }

        [Test]
        public void Case5_OfflineSession_SendInvite_ReturnsBeforeAnyLobbyWork()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.IsOfflineSession = true;
            var hcs = NewService(gameData, new FakeTransition(), new FakePartySession(), lobby: null);

            var send = hcs.SendInviteAsync("some-player");

            Assert.AreEqual(UniTaskStatus.Succeeded, send.Status, "an offline invite must return early, not dereference a lobby that does not exist");
        }

        [Test]
        public void Case5_OfflineSession_ALateSignIn_DoesNotRejoinThePresenceLobby()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.IsOfflineSession = true;
            var lobby = new FakePresenceLobby();
            var hcs = NewService(gameData, new FakeTransition(), new FakePartySession(), lobby);

            var init = (UniTask)typeof(HostConnectionService)
                .GetMethod("EnsureInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(hcs, null);

            Assert.AreEqual(UniTaskStatus.Succeeded, init.Status);
            Assert.AreEqual(0, lobby.JoinCalls,
                "auth can succeed while Relay keeps failing; that sign-in must not restart UGS traffic under an offline player");
        }

        // ── Case 6: offline, then Reconnect ──────────────────────────────────────────────

        [Test]
        public void Case6_Reconnect_ResetsTheParty_ShutsDown_ClearsTheFlag_ThenReboots()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.IsOfflineSession = true;
            var offline = new OfflineModeService(gameData) { OfflinePreferred = true };
            var order = new List<string>();
            var transition = new FakeTransition
            {
                OnShutdown = () => order.Add($"shutdown offline={gameData.IsOfflineSession}"),
                OnClearStale = () => order.Add("clearStale"),
            };
            var reconnect = new ReconnectService(gameData, null, null, transition, null, null, offline,
                resetPartyLayer: () => { order.Add("resetParty"); return UniTask.CompletedTask; },
                loadScene: scene => { order.Add($"load {scene} offline={gameData.IsOfflineSession}"); return UniTask.CompletedTask; });

            var run = reconnect.ReconnectAsync();

            Assert.AreEqual(UniTaskStatus.Succeeded, run.Status);
            Assert.IsTrue(run.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[]
            {
                "resetParty",                       // server-side memberships first, while the transport can still reach UGS
                "shutdown offline=True",            // the loopback host goes down while the session still says offline...
                "clearStale",
                "load Authentication offline=False" // ...and the flag is clear before the boot chain runs again
            }, order);
            Assert.IsFalse(offline.OfflinePreferred, "asking to come back online withdraws a recorded 'stay offline' choice");
        }

        // ── §4.2: the invariant ──────────────────────────────────────────────────────────

        [Test]
        public void ALateOnlineSuccess_NeverBuildsASessionOnTopOfALiveOfflineHost()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            var shutdown = new UniTaskCompletionSource<bool>();
            var transition = new FakeTransition { Shutdown = () => shutdown.Task };
            var session = new FakePartySession();
            var hcs = NewService(gameData, transition, session, new FakePresenceLobby());

            // Online at entry: the call passes its entry check and starts shutting the host down.
            var ensure = hcs.EnsurePartySessionAsync();
            Assert.AreEqual(1, transition.ShutdownCalls);
            Assert.AreEqual(UniTaskStatus.Pending, ensure.Status);

            // Meanwhile the boot gate gave up on Relay and brought the offline host up...
            gameData.IsOfflineSession = true;
            // ...and only now does this call's shutdown finish.
            shutdown.TrySetResult(true);

            Assert.AreEqual(0, session.CreateCalls,
                "a Relay session created now would start a host on top of the live loopback one");
            Assert.AreEqual(UniTaskStatus.Succeeded, ensure.Status);
        }

        // ── Fixtures ─────────────────────────────────────────────────────────────────────

        HostConnectionService NewService(GameDataSO gameData, INetworkTransitionService transition,
                                         IPartySessionService session, IPresenceLobbyService lobby)
        {
            // Inactive, so Awake never runs: no singleton claimed, no DontDestroyOnLoad. The
            // [Inject] fields are what Reflex would fill.
            var go = Track(new GameObject("HostConnectionService (test)"));
            go.SetActive(false);
            var hcs = go.AddComponent<HostConnectionService>();
            Set(hcs, "_gameData", gameData);
            Set(hcs, "_propertyWriter", new LobbyPropertyWriter(null));
            Set(hcs, "_networkTransition", transition);
            Set(hcs, "_partySessionService", session);
            Set(hcs, "_lobbyService", lobby);
            Set(hcs, "connectionData", Track(ScriptableObject.CreateInstance<HostConnectionDataSO>()));
            return hcs;
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        sealed class FakeTransition : INetworkTransitionService
        {
            public int ShutdownCalls;
            public Func<UniTask<bool>> Shutdown;
            public Action OnShutdown, OnClearStale;

            public UniTask<bool> ShutdownAsync(float timeoutSeconds, CancellationToken ct)
            {
                ShutdownCalls++;
                OnShutdown?.Invoke();
                return Shutdown != null ? Shutdown() : UniTask.FromResult(true);
            }

            public UniTask<bool> WaitForClientConnectionAsync(float timeoutSeconds, CancellationToken ct) => UniTask.FromResult(true);
            public UniTask<bool> WaitForSceneSyncAsync(string sceneName, float timeoutSeconds, CancellationToken ct) => UniTask.FromResult(true);
            public void ClearStaleReferences() => OnClearStale?.Invoke();
        }

        sealed class FakePartySession : IPartySessionService
        {
            public int CreateCalls;
            public ISession ActiveSession => null;
#pragma warning disable CS0067 // the interface's event; nothing here raises it
            public event Action<string> PlayerLeaving;
#pragma warning restore CS0067
            public float CreatedAtUnscaledTime => 0f;
            public UniTask CreateAsync(int maxPlayers) { CreateCalls++; return UniTask.CompletedTask; }
            public UniTask JoinByIdAsync(string sessionId) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId, bool asSpectator) => UniTask.CompletedTask;
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId) => UniTask.CompletedTask;
            public void ClearSession() { }
        }

        sealed class FakePresenceLobby : IPresenceLobbyService
        {
            public int JoinCalls;
            public ISession ActiveLobby => null;
            public Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }
            public UniTask JoinOrCreateAsync(int maxPlayers) { JoinCalls++; return UniTask.CompletedTask; }
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask SavePropertiesAsync(Dictionary<string, PlayerProperty> properties, string operationName) => UniTask.CompletedTask;
            public void ForceReset() { }
            public UniTask ConvergeToCanonicalAsync(int maxPlayers) => UniTask.CompletedTask;
        }
    }
}
#endif
