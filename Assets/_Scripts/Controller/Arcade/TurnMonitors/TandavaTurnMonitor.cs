using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md). There is no count to race to: the turn
    /// ends (server-side) the moment the swarm's outcome stops being Running - it escaped, or the pilots wiped it out,
    /// starved it or broke its final form - which <see cref="TandavaController"/> publishes into the mode's
    /// <see cref="TandavaScoringRuleSO"/>. The display channel shows what pilots must always know: the form the swarm
    /// wears and how close it is to the next one ("Serpent 62%").
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
            onUpdateTurnMonitorDisplay.Raise(controller.IsFinalForm
                ? controller.FormName
                : $"{controller.FormName} {UnityEngine.Mathf.RoundToInt(100f * controller.EvolveProgress)}%");
        }
    }
}
