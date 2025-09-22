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
        private static readonly int DarkColorID = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightColorID = Shader.PropertyToID("_BrightColor");
        
        [Header("Pool Managers")]
        [SerializeField] private PrismExplosionPoolManager explosionPool;
        [SerializeField] private PrismImplosionPoolManager implosionPool;
        // Add more later: PrismShockwavePoolManager, PrismDisintegrationPoolManager, etc.

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        [Header("Event Channels")]
        [SerializeField] private PrismEventChannelWithReturnSO _onPrismSpawnedEventChannel;

        private MaterialPropertyBlock mpb;
        
        #region Lifecycle
        private void OnEnable()
        {
            if (_onPrismSpawnedEventChannel)
                _onPrismSpawnedEventChannel.OnEventReturn += OnPrismSpawnedEventRaised;
            
            mpb = new MaterialPropertyBlock();
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
                    spawned = SpawnExplosion(data);
                    break;

                case PrismType.Implosion :
                    spawned = SpawnImplosion(data);
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
        GameObject SpawnExplosion(PrismEventData data)
        {
            var obj = explosionPool?.Spawn(data.SpawnPosition, data.Rotation, data.Velocity);
            ConfigureForTeam(obj.gameObject, data.ownDomain);
            return obj.gameObject;
        }

        GameObject SpawnImplosion(PrismEventData data)
        {
            var obj = implosionPool?.Spawn(data.SpawnPosition, data.Rotation, data.TargetTransform);
            ConfigureForTeam(obj.gameObject, data.ownDomain);
            return obj.gameObject;
        }
        #endregion

        #region Helpers
        private void ConfigureForTeam(GameObject obj, Domains domain)
        {
            if (!obj) return;

            if (!_themeManagerData || !_themeManagerData.ColorSet)
            {
                Debug.LogWarning("[PrismFactory] ThemeManagerData or ColorSet is null.");
                return;
            }

            if (!_themeManagerData.TeamMaterialSets.TryGetValue(domain, out var materialSet))
            {
                Debug.LogWarning($"[PrismFactory] No material set for team '{domain}'.");
                return;
            }

            if (!_themeManagerData.ColorSet.TryGetColorSetByDomain(domain, out var colorSet))
                return;

            var renderer = obj.GetComponent<Renderer>();
            if (renderer && materialSet)
            {
                renderer.GetPropertyBlock(mpb);
                // Apply basic material set — refine later if different prisms need different materials
                mpb.SetColor(DarkColorID, colorSet.OutsideBlockColor);
                mpb.SetColor(BrightColorID, colorSet.InsideBlockColor);
                renderer.SetPropertyBlock(mpb);
            }
        }
        #endregion
    }
}
