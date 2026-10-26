#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Multiplayer;
using UnityEngine;
using FakeSession = CosmicShore.Tests.PartyPushAndVoidedReadTests.FakeSession;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Block 5 step 5, the presence lobby's push (Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2; the August
    /// presence work's b0adfa7284 and 4022675284 on Ys-rescue-2026-08-24).
    ///
    /// WHY: nothing subscribed to the presence lobby, so every arrival, departure and status change was
    /// found by the next poll, and a lobby that dropped us was noticed only after three failed reads.
    /// The traps these tests hold: a push tick must read nothing and leave the connection watchdog alone,
    /// and our OWN leave or delete - which raises Deleted on the lobby we leave - must never read as
    /// being thrown out.
    /// </summary>
    [TestFixture]
    public class PresencePushTests
    {
        readonly List<Object> _made = new();
        T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        [SetUp] public void SetUp() => UgsRequestTelemetry.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
            UgsRequestTelemetry.Reset();
        }

        // ── The channel ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Channel_RosterPushes_RaiseOneDirtyFlag()
        {
            var channel = new PresencePushChannel();
            channel.Attach(FakeSession.Create(out var fake));

            fake.Raise("PlayerJoined", "p2");
            fake.Raise("PlayerHasLeft", "p3");
            fake.Raise("PlayerPropertiesChanged");

            Assert.IsTrue(channel.ConsumeRosterDirty(), "a burst of pushes is one refresh");
            Assert.IsFalse(channel.ConsumeRosterDirty());
            Assert.IsFalse(channel.ConsumeMembershipLost(), "a roster change is not a lost membership");
        }

        [Test]
        public void Channel_RemovedOrDeleted_RaisesMembershipLost()
        {
            var channel = new PresencePushChannel();
            channel.Attach(FakeSession.Create(out var fake));

            fake.Raise("RemovedFromSession");
            Assert.IsTrue(channel.ConsumeMembershipLost());
            fake.Raise("Deleted");
            Assert.IsTrue(channel.ConsumeMembershipLost());
            Assert.IsFalse(channel.ConsumeMembershipLost());
        }

        [Test]
        public void Channel_SwitchingLobbies_StopsListeningToTheOldOne_AndDropsItsFlags()
        {
            var channel = new PresencePushChannel();
            var first = FakeSession.Create(out var oldFake);
            channel.Attach(first);
            oldFake.Raise("PlayerJoined", "p2");
            channel.Attach(first);                                   // the same lobby again: nothing changes
            Assert.AreEqual(5, oldFake.HandlerCount, "PlayerJoined, PlayerHasLeft, PlayerPropertiesChanged, RemovedFromSession, Deleted");

            channel.Attach(FakeSession.Create(out var newFake));
            Assert.AreEqual(0, oldFake.HandlerCount, "no handler outlives the lobby it was attached to");
            Assert.AreEqual(5, newFake.HandlerCount);
            Assert.IsFalse(channel.ConsumeRosterDirty(), "a flag from a lobby we no longer hold is dropped");

            oldFake.Raise("Deleted");
            Assert.IsFalse(channel.ConsumeMembershipLost(), "the old lobby's delete is not our membership");
        }

        // ── The lobby service: one choke point, and our own leave is not being thrown out ────────

        [Test]
        public void LeavingOnPurpose_DoesNotReadAsBeingThrownOut()
        {
            var service = NewLobbyService();
            var lobby = FakeSession.Create(out var fake);
            service.SetActiveLobby(lobby);
            bool lostMidLeave = true;
            fake.OnLeave = () =>
            {
                fake.Raise("Deleted");                               // what the SDK does while we leave
                // A frame can run while the leave awaits the network: Update must not see a loss then.
                lostMidLeave = service.ConsumeMembershipLost();
            };

            service.LeaveAsync().GetAwaiter().GetResult();

            Assert.IsNull(service.ActiveLobby);
            Assert.IsFalse(lostMidLeave, "our own leave must not start a rejoin, even mid-leave");
            Assert.IsFalse(service.ConsumeMembershipLost());
            Assert.AreEqual(0, fake.HandlerCount);
        }

        [Test]
        public void ForceReset_StopsListening()
        {
            var service = NewLobbyService();
            service.SetActiveLobby(FakeSession.Create(out var fake));
            Assert.AreEqual(5, fake.HandlerCount);

            service.ForceReset();

            Assert.AreEqual(0, fake.HandlerCount);
            fake.Raise("PlayerJoined", "p2");
            Assert.IsFalse(service.ConsumeRosterDirty());
        }

        [Test]
        public void TheLobbyWeHold_PushesThroughTheService()
        {
            var service = NewLobbyService();
            service.SetActiveLobby(FakeSession.Create(out var fake));

            fake.Raise("PlayerJoined", "p2");
            Assert.IsTrue(service.ConsumeRosterDirty());
            fake.Raise("RemovedFromSession");
            Assert.IsTrue(service.ConsumeMembershipLost());
        }

        // ── HostConnectionService: a push tick reads nothing; a pushed loss rejoins at once ───────

        [Test]
        public void PushTick_ReadsNothing_PublishesAndLeavesTheWatchdogAlone()
        {
            var data = NewData();
            var lobby = new CountingLobby { Active = FakeSession.Create(out _) };
            var hcs = NewService(data, lobby, out int[] saves);
            SetField(hcs, "_consecutiveRefreshErrors", 2);

            Call(hcs, "RefreshAsync", false);

            Assert.AreEqual(0, lobby.Refreshes, "the SDK already applied the change: no GET");
            Assert.AreEqual(0, lobby.Converges, "a burst of pushes must not become a burst of queries");
            Assert.AreEqual(1, saves[0], "our own state still goes out");
            Assert.AreEqual(1, UgsRequestTelemetry.Total(UgsRequestCounter.PresencePush));
            Assert.AreEqual(2, GetField<int>(hcs, "_consecutiveRefreshErrors"), "a push tick neither clears nor feeds the watchdog");
        }

        [Test]
        public void FetchTick_StillReads()
        {
            // Negative control for the push tick above.
            var lobby = new CountingLobby { Active = FakeSession.Create(out _) };
            var hcs = NewService(NewData(), lobby, out _);

            Call(hcs, "RefreshAsync", true);

            Assert.AreEqual(1, lobby.Refreshes);
            Assert.AreEqual(0, UgsRequestTelemetry.Total(UgsRequestCounter.PresencePush));
        }

        [Test]
        public void PushedMembershipLoss_RejoinsAtOnce()
        {
            var lobby = new CountingLobby { Active = FakeSession.Create(out _) };
            var hcs = NewService(NewData(), lobby, out _);

            Call(hcs, "HandlePresenceMembershipLost");

            Assert.AreEqual(1, lobby.ForceResets, "the dead reference is dropped");
            Assert.AreEqual(1, lobby.Joins, "and the rejoin starts now, not after three failed reads");
            Assert.AreEqual(1, UgsRequestTelemetry.Total(UgsRequestCounter.PresenceForceReset));
        }

        // ── Fixtures ────────────────────────────────────────────────────────────────────────────

        PresenceLobbyService NewLobbyService() => new(NewData(), new LobbyPropertyWriter(null), null);

        HostConnectionDataSO NewData()
        {
            var data = Track(ScriptableObject.CreateInstance<HostConnectionDataSO>());
            data.PartyMembers = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.LocalPlayerId = "me";
            data.LocalDisplayName = "Me";
            return data;
        }

        HostConnectionService NewService(HostConnectionDataSO data, IPresenceLobbyService lobby, out int[] saves)
        {
            // Inactive, so Awake never runs (OfflineSessionTests' pattern); the [Inject] fields are what Reflex fills.
            var go = Track(new GameObject("HostConnectionService (test)"));
            go.SetActive(false);
            var hcs = go.AddComponent<HostConnectionService>();
            var counter = new int[1];
            saves = counter;
            var writer = new LobbyPropertyWriter(null)
            {
                Refresh = _ => UniTask.CompletedTask,
                Save = _ => { counter[0]++; return UniTask.CompletedTask; },
            };
            SetField(hcs, "_gameData", Track(ScriptableObject.CreateInstance<GameDataSO>()));
            SetField(hcs, "_propertyWriter", writer);
            SetField(hcs, "_partySessionService", new NoParty());
            SetField(hcs, "_lobbyService", lobby);
            SetField(hcs, "_scheduler", new LobbyRefreshScheduler(1.5f));
            SetField(hcs, "_memberService", new PartyMemberService(data, new SoapPartyEventBus(data)));
            SetField(hcs, "_eventBus", new SoapPartyEventBus(data));
            SetField(hcs, "connectionData", data);
            return hcs;
        }

        static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(target.GetType().Name, name);

        static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);
        static T GetField<T>(object target, string name) => (T)Field(target, name).GetValue(target);

        static void Call(object target, string method, params object[] args) =>
            (target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
             ?? throw new MissingMethodException(target.GetType().Name, method)).Invoke(target, args.Length == 0 ? null : args);

        sealed class CountingLobby : IPresenceLobbyService
        {
            public ISession Active;
            public int Refreshes, Converges, ForceResets, Joins;
            public ISession ActiveLobby => Active;
            public Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }
            public bool ConsumeRosterDirty() => false;
            public bool ConsumeMembershipLost() => false;
            public UniTask JoinOrCreateAsync(int maxPlayers) { Joins++; return UniTask.CompletedTask; }
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() { Refreshes++; return UniTask.CompletedTask; }
            public UniTask SavePropertiesAsync(Dictionary<string, PlayerProperty> properties, string operationName) => UniTask.CompletedTask;
            public void ForceReset() { ForceResets++; Active = null; }
            public UniTask ConvergeToCanonicalAsync(int maxPlayers) { Converges++; return UniTask.CompletedTask; }
        }

        sealed class NoParty : IPartySessionService
        {
            public ISession ActiveSession => null;
#pragma warning disable CS0067 // the interface's event; nothing here raises it
            public event Action<string> PlayerLeaving;
#pragma warning restore CS0067
            public float CreatedAtUnscaledTime => 0f;
            public bool ConsumeRosterChanged() => false;
            public UniTask CreateAsync(int maxPlayers) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId, bool asSpectator) => UniTask.CompletedTask;
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId) => UniTask.CompletedTask;
            public void ClearSession() { }
        }
    }
}
#endif
