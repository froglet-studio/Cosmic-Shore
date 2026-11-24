#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using FakeStats = CosmicShore.Tests.ScoringRuleFieldedDomainTests.FakeStats;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A POINTS-mode rule (golfRules off: highest wins) reveals a count, never a time.
    /// <see cref="ScoreReveal.FormatAsTime"/> makes the end-game sequencer print the value as
    /// MM:SS:CS (<c>EndGameSequencer</c> → <c>ScoreResultBuilder.FormatTime</c>), so a win in
    /// Tapestry showed 1,240 standing volume as "20:40:00". The golf-timed rules pass true on a
    /// win because their winner's Score IS a finish time; Tapestry and Bloomrush had copied that.
    /// </summary>
    [TestFixture]
    public class PointsRuleRevealTests
    {
        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        T MakeRule<T>(ScoringMetric metric) where T : ScoringRuleSO
        {
            var rule = Track(ScriptableObject.CreateInstance<T>());
            SetBaseField(rule, "metric", metric);
            SetBaseField(rule, "golfRules", false);
            return rule;
        }

        static void SetBaseField(ScoringRuleSO rule, string name, object value) =>
            typeof(ScoringRuleSO).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rule, value);

        GameDataSO MakeMatch()
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = 2;
            gameData.RoundStatsList.Add(new FakeStats
            {
                Name = "A", Domain = Domains.Jade, Score = 1240f, VolumeRemaining = 1240f, HostileVolumeDestroyed = 1240f,
            });
            gameData.RoundStatsList.Add(new FakeStats
            {
                Name = "B", Domain = Domains.Ruby, Score = 300f, VolumeRemaining = 300f, HostileVolumeDestroyed = 300f,
            });
            return gameData;
        }

        [Test]
        public void Tapestry_Victory_RevealsVolume_NotATime()
        {
            var rule = MakeRule<TapestryScoringRuleSO>(ScoringMetric.VolumeRemaining);
            var match = MakeMatch();
            var reveal = rule.BuildReveal(match, match.RoundStatsList[0], didWin: true);
            Assert.IsFalse(reveal.FormatAsTime, "the value is a volume standing, not seconds");
            Assert.AreEqual(1240, reveal.Value);
        }

        [Test]
        public void Bloomrush_Victory_RevealsVolume_NotATime()
        {
            var rule = MakeRule<BloomrushScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            var match = MakeMatch();
            var reveal = rule.BuildReveal(match, match.RoundStatsList[0], didWin: true);
            Assert.IsFalse(reveal.FormatAsTime, "the value is a volume bloomed, not seconds");
            Assert.AreEqual(1240, reveal.Value);
        }

        [Test]
        public void Tapestry_Reveal_ReadsTheAuthoredWords()
        {
            var rule = MakeRule<TapestryScoringRuleSO>(ScoringMetric.VolumeRemaining);
            typeof(TapestryScoringRuleSO).GetField("revealLabel", BindingFlags.Instance | BindingFlags.NonPublic)
                                         .SetValue(rule, "WOVEN");
            var match = MakeMatch();
            Assert.AreEqual("WOVEN", rule.BuildReveal(match, match.RoundStatsList[0], true).Label);
            Assert.AreEqual("WOVEN", rule.BuildReveal(match, match.RoundStatsList[1], false).Label);
        }

        [Test]
        public void Bloomrush_Reveal_ReadsTheAuthoredWords()
        {
            var rule = MakeRule<BloomrushScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            typeof(BloomrushScoringRuleSO).GetField("victoryHeader", BindingFlags.Instance | BindingFlags.NonPublic)
                                          .SetValue(rule, "BLOOMED");
            var match = MakeMatch();
            Assert.AreEqual("BLOOMED", rule.BuildReveal(match, match.RoundStatsList[0], true).Header);
        }

        /// <summary>
        /// The sweep: every shipped scoring-rule asset whose golfRules is off must reveal a win
        /// as a count. A new points mode that copies a golf-timed rule's reveal fails here.
        /// </summary>
        [Test]
        public void EveryShippedPointsRule_RevealsAWin_AsACount()
        {
            var checkedRules = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ScoringRuleSO"))
            {
                var rule = AssetDatabase.LoadAssetAtPath<ScoringRuleSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (rule == null || rule.GolfRules) continue;
                var match = MakeMatch();
                var reveal = rule.BuildReveal(match, match.RoundStatsList[0], didWin: true);
                Assert.IsFalse(reveal.FormatAsTime,
                    $"{rule.name} ({rule.GetType().Name}) is a points rule but reveals its win as a time");
                checkedRules.Add(rule.name);
            }
            CollectionAssert.Contains(checkedRules, "TapestryScoringRule", "the sweep must reach the shipped Tapestry rule");
            CollectionAssert.Contains(checkedRules, "BloomrushScoringRule", "the sweep must reach the shipped Bloomrush rule");
        }
    }
}
#endif
