#if UNITY_EDITOR
using System;
using System.Linq;
using CosmicShore.Editor;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Launch Console's reading of the launch-status branch (<see cref="LaunchStatusBoard"/>) and
    /// its pill rule (<see cref="LaunchRules"/>), plus the shipped <see cref="LaunchConsoleConfigSO"/>
    /// asset. The rule decides what the team reads as "can we land", so each branch of it is pinned:
    /// a failure is No-go and names itself, a success goes stale after 48 h (counted from the
    /// event date when a record carries one), and the audit's verdict is a floor the records cannot
    /// lift. Docs/LAUNCH/STATUS_CONTRACT.md, Docs/MENU_REDESIGN_B.md section 132.
    /// </summary>
    public class LaunchConsoleStatusTests
    {
        const double Stale = 48;
        static readonly DateTime Now = new DateTime(2026, 11, 6, 3, 40, 0, DateTimeKind.Utc);

        // status.json on launch-status @ 8a36d99e15 (2026-11-06), byte for byte apart from whitespace.
        const string Snapshot =
            @"{""components"":{""android-prerelease"":{""build"":""412826"",""channel"":""prerelease"",""id"":""android-prer"
            + @"elease"",""kind"":""channel"",""platform"":""android"",""result"":""success"",""schema"":1,""source"":""Phase "
            + @"1 audit (2026-11-05)"",""summary"":""Internal testing; uploaded by hand"",""updated_at"":""2026-11-06T03:38:15"
            + @"Z""},""ios-prerelease"":{""channel"":""prerelease"",""commit"":""e69a542a83"",""details"":{""event_date"":""20"
            + @"26-10-13""},""id"":""ios-prerelease"",""kind"":""channel"",""platform"":""ios"",""result"":""success"",""run_u"
            + @"rl"":""https://github.com/froglet-studio/Cosmic-Shore/actions/runs/38373220571"",""schema"":1,""source"":""Pha"
            + @"se 1 audit (2026-11-05)"",""summary"":""Last TestFlight upload 2026-10-13 from a hand export"",""updated_at"":"
            + @"""2026-11-06T03:37:42Z"",""version"":""0.2.0""},""trunk-static"":{""commit"":""1ac6f87ae0"",""details"":{""eve"
            + @"nt_date"":""2026-11-05""},""id"":""trunk-static"",""kind"":""gate"",""platform"":""quality"",""result"":""succ"
            + @"ess"",""run_url"":""https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42095529959"",""schema"":1,""s"
            + @"ource"":""Phase 1 audit (2026-11-05)"",""summary"":""Static checks green; Unity compile skipped (no licence)"""
            + @",""updated_at"":""2026-11-06T03:37:32Z""},""ugs-development"":{""channel"":""development"",""commit"":""2c450a"
            + @"acb3"",""details"":{""event_date"":""2026-11-01""},""id"":""ugs-development"",""kind"":""deploy"",""platform"""
            + @":""liveops"",""result"":""success"",""run_url"":""https://github.com/froglet-studio/Cosmic-Shore/actions/runs/"
            + @"41330402144"",""schema"":1,""source"":""Phase 1 audit (2026-11-05)"",""summary"":""Deploy UGS scripts to the d"
            + @"evelopment environment"",""updated_at"":""2026-11-06T03:37:47Z""},""ugs-production"":{""channel"":""production"
            + @""",""id"":""ugs-production"",""kind"":""deploy"",""platform"":""liveops"",""result"":""skipped"",""schema"":1,"
            + @"""source"":""Phase 1 audit (2026-11-05)"",""summary"":""Never deployed; builds still point at development"","""
            + @"updated_at"":""2026-11-06T03:37:55Z""},""unity-compile"":{""details"":{""last_success_commit"":""40c9b74423"","
            + @"""last_success_date"":""2026-10-06""},""id"":""unity-compile"",""kind"":""gate"",""link"":""https://github.com"
            + @"/froglet-studio/Cosmic-Shore/actions/runs/37488258879"",""platform"":""quality"",""result"":""failure"",""sche"
            + @"ma"":1,""source"":""Phase 1 audit (2026-11-05)"",""summary"":""No Unity compile in CI since 2026-10-06; PR 169"
            + @"5 waits on the unity-ci licence secrets. The link is the LAST successful compile (2026-10-06), not a failing r"
            + @"un"",""updated_at"":""2026-11-06T03:37:38Z""},""web-production"":{""channel"":""production"",""details"":{""ev"
            + @"ent_date"":""2026-10-22""},""id"":""web-production"",""kind"":""channel"",""link"":""https://www.froglet.games"
            + @"/play"",""platform"":""web"",""result"":""success"",""schema"":1,""source"":""Phase 1 audit (2026-11-05)"",""s"
            + @"ummary"":""Hand upload 2026-10-22 on the r2.dev host; commit unknown"",""updated_at"":""2026-11-06T03:38:04Z"""
            + @",""version"":""2""},""windows-prerelease"":{""channel"":""prerelease"",""id"":""windows-prerelease"",""kind"":"
            + @"""channel"",""platform"":""windows"",""result"":""skipped"",""schema"":1,""source"":""Phase 1 audit (2026-11-0"
            + @"5)"",""summary"":""No Steamworks app yet (A1-A4)"",""updated_at"":""2026-11-06T03:38:27Z""}},""generated_at"":"
            + @"""2026-11-06T03:38:29Z"",""schema"":1}";

        static LaunchStatusBoard Board(string json)
        {
            var b = LaunchStatusBoard.Parse(json, out var err);
            Assert.IsNotNull(b, err);
            return b;
        }

        static string One(string id, string result, string updatedAt, string eventDate = null) =>
            "{\"schema\":1,\"components\":{\"" + id + "\":{\"id\":\"" + id + "\",\"result\":\"" + result +
            "\",\"updated_at\":\"" + updatedAt + "\",\"source\":\"test\"" +
            (eventDate == null ? "" : ",\"details\":{\"event_date\":\"" + eventDate + "\"}") + "}}}";

        static LaunchRecord Rec(string result, string updatedAt, string eventDate = null) =>
            Board(One("x", result, updatedAt, eventDate)).Get("x");

        [Test]
        public void States_KeepTheirNumbers()
        {
            // The audit verdicts are serialized in the config asset as these ints.
            Assert.AreEqual(0, (int)LaunchState.Unknown);
            Assert.AreEqual(1, (int)LaunchState.Go);
            Assert.AreEqual(2, (int)LaunchState.AtRisk);
            Assert.AreEqual(3, (int)LaunchState.NoGo);
            Assert.AreEqual(0, (int)LaunchActionKind.Plain);
            Assert.AreEqual(1, (int)LaunchActionKind.CallToAction);
            Assert.AreEqual(2, (int)LaunchActionKind.Commit);
        }

        [Test]
        public void Parse_ReadsTheRealSnapshot()
        {
            var b = Board(Snapshot);
            Assert.AreEqual(8, b.Records.Count);
            Assert.AreEqual(new DateTime(2026, 11, 6, 3, 38, 29, DateTimeKind.Utc), b.GeneratedAt);
            var ios = b.Get("ios-prerelease");
            Assert.AreEqual("success", ios.Result);
            Assert.AreEqual("0.2.0 e69a542a8", ios.VersionLine);
            Assert.AreEqual(new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc), ios.EventDate);
            Assert.AreEqual(ios.EventDate, ios.AsOf, "the event date, not the publish time, is what a figure is as of");
            Assert.AreEqual("https://github.com/froglet-studio/Cosmic-Shore/actions/runs/38373220571", ios.OpenUrl);
            var android = b.Get("android-prerelease");
            Assert.IsNull(android.EventDate);
            Assert.AreEqual(android.UpdatedAt, android.AsOf);
            Assert.AreEqual("(412826)", android.VersionLine);
            Assert.AreEqual("https://www.froglet.games/play", b.Get("web-production").OpenUrl, "no run: the link is the page");
            Assert.AreEqual("failure", b.Get("unity-compile").Result);
            Assert.AreEqual("40c9b74423", b.Get("unity-compile").Details["last_success_commit"]);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"schema\":1}")]
        [TestCase("{\"components\":[]}")]
        public void Parse_RefusesWhatIsNotABoard(string json)
        {
            Assert.IsNull(LaunchStatusBoard.Parse(json, out var err));
            Assert.IsFalse(string.IsNullOrEmpty(err), "a board that cannot be read says why");
        }

        [Test]
        public void StateOf_FollowsTheRule()
        {
            Assert.AreEqual(LaunchState.NoGo, LaunchRules.StateOf(Rec("failure", "2026-11-06T03:00:00Z"), Now, Stale));
            Assert.AreEqual(LaunchState.Go, LaunchRules.StateOf(Rec("success", "2026-11-06T03:00:00Z"), Now, Stale));
            Assert.AreEqual(LaunchState.Go, LaunchRules.StateOf(Rec("success", "2026-11-04T03:40:00Z"), Now, Stale), "48 h exactly is still fresh");
            Assert.AreEqual(LaunchState.AtRisk, LaunchRules.StateOf(Rec("success", "2026-11-04T03:39:00Z"), Now, Stale), "older than 48 h is at risk");
            Assert.AreEqual(LaunchState.AtRisk, LaunchRules.StateOf(Rec("success", "2026-11-06T03:00:00Z", "2026-10-13"), Now, Stale), "a fresh publish of an old event is old");
            Assert.AreEqual(LaunchState.AtRisk, LaunchRules.StateOf(Rec("success", "not a time"), Now, Stale), "a success of unknown age is not Go");
            Assert.AreEqual(LaunchState.AtRisk, LaunchRules.StateOf(Rec("skipped", "2026-11-06T03:00:00Z"), Now, Stale));
            Assert.AreEqual(LaunchState.AtRisk, LaunchRules.StateOf(Rec("cancelled", "2026-11-06T03:00:00Z"), Now, Stale));
            Assert.AreEqual(LaunchState.AtRisk, LaunchRules.StateOf(Rec("running", "2026-11-06T03:00:00Z"), Now, Stale));
            Assert.AreEqual(LaunchState.Unknown, LaunchRules.StateOf(null, Now, Stale));
        }

        [Test]
        public void PlatformState_AFailureWinsAndNamesItself()
        {
            var b = Board(One("unity-compile", "failure", "2026-11-06T03:00:00Z"));
            var s = LaunchRules.PlatformState(new[] { "unity-compile" }, b, LaunchState.AtRisk, "audit says", Now, Stale, out var why);
            Assert.AreEqual(LaunchState.NoGo, s);
            Assert.AreEqual("unity-compile failed", why);
        }

        [Test]
        public void PlatformState_TheAuditIsAFloorTheRecordsCannotLift()
        {
            var b = Board(One("windows-prerelease", "success", "2026-11-06T03:00:00Z"));
            var s = LaunchRules.PlatformState(new[] { "windows-prerelease" }, b, LaunchState.NoGo, "No Steamworks app", Now, Stale, out var why);
            Assert.AreEqual(LaunchState.NoGo, s);
            Assert.AreEqual("No Steamworks app", why);

            s = LaunchRules.PlatformState(new[] { "windows-prerelease" }, b, LaunchState.AtRisk, "No CI", Now, Stale, out why);
            Assert.AreEqual(LaunchState.AtRisk, s);
            Assert.AreEqual("No CI", why);
        }

        [Test]
        public void PlatformState_StaleAndSkippedRecordsSayWhy()
        {
            var stale = Board(One("web-production", "success", "2026-11-06T03:00:00Z", "2026-10-22"));
            var s = LaunchRules.PlatformState(new[] { "web-production" }, stale, LaunchState.Unknown, null, Now, Stale, out var why);
            Assert.AreEqual(LaunchState.AtRisk, s);
            Assert.AreEqual("web-production is 15 days old", why);

            var skipped = Board(One("ugs-production", "skipped", "2026-11-06T03:00:00Z"));
            s = LaunchRules.PlatformState(new[] { "ugs-production" }, skipped, LaunchState.Go, null, Now, Stale, out why);
            Assert.AreEqual(LaunchState.AtRisk, s);
            Assert.AreEqual("ugs-production skipped", why);
        }

        [Test]
        public void PlatformState_GoOnlyWhenEveryRecordIsFreshAndTheAuditAgrees()
        {
            var b = Board(One("android-prerelease", "success", "2026-11-06T03:00:00Z"));
            Assert.AreEqual(LaunchState.Go,
                LaunchRules.PlatformState(new[] { "android-prerelease" }, b, LaunchState.Go, null, Now, Stale, out _));
            Assert.AreEqual(LaunchState.Go,
                LaunchRules.PlatformState(new[] { "android-prerelease", "android-production" }, b, LaunchState.Unknown, null, Now, Stale, out _),
                "a channel with no record yet is no evidence either way");
            Assert.AreEqual(LaunchState.Unknown,
                LaunchRules.PlatformState(new[] { "backend-x" }, b, LaunchState.Unknown, null, Now, Stale, out var why));
            Assert.AreEqual("no run recorded", why);
            Assert.AreEqual(LaunchState.Unknown,
                LaunchRules.PlatformState(new[] { "backend-x" }, null, LaunchState.Unknown, null, Now, Stale, out _), "no board read yet");
        }

        [Test]
        public void Words_Ages_DaysAndLinks()
        {
            Assert.AreEqual("No-go", LaunchRules.Word(LaunchState.NoGo));
            Assert.AreEqual("At risk", LaunchRules.Word(LaunchState.AtRisk));
            Assert.AreEqual("under an hour ago", LaunchRules.AgeText(TimeSpan.FromMinutes(59)));
            Assert.AreEqual("28 h ago", LaunchRules.AgeText(TimeSpan.FromHours(27.6)));
            Assert.AreEqual("24 days ago", LaunchRules.AgeText(TimeSpan.FromDays(24.15)));
            Assert.AreEqual(27, LaunchRules.DaysLeft(new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc), Now));
            Assert.AreEqual("https://cloud.unity.com", LaunchRules.SafeUrl("https://cloud.unity.com"));
            Assert.IsNull(LaunchRules.SafeUrl("http://cloud.unity.com"));
            Assert.IsNull(LaunchRules.SafeUrl("https://"));
            Assert.IsNull(LaunchRules.SafeUrl("https://a.example/x y"));
            Assert.IsNull(LaunchRules.SafeUrl("https://a.example/\"onclick"));
            Assert.IsNull(LaunchRules.SafeUrl("javascript:alert(1)"));
            Assert.IsNull(LaunchRules.SafeUrl(null));
        }

        [Test]
        public void TheConfigAsset_LoadsWithEveryPlatformAndWorkingLinks()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<LaunchConsoleConfigSO>(LaunchConsoleConfigSO.AssetPath);
            Assert.IsNotNull(cfg, LaunchConsoleConfigSO.AssetPath + " did not load");
            Assert.AreEqual("Invite build v1", cfg.milestoneName);
            Assert.AreEqual(new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc), cfg.MilestoneUtc);
            CollectionAssert.AreEqual(new[] { "windows", "ios", "android", "web", "liveops", "backend", "launch" },
                cfg.platforms.Select(p => p.key).ToArray(), "the artifact's PLATFORMS, in its order");
            Assert.IsNotNull(LaunchRules.SafeUrl(cfg.consoleUrl));
            foreach (var p in cfg.platforms)
            {
                Assert.IsNotEmpty(p.actions, p.key + " has no action");
                foreach (var a in p.actions)
                    Assert.IsNotNull(LaunchRules.SafeUrl(a.url), p.key + " / " + a.label + " has no https link");
                foreach (var rank in p.blockerRanks)
                    Assert.IsNotNull(cfg.Blocker(rank), p.key + " names blocker #" + rank + ", which the config lacks");
            }
            Assert.AreEqual(11, cfg.humanSteps.Count, "READINESS section 10, Needs a human, in order");
        }

        [Test]
        public void TheConfigAndTheSnapshot_GiveTheBoardsAnswer()
        {
            // Rev 58's boards were drawn from this snapshot: 4 no-go (Windows, iOS, Live ops, Launch-wide), 3 at risk.
            var cfg = AssetDatabase.LoadAssetAtPath<LaunchConsoleConfigSO>(LaunchConsoleConfigSO.AssetPath);
            Assert.IsNotNull(cfg);
            var b = Board(Snapshot);
            var states = cfg.platforms.ToDictionary(p => p.key, p => LaunchRules.PlatformState(
                LaunchConsoleConfigSO.IdsFor(p, b), b, p.auditVerdict, p.auditWhy, Now, cfg.staleHours, out _));
            Assert.AreEqual(LaunchState.NoGo, states["windows"]);
            Assert.AreEqual(LaunchState.NoGo, states["ios"]);
            Assert.AreEqual(LaunchState.AtRisk, states["android"]);
            Assert.AreEqual(LaunchState.AtRisk, states["web"]);
            Assert.AreEqual(LaunchState.NoGo, states["liveops"]);
            Assert.AreEqual(LaunchState.AtRisk, states["backend"]);
            Assert.AreEqual(LaunchState.NoGo, states["launch"]);
        }
    }
}
#endif
