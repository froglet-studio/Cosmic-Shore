using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md). There is no count to race to: the turn
    /// ends (server-side) the moment the swarm's outcome stops being Running - it escaped, or the pilots wiped it out,
    /// starved it or broke its final form - which <see cref="TandavaController"/> publishes into the mode's
    /// <see cref="TandavaScoringRuleSO"/>. The display channel shows what pilots must always know, which changes with the
    /// story: the form and how close it is to the next one ("Serpent (small) 62%"); where the banked Bull is going; the
    /// ring of fire and the drum during the dance ("Ring of fire 4/9 - drum 12 s"); the membrane's clock while it
    /// starves; and the final form by name.
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
            int clock = UnityEngine.Mathf.CeilToInt(controller.StoryClock);
            onUpdateTurnMonitorDisplay.Raise(controller.Phase switch
            {
                TandavaPhase.Dance => $"Ring of fire {controller.FlamesOut}/{controller.FlamesToBreak} - drum {clock} s",
                TandavaPhase.ToDance => $"{controller.FormName} - to the dance ground",
                TandavaPhase.Starving => clock >= 0 ? $"{controller.FormName} - starving {clock} s" : $"{controller.FormName} - starving",
                _ when controller.IsFinalForm => controller.FormName,
                _ => $"{controller.FormName} {UnityEngine.Mathf.RoundToInt(100f * controller.EvolveProgress)}%",
            });
        }
    }
}
