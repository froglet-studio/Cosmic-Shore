using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Utilities; // for PrismEventChannelWithReturnSO

namespace CosmicShore.Game
{
    public enum PrismType
    {
        Explosion,
        Implosion
    }
    
    public class PrismFactory : MonoBehaviour
    {
        [Header("Pool Managers")]
        [SerializeField] private PrismExplosionPoolManager explosionPool;
        [SerializeField] private PrismImplosionPoolManager implosionPool;
        // Add more later: PrismShockwavePoolManager, PrismDisintegrationPoolManager, etc.

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        [Header("Event Channels")]
        [SerializeField] private PrismEventChannelWithReturnSO _onPrismSpawnedEventChannel;

        #region Lifecycle
        private void OnEnable()
        {
            if (_onPrismSpawnedEventChannel)
                _onPrismSpawnedEventChannel.OnEventReturn += OnPrismSpawnedEventRaised;
        }

        private void OnDisable()
        {
            if (_onPrismSpawnedEventChannel)
                _onPrismSpawnedEventChannel.OnEventReturn -= OnPrismSpawnedEventRaised;
        }
        #endregion

        #region Event Handling
        private PrismReturnEventData OnPrismSpawnedEventRaised(PrismEventData data)
        {
            if (data == null)
            {
                Debug.LogError("[PrismFactory] Received null PrismEventData");
                return new PrismReturnEventData { SpawnedObject = null };
            }

            GameObject spawned = null;

            switch (data.PrismType)
            {
                case PrismType.Explosion :
                    spawned = SpawnExplosion(data.OwnTeam, data.Position, data.Rotation);
                    break;

                case PrismType.Implosion :
                    spawned = SpawnImplosion(data.OwnTeam, data.Position, data.Rotation);
                    break;

                // Add more cases here later
                // case "Shockwave":
                //     spawned = SpawnShockwave(data.OwnTeam, data.Position, data.Rotation);
                //     break;
            }

            return new PrismReturnEventData { SpawnedObject = spawned };
        }
        #endregion

        #region Public API
        public GameObject SpawnExplosion(Teams team, Vector3 pos, Quaternion rot)
        {
            var obj = explosionPool?.Get(pos, rot);
            ConfigureForTeam(obj.gameObject, team);
            return obj.gameObject;
        }

        public GameObject SpawnImplosion(Teams team, Vector3 pos, Quaternion rot)
        {
            var obj = implosionPool?.Get(pos, rot);
            ConfigureForTeam(obj.gameObject, team);
            return obj.gameObject;
        }
        #endregion

        #region Helpers
        private void ConfigureForTeam(GameObject obj, Teams team)
        {
            if (obj == null) return;

            if (_themeManagerData == null || _themeManagerData.TeamMaterialSets == null)
            {
                Debug.LogWarning("[PrismFactory] ThemeManagerData or TeamMaterialSets is null.");
                return;
            }

            if (!_themeManagerData.TeamMaterialSets.TryGetValue(team, out var materialSet))
            {
                Debug.LogWarning($"[PrismFactory] No material set for team '{team}'.");
                return;
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && materialSet != null)
            {
                // Apply basic material set — refine later if different prisms need different materials
                renderer.material = materialSet.ExplodingBlockMaterial;
            }
        }
        #endregion
    }
}
