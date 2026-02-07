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

        public override void RespawnCrystal(int crystalId)
        {
            var newPos = CalculateNewSpawnPos(crystalId);
            UpdateCrystalPos(crystalId, newPos);
        }

        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
        {
            if (!cellData.TryGetCrystalById(crystalId, out var crystal)) return;
            // [Visual Note] Safety check to prevent exploding dead objects
            if (crystal != null) 
                crystal.Explode(explodeParams);
        }

        void MiniGameTurnStarted()
        {
            SpawnBatchIfMissing();
        }
        
        void OnTurnEnded()
        {
            var crystals = cellData.Crystals;
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                var crystal = crystals[i];
                if (crystal)
                {
                    crystal.DestroyCrystal();
                }
            }
        }
    }
}