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
    /// Block 5 step 7, two absent reads before a player is removed (Docs/PartySystem/BRANCH_AUDIT_2026-10.md
    /// §2; the August presence work's c9c6db1772 on Ys-rescue-2026-08-24).
    ///
    /// WHY: one short read removed a player from the online list or the party. The SDK's stale-index
    /// fault, a read landing mid-change or a fresh converge all return short rosters, so the online panel
    /// emptied and refilled and party members vanished and came back. A departure the service states
    /// (its PlayerLeaving / PlayerHasLeft push, or turning spectator) still removes at once.
    /// </summary>
    [TestFixture]
    public class AbsenceStrikesTests
    {
        readonly List<Object> _made = new();
        T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ── The counter ────────────────────────────────────────────────────────────────────────

        [Test]
        public void OneAbsentRead_Holds_TheSecondRemoves()
        {
            var strikes = new AbsenceStrikes();
            Assert.IsFalse(strikes.Absent("p2", departureStated: false, serverRead: true), "one short read is not a departure");
            Assert.AreEqual(1, strikes.StrikesFor("p2"));
            Assert.IsTrue(strikes.Absent("p2", false, true), "the second absent read in a row removes");
            Assert.AreEqual(0, strikes.StrikesFor("p2"), "a removed player's strikes are forgotten");
        }

        [Test]
        public void BeingSeenAgain_ClearsTheStrike()
        {
            var strikes = new AbsenceStrikes();
            strikes.Absent("p2", false, true);
            strikes.Seen("p2");
            Assert.IsFalse(strikes.Absent("p2", false, true), "blips far apart never add up");
        }

        [Test]
        public void ReReadingTheLocalCopy_NeverAddsAStrike()
        {
            var strikes = new AbsenceStrikes();
            strikes.Absent("p2", false, serverRead: true);
            for (int i = 0; i < 5; i++)
                Assert.IsFalse(strikes.Absent("p2", false, serverRead: false), "a push tick re-reads the same short roster");
            Assert.AreEqual(1, strikes.StrikesFor("p2"));
        }

        [Test]
        public void AStatedDeparture_RemovesAtOnce_EvenWithoutAServerRead()
        {
            var strikes = new AbsenceStrikes();
            Assert.IsTrue(strikes.Absent("p2", departureStated: true, serverRead: false));
        }

        [Test]
        public void DepartureReports_AreTakenOnce_AndClearDropsThem()
        {
            var reports = new DepartureReports();
            var into = new HashSet<string> { "leftover" };
            reports.Report("p2");
            reports.Report(null);
            reports.TakeInto(into);
            CollectionAssert.AreEquivalent(new[] { "p2" }, into, "the set is cleared first and empty ids are ignored");
            reports.TakeInto(into);
            Assert.IsEmpty(into, "taken once");

            reports.Report("p3");
            reports.Clear();
            reports.TakeInto(into);
            Assert.IsEmpty(into);
        }

        // ── The two push sources report who left ───────────────────────────────────────────────

        [Test]
        public void PresenceChannel_ReportsPlayerHasLeft_AndDetachDropsIt()
        {
            var channel = new PresencePushChannel();
            channel.Attach(FakeSession.Create(out var fake));
            var into = new HashSet<string>();

            fake.Raise("PlayerHasLeft", "p2");
            channel.TakeDepartures(into);
            CollectionAssert.AreEquivalent(new[] { "p2" }, into);

            fake.Raise("PlayerHasLeft", "p3");
            channel.Detach();
            channel.TakeDepartures(into);
            Assert.IsEmpty(into, "a departure from a lobby we no longer hold says nothing about the next one");
        }

        [Test]
        public void PartySession_ReportsLeavingAndHasLeft_AndClearSessionDropsThem()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            var svc = new PartySessionService(NewData(), gameData, null);
            var session = FakeSession.Create(out var fake);
            gameData.ActiveSession = session;
            svc.Attach(session);
            var into = new HashSet<string>();

            fake.Raise("PlayerLeaving", "p2");
            fake.Raise("PlayerHasLeft", "p3");
            svc.TakeDepartures(into);
            CollectionAssert.AreEquivalent(new[] { "p2", "p3" }, into);

            fake.Raise("PlayerHasLeft", "p4");
            svc.ClearSession();
            svc.TakeDepartures(into);
            Assert.IsEmpty(into);
        }

        // ── The party list ─────────────────────────────────────────────────────────────────────

        [Test]
        public void PartyMember_MissingFromOneRead_KeepsTheSlot_UntilTheSecond()
        {
            var data = NewData();
            var members = new PartyMemberService(data, new SoapPartyEventBus(data));
            var two = RosterSession.Create(out var roster);
            roster.Players.Add(Player("p2"));
            members.SyncFromSession(two, "me");
            Assert.AreEqual(1, data.PartyMembers.Count);

            roster.Players.Clear();
            members.SyncFromSession(two, "me");
            Assert.AreEqual(1, data.PartyMembers.Count, "one short read keeps the member");
            members.SyncFromSession(two, "me");
            Assert.AreEqual(0, data.PartyMembers.Count, "the second absent read removes them");
        }

        [Test]
        public void PartyMember_LocalCopyTicks_NeverRemove_AndASightingResets()
        {
            var data = NewData();
            var members = new PartyMemberService(data, new SoapPartyEventBus(data));
            var session = RosterSession.Create(out var roster);
            roster.Players.Add(Player("p2"));
            members.SyncFromSession(session, "me");

            roster.Players.Clear();
            members.SyncFromSession(session, "me");                           // strike 1
            for (int i = 0; i < 4; i++)
                members.SyncFromSession(session, "me", null, serverRead: false);
            Assert.AreEqual(1, data.PartyMembers.Count, "ticks between GETs re-read the same copy and add no strike");

            roster.Players.Add(Player("p2"));
            members.SyncFromSession(session, "me");                           // seen: strike cleared
            roster.Players.Clear();
            members.SyncFromSession(session, "me");
            Assert.AreEqual(1, data.PartyMembers.Count, "the strike from before the sighting does not count");
        }

        [Test]
        public void PartyMember_StatedDepartureOrSpectator_GoesAtOnce()
        {
            var data = NewData();
            var members = new PartyMemberService(data, new SoapPartyEventBus(data));
            var session = RosterSession.Create(out var roster);
            roster.Players.Add(Player("p2"));
            roster.Players.Add(Player("p3"));
            members.SyncFromSession(session, "me");
            Assert.AreEqual(2, data.PartyMembers.Count);

            roster.Players.Clear();
            roster.Players.Add(Player("p3", spectator: true));
            members.SyncFromSession(session, "me", new HashSet<string> { "p2" }, serverRead: false);

            Assert.AreEqual(0, data.PartyMembers.Count, "the push said p2 left and p3 turned spectator: both are stated");
        }

        [Test]
        public void HostReconcile_AfterPlayerLeaving_RemovesOnTheFirstRead()
        {
            var data = NewData();
            var party = new RosterParty { Active = RosterSession.Create(out var roster) };
            roster.Players.Add(Player("p2"));
            var hcs = NewService(data, party, new RosterLobby { Active = RosterSession.Create(out _) });
            CallAsync(hcs, "RefreshPartyMembersAsync", true, true);
            Assert.AreEqual(1, data.PartyMembers.Count);

            roster.Players.Clear();
            party.Departed.Add("p2");
            CallAsync(hcs, "RefreshPartyMembersAsync", true, true);

            Assert.AreEqual(0, data.PartyMembers.Count, "PlayerLeaving drives ReconcilePartyMembersNow: one read is enough");
        }

        [Test]
        public void PartyPoll_OneShortRead_ThenAPushSync_KeepsTheMember()
        {
            var data = NewData();
            var party = new RosterParty { Active = RosterSession.Create(out var roster) };
            roster.Players.Add(Player("p2"));
            var hcs = NewService(data, party, new RosterLobby { Active = RosterSession.Create(out _) });
            CallAsync(hcs, "RefreshPartyMembersAsync", true, false);            // safety poll: a GET
            Assert.AreEqual(1, party.Refreshes);

            roster.Players.Clear();
            CallAsync(hcs, "RefreshPartyMembersAsync", true, false);            // second GET: strike 1
            CallAsync(hcs, "RefreshPartyMembersAsync", false, false);           // a push sync: the SDK's copy
            Assert.AreEqual(2, party.Refreshes, "a push sync does no GET (Block 5 step 8)");
            Assert.AreEqual(1, data.PartyMembers.Count, "a GET and a re-read of its copy are one read, not two");

            CallAsync(hcs, "RefreshPartyMembersAsync", true, false);
            Assert.AreEqual(0, data.PartyMembers.Count);
        }

        // ── The online list ────────────────────────────────────────────────────────────────────

        [Test]
        public void OnlinePlayer_MissingFromOneRead_StaysListed_UntilTheSecond()
        {
            var data = NewData();
            var lobby = new RosterLobby { Active = RosterSession.Create(out var roster) };
            roster.Players.Add(Player("p2"));
            var hcs = NewService(data, new RosterParty(), lobby);

            Call(hcs, "RefreshOnlinePlayersDiff", true);
            Assert.AreEqual(1, data.OnlinePlayers.Count);

            roster.Players.Clear();
            Call(hcs, "RefreshOnlinePlayersDiff", true);
            Assert.AreEqual(1, data.OnlinePlayers.Count, "one short read no longer empties the panel");
            Call(hcs, "RefreshOnlinePlayersDiff", false);
            Assert.AreEqual(1, data.OnlinePlayers.Count, "a push tick re-reads the same copy and adds no strike");
            Call(hcs, "RefreshOnlinePlayersDiff", true);
            Assert.AreEqual(0, data.OnlinePlayers.Count, "the second absent server read removes them");
        }

        [Test]
        public void OnlinePlayer_TheLobbySaidLeft_GoesOnThePushTick()
        {
            var data = NewData();
            var lobby = new RosterLobby { Active = RosterSession.Create(out var roster) };
            roster.Players.Add(Player("p2"));
            var hcs = NewService(data, new RosterParty(), lobby);
            Call(hcs, "RefreshOnlinePlayersDiff", true);

            roster.Players.Clear();
            lobby.Departed.Add("p2");
            Call(hcs, "RefreshOnlinePlayersDiff", false);

            Assert.AreEqual(0, data.OnlinePlayers.Count, "PlayerHasLeft is the service stating it");
        }

        [Test]
        public void OutgoingInvite_OutlivesOneShortRead_AndClearsWithTheRemoval()
        {
            var data = NewData();
            var lobby = new RosterLobby { Active = RosterSession.Create(out var roster) };
            roster.Players.Add(Player("p2"));
            var hcs = NewService(data, new RosterParty(), lobby);
            var invites = new InviteService();
            invites.AddOrRefresh("p2", "party", "me", "Me", 0, float.MaxValue);
            SetField(hcs, "_inviteService", invites);
            Call(hcs, "RefreshOnlinePlayersDiff", true);

            roster.Players.Clear();
            Call(hcs, "RefreshOnlinePlayersDiff", true);
            Assert.IsTrue(invites.Contains("p2"), "the invite follows the list: one short read keeps both");
            Call(hcs, "RefreshOnlinePlayersDiff", true);
            Assert.IsFalse(invites.Contains("p2"), "removed from the list, so the invite's slot is freed");
        }

        // ── Fixtures ────────────────────────────────────────────────────────────────────────────

        HostConnectionDataSO NewData()
        {
            var data = Track(ScriptableObject.CreateInstance<HostConnectionDataSO>());
            data.PartyMembers  = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.OnlinePlayers = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.LocalPlayerId = "me";
            data.LocalDisplayName = "Me";
            return data;
        }

        HostConnectionService NewService(HostConnectionDataSO data, IPartySessionService party, IPresenceLobbyService lobby)
        {
            // Inactive, so Awake never runs (OfflineSessionTests' pattern); the [Inject] fields are what Reflex fills.
            var go = Track(new GameObject("HostConnectionService (test)"));
            go.SetActive(false);
            var hcs = go.AddComponent<HostConnectionService>();
            SetField(hcs, "_gameData", Track(ScriptableObject.CreateInstance<GameDataSO>()));
            SetField(hcs, "_propertyWriter", new LobbyPropertyWriter(null)
            {
                Refresh = _ => UniTask.CompletedTask,
                Save = _ => UniTask.CompletedTask,
            });
            SetField(hcs, "_partySessionService", party);
            SetField(hcs, "_lobbyService", lobby);
            SetField(hcs, "_scheduler", new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS));
            SetField(hcs, "_memberService", new PartyMemberService(data, new SoapPartyEventBus(data)));
            SetField(hcs, "_eventBus", new SoapPartyEventBus(data));
            SetField(hcs, "_inviteService", new InviteService());
            SetField(hcs, "connectionData", data);
            return hcs;
        }

        static IReadOnlyPlayer Player(string id, bool spectator = false)
        {
            var proxy = DispatchProxy.Create<IReadOnlyPlayer, RosterPlayer>();
            var p = (RosterPlayer)(object)proxy;
            p.IdValue = id;
            if (spectator)
                p.PropertiesValue[PartySessionService.SPECTATOR_KEY] = new PlayerProperty("1", VisibilityPropertyOptions.Public);
            return proxy;
        }

        static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(target.GetType().Name, name);

        static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);

        static object Call(object target, string method, params object[] args) =>
            (target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
             ?? throw new MissingMethodException(target.GetType().Name, method)).Invoke(target, args);

        /// <summary>Runs an async method whose every await completes at once (the stubs here), and rethrows its fault.</summary>
        static void CallAsync(object target, string method, params object[] args)
        {
            var task = (UniTask)Call(target, method, args);
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status, $"{method} should finish synchronously against the stubs");
            task.GetAwaiter().GetResult();
        }

        /// <summary>An ISession whose Players the test sets.</summary>
        public class RosterSession : DispatchProxy
        {
            public readonly List<IReadOnlyPlayer> Players = new();

            public static ISession Create(out RosterSession roster)
            {
                var proxy = Create<ISession, RosterSession>();
                roster = (RosterSession)(object)proxy;
                return proxy;
            }

            protected override object Invoke(MethodInfo m, object[] args)
            {
                switch (m.Name)
                {
                    case "get_Id": return "session";
                    case "get_Players": return Players;
                }
                if (m.ReturnType == typeof(System.Threading.Tasks.Task)) return System.Threading.Tasks.Task.CompletedTask;
                return m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
            }
        }

        public class RosterPlayer : DispatchProxy
        {
            public string IdValue;
            public readonly Dictionary<string, PlayerProperty> PropertiesValue = new();

            protected override object Invoke(MethodInfo m, object[] args)
            {
                switch (m.Name)
                {
                    case "get_Id": return IdValue;
                    case "get_Properties": return PropertiesValue;
                }
                return m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
            }
        }

        sealed class RosterParty : IPartySessionService
        {
            public ISession Active;
            public readonly HashSet<string> Departed = new();
            public int Refreshes;
            public ISession ActiveSession => Active;
#pragma warning disable CS0067 // the interface's event; nothing here raises it
            public event Action<string> PlayerLeaving;
#pragma warning restore CS0067
            public float CreatedAtUnscaledTime => -1000f;
            public bool ConsumeRosterChanged() => false;
            public void TakeDepartures(ISet<string> into) { into.Clear(); into.UnionWith(Departed); Departed.Clear(); }
            public UniTask CreateAsync(int maxPlayers) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId, bool asSpectator) => UniTask.CompletedTask;
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() { Refreshes++; return UniTask.CompletedTask; }
            public UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId) => UniTask.CompletedTask;
            public void ClearSession() => Active = null;
        }

        sealed class RosterLobby : IPresenceLobbyService
        {
            public ISession Active;
            public readonly HashSet<string> Departed = new();
            public ISession ActiveLobby => Active;
            public Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }
            public bool ConsumeRosterDirty() => false;
            public bool ConsumeMembershipLost() => false;
            public void TakeDepartures(ISet<string> into) { into.Clear(); into.UnionWith(Departed); Departed.Clear(); }
            public UniTask JoinOrCreateAsync(int maxPlayers) => UniTask.CompletedTask;
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask SavePropertiesAsync(Dictionary<string, PlayerProperty> properties, string operationName) => UniTask.CompletedTask;
            public void ForceReset() => Active = null;
            public UniTask ConvergeToCanonicalAsync(int maxPlayers) => UniTask.CompletedTask;
        }
    }
}
#endif
