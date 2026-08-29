#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="TournamentStateMachine"/> - the Maelstrom (Tournament) phase tracker.
    ///
    /// The race-to-6 end condition depends on the machine being able to reach
    /// <see cref="TournamentPhase.Summary"/> when a domain hits the win target. The controller decides
    /// this from the authoritative <c>TournamentDataSO.IsShuffleComplete</c> at the Maelstrom scene load
    /// (see <c>TournamentController.EnterSummary</c>), routing through <see cref="TournamentPhase.Complete"/>.
    /// That route must be valid from BOTH <see cref="TournamentPhase.InGame"/> (normal: the deciding game's
    /// end set Complete) and <see cref="TournamentPhase.Lobby"/> (recovery: the end-of-game Complete
    /// transition was missed, so the hub load must still be able to reach the summary). Regression guard for
    /// the bug where scoring 6 started another game instead of ending the tournament.
    /// </summary>
    [TestFixture]
    public class TournamentStateMachineTests
    {
        TournamentStateMachine _sm;

        [SetUp]
        public void SetUp() => _sm = new TournamentStateMachine();

        [Test]
        public void StartsIdle()
        {
            Assert.AreEqual(TournamentPhase.Idle, _sm.Current);
        }

        [Test]
        public void HappyPath_IdleToSummary()
        {
            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Lobby));
            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.InGame));
            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Complete));
            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Summary));
            Assert.AreEqual(TournamentPhase.Summary, _sm.Current);
        }

        // The deciding game ended in InGame and set Complete - the standard EnterSummary route.
        [Test]
        public void EnterSummary_FromInGame_ReachesSummary()
        {
            _sm.TransitionTo(TournamentPhase.Lobby);
            _sm.TransitionTo(TournamentPhase.InGame);

            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Complete), "InGame -> Complete must be valid.");
            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Summary), "Complete -> Summary must be valid.");
            Assert.AreEqual(TournamentPhase.Summary, _sm.Current);
        }

        // RECOVERY ROUTE (the fix): the deciding game's InGame -> Complete transition was missed, so the
        // hub (Lobby) load detects IsShuffleComplete and must still reach the summary. This requires
        // Lobby -> Complete to be a valid transition.
        [Test]
        public void EnterSummary_FromLobby_ReachesSummary()
        {
            _sm.TransitionTo(TournamentPhase.Lobby);

            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Complete),
                "Lobby -> Complete must be valid so a missed end-of-game Complete can still surface the summary.");
            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Summary));
            Assert.AreEqual(TournamentPhase.Summary, _sm.Current);
        }

        // Between-round hub: a game ends without deciding the shuffle -> back to Lobby.
        [Test]
        public void BetweenRounds_InGameToLobby()
        {
            _sm.TransitionTo(TournamentPhase.Lobby);
            _sm.TransitionTo(TournamentPhase.InGame);

            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.Lobby), "InGame -> Lobby (hub) must be valid.");
            Assert.AreEqual(TournamentPhase.Lobby, _sm.Current);
        }

        // Play Again from the summary re-enters a fresh run.
        [Test]
        public void Summary_CanRestartIntoInGame()
        {
            _sm.TransitionTo(TournamentPhase.Lobby);
            _sm.TransitionTo(TournamentPhase.InGame);
            _sm.TransitionTo(TournamentPhase.Complete);
            _sm.TransitionTo(TournamentPhase.Summary);

            Assert.IsTrue(_sm.TransitionTo(TournamentPhase.InGame), "Summary -> InGame (Play Again) must be valid.");
        }

        [Test]
        public void RejectsInvalidTransition_IdleToComplete()
        {
            Assert.IsFalse(_sm.TransitionTo(TournamentPhase.Complete));
            Assert.AreEqual(TournamentPhase.Idle, _sm.Current, "A rejected transition must not change phase.");
        }

        [Test]
        public void SamePhase_IsNoOp()
        {
            _sm.TransitionTo(TournamentPhase.Lobby);
            Assert.IsFalse(_sm.TransitionTo(TournamentPhase.Lobby), "Transitioning to the current phase is a no-op.");
        }

        [Test]
        public void ResetToIdle_FromAnyPhase()
        {
            _sm.TransitionTo(TournamentPhase.Lobby);
            _sm.TransitionTo(TournamentPhase.InGame);
            _sm.ResetToIdle();
            Assert.AreEqual(TournamentPhase.Idle, _sm.Current);
        }
    }
}
#endif
