using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Utilities; // for PrismEventChannelWithReturnSO

namespace CosmicShore.Game
{
    public enum PrismType
    {
        Dolphin,
        Serpent,
        Sparrow,
        Manta,
        Squirrel,
        Rhino,
        Interactive,
        Explosion,
        Implosion,
        Grow
    }
    
    public class PrismFactory : MonoBehaviour
    {
        private static readonly int DarkColorID = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightColorID = Shader.PropertyToID("_BrightColor");
        
        [Header("Pool Managers")]
        [SerializeField] private InteractivePrismPoolManager dolphinPrismPool;
        [SerializeField] private InteractivePrismPoolManager serpentPrismPool;
        [SerializeField] private InteractivePrismPoolManager sparrowPrismPool;
        [SerializeField] private InteractivePrismPoolManager mantaPrismPool;
        [SerializeField] private InteractivePrismPoolManager squirrelPrismPool;
        [SerializeField] private InteractivePrismPoolManager rhinoPrismPool;
        [SerializeField] private InteractivePrismPoolManager interactivePrismPool;
        
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
                case PrismType.Interactive:
                    spawned = SpawnInteractivePrism(data);
                    break;
                case PrismType.Dolphin:
                    spawned = SpawnDolphinPrism(data);
                    break;
                case PrismType.Serpent:
                    spawned = SpawnSerpentPrism(data);
                    break;
                case PrismType.Rhino:
                    spawned = SpawnRhinoPrism(data);
                    break;
                case PrismType.Squirrel:
                    spawned = SpawnSquirrelPrism(data);
                    break;
                case PrismType.Manta:
                    spawned = SpawnMantaPrism(data);
                    break;
                case PrismType.Sparrow:
                    spawned = SpawnSparrowPrism(data);
                    break;
                
                case PrismType.Explosion :
                    spawned = SpawnExplosion(data);
                    break;

                case PrismType.Implosion :
                    spawned = SpawnImplosion(data);
                    break;
                
                case PrismType.Grow :
                    spawned = SpawnGrow(data);
                    break;

                // Add more cases here later
                // case "Shockwave":
                //     spawned = SpawnShockwave(data.OwnTeam, data.Position, data.Rotation);
                //     break;
            }

            return new PrismReturnEventData { SpawnedObject = spawned };
        }
        #endregion

        #region Spawners

        
        GameObject SpawnInteractivePrism(PrismEventData data)
        {
            if (interactivePrismPool == null) { Debug.LogWarning("[PrismFactory] interactivePrismPool not set."); return null; }
            var prism = interactivePrismPool.Get(data.SpawnPosition, data.Rotation, interactivePrismPool.transform);
            return prism ? prism.gameObject : null;
        }
            
        GameObject SpawnDolphinPrism(PrismEventData data)
        {
            if (dolphinPrismPool == null) { Debug.LogWarning("[PrismFactory] dolphinPrismPool not set."); return null; }
            var prism = dolphinPrismPool.Get(data.SpawnPosition, data.Rotation, dolphinPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnSerpentPrism(PrismEventData data)
        {
            if (serpentPrismPool == null) { Debug.LogWarning("[PrismFactory] serpentPrismPool not set."); return null; }
            var prism = serpentPrismPool.Get(data.SpawnPosition, data.Rotation, serpentPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnSparrowPrism(PrismEventData data)
        {
            if (sparrowPrismPool == null) { Debug.LogWarning("[PrismFactory] sparrowPrismPool not set."); return null; }
            var prism = sparrowPrismPool.Get(data.SpawnPosition, data.Rotation, sparrowPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnMantaPrism(PrismEventData data)
        {
            if (mantaPrismPool == null) { Debug.LogWarning("[PrismFactory] mantaPrismPool not set."); return null; }
            var prism = mantaPrismPool.Get(data.SpawnPosition, data.Rotation, mantaPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnSquirrelPrism(PrismEventData data)
        {
            if (squirrelPrismPool == null) { Debug.LogWarning("[PrismFactory] squirrelPrismPool not set."); return null; }
            var prism = squirrelPrismPool.Get(data.SpawnPosition, data.Rotation, squirrelPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnRhinoPrism(PrismEventData data)
        {
            if (rhinoPrismPool == null) { Debug.LogWarning("[PrismFactory] rhinoPrismPool not set."); return null; }
            var prism = rhinoPrismPool.Get(data.SpawnPosition, data.Rotation, rhinoPrismPool.transform);
            return prism ? prism.gameObject : null;
        }
        
        GameObject SpawnExplosion(PrismEventData data)
        {
            var obj = explosionPool?.Get(data.SpawnPosition, data.Rotation, explosionPool.transform);
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain);
            obj.TriggerExplosion(data.Velocity);
            return obj.gameObject;
        }

        GameObject SpawnImplosion(PrismEventData data)
        {
            var obj = implosionPool?.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain);
            obj.StartImplosion(data.TargetTransform);
            return obj.gameObject;
        }
        
        GameObject SpawnGrow(PrismEventData data)
        {
            var obj = implosionPool?.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain);

            obj.OnReturnToPool += _ =>
            {
                data.OnGrowCompleted?.Invoke();
            };

            obj.StartGrow(data.TargetTransform); // adjust if different

            return obj.gameObject;
        }
        
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
