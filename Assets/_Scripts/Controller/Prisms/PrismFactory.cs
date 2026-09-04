using System;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    // Explicit values pin the serialized meaning of every prismType field in
    // prefabs/SO assets (CLAUDE.md: enums always carry static numeric values).
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
        // Gibbon (Spider) tether anchor prisms. 11, not 10: upstream claimed 10 for
        // Boost while this vessel lived on a branch. See PrismFactory.SpawnSpiderPrism.
        Spider = 11,
    }
    
    public class PrismFactory : MonoBehaviour
    {
        private static readonly int DarkColorID = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightColorID = Shader.PropertyToID("_BrightColor");

        /// <summary>
        /// Benchmark/diagnostic override of the retired per-frame pooled-death VFX
        /// caps. Death visuals are batched (D4) and no longer consult a spawn
        /// budget; the explosion harness still writes this so lift/restore stays
        /// a no-op rather than a missing-field compile break. Gameplay never sets it.
        /// </summary>
        public static int VFXBudgetPerFrameOverride = 0;

        /// <summary>
        /// Benchmark/diagnostic switch: disables the pressure-shortening of effect
        /// durations (every death animates at full length regardless of how many
        /// effects are live). Shared home so <see cref="PrismExplosion.PressuredDuration"/>
        /// (still used if a pooled explosion is ever Get()d) can gate on one flag.
        /// Gameplay never sets this.
        /// </summary>
        public static bool EffectPressureScalingDisabled = false;

        static bool _loggedDeathVisualRefuse;

        // Benchmark-harness overrides; a play exit mid-run must not leave them lifted.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBudgetOverrides()
        {
            VFXBudgetPerFrameOverride = 0;
            EffectPressureScalingDisabled = false;
            _loggedDeathVisualRefuse = false;
        }

        [Header("Pool Managers")]
        [SerializeField] private InteractivePrismPoolManager dolphinPrismPool;
        [SerializeField] private InteractivePrismPoolManager serpentPrismPool;
        [SerializeField] private InteractivePrismPoolManager sparrowPrismPool;
        [SerializeField] private InteractivePrismPoolManager mantaPrismPool;
        [SerializeField] private InteractivePrismPoolManager squirrelPrismPool;
        [SerializeField] private InteractivePrismPoolManager rhinoPrismPool;
        [SerializeField] private InteractivePrismPoolManager interactivePrismPool;
        [SerializeField] private InteractivePrismPoolManager spiderPrismPool;

        [Tooltip("Dedicated pool for fast-growing, collider-live-on-spawn boost prisms - the " +
                 "BoostRingBuilder rings (omnicrystal, joust, Squirrel tube). Serves the " +
                 "FastGrowPrism prefab. Separate from the shared pools so the waitTime/GrowthRate " +
                 "overrides can't leak into normal trail/AOE prisms.")]
        [SerializeField] private InteractivePrismPoolManager boostPrismPool;

        [Tooltip("Prefab on this pool is the authored death-explosion CONFIG " +
                 "(mesh / material / layer / clamp / duration) PrismDebris reads. " +
                 "Gameplay never Get()s this pool (D4); do not delete the reference.")]
        [SerializeField] private PrismExplosionPoolManager explosionPool;
        [Tooltip("Prefab is the authored suction CONFIG PrismDebris reads AND the " +
                 "live Grow pool (Sparrow ReverseSuction via PrismType.Grow). Death " +
                 "implosions never Get() this pool (D4); Grow still does.")]
        [SerializeField] private PrismImplosionPoolManager implosionPool;
        // Add more later: PrismShockwavePoolManager, PrismDisintegrationPoolManager, etc.

        [Header("Boost Prism Tuning")]
        [Tooltip("Grow-in speed for boost prisms (fast bloom). The clock growth stamp " +
                 "clamps the derived rate (PrismScaleAnimator.ClockRateK: growthRate * 0.04 " +
                 "into [0.05, 0.1]), so values below ~6 are indistinguishable from the " +
                 "default; 8 pins the bloom at the max speed across framerates. The collider " +
                 "never waits on this — under the clock law the transform is final at stamp, " +
                 "so boost prisms have a full-size world footprint from frame 0.")]
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
        // PrismEventData is a STRUCT (see its declaration): the payload can no longer
        // be null, so the old null-guard is gone rather than made unreachable. An
        // unset field arrives as default, which every spawner below already tolerates.
        private PrismReturnEventData OnPrismSpawnedEventRaised(PrismEventData data)
        {
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

                case PrismType.Spider:
                    spawned = SpawnSpiderPrism(data);
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
            
        GameObject SpawnSpiderPrism(PrismEventData data)
        {
            // Spider anchor prisms fall back to the interactive pool when no
            // dedicated spider pool is assigned.
            var pool = spiderPrismPool != null ? spiderPrismPool : interactivePrismPool;
            if (pool == null) { CSDebug.LogWarning("[PrismFactory] No pool available for Spider prism."); return null; }
            var prism = pool.Get(data.SpawnPosition, data.Rotation, pool.transform);
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
            // Batched pure-entity debris is the ONLY death-explosion carrier (D4).
            // The pool prefab stays the authored CONFIG source (mesh / material /
            // layer / clamp / duration). The factory never Get()s explosionPool
            // for this type. Callers treat a null spawn as fire-and-forget.
            if (CosmicShore.Utility.PrismDebris.Configure(explosionPool != null ? explosionPool.Prefab : null) &&
                TryGetTeamColors(data.ownDomain, data.Kind, out var bright, out var dark) &&
                CosmicShore.Utility.PrismDebris.TryRequestExplosion(
                    data.SpawnPosition, data.Rotation, data.Scale,
                    bright, dark, data.Velocity, data.DebrisSpeedLimit, data.Kind))
            {
                return null;
            }

            LogDeathVisualRefused("explosion");
            return null;
        }

        GameObject SpawnImplosion(PrismEventData data)
        {
            // Batched suction is the ONLY death-implosion carrier (D4). Grow
            // (PrismType.Grow / SpawnGrow) still Get()s implosionPool because the
            // batched carrier has no completion callback — ReverseSuction needs
            // OnGrowCompleted to spawn the real prism.
            if (CosmicShore.Utility.PrismDebris.ConfigureImplosion(implosionPool != null ? implosionPool.Prefab : null) &&
                TryGetTeamColors(data.ownDomain, data.Kind, out var bright, out var dark) &&
                CosmicShore.Utility.PrismDebris.TryRequestImplosion(
                    data.SpawnPosition, data.Rotation, data.Scale,
                    bright, dark, data.TargetTransform))
            {
                return null;
            }

            LogDeathVisualRefused("implosion");
            return null;
        }

        static void LogDeathVisualRefused(string family)
        {
            if (_loggedDeathVisualRefuse) return;
            _loggedDeathVisualRefuse = true;
            CSDebug.LogError(
                $"[PrismFactory] Batched {family} debris declined (unconfigured prefab, " +
                "missing theme colours, PrismRenderService off, or a 5s drain-fail hold). " +
                "Pooled death spawn is retired (Docs/PRISM_ANIMATION.md D4) — this death " +
                "has no visual. Grow (Sparrow ReverseSuction) is unaffected.");
        }

        GameObject SpawnGrow(PrismEventData data)
        {
            // LIVE gameplay carrier (Sparrow ReverseSuction). D4 retired pooled
            // death spawn; Grow stays on this pool because batched implosion is
            // fire-and-forget and has no OnGrowCompleted machinery.
            if (implosionPool == null)
            {
                CSDebug.LogError("[PrismFactory] SpawnGrow: implosionPool is unassigned — ReverseSuction visual dropped.");
                return null;
            }
            var obj = implosionPool.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
            if (obj == null)
            {
                CSDebug.LogError("[PrismFactory] SpawnGrow: implosionPool.Get returned null — ReverseSuction visual dropped.");
                return null;
            }
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain, data.Kind);

            // Self-unsubscribing callback so lambdas don't accumulate on pool reuse
            Action<PrismImplosion> growCallback = null;
            growCallback = _ =>
            {
                obj.OnReturnToPool -= growCallback;
                data.OnGrowCompleted?.Invoke();
            };
            obj.OnReturnToPool += growCallback;

            obj.StartGrow(data.TargetTransform, data.GrowDuration);

            return obj.gameObject;
        }
        
        /// <summary>
        /// Palette lookup shared by the entity-debris path (which has no GameObject for
        /// <see cref="ConfigureForTeam"/> to visit). Keyed on (domain, kind) and routed
        /// through <see cref="SO_ColorSet.TryGetPrismKindColors"/> — the same composition
        /// <c>ThemeManager</c> paints the live prism with — so a death visual always wears
        /// the colours of the mass it came from. A danger prism therefore shatters into the
        /// hot danger rim over its domain's shielded base, not into plain-domain debris.
        /// False when the theme is not populated yet — the death visual is skipped
        /// (pooled death spawn is retired; Grow still tints via <see cref="ConfigureForTeam"/>).
        /// </summary>
        private bool TryGetTeamColors(Domains domain, PrismKind kind, out Color bright, out Color dark)
        {
            bright = Color.white;
            dark = Color.black;
            if (!_themeManagerData || !_themeManagerData.ColorSet) return false;
            return _themeManagerData.ColorSet.TryGetPrismKindColors(domain, kind, out bright, out dark);
        }

        private void ConfigureForTeam(GameObject obj, Domains domain, PrismKind kind)
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

            if (!_themeManagerData.ColorSet.TryGetPrismKindColors(domain, kind, out var bright, out var dark))
                return;

            // Effect components route team colors to whichever render path is
            // active (companion entity overrides or legacy MPB) when their
            // animation starts — the factory just hands them the palette.
            if (obj.TryGetComponent(out CosmicShore.Utility.PrismExplosion explosion))
            {
                explosion.SetTeamColors(bright, dark);
                return;
            }
            if (obj.TryGetComponent(out CosmicShore.Utility.PrismImplosion implosion))
            {
                implosion.SetTeamColors(bright, dark);
                return;
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer && materialSet)
            {
                renderer.GetPropertyBlock(mpb);
                // Apply basic material set - refine later if different prisms need different materials
                mpb.SetColor(DarkColorID, dark);
                mpb.SetColor(BrightColorID, bright);
                renderer.SetPropertyBlock(mpb);
            }
        }
        #endregion
    }
}
