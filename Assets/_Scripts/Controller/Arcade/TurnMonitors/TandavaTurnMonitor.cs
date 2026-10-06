using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md). There is no count to race to: the turn
    /// ends (server-side) the moment the swarm's outcome stops being Running - the Sea Lion ate its last feast, or the
    /// pilots shattered it, starved it, broke its dance or held it off until the clock ran out - which
    /// <see cref="TandavaController"/> publishes into the mode's <see cref="TandavaScoringRuleSO"/>.
    ///
    /// <para>The top-left goal stack draws Tandava's rows itself (TandavaController is its <c>IGoalSource</c>: the form
    /// and how close it is to the next, what the creature is doing and its body, the clock or the drum). This monitor's
    /// display channel is what makes the stack REBUILD each tick, so it raises a short readout that also stands on its
    /// own (the progress, or the halo) for any HUD that still reads the raw string.</para>
    /// </summary>
    public class TandavaTurnMonitor : TurnMonitor
    {
        public override void StartMonitor()
        {
            base.StartMonitor();
            UpdateDisplay();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer || gameData.ScoringRule == null) return false;
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate() => UpdateDisplay();

        void UpdateDisplay()
        {
            if (!onUpdateTurnMonitorDisplay) return;
            var controller = TandavaController.Current;
            if (!controller) return;
            onUpdateTurnMonitorDisplay.Raise(controller.InDance
                ? $"{controller.HaloBroken}/{controller.HaloToBreak}"
                : $"{UnityEngine.Mathf.RoundToInt(100f * controller.EvolveProgress)}%");
        }
    }
}
