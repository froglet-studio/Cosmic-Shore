#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The cell-change loading screen's readout (Docs/MENU_REDESIGN_B.md §47.4): the build step it lights and how full
    /// its crystal is follow the real build phases, and neither ever goes back.
    /// </summary>
    public class EnvironmentLoadVeilStageTests
    {
        [Test]
        public void NothingLightsBeforeTheBuildStarts()
        {
            Assert.AreEqual(-1, EnvironmentLoadVeil.StepFor(-1, gated: false, released: false, laying: false, growRemaining: 0, sawBuild: false));
            // Gated, but the first lay slice has not run yet: still waiting, not "settling".
            Assert.AreEqual(-1, EnvironmentLoadVeil.StepFor(-1, gated: true, released: false, laying: false, growRemaining: 0, sawBuild: false));
        }

        [Test]
        public void TheStepsFollowThePhases()
        {
            Assert.AreEqual(0, EnvironmentLoadVeil.StepFor(-1, true, false, laying: true, growRemaining: 0, sawBuild: true));
            Assert.AreEqual(1, EnvironmentLoadVeil.StepFor(0, true, false, laying: false, growRemaining: 40, sawBuild: true));
            Assert.AreEqual(2, EnvironmentLoadVeil.StepFor(1, true, false, laying: false, growRemaining: 0, sawBuild: true));
            Assert.AreEqual(3, EnvironmentLoadVeil.StepFor(2, true, released: true, laying: false, growRemaining: 0, sawBuild: true));
        }

        [Test]
        public void AStepNeverGoesBack()
        {
            // A second lay starting while the first cohort grows does not unlight "growing".
            Assert.AreEqual(1, EnvironmentLoadVeil.StepFor(1, true, false, laying: true, growRemaining: 10, sawBuild: true));
        }

        [Test]
        public void TheCrystalRisesWithTheWorkAndNeverFalls()
        {
            Assert.AreEqual(0f, EnvironmentLoadVeil.CrystalLevel(0f, -1, 0f, 0, 0));
            Assert.AreEqual(0.35f, EnvironmentLoadVeil.CrystalLevel(0f, 0, 0.5f, 0, 0), 1e-4f);
            Assert.AreEqual(0.825f, EnvironmentLoadVeil.CrystalLevel(0.7f, 1, 1f, 50, 100), 1e-4f);
            Assert.AreEqual(1f, EnvironmentLoadVeil.CrystalLevel(0.97f, 3, 1f, 0, 100));
            // A lay that restarts its own progress does not drain the crystal.
            Assert.AreEqual(0.6f, EnvironmentLoadVeil.CrystalLevel(0.6f, 0, 0.1f, 0, 0), 1e-4f);
        }
    }
}
#endif
