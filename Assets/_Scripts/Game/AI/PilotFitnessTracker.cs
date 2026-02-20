using UnityEngine;

namespace CosmicShore.Game.AI
{
    /// <summary>
    /// Tracks AI race performance and reports fitness to PilotEvolution.
    /// Attach to the same GameObject as AIPilot (the vessel).
    /// Subscribes to the same static events the HexRaceScoreTracker uses.
    /// </summary>
    public class PilotFitnessTracker : MonoBehaviour
    {
        [SerializeField] PilotEvolution evolution;

        [Header("Fitness Weights")]
        [SerializeField] float crystalWeight = 100f;
        [SerializeField] float collisionPenalty = 30f;
        [SerializeField] float boostTimeWeight = 5f;
        [SerializeField] float timePenaltyWeight = 1f;

        string _playerName;
        bool _tracking;
        int _crystalsCollected;
        int _prismCollisions;
        float _highBoostTime;
        float _raceTime;
        int _genomeIndex = -1;

        IVesselStatus _vesselStatus;

        /// <summary>
        /// Called by AIPilot after CheckoutGenome to bind this tracker to a specific genome.
        /// </summary>
        public void SetGenomeIndex(int genomeIndex)
        {
            _genomeIndex = genomeIndex;
        }

        public void StartTracking(IVesselStatus vesselStatus)
        {
            _vesselStatus = vesselStatus;
            _playerName = vesselStatus.PlayerName;
            _crystalsCollected = 0;
            _prismCollisions = 0;
            _highBoostTime = 0f;
            _raceTime = 0f;
            _tracking = true;

            ElementalCrystalImpactor.OnCrystalCollected += OnCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision += OnPrismCollision;
        }

        public void StopTracking()
        {
            if (!_tracking) return;
            _tracking = false;

            ElementalCrystalImpactor.OnCrystalCollected -= OnCrystalCollected;
            VesselResetBoostPrismEffectSO.OnPrismCollision -= OnPrismCollision;

            ReportFitness();
        }

        void OnDisable()
        {
            if (_tracking) StopTracking();
        }

        void Update()
        {
            if (!_tracking || _vesselStatus == null) return;

            _raceTime += Time.deltaTime;

            if (_vesselStatus.IsBoosting && _vesselStatus.BoostMultiplier >= 2.0f)
                _highBoostTime += Time.deltaTime;
        }

        void OnCrystalCollected(string playerName)
        {
            if (_tracking && playerName == _playerName)
                _crystalsCollected++;
        }

        void OnPrismCollision(string playerName)
        {
            if (_tracking && playerName == _playerName)
                _prismCollisions++;
        }

        void ReportFitness()
        {
            if (evolution == null || _genomeIndex < 0) return;

            float fitness = _crystalsCollected * crystalWeight
                          - _prismCollisions * collisionPenalty
                          + _highBoostTime * boostTimeWeight
                          - _raceTime * timePenaltyWeight;

            evolution.ReturnFitness(_genomeIndex, fitness);

            Debug.Log($"[PilotFitness] {_playerName} | Gen {evolution.Generation} | " +
                $"Genome {_genomeIndex} | Fitness: {fitness:F1} | " +
                $"Crystals: {_crystalsCollected} | Collisions: {_prismCollisions} | " +
                $"BoostTime: {_highBoostTime:F1}s | RaceTime: {_raceTime:F1}s");
        }
    }
}
