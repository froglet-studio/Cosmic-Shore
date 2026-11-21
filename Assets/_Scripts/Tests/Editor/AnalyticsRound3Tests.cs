#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Data;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The 2026-11-07 analytics round (Shombith): sessions (session_started, session_ended's id and
    /// length), balance (game_completed's score, placement, players_in_match, ai_difficulty,
    /// behind_by), quits (match_abandoned), invite attribution (invite_id, inviter_id) and
    /// client_exception - all of them Froglet Analytics only.
    ///
    /// WHY THIS MATTERS:
    /// 1. UGS DISCARDS AN EVENT WITH AN UNDECLARED PARAMETER, so every new field must be stripped and
    ///    every new event dropped before UGS (the mirror itself is AnalyticsFrogletOnlyTests).
    /// 2. CLIENT_EXCEPTION MUST NEVER CARRY MESSAGE TEXT (it can hold names): only a hash of it, its
    ///    type and a code frame. It is fed off the main thread, so its log must count right under
    ///    concurrent writers.
    /// 3. BOTH ENDS OF AN INVITE MUST COMPUTE THE SAME invite_id, or the funnel cannot join them.
    /// 4. A SESSION SURVIVES A SHORT TRIP TO THE BACKGROUND and does not count time spent there.
    /// </summary>
    [TestFixture]
    public class AnalyticsRound3Tests
    {
        static JObject s_schema;

        static JObject Events
        {
            get
            {
                if (s_schema == null)
                {
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Analytics", "EVENT_SCHEMA.json"));
                    Assert.IsTrue(File.Exists(path), "the event contract: " + path);
                    s_schema = JObject.Parse(File.ReadAllText(path));
                }
                return (JObject)s_schema["events"];
            }
        }

        static List<string> Declared(string eventName) =>
            ((JObject)Events[eventName]["parameters"]).Properties().Select(p => p.Name).ToList();

        static void AssertKeysDeclared(string eventName, IDictionary<string, object> payload)
        {
            var declared = Declared(eventName);
            foreach (var key in payload.Keys)
                Assert.That(declared, Does.Contain(key), eventName + "." + key + " is in EVENT_SCHEMA.json");
        }

        static bool IsFrogletOnly(JToken entry) =>
            entry is JObject o && o["sinks"] is JArray sinks && sinks.All(s => (string)s != "ugs");

        // ── The schema and the UGS filter ──────────────────────────────────────

        [Test]
        public void NewEventsAreFrogletOnlyInTheSchemaAndNeverReachUgs()
        {
            foreach (var name in new[] { UGSKeys.EventSessionStarted, UGSKeys.EventMatchAbandoned, UGSKeys.EventClientException })
            {
                Assert.IsNotNull(Events[name], name + " is in EVENT_SCHEMA.json");
                Assert.IsTrue(IsFrogletOnly(Events[name]), name + " is marked \"sinks\": [\"froglet\"]");
                Assert.IsFalse(UgsAnalyticsSink.TryFilterForUgs(name, new Dictionary<string, object> { { "x", 1 } }, out _),
                    name + " is dropped before UGS");
            }
        }

        [Test]
        public void NewParametersOnUgsEventsAreFrogletOnly()
        {
            var expected = new[]
            {
                "session_ended.session_id", "session_ended.session_seconds",
                "game_completed.score", "game_completed.placement", "game_completed.players_in_match",
                "game_completed.ai_difficulty", "game_completed.behind_by",
                "party_invite_sent.invite_id", "party_invite_received.invite_id",
                "party_joined.invite_id", "party_joined.inviter_id"
            };

            foreach (var field in expected)
            {
                var parts = field.Split('.');
                var entry = Events[parts[0]]["parameters"][parts[1]];
                Assert.IsNotNull(entry, field + " is declared");
                Assert.IsTrue(IsFrogletOnly(entry), field + " is Froglet-only");
                Assert.That(UgsAnalyticsSink.FrogletOnlyFields, Does.Contain(field));
            }

            Assert.AreEqual("P1", (string)Events["party_joined"]["parameters"]["inviter_id"]["privacy"],
                "inviter_id is a UGS player id");
        }

        [Test]
        public void SessionEndedLosesOnlyItsNewFieldsForUgs()
        {
            var payload = AnalyticsServiceFacade.BuildSessionEndedParameters("pause", "OpenArcade", "MainMenu", "abc", 61.4);
            AssertKeysDeclared(UGSKeys.EventSessionEnded, payload);
            Assert.AreEqual(61, payload[UGSKeys.ParamSessionSeconds]);

            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventSessionEnded, payload, out var ugs));
            CollectionAssert.AreEquivalent(new[] { "reason", "last_ui_action", "app_state" }, ugs.Keys);
            Assert.AreEqual(5, payload.Count, "the shared payload keeps both for Froglet Analytics");
        }

        [Test]
        public void GameCompletedBalanceFieldsAreStrippedForUgs()
        {
            var payload = new Dictionary<string, object> { { "game_mode", "Joust" }, { UGSKeys.ParamAiDifficulty, "Hard" } };
            new MatchStanding(12f, 2, 4, 3f).AddTo(payload);
            AssertKeysDeclared(UGSKeys.EventGameCompleted, payload);

            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventGameCompleted, payload, out var ugs));
            CollectionAssert.AreEquivalent(new[] { "game_mode" }, ugs.Keys);
        }

        [Test]
        public void InviteFieldsAreStrippedForUgsAndKeepTheOldIds()
        {
            var sent = AnalyticsServiceFacade.BuildPartyInviteParameters("target_id", "B", "0123456789abcdef");
            AssertKeysDeclared(UGSKeys.EventPartyInviteSent, sent);
            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventPartyInviteSent, sent, out var ugsSent));
            CollectionAssert.AreEquivalent(new[] { "target_id" }, ugsSent.Keys);

            var received = AnalyticsServiceFacade.BuildPartyInviteParameters("host_id", "A", "0123456789abcdef");
            AssertKeysDeclared(UGSKeys.EventPartyInviteReceived, received);

            var joined = AnalyticsServiceFacade.BuildPartyJoinedParameters("0123456789abcdef", "A");
            AssertKeysDeclared(UGSKeys.EventPartyJoined, joined);
            Assert.IsTrue(UgsAnalyticsSink.TryFilterForUgs(UGSKeys.EventPartyJoined, joined, out var ugsJoined));
            CollectionAssert.IsEmpty(ugsJoined.Keys, "UGS's party_joined stays parameterless");
        }

        [Test]
        public void AnInviteWithNoIdSendsNoIdField()
        {
            var sent = AnalyticsServiceFacade.BuildPartyInviteParameters("target_id", "B", null);
            CollectionAssert.AreEquivalent(new[] { "target_id" }, sent.Keys);
            CollectionAssert.IsEmpty(AnalyticsServiceFacade.BuildPartyJoinedParameters(null, "A"),
                "a join no invite led to carries nothing, not an inviter alone");
        }

        [Test]
        public void SessionStartedCarriesExactlyTheSchemaParameters()
        {
            var p = AnalyticsServiceFacade.BuildSessionStartedParameters("s1", AnalyticsSessionClock.LaunchResume);
            CollectionAssert.AreEquivalent(Declared(UGSKeys.EventSessionStarted), p.Keys);
            Assert.AreEqual("resume", p[UGSKeys.ParamLaunchKind]);
        }

        [Test]
        public void ClientExceptionCarriesExactlyTheSchemaParameters()
        {
            var p = AnalyticsServiceFacade.BuildClientExceptionParameters(
                new ClientExceptionLog.Entry("NullReferenceException", "CosmicShore.Gameplay.Foo.Bar", "Menu_Main", "8d71079e5e8d", 3));
            CollectionAssert.AreEquivalent(Declared(UGSKeys.EventClientException), p.Keys);
            Assert.AreEqual(3, p[UGSKeys.ParamCount]);
        }

        // ── match_abandoned ────────────────────────────────────────────────────

        static Dictionary<string, object> GameStartedLike() => new()
        {
            { "match_id", "m1" }, { "party_id", "p1" }, { "invite_triggered", false },
            { "player_ids", "A,B" }, { "player_count_human", 2 }, { "player_count_ai", 1 },
            { "game_mode", "Joust" }, { "intensity", 2 }, { "vessel_class", "Rhino" },
            { "player_count", 3 }, { "ai_count", 1 }, { "is_multiplayer", true },
            { UGSKeys.ParamElement, "Mass" }, { UGSKeys.ParamDomain, "Jade" },
            { UGSKeys.ParamHumansOnMyDomain, 1 }, { UGSKeys.ParamHumansOnOtherDomains, 1 },
            { UGSKeys.ParamAiOnMyDomain, 0 }, { UGSKeys.ParamAiOnOtherDomains, 1 }
        };

        [Test]
        public void MatchAbandonedRepeatsGameStartedWithoutThePlayerList()
        {
            var start = GameStartedLike();
            var p = AnalyticsServiceFacade.BuildMatchAbandonedParameters(start, 95, 80.5f,
                MatchAbandonReason.QuitToMenu, "Hard", 12f, 2);

            AssertKeysDeclared(UGSKeys.EventMatchAbandoned, p);
            Assert.IsFalse(p.ContainsKey("player_ids"), "only game_started carries the player list");
            Assert.AreEqual("Joust", p["game_mode"]);
            Assert.AreEqual(1, p[UGSKeys.ParamAiOnOtherDomains], "the setup fields ride along");
            Assert.AreEqual(95, p[UGSKeys.ParamElapsedSeconds]);
            Assert.AreEqual("quit_to_menu", p[UGSKeys.ParamReason]);
            Assert.IsTrue(start.ContainsKey("player_ids"), "the game_started snapshot is not modified");

            // Every field the schema requires is sent.
            foreach (var prop in ((JObject)Events[UGSKeys.EventMatchAbandoned]["parameters"]).Properties())
                if (prop.Value["optional"] == null || !(bool)prop.Value["optional"])
                    Assert.That(p.Keys, Does.Contain(prop.Name), "required match_abandoned." + prop.Name);
        }

        [Test]
        public void MatchAbandonedLeavesOutWhatItDoesNotKnow()
        {
            var p = AnalyticsServiceFacade.BuildMatchAbandonedParameters(GameStartedLike(), -3, null,
                MatchAbandonReason.AppQuit, null, null, null);
            Assert.IsFalse(p.ContainsKey(UGSKeys.ParamFlightTimeSeconds));
            Assert.IsFalse(p.ContainsKey(UGSKeys.ParamAiDifficulty));
            Assert.IsFalse(p.ContainsKey(UGSKeys.ParamScoreAtQuit));
            Assert.IsFalse(p.ContainsKey(UGSKeys.ParamPlacementAtQuit));
            Assert.AreEqual(0, p[UGSKeys.ParamElapsedSeconds], "never negative");
            Assert.AreEqual("app_quit", p[UGSKeys.ParamReason]);
        }

        [Test]
        public void SceneExitReasonIsDisconnectOnlyWhenTheLinkWent()
        {
            Assert.AreEqual("quit_to_menu", MatchAbandonReason.ForSceneExit(false, false));
            Assert.AreEqual("disconnect", MatchAbandonReason.ForSceneExit(true, false));
            Assert.AreEqual("disconnect", MatchAbandonReason.ForSceneExit(false, true));
        }

        // ── game_completed standing ────────────────────────────────────────────

        [Test]
        public void StandingReadsTheLocalRowFromTheRankedResults()
        {
            // Points mode, best first: 30, 20 (local), 10.
            Assert.IsTrue(MatchStanding.TryResolve(new List<float> { 30f, 20f, 10f }, 1, false, out var s));
            Assert.AreEqual(20f, s.Score);
            Assert.AreEqual(2, s.Placement);
            Assert.AreEqual(3, s.PlayersInMatch);
            Assert.AreEqual(10f, s.BehindBy, 1e-4f);
        }

        [Test]
        public void StandingGapWorksForGolfRules()
        {
            // Finish times, best (lowest) first: 61.0 s, 64.5 s (local).
            Assert.IsTrue(MatchStanding.TryResolve(new List<float> { 61f, 64.5f }, 1, false, out var s));
            Assert.AreEqual(2, s.Placement);
            Assert.AreEqual(3.5f, s.BehindBy, 1e-4f, "positive gap whatever the direction");
        }

        [Test]
        public void AWinnerIsBehindByNothing()
        {
            // A team win where a teammate topped the board: the side won, so no gap.
            Assert.IsTrue(MatchStanding.TryResolve(new List<float> { 50f, 40f, 45f }, 1, true, out var s));
            Assert.AreEqual(0f, s.BehindBy);
            Assert.AreEqual(2, s.Placement);
        }

        [Test]
        public void NoLocalRowMeansNoStanding()
        {
            Assert.IsFalse(MatchStanding.TryResolve(new List<float> { 1f, 2f }, -1, null, out _));
            Assert.IsFalse(MatchStanding.TryResolve(new List<float>(), 0, null, out _));
            Assert.IsFalse(MatchStanding.TryResolve(null, 0, null, out _));
        }

        [Test]
        public void AiDifficultyNamesOneLevelOrMixed()
        {
            Assert.IsNull(MatchStanding.AiDifficultyLabel(new AIDifficulty[0]), "no AI opponents: nothing sent");
            Assert.AreEqual("Hard", MatchStanding.AiDifficultyLabel(new[] { AIDifficulty.Hard, AIDifficulty.Hard }));
            Assert.AreEqual("Mixed", MatchStanding.AiDifficultyLabel(new[] { AIDifficulty.Easy, AIDifficulty.Legendary }));
        }

        [Test]
        public void PlacementAtQuitNeedsSomeScoreOnTheBoard()
        {
            Assert.IsNull(MatchStanding.PlacementAmong(new List<float> { 0f, 0f, 0f }, 0f), "a row of zeros is no order");
            Assert.AreEqual(1, MatchStanding.PlacementAmong(new List<float> { 5f, 3f }, 5f));
            Assert.AreEqual(3, MatchStanding.PlacementAmong(new List<float> { 5f, 5f, 3f }, 3f), "two ahead of 3");
            Assert.AreEqual(1, MatchStanding.PlacementAmong(new List<float> { 5f, 5f }, 5f), "ties share a rank");
        }

        // ── Sessions ───────────────────────────────────────────────────────────

        [Test]
        public void ASessionStartsColdAndSurvivesAShortBackground()
        {
            var t0 = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
            var clock = new AnalyticsSessionClock(t0);
            string id = clock.SessionId;
            Assert.AreEqual("cold", clock.LaunchKind);
            Assert.AreEqual(32, id.Length);

            clock.EnterBackground(t0.AddMinutes(10));
            Assert.IsFalse(clock.ReturnToForeground(t0.AddMinutes(25)), "15 minutes away is the same session");
            Assert.AreEqual(id, clock.SessionId);
            Assert.AreEqual(15 * 60, clock.ForegroundSeconds(t0.AddMinutes(30)), 1e-6,
                "10 minutes before the pause and 5 after it; the 15 away are not counted");
        }

        [Test]
        public void ALongBackgroundStartsAResumeSession()
        {
            var t0 = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
            var clock = new AnalyticsSessionClock(t0) { StartRecorded = true };
            string id = clock.SessionId;

            clock.EnterBackground(t0.AddMinutes(5));
            Assert.IsTrue(clock.ReturnToForeground(t0.AddMinutes(40)));
            Assert.AreNotEqual(id, clock.SessionId);
            Assert.AreEqual("resume", clock.LaunchKind);
            Assert.IsFalse(clock.StartRecorded, "the new session's session_started is still to send");
            Assert.AreEqual(60, clock.ForegroundSeconds(t0.AddMinutes(41)), 1e-6);
        }

        [Test]
        public void ForegroundTimeStopsInTheBackground()
        {
            var t0 = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
            var clock = new AnalyticsSessionClock(t0);
            clock.EnterBackground(t0.AddSeconds(90));
            clock.EnterBackground(t0.AddSeconds(200)); // a second pause notice changes nothing
            Assert.AreEqual(90, clock.ForegroundSeconds(t0.AddHours(3)), 1e-6);
            Assert.IsFalse(new AnalyticsSessionClock(t0).ReturnToForeground(t0.AddHours(1)), "never away: no new session");
        }

        // ── client_exception ───────────────────────────────────────────────────

        const string UnityLogErrorTrace =
            "UnityEngine.Debug:LogError (object)\n" +
            "CosmicShore.Utility.CSDebug:LogError (string)\n" +
            "CosmicShore.Gameplay.SceneLoader:Start () (at Assets/_Scripts/System/SceneLoader.cs:97)\n";

        const string UnityExceptionTrace =
            "CosmicShore.Gameplay.VesselStatus.get_Speed () [0x00000] in <abc>:0\n" +
            "CosmicShore.UI.SpeedHud.Update () (at Assets/_Scripts/UI/SpeedHud.cs:42)\n";

        [Test]
        public void TopFrameIsTheFirstProjectFrameNotTheLogger()
        {
            Assert.AreEqual("CosmicShore.Gameplay.SceneLoader.Start", ClientExceptionLog.TopFrameOf(UnityLogErrorTrace));
            Assert.AreEqual("CosmicShore.Gameplay.VesselStatus.get_Speed", ClientExceptionLog.TopFrameOf(UnityExceptionTrace));
            Assert.AreEqual("UnityEngine.Rendering.Foo.Bar", ClientExceptionLog.TopFrameOf("UnityEngine.Rendering.Foo:Bar ()\n"),
                "no project frame: the first frame");
            Assert.AreEqual("unknown", ClientExceptionLog.TopFrameOf(""));
            Assert.AreEqual("unknown", ClientExceptionLog.TopFrameOf("Rethrow as Some text with spaces"));
        }

        [Test]
        public void ExceptionTypeIsANameNeverMessageText()
        {
            Assert.AreEqual("NullReferenceException",
                ClientExceptionLog.ExceptionTypeOf("NullReferenceException: Object reference not set", true));
            Assert.AreEqual("Exception", ClientExceptionLog.ExceptionTypeOf("player Bob said hi: oops", true),
                "a head with spaces is message text, not a type");
            Assert.AreEqual("LogError", ClientExceptionLog.ExceptionTypeOf("[SceneLoader] gameData was not injected", false));
        }

        [Test]
        public void MessageHashIsStableAndIgnoresDigits()
        {
            const string message = "NullReferenceException: Object reference not set to an instance of an object";
            Assert.AreEqual("8d71079e5e8d", ClientExceptionLog.MessageHashOf(message), "the same hash on every run and platform");
            Assert.AreEqual(ClientExceptionLog.MessageHashOf("took 312 ms for seat 4"),
                ClientExceptionLog.MessageHashOf("took 2041 ms for seat 1"));
            Assert.AreNotEqual(ClientExceptionLog.MessageHashOf("seat lost"), ClientExceptionLog.MessageHashOf("seat found"));
            Assert.AreEqual(ClientExceptionLog.MessageHashOf("a fault\nat line 1"), ClientExceptionLog.MessageHashOf("a fault\nat line 2"),
                "only the first line counts");
        }

        [Test]
        public void ThePayloadHoldsNoMessageText()
        {
            const string message = "Player DisplayNameBob left session 9f2c";
            var log = new ClientExceptionLog();
            log.Observe(ClientExceptionLog.ExceptionTypeOf(message, false), ClientExceptionLog.TopFrameOf(UnityLogErrorTrace),
                "Menu_Main", ClientExceptionLog.MessageHashOf(message));
            var p = AnalyticsServiceFacade.BuildClientExceptionParameters(log.TakeHeld().Single());
            foreach (var value in p.Values)
                StringAssert.DoesNotContain("Bob", Convert.ToString(value));
        }

        [Test]
        public void RepeatsFoldIntoOneHeldEntry()
        {
            var log = new ClientExceptionLog();
            Assert.IsTrue(log.Observe("LogError", "A.B.C", "S", "h1"), "first sighting is new");
            Assert.IsFalse(log.Observe("LogError", "A.B.C", "S", "h1"));
            Assert.IsFalse(log.Observe("LogError", "A.B.C", "S", "h1"));

            var held = log.TakeHeld();
            Assert.AreEqual(1, held.Count);
            Assert.AreEqual(3, held[0].Count);

            Assert.IsFalse(log.Observe("LogError", "A.B.C", "S", "h1"), "sent once per session: not held again");
            CollectionAssert.IsEmpty(log.TakeHeld());

            log.Reset();
            Assert.IsTrue(log.Observe("LogError", "A.B.C", "S", "h1"), "a new session may send it again");
        }

        [Test]
        public void AtMostTwentyFaultsPerSession()
        {
            var log = new ClientExceptionLog();
            int taken = 0;
            for (int i = 0; i < 50; i++)
                if (log.Observe("LogError", "A.B.C", "S", "hash" + (char)('A' + i)))
                    taken++;
            Assert.AreEqual(ClientExceptionLog.MaxPerSession, taken);
            Assert.AreEqual(20, log.TakeHeld().Count);
        }

        [Test]
        public void ObserveCountsRightFromManyThreads()
        {
            // logMessageReceivedThreaded arrives on any thread: no repeat may be lost or doubled.
            var log = new ClientExceptionLog();
            Parallel.For(0, 4000, i => log.Observe("Exception", "A.B.C", "S", "h" + (i % 4)));
            var held = log.TakeHeld();
            Assert.AreEqual(4, held.Count);
            Assert.AreEqual(4000, held.Sum(e => e.Count));
        }

        // ── Invite attribution ─────────────────────────────────────────────────

        [Test]
        public void BothEndsComputeTheSameInviteId()
        {
            // The sender: itself, the player it invited, its party session.
            string sent = InviteAttribution.InviteIdFor("playerA", "playerB", "session1");
            // The invitee: the invite's sender, itself, the invite's session.
            string received = InviteAttribution.InviteIdFor("playerA", "playerB", "session1");
            Assert.AreEqual(sent, received);
            Assert.AreEqual("cca1044d9500cd21", sent, "stable across runs and platforms");
            Assert.AreNotEqual(sent, InviteAttribution.InviteIdFor("playerA", "playerB", "session2"));
            Assert.AreNotEqual(sent, InviteAttribution.InviteIdFor("playerB", "playerA", "session1"), "direction matters");
            Assert.IsNull(InviteAttribution.InviteIdFor("playerA", "", "session1"));
            StringAssert.DoesNotContain("player", sent, "the id names no player");
        }

        [Test]
        public void AJoinIsAttributedToTheInviteForItsSessionOnce()
        {
            var invites = new InviteAttribution();
            invites.RememberReceived("s1", "id1", "playerA");
            invites.RememberReceived("s2", "id2", "playerC");

            Assert.IsTrue(invites.TryTakeForJoin("s1", out var id, out var inviter));
            Assert.AreEqual("id1", id);
            Assert.AreEqual("playerA", inviter);
            Assert.IsFalse(invites.TryTakeForJoin("s1", out _, out _), "one invite attributes one join");
            Assert.IsFalse(invites.TryTakeForJoin("s9", out _, out _), "a presence join names no invite");
            Assert.IsFalse(invites.TryTakeForJoin(null, out _, out _));
        }

        [Test]
        public void OnlyTheNewestInvitesAreRemembered()
        {
            var invites = new InviteAttribution();
            for (int i = 0; i < InviteAttribution.MaxRemembered + 3; i++)
                invites.RememberReceived("s" + i, "id" + i, "p" + i);
            Assert.IsFalse(invites.TryTakeForJoin("s0", out _, out _), "the oldest fell off");
            Assert.IsTrue(invites.TryTakeForJoin("s" + (InviteAttribution.MaxRemembered + 2), out _, out _));
        }
    }
}
#endif
