using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerDogFightController : MultiplayerJoustController
    {
        [Header("Dog Fight — Intensity Scaling")]
        [Tooltip("Base hits needed at Intensity 1. Multiplied by intensity level at runtime.")]
        [SerializeField] int baseHitsNeeded = 100;

        public int BaseHitsNeeded => baseHitsNeeded;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            int intensity = Mathf.Max(1, gameData.SelectedIntensity.Value);
            int scaledHits = baseHitsNeeded * intensity;
            joustTurnMonitor.SetCollisionsNeeded(scaledHits);

            CSDebug.Log($"[DogFightController] Intensity={intensity} HitsNeeded={scaledHits} (base={baseHitsNeeded})");
        }
    }
}
