using UnityEngine;

namespace CosmicShore.Game
{
    public class LocalCrystalManager : CrystalManager
    {
        private void OnEnable()
        {
            gameData.OnMiniGmaeTurnStarted.OnRaised += MiniGmaeTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
        }

        private void OnDisable()
        {
            gameData.OnMiniGmaeTurnStarted.OnRaised -= MiniGmaeTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
        }

        public override void RespawnCrystal() =>
            UpdateCrystalPos(CalculateNewSpawnPos());

        public override void ExplodeCrystal(Crystal.ExplodeParams explodeParams) =>
            cellData.Crystal.Explode(explodeParams);

        protected override void Spawn(Vector3 spawnPos)
        {
            var crystal = Instantiate(crystalPrefab, spawnPos, Quaternion.identity, transform);
            crystal.InjectDependencies(this);
            cellData.Crystal = crystal;
            TryInitializeAndAdd(crystal);
            cellData.OnCrystalSpawned.Raise();
        }
        
        void MiniGmaeTurnStarted() => Spawn(CalculateSpawnPos());
        
        void OnTurnEnded()
        {
            cellData.Crystal.DestroyCrystal();
        }
    }
}