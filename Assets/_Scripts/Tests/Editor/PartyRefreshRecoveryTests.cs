#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B31-B33 (Docs/PartySystem/BUGS.md), found in the first real two-machine multiplayer logs
    /// (Caleb and ys1, 2026-10-16). Each test pins the decision where it is made, with no
    /// NetworkManager and no UGS:
    /// <list type="bullet">
    /// <item>B31: the refresh stays under UGS Lobby's 1 GET/s/player (it was 1.33/s: a 429 about every 10 s
    /// while a party existed); since Block 5 step 8 it is one 20 s safety poll under the pushes.</item>
    /// <item>B32: leaving the shared presence lobby deletes it only when we are alone in it; a lobby
    /// that is gone is rejoined at once; a rejoin takes the party out of Reconnecting.</item>
    /// <item>B33: a new Play starts with no session, no offline flag and no party flags from the last.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class PartyRefreshRecoveryTests
    {
        readonly List<Object> _made = new();

        double _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 1000;
            LobbyReadPacer.Now = () => _clock;
            LobbyReadPacer.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
            LobbyReadPacer.Now = () => Time.realtimeSinceStartupAsDouble;
            LobbyReadPacer.Reset();
        }

        T Make<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _made.Add(so);
            return so;
        }

        /// <summary>
        /// A loaded asset, as the Play-start hooks see one: Unity's CreateInstance runs OnEnable (which
        /// registers it); Amoebius's test engine does not, so it is run here too. A second call is a no-op.
        /// </summary>
        T MakeLoaded<T>() where T : ScriptableObject
        {
            var so = Make<T>();
            typeof(T).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(so, null);
            return so;
        }

        // ── B31: the refresh's GETs fit UGS Lobby's 1 GET/s/player ───────────────────────────

        // Block 5 step 8: the pushes carry every change, so the poll became one safety tick that reads both
        // lobbies once. It replaced a 1.5 s tick (1.1 s boosted), a 6 s party read and a 4 s converge.

        [Test]
        public void SafetyPoll_FiresOncePerJitteredTwentySeconds()
        {
            var s = new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS);
            float lo = LobbyRefreshScheduler.SAFETY_POLL_SECONDS * (1f - LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION);
            float hi = LobbyRefreshScheduler.SAFETY_POLL_SECONDS * (1f + LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION);
            for (int round = 0; round < 20; round++)
            {
                float t = 0f;
                while (!s.ShouldFireNow(0.1f)) t += 0.1f;
                t += 0.1f;
                Assert.GreaterOrEqual(t, lo - 0.11f, $"round {round}: never sooner than {lo} s");
                Assert.LessOrEqual(t, hi + 0.11f, $"round {round}: never later than {hi} s");
            }
        }

        [Test]
        public void SafetyPoll_ComesSoonAfterAJoin()
        {
            var s = new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS);
            Assert.IsFalse(s.ShouldFireNow(1f));
            s.FireAfter(LobbyRefreshScheduler.FIRST_POLL_AFTER_JOIN_SECONDS);
            Assert.IsFalse(s.ShouldFireNow(LobbyRefreshScheduler.FIRST_POLL_AFTER_JOIN_SECONDS - 0.1f), "not before the settle");
            Assert.IsTrue(s.ShouldFireNow(0.2f), "the online list fills two seconds after a join, not twenty");
            Assert.IsFalse(s.ShouldFireNow(1f), "then back to the safety cadence");
        }

        [Test]
        public void SafetyPoll_StaysFarUnderTheReadCap()
        {
            const float limit = 1f; // UGS Lobby: Get 1 req/s per player
            float fastest = LobbyRefreshScheduler.SAFETY_POLL_SECONDS * (1f - LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION);
            float perSecond = 2f / fastest; // the presence read and the party read, once per tick
            Assert.Less(perSecond, 0.15f * limit, $"{perSecond:0.000} GETs/s");

            // What it replaced (B31): presence every 1.5 s plus the party every 6 s, 1.1 s / 15 s while boosted.
            Assert.Greater(1f / 1.5f + 1f / 6f, 0.8f, "the old resting cadence spent 83% of the cap (negative control)");
            Assert.Greater(1f / 1.1f + 1f / 15f, 0.95f, "and the boost 98% of it");
        }

        // B31 again (2026-10-24): the average was under the cap, but the presence and party reads went
        // out in the same second, and Caleb's and Gradi's logs refused 1,061 party reads against 12
        // presence ones. Spacing is what UGS counts.

        [Test]
        public void ReadPacer_HoldsEveryReadAtLeastASecondAfterTheLast()
        {
            Assert.IsTrue(LobbyReadPacer.TryTakeTurn(), "the presence read goes out");
            Assert.IsFalse(LobbyReadPacer.TryTakeTurn(), "the party read right behind it waits (it was the 429)");
            _clock += 0.9;
            Assert.IsFalse(LobbyReadPacer.TryTakeTurn(), "still inside UGS's one second");
            Assert.AreEqual(LobbyReadPacer.MIN_SPACING_SECONDS - 0.9, LobbyReadPacer.SecondsUntilNextRead(), 1e-9);
            _clock += 0.15;
            Assert.IsTrue(LobbyReadPacer.TryTakeTurn(), "1.05 s after the last read it goes");
            Assert.GreaterOrEqual(LobbyReadPacer.MIN_SPACING_SECONDS, 1.0, "never under UGS's 1 GET/s/player");
        }

        [Test]
        public void ReadPacer_ARefusedTurnClaimsNothing_AndANewPlayStartsFree()
        {
            Assert.IsTrue(LobbyReadPacer.TryTakeTurn());
            _clock += 0.5;
            Assert.IsFalse(LobbyReadPacer.TryTakeTurn());
            _clock += 0.55;
            Assert.IsTrue(LobbyReadPacer.TryTakeTurn(), "the refused try at +0.5 s did not push the slot out");
            LobbyReadPacer.Reset();
            Assert.AreEqual(0, LobbyReadPacer.SecondsUntilNextRead(), "the Play-start reset frees the slot");
            Assert.IsNotNull(typeof(LobbyReadPacer).GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>(), "Reset runs when Play starts");
        }

        // ── B32: the shared presence lobby survives its host leaving ─────────────────────────

        [Test]
        public void PresenceLeave_DeletesOnlyALobbyWeHostAlone()
        {
            Assert.IsTrue(PresenceLobbyService.DeletesOnLeave(isHost: true, playerCount: 1), "alone: clean it up");
            Assert.IsTrue(PresenceLobbyService.DeletesOnLeave(isHost: true, playerCount: 0));
            Assert.IsFalse(PresenceLobbyService.DeletesOnLeave(isHost: true, playerCount: 2),
                "ys1 hosted the lobby Caleb was in: deleting it evicted him");
            Assert.IsFalse(PresenceLobbyService.DeletesOnLeave(isHost: false, playerCount: 1), "a member only ever leaves");
        }

        [Test]
        public void PresenceRefresh_GoneRejoinsAtOnce_OtherErrorsWaitForThree()
        {
            Assert.IsTrue(HostConnectionService.PresenceErrorForcesRejoin(UgsFailureClass.Gone, 1),
                "NotInLobby: the lobby will not come back, so no 3 x 10 s wait in a match");
            Assert.IsFalse(HostConnectionService.PresenceErrorForcesRejoin(UgsFailureClass.Transient, 1),
                "one transient error is not a lost lobby (negative control)");
            Assert.IsFalse(HostConnectionService.PresenceErrorForcesRejoin(UgsFailureClass.Transient, 2));
            Assert.IsTrue(HostConnectionService.PresenceErrorForcesRejoin(UgsFailureClass.Transient, 3));
        }

        [Test]
        public void PresenceRejoin_LeavesReconnecting_ForWhereThePartyIs()
        {
            Assert.AreEqual(PartyState.InParty, HostConnectionService.StateAfterPresenceRejoin(partySessionLive: true),
                "Caleb's party kept running; it belongs back in InParty");
            Assert.AreEqual(PartyState.InPresenceLobby, HostConnectionService.StateAfterPresenceRejoin(partySessionLive: false));

            foreach (bool live in new[] { true, false })
            {
                var machine = new PartyStateMachine();
                Assert.IsTrue(machine.TryTransition(PartyState.InPresenceLobby));
                Assert.IsTrue(machine.TryTransition(PartyState.HostingParty));
                Assert.IsTrue(machine.TryTransition(PartyState.InParty));
                Assert.IsTrue(machine.TryTransition(PartyState.Reconnecting));
                Assert.IsTrue(machine.TryTransition(HostConnectionService.StateAfterPresenceRejoin(live)),
                    $"Reconnecting -> {HostConnectionService.StateAfterPresenceRejoin(live)} is a legal way out");
                Assert.AreNotEqual(PartyState.Reconnecting, machine.CurrentState);
            }
        }

        // ── B33: a new Play does not start inside the last Play's session ────────────────────

        [Test]
        public void GameData_PlayStartClearsTheCarriedOfflineFlag()
        {
            var data = Make<GameDataSO>();
            data.IsOfflineSession = true;
            Assert.IsNull(data.ClearPlaySessionState(), "no session to carry here");
            Assert.IsFalse(data.IsOfflineSession);
            Assert.IsNull(data.ActiveSession);
        }

        [Test]
        public void GameData_ThePlayStartHookReachesEveryLoadedAsset()
        {
            var data = MakeLoaded<GameDataSO>();
            data.IsOfflineSession = true;
            InvokeStatic(typeof(GameDataSO), "ClearSessionStateFromLastPlay");
            Assert.IsFalse(data.IsOfflineSession, "the SubsystemRegistration hook cleared the asset it never named");
        }

        [Test]
        public void ConnectionData_PlayStartClearsThePartyFlags()
        {
            var data = MakeLoaded<HostConnectionDataSO>();
            data.IsConnected = data.IsPresenceLobbyHost = data.IsPartyHost = data.PartyFormedByInvite = data.IsSpectating = true;
            InvokeStatic(typeof(HostConnectionDataSO), "ClearFlagsFromLastPlay");
            Assert.IsFalse(data.IsConnected);
            Assert.IsFalse(data.IsPresenceLobbyHost);
            Assert.IsFalse(data.IsPartyHost, "a new Play is nobody's host yet");
            Assert.IsFalse(data.PartyFormedByInvite);
            Assert.IsFalse(data.IsSpectating, "nor anybody's spectator");
        }

        [Test]
        public void PlayStartHooks_ForgetADestroyedAsset()
        {
            var data = MakeLoaded<GameDataSO>();
            typeof(GameDataSO).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(data, null);
            data.IsOfflineSession = true;
            InvokeStatic(typeof(GameDataSO), "ClearSessionStateFromLastPlay");
            Assert.IsTrue(data.IsOfflineSession, "an unloaded asset is no longer reached (negative control)");
        }

        static void InvokeStatic(System.Type type, string method)
        {
            var m = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, $"{type.Name}.{method} exists");
            Assert.IsNotNull(m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>(), $"{method} runs when Play starts");
            m.Invoke(null, null);
        }
    }
}
#endif
