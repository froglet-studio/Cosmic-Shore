#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
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
    /// Block 5 step 9, the explicit leave on quit and background (Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2;
    /// the August presence work's 52b8f5f656 on Ys-rescue-2026-08-24).
    ///
    /// WHY: the only leave was an async OnDestroy that Unity does not wait for during shutdown, so a quitting
    /// player stayed in everyone's online list until UGS reaped them (about 30 s), and the pushes of steps 4-7
    /// had no leave to fan out. The quit is now held (at most 1.5 s) while the leaves go out; a backgrounded
    /// app leaves the presence lobby only and rejoins on resume.
    /// </summary>
    [TestFixture]
    public class DepartureLeaveTests
    {
        readonly List<Object> _made = new();
        T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        int _quits;
        Action _quitBefore;

        [SetUp]
        public void SetUp()
        {
            ResetLifecycleStatics();
            _quits = 0;
            _quitBefore = ApplicationLifecycleManager.QuitNow;
            ApplicationLifecycleManager.QuitNow = () => _quits++;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
            ApplicationLifecycleManager.QuitNow = _quitBefore;
            ResetLifecycleStatics();
        }

        static void ResetLifecycleStatics() =>
            typeof(ApplicationLifecycleManager).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);

        // ── The quit drain ─────────────────────────────────────────────────────────────────────

        [Test]
        public void WithNothingToLeave_TheQuitGoesAtOnce()
        {
            Assert.IsTrue(ApplicationLifecycleManager.DeferQuitOnce(), "no subscriber: no delay on quit");
            Assert.AreEqual(0, _quits, "and the drain never ran");
        }

        [Test]
        public void TheQuitWaits_UntilTheLeaveEnds_ThenGoesThrough()
        {
            var leave = new UniTaskCompletionSource();
            int asked = 0;
            ApplicationLifecycleManager.OnAppQuitRequested += () => { asked++; return leave.Task; };

            Assert.IsFalse(ApplicationLifecycleManager.DeferQuitOnce(), "the first request is held");
            Assert.IsFalse(ApplicationLifecycleManager.DeferQuitOnce(), "a second click while draining is held too");
            Assert.AreEqual(1, asked, "and starts no second drain");
            Assert.AreEqual(0, _quits, "nothing quits while the leave is out");

            leave.TrySetResult();

            Assert.AreEqual(1, _quits, "the leave landed: the drain quits");
            Assert.IsTrue(ApplicationLifecycleManager.DeferQuitOnce(), "and the quit it re-requested goes through");
        }

        [Test]
        public void AThrowingSubscriber_DoesNotTrapThePlayer()
        {
            ApplicationLifecycleManager.OnAppQuitRequested += () => throw new InvalidOperationException("boom");

            Assert.IsFalse(ApplicationLifecycleManager.DeferQuitOnce());
            Assert.AreEqual(1, _quits, "the game still closes");
        }

        // ── HostConnectionService ──────────────────────────────────────────────────────────────

        [Test]
        public void Quit_LeavesThePresenceLobbyAndTheParty()
        {
            var lobby = new LeaveCountingLobby { Active = FakeSession.Create(out _) };
            var party = new LeaveCountingParty { Active = FakeSession.Create(out _) };
            var hcs = NewOnlineService(lobby, party);

            var work = (UniTask)Call(hcs, "HandleAppQuitRequested");

            Assert.AreEqual(UniTaskStatus.Succeeded, work.Status, "the quit waits on this, and it finished");
            Assert.AreEqual(1, lobby.Leaves, "peers get a PlayerHasLeft push now, not a ghost row for ~30 s");
            Assert.AreEqual(1, party.Leaves, "we are not coming back: the party goes too");
            Assert.AreEqual(PartyState.Disconnected, StateOf(hcs));
        }

        [Test]
        public void Quit_WithNothingHeld_LeavesNothing()
        {
            var lobby = new LeaveCountingLobby();
            var party = new LeaveCountingParty();
            var hcs = NewOnlineService(lobby, party);

            var work = (UniTask)Call(hcs, "HandleAppQuitRequested");

            Assert.AreEqual(UniTaskStatus.Succeeded, work.Status);
            Assert.AreEqual(0, lobby.Leaves + party.Leaves);
        }

        [Test]
        public void Background_LeavesThePresenceLobbyOnly_AndResumeRejoins()
        {
            var lobby = new LeaveCountingLobby { Active = FakeSession.Create(out _) };
            var party = new LeaveCountingParty { Active = FakeSession.Create(out _) };
            var hcs = NewOnlineService(lobby, party);

            Call(hcs, "HandleAppPaused", true);
            Call(hcs, "HandleAppPaused", true);

            Assert.AreEqual(1, lobby.Leaves, "backgrounded: out of everyone's online list (once)");
            Assert.AreEqual(0, party.Leaves, "a short app switch keeps the party");
            Assert.AreNotEqual(PartyState.Disconnected, StateOf(hcs),
                "not Disconnected: IsInitialized would turn false and the resume would never rejoin");

            Call(hcs, "HandleAppPaused", false);

            Assert.AreEqual(1, lobby.Joins, "resumed: back in the lobby");
            Assert.IsNotNull(lobby.Active);
        }

        [Test]
        public void Resume_WithoutABackgroundLeave_DoesNothing()
        {
            var lobby = new LeaveCountingLobby { Active = FakeSession.Create(out _) };
            var hcs = NewOnlineService(lobby, new LeaveCountingParty());

            Call(hcs, "HandleAppPaused", false);

            Assert.AreEqual(0, lobby.Joins + lobby.Leaves);
        }

        // ── Fixtures ────────────────────────────────────────────────────────────────────────────

        HostConnectionService NewOnlineService(LeaveCountingLobby lobby, LeaveCountingParty party)
        {
            var data = Track(ScriptableObject.CreateInstance<HostConnectionDataSO>());
            data.PartyMembers = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.OnlinePlayers = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.LocalPlayerId = "me";
            data.LocalDisplayName = "Me";

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
            ((PartyStateMachine)Field(hcs, "_stateMachine").GetValue(hcs)).TryTransition(PartyState.InPresenceLobby);
            return hcs;
        }

        static PartyState StateOf(HostConnectionService hcs) =>
            ((PartyStateMachine)Field(hcs, "_stateMachine").GetValue(hcs)).CurrentState;

        static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(target.GetType().Name, name);

        static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);

        static object Call(object target, string method, params object[] args) =>
            (target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
             ?? throw new MissingMethodException(target.GetType().Name, method)).Invoke(target, args.Length == 0 ? null : args);

        sealed class LeaveCountingLobby : IPresenceLobbyService
        {
            public ISession Active;
            public int Leaves, Joins;
            public ISession ActiveLobby => Active;
            public Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }
            public bool ConsumeRosterDirty() => false;
            public bool ConsumeMembershipLost() => false;
            public void TakeDepartures(ISet<string> into) => into.Clear();
            public UniTask JoinOrCreateAsync(int maxPlayers)
            {
                if (Active != null) return UniTask.CompletedTask;   // idempotent, as the real one
                Joins++;
                Active = FakeSession.Create(out _);
                return UniTask.CompletedTask;
            }
            public UniTask LeaveAsync() { Leaves++; Active = null; return UniTask.CompletedTask; }
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask SavePropertiesAsync(Dictionary<string, PlayerProperty> properties, string operationName) => UniTask.CompletedTask;
            public void ForceReset() => Active = null;
            public UniTask ConvergeToCanonicalAsync(int maxPlayers) => UniTask.CompletedTask;
        }

        sealed class LeaveCountingParty : IPartySessionService
        {
            public ISession Active;
            public int Leaves;
            public ISession ActiveSession => Active;
#pragma warning disable CS0067 // the interface's event; nothing here raises it
            public event Action<string> PlayerLeaving;
#pragma warning restore CS0067
            public float CreatedAtUnscaledTime => -1000f;
            public bool ConsumeRosterChanged() => false;
            public void TakeDepartures(ISet<string> into) => into.Clear();
            public UniTask CreateAsync(int maxPlayers) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId, bool asSpectator) => UniTask.CompletedTask;
            public UniTask LeaveAsync() { Leaves++; Active = null; return UniTask.CompletedTask; }
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId) => UniTask.CompletedTask;
            public void ClearSession() => Active = null;
        }
    }
}
#endif
