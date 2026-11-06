#if UNITY_EDITOR
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The live flags' client rules (Docs/LAUNCH/LIVE_FLAGS.md): the version compare, the kill-switch composition
    /// (a switch only turns a gate off), the fallback order (live, then a fresh saved answer, then the safe default),
    /// who may raise the update wall, and the parser. The script's own rules are tested by
    /// <c>node Tools/UGS/CloudCode/launch-flags.test.cjs</c>.
    /// </summary>
    [TestFixture]
    public class LaunchFlagsTests
    {
        const string Full = @"{
            ""schema"": 1,
            ""minimumVersion"": ""0.3.0"",
            ""recommendedVersion"": ""0.4.0-beta.2"",
            ""updateUrls"": { ""windows"": ""https://w.example"", ""ios"": ""https://i.example"", ""android"": ""https://a.example"" },
            ""killSwitches"": { ""economy"": true, ""scores"": false, ""matchReports"": ""true"", ""trust"": true, ""futureThing"": true },
            ""disabledModes"": [ ""Elimination"", ""NotAModeYet"", ""3"", 7 ],
            ""message"": { ""title"": ""Maintenance"", ""body"": ""Online play pauses at 02:00 UTC."" }
        }";

        const long Now = 1_800_000_000L;

        static LaunchFlagsDocument Parse(string json)
        {
            Assert.IsTrue(LaunchFlagsDocument.TryParse(json, out var doc), "expected the document to parse");
            return doc;
        }

        [TearDown]
        public void RestoreDefault() => LaunchFlags.Apply(LaunchFlagsDocument.CreateDefault(), LaunchFlagsSource.Default);

        // ── The version compare ────────────────────────────────────────────────────────────────────────────────

        [TestCase("0.2.0", "0.3.0", true)]
        [TestCase("0.3.0", "0.3.0", false)]
        [TestCase("0.10.0", "0.9.9", false)]
        [TestCase("1.2", "1.2.0", false)]
        [TestCase("1.2", "1.2.1", true)]
        [TestCase("v1.2.3", "1.2.4", true)]
        [TestCase("1.2.3+abc123", "1.2.3", false)]
        [TestCase("1.0.0-beta", "1.0.0", true)]
        [TestCase("1.0.0-beta.2", "1.0.0-beta.11", true)]
        [TestCase("1.0.0-rc.1", "1.0.0-beta", false)]
        [TestCase("1.0.0-alpha", "1.0.0-alpha.1", true)]
        public void IsBelow_OrdersAsSemver(string current, string required, bool below) =>
            Assert.AreEqual(below, LaunchVersion.IsBelow(current, required), $"{current} < {required}");

        [TestCase("", "1.0.0")]
        [TestCase("1.0.0", "")]
        [TestCase(null, "1.0.0")]
        [TestCase("dev-build", "1.0.0")]
        [TestCase("0.1.0", "one.two")]
        [TestCase("0.1.0", "1.2.3.4")]
        public void IsBelow_AnUnreadableVersionNeverWalls(string current, string required) =>
            Assert.IsFalse(LaunchVersion.IsBelow(current, required));

        // ── Kill switches only ever turn a gate off ──────────────────────────────────────────────────────────

        [Test]
        public void Gate_IsCompiledAndNotKilled()
        {
            LaunchFlags.Apply(Parse(Full), LaunchFlagsSource.Live);
            Assert.IsFalse(LaunchFlags.Gate(true, LaunchKillSwitch.Economy), "economy is killed");
            Assert.IsFalse(LaunchFlags.Gate(true, LaunchKillSwitch.Trust), "trust is killed");
            Assert.IsTrue(LaunchFlags.Gate(true, LaunchKillSwitch.Scores), "scores: false is not killed");
            Assert.IsTrue(LaunchFlags.Gate(true, LaunchKillSwitch.MatchReports), "a string \"true\" is not a kill");
            Assert.IsTrue(LaunchFlags.Gate(true, LaunchKillSwitch.Entitlements), "absent is not killed");
        }

        [Test]
        public void Gate_NeverTurnsOnAGateTheBuildShippedOff()
        {
            LaunchFlags.Apply(LaunchFlagsDocument.CreateDefault(), LaunchFlagsSource.Default);
            foreach (var s in new[] { LaunchKillSwitch.Economy, LaunchKillSwitch.Scores, LaunchKillSwitch.MatchReports,
                                      LaunchKillSwitch.Trust, LaunchKillSwitch.Entitlements })
                Assert.IsFalse(LaunchFlags.Gate(false, s), s);
        }

        [Test]
        public void EveryServerGate_ReadsThroughTheFlags()
        {
            // The five gates ship off, so IsActive is off whatever the flags say; with them killed it stays off.
            LaunchFlags.Apply(Parse(Full), LaunchFlagsSource.Live);
            Assert.AreEqual(ServerEconomyGate.Enabled && !LaunchFlags.IsKilled(LaunchKillSwitch.Economy), ServerEconomyGate.IsActive);
            Assert.AreEqual(ServerScoreGate.Enabled && !LaunchFlags.IsKilled(LaunchKillSwitch.Scores), ServerScoreGate.IsActive);
            Assert.AreEqual(MatchReportGate.Enabled && !LaunchFlags.IsKilled(LaunchKillSwitch.MatchReports), MatchReportGate.IsActive);
            Assert.AreEqual(FairPlayTrustGate.Enabled && !LaunchFlags.IsKilled(LaunchKillSwitch.Trust), FairPlayTrustGate.IsActive);
            Assert.AreEqual(ServerEntitlementGate.Enabled && !LaunchFlags.IsKilled(LaunchKillSwitch.Entitlements), ServerEntitlementGate.IsActive);
            Assert.IsFalse(ServerEconomyGate.IsActive);
        }

        // ── Live, then a fresh saved answer, then the safe default ──────────────────────────────────────────

        [Test]
        public void Resolve_LiveWins()
        {
            var (doc, source) = LaunchFlags.Resolve(Full, null, 0, Now, 72f);
            Assert.AreEqual(LaunchFlagsSource.Live, source);
            Assert.IsTrue(doc.IsKilled(LaunchKillSwitch.Economy));
        }

        [Test]
        public void Resolve_NothingReached_IsTheSafeDefault()
        {
            var (doc, source) = LaunchFlags.Resolve(null, null, 0, Now, 72f);
            Assert.AreEqual(LaunchFlagsSource.Default, source);
            Assert.IsEmpty(doc.KilledSwitches, "nothing is killed by default");
            Assert.IsFalse(LaunchVersion.IsBelow("0.0.1", doc.MinimumVersion), "every version is allowed by default");
        }

        [Test]
        public void Resolve_AFreshSavedAnswerIsUsed_AStaleOneIsNot()
        {
            Assert.AreEqual(LaunchFlagsSource.Cache, LaunchFlags.Resolve(null, Full, Now - 3600, Now, 72f).source);
            Assert.AreEqual(LaunchFlagsSource.Cache, LaunchFlags.Resolve(null, Full, Now - 72 * 3600, Now, 72f).source);
            Assert.AreEqual(LaunchFlagsSource.Default, LaunchFlags.Resolve(null, Full, Now - 72 * 3600 - 1, Now, 72f).source);
            Assert.AreEqual(LaunchFlagsSource.Cache, LaunchFlags.Resolve("not json", Full, Now - 60, Now, 72f).source,
                "a broken live answer falls back to the saved one");
        }

        [Test]
        public void Resolve_ASavedAnswerFromTheFutureOrWithNoTime_IsStale()
        {
            Assert.AreEqual(LaunchFlagsSource.Default, LaunchFlags.Resolve(null, Full, Now + 60, Now, 72f).source);
            Assert.AreEqual(LaunchFlagsSource.Default, LaunchFlags.Resolve(null, Full, 0, Now, 72f).source);
            Assert.AreEqual(LaunchFlagsSource.Default, LaunchFlags.Resolve(null, "{\"schema\":2}", Now - 60, Now, 72f).source);
        }

        // ── Only this session's answer raises the wall ──────────────────────────────────────────────────────

        [Test]
        public void UpdateRequired_OnlyOnALiveAnswer()
        {
            var doc = Parse(Full); // minimum 0.3.0
            Assert.IsTrue(LaunchFlags.UpdateRequiredFor("0.2.0", doc, LaunchFlagsSource.Live));
            Assert.IsFalse(LaunchFlags.UpdateRequiredFor("0.2.0", doc, LaunchFlagsSource.Cache), "offline play is never walled by a saved answer");
            Assert.IsFalse(LaunchFlags.UpdateRequiredFor("0.2.0", doc, LaunchFlagsSource.Default));
            Assert.IsFalse(LaunchFlags.UpdateRequiredFor("0.3.0", doc, LaunchFlagsSource.Live));
        }

        [Test]
        public void UpdateRecommended_IsTheNoticeBetweenMinimumAndRecommended()
        {
            var doc = Parse(Full); // minimum 0.3.0, recommended 0.4.0-beta.2
            Assert.IsTrue(LaunchFlags.UpdateRecommendedFor("0.3.5", doc, LaunchFlagsSource.Live));
            Assert.IsFalse(LaunchFlags.UpdateRecommendedFor("0.2.0", doc, LaunchFlagsSource.Live), "below the minimum it is the wall, not the notice");
            Assert.IsFalse(LaunchFlags.UpdateRecommendedFor("0.4.0", doc, LaunchFlagsSource.Live));
            Assert.IsFalse(LaunchFlags.UpdateRecommendedFor("0.3.5", doc, LaunchFlagsSource.Cache));
        }

        // ── The parser ───────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Parse_ReadsEveryField()
        {
            var doc = Parse(Full);
            Assert.AreEqual("0.3.0", doc.MinimumVersion);
            Assert.AreEqual("0.4.0-beta.2", doc.RecommendedVersion);
            Assert.AreEqual("https://a.example", doc.UpdateUrlFor(RuntimePlatform.Android));
            Assert.AreEqual("https://i.example", doc.UpdateUrlFor(RuntimePlatform.IPhonePlayer));
            Assert.AreEqual("https://w.example", doc.UpdateUrlFor(RuntimePlatform.WindowsPlayer));
            Assert.AreEqual("https://w.example", doc.UpdateUrlFor(RuntimePlatform.WindowsEditor));
            Assert.AreEqual("Maintenance", doc.MessageTitle);
            Assert.AreEqual("Online play pauses at 02:00 UTC.", doc.MessageBody);
        }

        [Test]
        public void Parse_IgnoresWhatThisBuildDoesNotKnow()
        {
            var doc = Parse(Full);
            Assert.IsTrue(doc.IsKilled("futureThing"), "a newer switch is kept by name; no gate here reads it");
            Assert.IsTrue(doc.IsModeDisabled(GameModes.Elimination));
            Assert.AreEqual(1, doc.DisabledModes.Count, "unknown names, a number as a string and a number are all ignored");
        }

        [Test]
        public void Parse_AMissingFieldTakesItsSafeDefault()
        {
            var doc = Parse("{\"schema\":1}");
            Assert.IsEmpty(doc.KilledSwitches);
            Assert.IsEmpty(doc.DisabledModes);
            Assert.AreEqual(string.Empty, doc.MinimumVersion);
            Assert.AreEqual(string.Empty, doc.UpdateUrlFor(RuntimePlatform.Android));
            Assert.AreEqual(string.Empty, doc.MessageTitle);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1,2]")]
        [TestCase("{\"schema\":2}")]
        [TestCase("{\"schema\":\"1\"}")]
        [TestCase("{\"minimumVersion\":\"9.9.9\"}")]
        public void Parse_RefusesWhatIsNotAFlagsDocument(string json)
        {
            Assert.IsFalse(LaunchFlagsDocument.TryParse(json, out var doc));
            Assert.IsNull(doc);
        }

        [Test]
        public void Apply_RaisesOnChanged_AndNullIsTheDefault()
        {
            int raised = 0;
            void Count() => raised++;
            LaunchFlags.OnChanged += Count;
            try
            {
                LaunchFlags.Apply(Parse(Full), LaunchFlagsSource.Live);
                LaunchFlags.Apply(null, LaunchFlagsSource.Live);
            }
            finally { LaunchFlags.OnChanged -= Count; }
            Assert.AreEqual(2, raised);
            Assert.AreEqual(LaunchFlagsSource.Default, LaunchFlags.Source);
            Assert.IsEmpty(LaunchFlags.Current.KilledSwitches);
        }

        // ── The authored words ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Config_IsInResources_WithEveryLineAuthored()
        {
            var config = Resources.Load<LaunchFlagsConfigSO>(LaunchFlagsConfigSO.ResourcePath);
            Assert.IsNotNull(config, "Assets/Resources/LaunchFlagsConfig.asset is missing or does not load");
            Assert.Greater(config.CacheMaxAgeHours, 0f);
            Assert.IsNotEmpty(config.RequiredTitle);
            Assert.IsNotEmpty(config.UpdateButton);
            StringAssert.Contains("{minimum}", config.RequiredBody);
        }
    }
}
#endif
