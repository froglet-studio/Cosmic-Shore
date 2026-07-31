using System;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public enum PrismType
    {
        Dolphin = 0,
        Serpent = 1,
        Sparrow = 2,
        Manta = 3,
        Squirrel = 4,
        Rhino = 5,
        Interactive = 6,
        Explosion = 7,
        Implosion = 8,
        Grow = 9,
        // Fast-growing, collider-live-on-spawn prisms drawn from a dedicated pool: a
        // surface a skimmer can boost off, but usually flown past without a body hit
        // (Squirrel tube + joust danger-block formations). See PrismFactory.SpawnBoostPrism.
        Boost = 10,
    }
    
    public class PrismFactory : MonoBehaviour
    {
        private static readonly int DarkColorID = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightColorID = Shader.PropertyToID("_BrightColor");

        // Per-frame VFX spawn caps to prevent pool exhaustion when AOE hits many prisms
        private const int MaxExplosionVFXPerFrame = 64;
        private const int MaxImplosionVFXPerFrame = 64;
        private int _explosionVFXCount;
        private int _implosionVFXCount;
        private int _lastExplosionFrame;
        private int _lastImplosionFrame;

        [Header("Pool Managers")]
        [SerializeField] private InteractivePrismPoolManager dolphinPrismPool;
        [SerializeField] private InteractivePrismPoolManager serpentPrismPool;
        [SerializeField] private InteractivePrismPoolManager sparrowPrismPool;
        [SerializeField] private InteractivePrismPoolManager mantaPrismPool;
        [SerializeField] private InteractivePrismPoolManager squirrelPrismPool;
        [SerializeField] private InteractivePrismPoolManager rhinoPrismPool;
        [SerializeField] private InteractivePrismPoolManager interactivePrismPool;
        [Tooltip("Dedicated pool for fast-growing, collider-live-on-spawn boost prisms - the " +
                 "BoostRingBuilder rings (omnicrystal, joust, Squirrel tube). Serves the " +
                 "FastGrowPrism prefab. Separate from the shared pools so the waitTime/GrowthRate " +
                 "overrides can't leak into normal trail/AOE prisms.")]
        [SerializeField] private InteractivePrismPoolManager boostPrismPool;

        [SerializeField] private PrismExplosionPoolManager explosionPool;
        [SerializeField] private PrismImplosionPoolManager implosionPool;
        // Add more later: PrismShockwavePoolManager, PrismDisintegrationPoolManager, etc.

        [Header("Boost Prism Tuning")]
        [Tooltip("Grow-in speed for boost prisms (fast bloom). PrismScaleManager clamps " +
                 "growthRate * deltaTime into [0.05, 0.1] lerp/frame, so values below ~6 are " +
                 "indistinguishable from the default; 8 pins the bloom at the max speed across " +
                 "framerates. The collider never waits on this - boost prisms hold a full-size " +
                 "collider from frame 0 (Prism.HoldColliderAtFullSize).")]
        [SerializeField] private float boostPrismGrowthRate = 8f;

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
                CSDebug.LogError("[PrismFactory] Received null PrismEventData");
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

                case PrismType.Boost :
                    spawned = SpawnBoostPrism(data);
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
            if (interactivePrismPool == null) { CSDebug.LogWarning("[PrismFactory] interactivePrismPool not set."); return null; }
            var prism = interactivePrismPool.Get(data.SpawnPosition, data.Rotation, interactivePrismPool.transform);
            return prism ? prism.gameObject : null;
        }
            
        GameObject SpawnDolphinPrism(PrismEventData data)
        {
            if (dolphinPrismPool == null) { CSDebug.LogWarning("[PrismFactory] dolphinPrismPool not set."); return null; }
            var prism = dolphinPrismPool.Get(data.SpawnPosition, data.Rotation, dolphinPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnSerpentPrism(PrismEventData data)
        {
            if (serpentPrismPool == null) { CSDebug.LogWarning("[PrismFactory] serpentPrismPool not set."); return null; }
            var prism = serpentPrismPool.Get(data.SpawnPosition, data.Rotation, serpentPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnSparrowPrism(PrismEventData data)
        {
            if (sparrowPrismPool == null) { CSDebug.LogWarning("[PrismFactory] sparrowPrismPool not set."); return null; }
            var prism = sparrowPrismPool.Get(data.SpawnPosition, data.Rotation, sparrowPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnMantaPrism(PrismEventData data)
        {
            if (mantaPrismPool == null) { CSDebug.LogWarning("[PrismFactory] mantaPrismPool not set."); return null; }
            var prism = mantaPrismPool.Get(data.SpawnPosition, data.Rotation, mantaPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnSquirrelPrism(PrismEventData data)
        {
            if (squirrelPrismPool == null) { CSDebug.LogWarning("[PrismFactory] squirrelPrismPool not set."); return null; }
            var prism = squirrelPrismPool.Get(data.SpawnPosition, data.Rotation, squirrelPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        GameObject SpawnRhinoPrism(PrismEventData data)
        {
            if (rhinoPrismPool == null) { CSDebug.LogWarning("[PrismFactory] rhinoPrismPool not set."); return null; }
            var prism = rhinoPrismPool.Get(data.SpawnPosition, data.Rotation, rhinoPrismPool.transform);
            return prism ? prism.gameObject : null;
        }

        // Fast-growing, collider-live-on-spawn prisms for boost-off surfaces (Squirrel tube,
        // joust danger blocks). waitTime 0 → the collider comes on the frame after Initialize
        // instead of after the 0.6s spawn window, so a skimmer can boost off it right away;
        // a high GrowthRate blooms it in fast. These timing overrides are safe because this is
        // a DEDICATED pool - they never recycle into a normal trail/AOE prism.
        GameObject SpawnBoostPrism(PrismEventData data)
        {
            if (boostPrismPool == null) { CSDebug.LogWarning("[PrismFactory] boostPrismPool not set."); return null; }
            var prism = boostPrismPool.Get(data.SpawnPosition, data.Rotation, boostPrismPool.transform);
            if (!prism) return null;

            prism.waitTime = 0f;
            prism.SetGrowthRate(boostPrismGrowthRate);

            // Pooled reuse: prismProperties kind flags persist across pool lives and
            // Initialize re-engages from them, so a prior life's shield/danger state
            // would leak into this one (the same boost pool serves shielded omnicrystal
            // rings AND danger joust rings). Clear them here; the consumer applies its
            // own kind after Initialize (see BoostRingBuilder.LayOne).
            if (prism.prismProperties != null)
            {
                prism.prismProperties.IsShielded = false;
                prism.prismProperties.IsSuperShielded = false;
                prism.prismProperties.IsDangerous = false;
                prism.prismProperties.speedDebuffAmount = 0f;
            }
            return prism.gameObject;
        }
        
        GameObject SpawnExplosion(PrismEventData data)
        {
            // Cap explosion VFX per frame to prevent pool exhaustion.
            // Prism destruction still happens (Damage already applied), we just skip the visual.
            if (Time.frameCount != _lastExplosionFrame)
            {
                _lastExplosionFrame = Time.frameCount;
                _explosionVFXCount = 0;
            }
            if (_explosionVFXCount >= MaxExplosionVFXPerFrame)
                return null;
            _explosionVFXCount++;

            var obj = explosionPool?.Get(data.SpawnPosition, data.Rotation, explosionPool.transform);
            if (obj == null) return null;
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain);
            obj.TriggerExplosion(data.Velocity, data.DebrisSpeedLimit);
            return obj.gameObject;
        }

        GameObject SpawnImplosion(PrismEventData data)
        {
            // Cap implosion VFX per frame for the same reason as explosions.
            if (Time.frameCount != _lastImplosionFrame)
            {
                _lastImplosionFrame = Time.frameCount;
                _implosionVFXCount = 0;
            }
            if (_implosionVFXCount >= MaxImplosionVFXPerFrame)
                return null;
            _implosionVFXCount++;

            var obj = implosionPool?.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
            if (obj == null) return null;
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

            // Self-unsubscribing callback so lambdas don't accumulate on pool reuse
            Action<PrismImplosion> growCallback = null;
            growCallback = _ =>
            {
                obj.OnReturnToPool -= growCallback;
                data.OnGrowCompleted?.Invoke();
            };
            obj.OnReturnToPool += growCallback;

            obj.StartGrow(data.TargetTransform);

            return obj.gameObject;
        }
        
        private void ConfigureForTeam(GameObject obj, Domains domain)
        {
            if (!obj) return;

            if (!_themeManagerData || !_themeManagerData.ColorSet)
            {
                CSDebug.LogWarning("[PrismFactory] ThemeManagerData or ColorSet is null.");
                return;
            }

            if (!_themeManagerData.TeamMaterialSets.TryGetValue(domain, out var materialSet))
            {
                CSDebug.LogWarning($"[PrismFactory] No material set for team '{domain}'.");
                return;
            }

            if (!_themeManagerData.ColorSet.TryGetColorSetByDomain(domain, out var colorSet))
                return;

            // Effect components route team colors to whichever render path is
            // active (companion entity overrides or legacy MPB) when their
            // animation starts — the factory just hands them the palette.
            if (obj.TryGetComponent(out CosmicShore.Utility.PrismExplosion explosion))
            {
                explosion.SetTeamColors(colorSet.InsideBlockColor, colorSet.OutsideBlockColor);
                return;
            }
            if (obj.TryGetComponent(out CosmicShore.Utility.PrismImplosion implosion))
            {
                implosion.SetTeamColors(colorSet.InsideBlockColor, colorSet.OutsideBlockColor);
                return;
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer && materialSet)
            {
                renderer.GetPropertyBlock(mpb);
                // Apply basic material set - refine later if different prisms need different materials
                mpb.SetColor(DarkColorID, colorSet.OutsideBlockColor);
                mpb.SetColor(BrightColorID, colorSet.InsideBlockColor);
                renderer.SetPropertyBlock(mpb);
            }
        }
        #endregion
    }
}
