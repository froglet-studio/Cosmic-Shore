using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Scoring for LifeForm kills in Wildlife Blitz mode.
    /// Listens to LifeForm.OnLifeFormDeath event.
    /// </summary>
    public class LifeFormsKilledScoring : BaseScoring
    {
        private int totalLifeFormsKilled;
        public float ScorePerKill => scoreMultiplier;

        public LifeFormsKilledScoring(IScoreTracker tracker,GameDataSO gameData, float multiplier)
            : base(tracker,gameData, multiplier)
        {
        }

        public override void Subscribe()
        {
            // PORT Deviation (SA1, restore when LifeForm ports): LifeForm.OnLifeFormDeath += HandleLifeFormDeath;
            totalLifeFormsKilled = 0;
        }

        public override void Unsubscribe()
        {
            // PORT Deviation (SA1, restore when LifeForm ports): LifeForm.OnLifeFormDeath -= HandleLifeFormDeath;
        }

        void HandleLifeFormDeath(string killerName, int cellId)
        {
            totalLifeFormsKilled++;
            Score += scoreMultiplier;
        }

        public int GetTotalLifeFormsKilled() => totalLifeFormsKilled;
    }
}
