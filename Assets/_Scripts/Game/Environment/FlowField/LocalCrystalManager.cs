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

        public override void RespawnCrystal(int crystalId) =>
            UpdateCrystalPos(crystalId, CalculateNewSpawnPos(crystalId));

        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
        {
            if (cellData.TryGetCrystalById(crystalId, out var crystal))
                crystal.Explode(explodeParams);
        }

        void MiniGameTurnStarted()
        {
            // Spawn N crystals (id = 1..batchCount), each gets a position from CalculateSpawnPos()
            SpawnBatchIfMissing();
        }
        
        void OnTurnEnded()
        {
            var crystals = cellData.Crystals;
            foreach (var crystal in crystals)
                crystal.DestroyCrystal();
        }
    }
}