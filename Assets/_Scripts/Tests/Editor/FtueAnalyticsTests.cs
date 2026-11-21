#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Core;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The activation funnel's top rows - the quest graph's <c>ftue_step_completed</c> /
    /// <c>ftue_phase_completed</c> / <c>ftue_completed</c> (Docs/Analytics/event-taxonomy.md §4, P0).
    ///
    /// WHY THIS MATTERS:
    /// 1. THE NAMES ARE A CONTRACT WITH THE DASHBOARD. UGS discards any event its Event Manager
    ///    does not declare, silently, so a renamed constant is a funnel that goes dark with no error.
    /// 2. step_id MUST BE THE STABLE NODE ID. A display name is authoring copy; keying the funnel on
    ///    it would fork the funnel every time a designer re-words a node.
    /// 3. AN UNNAMED NODE MUST NOT REPORT AN EMPTY NAME, or every unnamed step groups as one.
    /// 4. THE RUNNER'S EVENT REACHES ITS SUBSCRIBERS - the facade hears nothing otherwise.
    /// </summary>
    [TestFixture]
    public class FtueAnalyticsTests
    {
        [Test]
        public void EventNamesMatchTheTaxonomy()
        {
            Assert.AreEqual("ftue_step_completed", UGSKeys.EventFtueStepCompleted);
            Assert.AreEqual("ftue_phase_completed", UGSKeys.EventFtuePhaseCompleted);
            Assert.AreEqual("ftue_completed", UGSKeys.EventFtueCompleted);
        }

        [Test]
        public void StepIdIsTheStableNodeIdNotTheDisplayName()
        {
            var p = AnalyticsServiceFacade.BuildFtueStepParameters(
                "MainQuest", 0, "8943a1eafac648e2917dd95481f4b9ae", "Prompt: Slow Down", 42);

            Assert.AreEqual("8943a1eafac648e2917dd95481f4b9ae", p["step_id"]);
            Assert.AreEqual("Prompt: Slow Down", p["step_name"]);
            Assert.AreEqual("MainQuest", p["quest_id"]);
            Assert.AreEqual(0, p["phase_index"]);
            Assert.AreEqual(42, p["seconds_since_launch"]);
        }

        [Test]
        public void UnnamedNodeReportsItsIdAsItsName()
        {
            var p = AnalyticsServiceFacade.BuildFtueStepParameters("MainQuest", 2, "abc", "  ", 1);
            Assert.AreEqual("abc", p["step_name"]);
        }

        [Test]
        public void MissingIdsReportUnknownRatherThanEmpty()
        {
            var p = AnalyticsServiceFacade.BuildFtueStepParameters(null, 1, null, null, 1);
            Assert.AreEqual("unknown", p["quest_id"]);
            Assert.AreEqual("unknown", p["step_id"]);
            Assert.AreEqual("unknown", p["step_name"]);
        }

        [Test]
        public void RunnerNodeEventReachesSubscribers()
        {
            string heardQuest = null, heardNode = null, heardName = null;
            int heardPhase = -1;
            void Handler(string q, int ph, string n, string name)
            {
                heardQuest = q; heardPhase = ph; heardNode = n; heardName = name;
            }

            FTUEEventManager.OnQuestNodeCompleted += Handler;
            try
            {
                FTUEEventManager.RaiseQuestNodeCompleted("MainQuest", 3, "node-1", "Unlock Joust");
            }
            finally
            {
                FTUEEventManager.OnQuestNodeCompleted -= Handler;
            }

            Assert.AreEqual("MainQuest", heardQuest);
            Assert.AreEqual(3, heardPhase);
            Assert.AreEqual("node-1", heardNode);
            Assert.AreEqual("Unlock Joust", heardName);
        }
    }
}
#endif
