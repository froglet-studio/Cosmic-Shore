using UnityEngine;

namespace CosmicShore.Game
{
    public class LocalCrystalManager : CrystalManager
    {
        private void OnEnable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += MiniGameTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
        }

        private void OnDisable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
        }

        public override void RespawnCrystal() =>
            UpdateCrystalPos(CalculateNewSpawnPos());

        public override void ExplodeCrystal(Crystal.ExplodeParams explodeParams) =>
            cellData.Crystal.Explode(explodeParams);
        
        void MiniGameTurnStarted() => Spawn(CalculateSpawnPos());
        
        void OnTurnEnded()
        {
            cellData.Crystal.DestroyCrystal();
        }
    }
}