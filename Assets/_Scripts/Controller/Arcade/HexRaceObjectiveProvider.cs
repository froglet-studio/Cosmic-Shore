using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for HexRace: the local player's next crystal — the
    /// closest live <see cref="Crystal"/> to the player's vessel.
    /// </summary>
    public class HexRaceObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        [Header("Refresh")]
        [Tooltip("How often (seconds) to rescan the scene for live Crystal objects.")]
        [SerializeField] float rescanInterval = 0.5f;

        Crystal[] _crystals;
        float _nextRescanTime;

        public bool TryGetObjective(out Transform target)
        {
            target = null;

            if (gameData == null) return false;

            var localVessel = gameData.LocalPlayer?.Vessel;
            if (localVessel == null) return false;

            RescanIfDue();
            if (_crystals == null || _crystals.Length == 0) return false;

            var origin = localVessel.Transform.position;
            float bestSqr = float.MaxValue;
            Transform bestTransform = null;

            for (int i = 0; i < _crystals.Length; i++)
            {
                var crystal = _crystals[i];
                if (crystal == null || crystal.IsExploding) continue;

                float sqr = (crystal.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestTransform = crystal.transform;
                }
            }

            target = bestTransform;
            return target != null;
        }

        void RescanIfDue()
        {
            if (Time.time < _nextRescanTime && _crystals != null) return;
            _nextRescanTime = Time.time + rescanInterval;
            _crystals = FindObjectsByType<Crystal>(FindObjectsSortMode.None);
        }
    }
}
