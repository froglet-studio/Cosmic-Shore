#if UNITY_EDITOR
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Quests window's arithmetic (QuestsRules, Docs/MENU_REDESIGN_B.md section 200): where the stations
    /// stand, the course's bends, how many goals are done, and the reward prism's colours.</summary>
    [TestFixture]
    public class QuestsRulesTests
    {
        [Test]
        public void StationsSpreadWithHalfAStepAtEachEnd()
        {
            Assert.AreEqual(-800f, QuestsRules.Station(0, 5, 2000f).x, 1e-3f);
            Assert.AreEqual(0f, QuestsRules.Station(2, 5, 2000f).x, 1e-3f);
            Assert.AreEqual(800f, QuestsRules.Station(4, 5, 2000f).x, 1e-3f);
        }

        [Test]
        public void StationsStepDownThenUp()
        {
            Assert.AreEqual(-QuestsRules.RouteSwing, QuestsRules.Station(0, 5, 1920f).y);
            Assert.AreEqual(QuestsRules.RouteSwing, QuestsRules.Station(1, 5, 1920f).y);
            Assert.AreEqual(-QuestsRules.RouteSwing, QuestsRules.Station(2, 5, 1920f).y);
        }

        [Test]
        public void NoStationsStandAtTheMiddle() => Assert.AreEqual(Vector2.zero, QuestsRules.Station(0, 0, 1920f));

        [Test]
        public void TheCourseBendsOffTheChordsMiddle()
        {
            var a = new Vector2(0f, 0f);
            var b = new Vector2(100f, 0f);
            var c = QuestsRules.Control(a, b, 30f);
            Assert.AreEqual(50f, c.x, 1e-3f);
            Assert.AreEqual(30f, Mathf.Abs(c.y), 1e-3f);
            Assert.AreEqual(-QuestsRules.Control(a, b, -30f).y, c.y, 1e-3f);
        }

        [Test]
        public void TheBendsAlternate()
        {
            Assert.AreEqual(30f, QuestsRules.BendOf(0));
            Assert.AreEqual(-30f, QuestsRules.BendOf(1));
            Assert.AreEqual(30f, QuestsRules.BendOf(2));
        }

        [Test]
        public void TheQuadraticRunsFromEndToEnd()
        {
            var a = new Vector2(-10f, 0f);
            var c = new Vector2(0f, 20f);
            var b = new Vector2(10f, 0f);
            Assert.AreEqual(a, QuestsRules.Quadratic(a, c, b, 0f));
            Assert.AreEqual(b, QuestsRules.Quadratic(a, c, b, 1f));
            Assert.AreEqual(10f, QuestsRules.Quadratic(a, c, b, 0.5f).y, 1e-3f);
        }

        [TestCase(QuestItemState.Locked, 4, 0)]
        [TestCase(QuestItemState.Unlocked, 2, 0)]
        [TestCase(QuestItemState.Unlocked, 3, 1)]
        [TestCase(QuestItemState.Unlocked, 4, 2)]
        [TestCase(QuestItemState.ReadyToClaim, 1, 2)]
        [TestCase(QuestItemState.Claimed, 1, 2)]
        public void AGameQuestsGoalsFollowItsIntensities(QuestItemState state, int unlocked, int done) =>
            Assert.AreEqual(done, QuestsRules.GoalsDone(state, 2, unlocked));

        [Test]
        public void AFeatureQuestsOneGoalIsDoneOnlyWhenReady()
        {
            Assert.AreEqual(0, QuestsRules.GoalsDone(QuestItemState.Unlocked, 1, 4));
            Assert.AreEqual(1, QuestsRules.GoalsDone(QuestItemState.ReadyToClaim, 1, 0));
        }

        [Test]
        public void OnlyTheAuthoredGoalsShow()
        {
            var quest = ScriptableObject.CreateInstance<SO_UnlockData>();
            try
            {
                quest.FeatureKind = FeatureKind.GameMode;
                quest.Intensity3GoalDescription = "Win at Intensity 2";
                quest.Intensity4GoalDescription = "";
                quest.Description = "A description";
                CollectionAssert.AreEqual(new[] { "Win at Intensity 2" }, QuestsRules.Goals(quest));
                quest.Intensity3GoalDescription = "";
                CollectionAssert.AreEqual(new[] { "A description" }, QuestsRules.Goals(quest));
            }
            finally
            {
                Object.DestroyImmediate(quest);
            }
        }

        [Test]
        public void NoQuestHasNoGoals() => Assert.IsEmpty(QuestsRules.Goals(null));

        [Test]
        public void ASwatchKeepsTheHueAndBringsItIntoRange()
        {
            var c = QuestsRules.Swatch(new Color(4f, 2f, 1f, 0.3f));
            Assert.AreEqual(1f, c.r, 1e-4f);
            Assert.AreEqual(0.5f, c.g, 1e-4f);
            Assert.AreEqual(0.25f, c.b, 1e-4f);
            Assert.AreEqual(1f, c.a);
        }

        [Test]
        public void ASwatchInRangeIsLeftAlone()
        {
            var c = QuestsRules.Swatch(new Color(0.2f, 0.4f, 0.6f, 0f));
            Assert.AreEqual(0.4f, c.g, 1e-4f);
            Assert.AreEqual(1f, c.a);
        }
    }
}
#endif
