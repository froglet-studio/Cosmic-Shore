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
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Block 5 (push instead of poll), first four steps, ported from the August presence work on
    /// Ys-rescue-2026-08-24 (Docs/PartySystem/BRANCH_AUDIT_2026-10.md §2).
    ///
    /// WHY: the SDK's stale-index fault voids 12-43% of lobby reads in MPPM. It used to unwind past the
    /// party sync and our own publish (so our party size reached peers on a coin flip) and was counted
    /// nowhere; a background presence rejoin cleared a live party roster; and the party session's own
    /// roster events were ignored, so a join waited for the next poll.
    /// </summary>
    [TestFixture]
    public class PartyPushAndVoidedReadTests
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

        // ── Step 4: the party session's push ───────────────────────────────────────────────────

        [Test]
        public void PartySession_EachRosterEvent_RaisesTheFlagOnce()
        {
            var svc = new PartySessionService(NewData(), Track(ScriptableObject.CreateInstance<GameDataSO>()), null);
            var session = FakeSession.Create(out var fake);
            svc.Attach(session);

            Assert.IsFalse(svc.ConsumeRosterChanged(), "nothing pushed yet");
            fake.Raise("PlayerJoined", "p2");
            Assert.IsTrue(svc.ConsumeRosterChanged(), "a join is a roster change");
            Assert.IsFalse(svc.ConsumeRosterChanged(), "consumed once");

            fake.Raise("PlayerHasLeft", "p2");
            fake.Raise("PlayerPropertiesChanged");
            Assert.IsTrue(svc.ConsumeRosterChanged(), "two pushes before a drain are one sync");
            Assert.IsFalse(svc.ConsumeRosterChanged());
        }

        [Test]
        public void PartySession_Detach_StopsListening()
        {
            var svc = new PartySessionService(NewData(), Track(ScriptableObject.CreateInstance<GameDataSO>()), null);
            var session = FakeSession.Create(out var fake);
            svc.Attach(session);
            Assert.AreEqual(4, fake.HandlerCount, "PlayerLeaving, PlayerJoined, PlayerHasLeft, PlayerPropertiesChanged");

            svc.Detach(session);
            Assert.AreEqual(0, fake.HandlerCount, "no handler outlives the session it was attached to");
            fake.Raise("PlayerJoined", "p2");
            Assert.IsFalse(svc.ConsumeRosterChanged());
        }

        // ── Step 3: a background rejoin keeps a live party ─────────────────────────────────────

        [Test]
        public void SeedLocalPlayer_WithoutClear_IsIdempotent_AndKeepsTheParty()
        {
            var data = NewData();
            var members = new PartyMemberService(data, new SoapPartyEventBus(data));
            data.PartyMembers.Add(new PartyPlayerData("p2", "Two", 1));

            members.SeedLocalPlayer(clearFirst: false);
            members.SeedLocalPlayer(clearFirst: false);

            Assert.AreEqual(2, data.PartyMembers.Count, "the remote member stays and the local player is seated once");
        }

        [Test]
        public void PostJoin_WithALiveParty_DoesNotClearTheRoster()
        {
            var data = NewData();
            data.PartyMembers.Add(data.LocalPlayerData);
            data.PartyMembers.Add(new PartyPlayerData("p2", "Two", 1));
            var party = new StubPartySession { Active = FakeSession.Create(out _) };
            var hcs = NewService(data, party, new StubPresenceLobby { Active = FakeSession.Create(out _) });

            Call(hcs, "ApplyPostLobbyJoinState");

            Assert.AreEqual(2, data.PartyMembers.Count, "a presence rejoin no longer wipes a party that is still alive");
        }

        [Test]
        public void PostJoin_WithNoParty_StillStartsFromJustTheLocalPlayer()
        {
            var data = NewData();
            data.PartyMembers.Add(new PartyPlayerData("stale", "Old", 0));
            var hcs = NewService(data, new StubPartySession(), new StubPresenceLobby { Active = FakeSession.Create(out _) });

            Call(hcs, "ApplyPostLobbyJoinState");

            Assert.AreEqual(1, data.PartyMembers.Count, "first join is unchanged: the list is reset to the local player");
            Assert.AreEqual(data.LocalPlayerId, data.PartyMembers[0].PlayerId);
        }

        // ── Steps 1-2: a voided read is counted and no longer skips the publish ─────────────────

        [Test]
        public void VoidedPresenceRead_IsCounted_AndThePublishStillGoesOut()
        {
            var data = NewData();
            var lobby = new StubPresenceLobby
            {
                Active = FakeSession.Create(out _),
                OnRefresh = () => throw new SessionException("Index was out of range. Must be non-negative", SessionError.Unknown, null),
            };
            var hcs = NewService(data, new StubPartySession(), lobby, out int[] saves);
            SetField(hcs, "_consecutiveRefreshErrors", 2);

            Call(hcs, "RefreshAsync");

            Assert.AreEqual(1, UgsRequestTelemetry.Total(UgsRequestCounter.Benign), "the voided read is counted");
            Assert.AreEqual(1, saves[0], "our party state is published even though the read was voided");
            Assert.AreEqual(2, GetField<int>(hcs, "_consecutiveRefreshErrors"), "a voided read is no evidence of health");
        }

        [Test]
        public void HealthyPresenceRead_PublishesAndClearsTheErrorCount()
        {
            var data = NewData();
            var hcs = NewService(data, new StubPartySession(), new StubPresenceLobby { Active = FakeSession.Create(out _) }, out int[] saves);
            SetField(hcs, "_consecutiveRefreshErrors", 2);

            Call(hcs, "RefreshAsync");

            Assert.AreEqual(0, UgsRequestTelemetry.Total(UgsRequestCounter.Benign));
            Assert.AreEqual(1, saves[0]);
            Assert.AreEqual(0, GetField<int>(hcs, "_consecutiveRefreshErrors"));
        }

        [Test]
        public void RateLimitedPresenceRead_StillSkipsThePublish()
        {
            // Negative control: only the benign fault is absorbed at the read. A 429 means the lobby
            // itself is in trouble, so it keeps the outer catch's back-off and publishes nothing.
            var data = NewData();
            var lobby = new StubPresenceLobby
            {
                Active = FakeSession.Create(out _),
                OnRefresh = () => throw new SessionException("rate limited", SessionError.RateLimitExceeded, null),
            };
            var hcs = NewService(data, new StubPartySession(), lobby, out int[] saves);

            Call(hcs, "RefreshAsync");

            Assert.AreEqual(0, saves[0], "nothing is published into a lobby that is throttling us");
            Assert.AreEqual(1, UgsRequestTelemetry.Total(UgsRequestCounter.RateLimited));
            Assert.AreEqual(0, UgsRequestTelemetry.Total(UgsRequestCounter.Benign));
        }

        // ── Fixtures ───────────────────────────────────────────────────────────────────────────

        HostConnectionDataSO NewData()
        {
            var data = Track(ScriptableObject.CreateInstance<HostConnectionDataSO>());
            data.PartyMembers = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.LocalPlayerId = "me";
            data.LocalDisplayName = "Me";
            return data;
        }

        HostConnectionService NewService(HostConnectionDataSO data, IPartySessionService party, IPresenceLobbyService lobby) =>
            NewService(data, party, lobby, out _);

        HostConnectionService NewService(HostConnectionDataSO data, IPartySessionService party, IPresenceLobbyService lobby,
                                         out int[] saves)
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
            SetField(hcs, "_partySessionService", party);
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

        static void Call(object target, string method)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new MissingMethodException(target.GetType().Name, method);
            // Optional parameters take their defaults (RefreshAsync's fetchFromServer, Block 5 step 5).
            var ps = m.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
            m.Invoke(target, ps.Length == 0 ? null : args);
        }

        /// <summary>An ISession whose events can be raised, whose roster is empty and whose player takes any property.</summary>
        public class FakeSession : DispatchProxy
        {
            readonly Dictionary<string, Delegate> _handlers = new();
            public int HandlerCount { get { int n = 0; foreach (var d in _handlers.Values) if (d != null) n += d.GetInvocationList().Length; return n; } }

            public static ISession Create(out FakeSession fake)
            {
                var proxy = Create<ISession, FakeSession>();
                fake = (FakeSession)(object)proxy;
                return proxy;
            }

            /// <summary>Runs inside LeaveAsync / DeleteAsync, before they complete (as the SDK raises Deleted).</summary>
            public Action OnLeave;
            public bool IsHostValue;

            public void Raise(string evt, params object[] args)
            {
                if (_handlers.TryGetValue(evt, out var d) && d != null) d.DynamicInvoke(args);
            }

            protected override object Invoke(MethodInfo m, object[] args)
            {
                if (m.Name.StartsWith("add_")) { var k = m.Name[4..]; _handlers.TryGetValue(k, out var d); _handlers[k] = Delegate.Combine(d, (Delegate)args[0]); return null; }
                if (m.Name.StartsWith("remove_")) { var k = m.Name[7..]; _handlers.TryGetValue(k, out var d); _handlers[k] = Delegate.Remove(d, (Delegate)args[0]); return null; }
                switch (m.Name)
                {
                    case "get_Id": return "session";
                    case "get_Players": return Array.Empty<IReadOnlyPlayer>();
                    case "get_CurrentPlayer": return DispatchProxy.Create<Unity.Services.Multiplayer.IPlayer, AnyPlayer>();
                    case "get_IsHost": return IsHostValue;
                    case "AsHost": return null;
                    case "LeaveAsync": OnLeave?.Invoke(); return System.Threading.Tasks.Task.CompletedTask;
                }
                if (m.ReturnType == typeof(System.Threading.Tasks.Task)) return System.Threading.Tasks.Task.CompletedTask;
                return m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
            }
        }

        public class AnyPlayer : DispatchProxy
        {
            protected override object Invoke(MethodInfo m, object[] args) =>
                m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
        }

        sealed class StubPartySession : IPartySessionService
        {
            public ISession Active;
            public ISession ActiveSession => Active;
#pragma warning disable CS0067 // the interface's event; nothing here raises it
            public event Action<string> PlayerLeaving;
#pragma warning restore CS0067
            public float CreatedAtUnscaledTime => -1000f;
            public bool ConsumeRosterChanged() => false;
            public UniTask CreateAsync(int maxPlayers) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId) => UniTask.CompletedTask;
            public UniTask JoinByIdAsync(string sessionId, bool asSpectator) => UniTask.CompletedTask;
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() => UniTask.CompletedTask;
            public UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId) => UniTask.CompletedTask;
            public void ClearSession() => Active = null;
        }

        sealed class StubPresenceLobby : IPresenceLobbyService
        {
            public ISession Active;
            public Action OnRefresh;
            public ISession ActiveLobby => Active;
            public Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }
            public bool ConsumeRosterDirty() => false;
            public bool ConsumeMembershipLost() => false;
            public UniTask JoinOrCreateAsync(int maxPlayers) => UniTask.CompletedTask;
            public UniTask LeaveAsync() => UniTask.CompletedTask;
            public UniTask RefreshAsync() { OnRefresh?.Invoke(); return UniTask.CompletedTask; }
            public UniTask SavePropertiesAsync(Dictionary<string, PlayerProperty> properties, string operationName) => UniTask.CompletedTask;
            public void ForceReset() { }
            public UniTask ConvergeToCanonicalAsync(int maxPlayers) => UniTask.CompletedTask;
        }
    }
}
#endif
