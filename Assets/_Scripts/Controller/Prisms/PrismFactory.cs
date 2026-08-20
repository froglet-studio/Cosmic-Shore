using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Benchmark/diagnostic override of the per-frame VFX spawn caps (0 = the
        /// authored defaults above). The caps were sized for the CPU-per-effect era;
        /// the stress rig lifts them to measure the clock-material system UNWEAKENED
        /// (a death's effect costs one stamp + one entity, not a per-frame update).
        /// Gameplay never sets this.
        /// </summary>
        public static int VFXBudgetPerFrameOverride = 0;

        /// <summary>
        /// Benchmark/diagnostic switch: disables the pressure-shortening of effect
        /// durations (every death animates at full length regardless of how many
        /// effects are live). Shared home so both the clock branch
        /// (PrismExplosion.PressuredDuration) and legacy branches
        /// (PrismEffectsManager.PressuredDuration) can gate on one flag.
        /// Gameplay never sets this.
        /// </summary>
        public static bool EffectPressureScalingDisabled = false;

        // Benchmark-harness overrides; a play exit mid-run must not leave them lifted.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBudgetOverrides()
        {
            VFXBudgetPerFrameOverride = 0;
            EffectPressureScalingDisabled = false;
        }

        static int EffectiveExplosionVFXBudget =>
            VFXBudgetPerFrameOverride > 0 ? VFXBudgetPerFrameOverride : MaxExplosionVFXPerFrame;
        static int EffectiveImplosionVFXBudget =>
            VFXBudgetPerFrameOverride > 0 ? VFXBudgetPerFrameOverride : MaxImplosionVFXPerFrame;
        // Deferred-queue bound scales with the lifted budget so a burst the operator
        // deliberately unthrottled is not silently truncated by the queue instead.
        static int EffectiveDeferredCap
        {
            get
            {
                long scaled = (long)EffectiveExplosionVFXBudget * 3;
                return scaled > int.MaxValue ? int.MaxValue
                    : (int)System.Math.Max(MaxDeferredVFX, scaled);
            }
        }
        private int _explosionVFXCount;
        private int _implosionVFXCount;
        private int _lastExplosionFrame;
        private int _lastImplosionFrame;

        // A destruction whose visual does not fit in this frame's cap is DEFERRED, not
        // dropped. Prism.SetupDestruction has already hidden the prism, so the pooled
        // VFX is the only thing standing in for it - skipping the spawn makes the prism
        // vanish mid-air, which the continuity law forbids ("deaths animate out"; see
        // CLAUDE.md). Deferred effects are spawned within a later frame's cap, so the
        // per-frame ceiling this budget exists to enforce is unchanged.
        //
        // The queue is bounded at a few frames' worth: past that the visual would play
        // so long after the death that it reads as a random pop somewhere the fight has
        // already left. When full we discard the STALEST, matching PrismEffectsManager's
        // recycle-the-oldest policy for the concurrent-effect ceiling.
        private const int MaxDeferredVFX = 192;
        private readonly Queue<PrismEventData> _deferredExplosions = new(64);
        private readonly Queue<PrismEventData> _deferredImplosions = new(64);

        /// <summary>Destruction visuals waiting on a later frame's spawn budget.</summary>
        public int DeferredExplosionCount => _deferredExplosions.Count;

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
        [Tooltip("Grow-in speed for boost prisms (fast bloom). The clock growth stamp " +
                 "clamps the derived rate (PrismScaleAnimator.ClockRateK: growthRate * 0.04 " +
                 "into [0.05, 0.1]), so values below ~6 are indistinguishable from the " +
                 "default; 8 pins the bloom at the max speed across framerates. The collider " +
                 "never waits on this - boost prisms hold a full-size collider from frame 0 " +
                 "(Prism.HoldColliderAtFullSize).")]
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

            // Queued visuals belong to deaths in the scene being torn down - replaying
            // them after a reload would pop effects at coordinates nothing occupies.
            _deferredExplosions.Clear();
            _deferredImplosions.Clear();
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
            // Batched pure-entity debris path (Docs/PRISM_ANIMATION.md B3): no
            // GameObject, no pool, no per-frame budget — a whole burst spawns as
            // one prototype-instantiate batch at LateUpdate and the GPU animates
            // every piece at full length. The pooled path below is a ROUTE, not a
            // working visual fallback — see the note on SpawnImplosion.
            if (CosmicShore.Utility.PrismDebris.Configure(explosionPool != null ? explosionPool.Prefab : null) &&
                TryGetTeamColors(data.ownDomain, data.Kind, out var bright, out var dark) &&
                CosmicShore.Utility.PrismDebris.TryRequestExplosion(
                    data.SpawnPosition, data.Rotation, data.Scale,
                    bright, dark, data.Velocity, data.DebrisSpeedLimit, data.Kind))
            {
                // Callers treat a null spawn as fire-and-forget (the deferral
                // branch below has always returned null).
                return null;
            }

            RollExplosionFrameBudget();

            // Over budget: queue the visual for a later frame rather than dropping it.
            // The prism is already hidden, so a dropped visual IS a disappearing prism.
            if (_explosionVFXCount >= EffectiveExplosionVFXBudget)
            {
                Defer(_deferredExplosions, data);
                return null;
            }

            return SpawnExplosionNow(data);
        }

        GameObject SpawnImplosion(PrismEventData data)
        {
            // Batched pure-entity suction path (Docs/PRISM_ANIMATION.md B3) — the
            // implosion half of the explosion batch above. No GameObject, no pool,
            // no per-frame budget, and no per-instance MonoBehaviour Update (the
            // pooled carrier's watchdog): a swarm-eat frame's consumptions spawn as
            // ONE prototype-instantiate batch and the GPU runs every suction off the
            // shader clock. The moving convergence target keeps working — PrismDebris
            // retains the target Transform per record and refreshes _Location while
            // it lives (the §1 exception).
            //
            // The pooled path below is a ROUTE, not a working visual fallback: under
            // strict clock mode PrismImplosion with no render entity draws a static,
            // un-animated block and logs, and PrismExplosion draws nothing at all
            // (TriggerExplosion disables the renderer unconditionally). It is reached
            // only if the effect prefab is misconfigured or PrismRenderService is off —
            // and being loud there is the intended forcing function, not a degradation.
            // See Docs/PRISM_ANIMATION.md §4.6 for what retiring it actually requires.
            if (CosmicShore.Utility.PrismDebris.ConfigureImplosion(implosionPool != null ? implosionPool.Prefab : null) &&
                TryGetTeamColors(data.ownDomain, data.Kind, out var bright, out var dark) &&
                CosmicShore.Utility.PrismDebris.TryRequestImplosion(
                    data.SpawnPosition, data.Rotation, data.Scale,
                    bright, dark, data.TargetTransform))
            {
                // Callers treat a null spawn as fire-and-forget (the deferral
                // branch below has always returned null).
                return null;
            }

            RollImplosionFrameBudget();

            // Same reasoning as explosions - defer, never drop.
            if (_implosionVFXCount >= EffectiveImplosionVFXBudget)
            {
                Defer(_deferredImplosions, data);
                return null;
            }

            return SpawnImplosionNow(data);
        }

        private void RollExplosionFrameBudget()
        {
            if (Time.frameCount == _lastExplosionFrame) return;
            _lastExplosionFrame = Time.frameCount;
            _explosionVFXCount = 0;
        }

        private void RollImplosionFrameBudget()
        {
            if (Time.frameCount == _lastImplosionFrame) return;
            _lastImplosionFrame = Time.frameCount;
            _implosionVFXCount = 0;
        }

        /// <summary>
        /// Spawns the effect without consulting the frame budget - callers have already
        /// reserved a slot. Keeping this separate from <see cref="SpawnExplosion"/> means
        /// the drain can never re-enter the defer branch and cycle the queue.
        /// </summary>
        private GameObject SpawnExplosionNow(PrismEventData data)
        {
            _explosionVFXCount++;

            var obj = explosionPool?.Get(data.SpawnPosition, data.Rotation, explosionPool.transform);
            if (obj == null) return null;
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain, data.Kind);
            obj.TriggerExplosion(data.Velocity, data.DebrisSpeedLimit, data.Kind);
            return obj.gameObject;
        }

        private GameObject SpawnImplosionNow(PrismEventData data)
        {
            _implosionVFXCount++;

            var obj = implosionPool?.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
            if (obj == null) return null;
            obj.transform.localScale = data.Scale;
            ConfigureForTeam(obj.gameObject, data.ownDomain, data.Kind);
            obj.StartImplosion(data.TargetTransform);
            return obj.gameObject;
        }

        private static void Defer(Queue<PrismEventData> queue, PrismEventData data)
        {
            while (queue.Count >= EffectiveDeferredCap)
                queue.Dequeue(); // stalest first - see MaxDeferredVFX
            queue.Enqueue(data);
        }

        /// <summary>
        /// Spends whatever is left of this frame's VFX budget on deaths whose visual an
        /// earlier frame could not afford, oldest first. Runs in LateUpdate so every
        /// destruction the frame produced has already had first claim on the budget - a
        /// burst therefore animates out over the next frame or two instead of vanishing.
        /// </summary>
        private void LateUpdate()
        {
            if (_deferredExplosions.Count > 0)
            {
                RollExplosionFrameBudget();
                while (_deferredExplosions.Count > 0 && _explosionVFXCount < EffectiveExplosionVFXBudget)
                    SpawnExplosionNow(_deferredExplosions.Dequeue());
            }

            if (_deferredImplosions.Count > 0)
            {
                RollImplosionFrameBudget();
                while (_deferredImplosions.Count > 0 && _implosionVFXCount < EffectiveImplosionVFXBudget)
                {
                    var data = _deferredImplosions.Dequeue();
                    // An implosion converges on a target; if the consumer died while the
                    // visual was queued there is nothing to converge on.
                    if (data.TargetTransform == null) continue;
                    SpawnImplosionNow(data);
                }
            }
        }
        
        GameObject SpawnGrow(PrismEventData data)
        {
            var obj = implosionPool?.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
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
        /// False when the theme is not populated yet — the caller falls back to the pooled
        /// path, whose own warning covers the misconfiguration.
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
