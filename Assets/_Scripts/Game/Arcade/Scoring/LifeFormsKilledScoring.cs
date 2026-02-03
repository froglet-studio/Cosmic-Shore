using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    /// <summary>
    /// Scoring for LifeForm kills in Wildlife Blitz mode.
    /// Listens to LifeForm.OnLifeFormDeath event.
    /// </summary>
    public class LifeFormsKilledScoring : BaseScoring
    {
        private int totalLifeFormsKilled;
        public float ScorePerKill => scoreMultiplier;

        public LifeFormsKilledScoring(GameDataSO gameData, float multiplier) 
            : base(gameData, multiplier)
        {
        }

        public override void Subscribe()
        {
            LifeForm.OnLifeFormDeath += HandleLifeFormDeath;
            totalLifeFormsKilled = 0;
        }

        public override void Unsubscribe()
        {
            LifeForm.OnLifeFormDeath -= HandleLifeFormDeath;
        }

        void HandleLifeFormDeath(string killerName, int cellId)
        {
            totalLifeFormsKilled++;
            Score += scoreMultiplier; 
        }

        public int GetTotalLifeFormsKilled() => totalLifeFormsKilled;
    }
}