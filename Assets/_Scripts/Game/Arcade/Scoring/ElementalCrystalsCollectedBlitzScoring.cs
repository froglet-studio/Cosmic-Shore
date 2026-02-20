using CosmicShore.Game;
using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    /// <summary>
    /// Scoring for Elemental Crystal collection in Wildlife Blitz mode.
    /// Listens to ElementalCrystalImpactor.OnCrystalCollected event.
    /// </summary>
    public class ElementalCrystalsCollectedBlitzScoring : BaseScoring
    {
        private int totalCrystalsCollected;

        public ElementalCrystalsCollectedBlitzScoring(IScoreTracker tracker, GameDataSO gameData, float multiplier) 
            : base(tracker, gameData, multiplier)
        {
        }

        public override void Subscribe()
        {
            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            totalCrystalsCollected = 0;
        }

        public override void Unsubscribe()
        {
            ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
        }

        void HandleCrystalCollected(string playerName)
        {
            if (GameData.LocalPlayer == null || GameData.LocalPlayer.Name != playerName) return;

            totalCrystalsCollected++;
            UnityEngine.Debug.Log($"<color=cyan>[COLLECT] {playerName} collected Crystal #{totalCrystalsCollected}! +{scoreMultiplier} pts</color>");

            Score += scoreMultiplier;
        }

        public float GetScoreMultiplier() => scoreMultiplier;
        public int GetTotalCrystalsCollected() => totalCrystalsCollected;
    }
}