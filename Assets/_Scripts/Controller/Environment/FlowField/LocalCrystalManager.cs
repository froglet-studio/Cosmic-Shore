using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class LocalCrystalManager : CrystalManager
    {
        [Header("Early Spawn")]
        [Tooltip("When true, crystals spawn once all players are ready (before the ready button) instead of on turn start.")]
        [SerializeField] private bool spawnOnClientReady;

        private void OnEnable()
        {
            if (spawnOnClientReady)
                gameData.OnClientReady.OnRaised += MiniGameTurnStarted;
            else
                gameData.OnMiniGameTurnStarted.OnRaised += MiniGameTurnStarted;

            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }

        private void OnDisable()
        {
            if (spawnOnClientReady)
                gameData.OnClientReady.OnRaised -= MiniGameTurnStarted;
            else
                gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStarted;

            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }

        private void OnResetForReplay()
        {
            ResetSpawnState();
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
        
        public void ManualTurnEnded() => OnTurnEnded();
        
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