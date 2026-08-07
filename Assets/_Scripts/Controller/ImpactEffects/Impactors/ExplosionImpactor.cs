using System.Collections.Generic;
using CosmicShore.Gameplay;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(AOEExplosion))]
    public class ExplosionImpactor : ImpactorBase
    {
        [SerializeField] private ExplosionImpactorDataContainerSO explosionImpactorDataContainer;

        [SerializeField] bool affectSelf;
        [SerializeField] bool destructive = true;
        [SerializeField] bool devastating;
        [SerializeField] bool shielding;

        AOEExplosion explosion;

        public override Domains OwnDomain => explosion.Domain;

        /// <summary>
        /// Per-instance friendly fire. False spares the blast's own domain (allied prisms are
        /// shielded rather than damaged, allied vessels are skipped entirely); true lets the blast
        /// hit everyone. Set by <see cref="AOEExplosion.InitializeStruct.AffectSelfOverride"/> so an
        /// elemental upgrade can hand a pilot a blast that no longer eats their own team's mass.
        /// Safe on the instance: every explosion is a fresh Instantiate of its prefab.
        /// </summary>
        public void SetAffectSelf(bool value) => affectSelf = value;

        // Batch AOE processing - bypasses Physics for prisms entirely
        private bool _useBatchProcessing;
        private static int _trailBlockLayer = -1;
        private HashSet<int> _batchHitTracker;

        // Damage deferred by the per-frame budget. Entries here are already claimed
        // in _batchHitTracker, so the spatial query will never re-emit them - this
        // queue is their only resolution path. See MAX_NEW_HITS_PER_FRAME.
        private Queue<PendingExplosionHit> _batchPending;

        public bool IsBatchProcessing => _useBatchProcessing;

        /// <summary>True while budget-deferred damage is still waiting to resolve.</summary>
        public bool HasPendingBatchWork => _batchPending != null && _batchPending.Count > 0;

        /// <summary>
        /// When true, BeginBatchProcessing() is a no-op - forces Physics OnTriggerEnter
        /// for all collisions. Used by AOEBenchmarkOverlay for A/B comparison.
        /// </summary>
        public static bool ForceLegacyPhysics { get; set; }

        // --- ProfilerMarkers ---
        private static readonly ProfilerMarker s_onTriggerEnter = new("AOE.OnTriggerEnter");
        private static readonly ProfilerMarker s_onTriggerSkipped = new("AOE.OnTriggerEnter.Skipped");
        private static readonly ProfilerMarker s_processBatch = new("AOE.ProcessBatchFrame");

        void Awake()
        {
            explosion ??= GetComponent<AOEExplosion>();
            if (_trailBlockLayer < 0)
                _trailBlockLayer = LayerMask.NameToLayer("TrailBlocks");
        }

        /// <summary>
        /// Begins batch AOE processing for this explosion's lifetime.
        /// Call once when the explosion starts. While active, prism collisions
        /// are skipped in OnTriggerEnter and handled by ProcessBatchFrame instead.
        /// </summary>
        public void BeginBatchProcessing()
        {
            if (ForceLegacyPhysics) return;

            var registry = PrismSpatialIndex.Instance;
            if (registry == null || !registry.IsAvailable)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("[ExplosionImpactor] PrismSpatialIndex unavailable - falling back to Physics triggers");
#endif
                return;
            }
            _useBatchProcessing = true;
            // Reuse cached HashSet to avoid GC allocation per explosion
            if (_batchHitTracker == null)
                _batchHitTracker = new HashSet<int>(256);
            else
                _batchHitTracker.Clear();

            if (_batchPending == null)
                _batchPending = new Queue<PendingExplosionHit>(256);
            else
                _batchPending.Clear();
        }

        /// <summary>
        /// Processes one frame of batch AOE damage via the PrismSpatialIndex.
        /// Called from AOEExplosion.ExplodeAsync each frame instead of relying on Physics.
        /// center/radius describe this frame's blast sphere (stationary centre,
        /// growing radius - so each frame's volume strictly contains the last);
        /// blastOrigin is the emission point all impact vectors radiate from.
        /// The conic explosion uses <see cref="ProcessBatchConeFrame"/> instead.
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism).
        /// </summary>
        public bool ProcessBatchFrame(Vector3 center, float radius, Vector3 blastOrigin, in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                if (!_useBatchProcessing) return true;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) return true;

                return registry.ProcessExplosionFrame(
                    center, radius, blastOrigin, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion,
                    explosion.Vessel,
                    _batchHitTracker,
                    _batchPending);
            }
        }

        /// <summary>
        /// Processes one frame of batch AOE damage for the CONIC explosion: an exact
        /// test against the swept blast over the axial slab [sliceMin, sliceMax]
        /// it newly covers this frame. Successive slabs tile the sweep exactly,
        /// so coverage does not depend on frame rate and never reaches past the
        /// visible tip.
        ///
        /// The cross-section is a CAPSULE (a stadium): a disc of radius
        /// tanCoreHalfAngle*s dragged along <paramref name="gapeAxis"/> for
        /// +/- tanGapePerUnit*s. Both tangents are invariant as the self-similar
        /// blast grows; tanGapePerUnit = 0 is the plain circular cone.
        /// Returns true if the explosion should continue, false if it should be
        /// destroyed (e.g. hit a super-shielded enemy prism).
        /// </summary>
        public bool ProcessBatchConeFrame(
            Vector3 apex, Vector3 axis, Vector3 gapeAxis, float sliceMin, float sliceMax,
            float tanCoreHalfAngle, float tanGapePerUnit, in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                if (!_useBatchProcessing) return true;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) return true;

                return registry.ProcessExplosionConeFrame(
                    apex, axis, gapeAxis, sliceMin, sliceMax,
                    tanCoreHalfAngle, tanGapePerUnit, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion,
                    explosion.Vessel,
                    _batchHitTracker,
                    _batchPending);
            }
        }

        /// <summary>
        /// Drains one frame's worth of budget-deferred damage without running a new
        /// spatial query. Called after the explosion's visual finishes so a blast
        /// dense enough to exceed the per-frame budget still damages everything it
        /// enclosed. Returns true while work remains.
        /// </summary>
        public bool DrainPendingBatchFrame(in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                if (!HasPendingBatchWork) return false;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) { _batchPending.Clear(); return false; }

                return registry.DrainPendingExplosionDamage(
                    _batchPending, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion, explosion.Vessel);
            }
        }

        /// <summary>
        /// How many distinct prisms this blast has claimed so far. The batch tracker already keys
        /// every prism the blast reached, so this is free — it is the blast's own footprint, not a
        /// second count kept alongside it.
        /// </summary>
        public int BatchHitCount => _batchHitTracker?.Count ?? 0;

        /// <summary>
        /// Raised once per blast as it retires, with the vessel that fired it and how many prisms
        /// it claimed. Presentation only (a HUD tally) — listeners must not change outcomes.
        /// Static because explosions are spawned and destroyed per shot, so there is nothing
        /// durable for a HUD to subscribe to; listeners filter by the vessel they own.
        /// </summary>
        public static event System.Action<IVessel, int> OnBlastResolved;

        /// <summary>
        /// Ends batch processing and cleans up tracking data.
        /// </summary>
        public void EndBatchProcessing()
        {
            if (_useBatchProcessing && explosion != null && explosion.Vessel != null)
                OnBlastResolved?.Invoke(explosion.Vessel, BatchHitCount);

            _useBatchProcessing = false;
            // Keep HashSet/Queue allocated for reuse - cleared on next BeginBatchProcessing.
            // Any hits still pending here are abandoned deliberately: EndBatchProcessing
            // runs on cancellation (turn end) and on the destroy paths, where further
            // damage must not land.
            _batchPending?.Clear();
        }

        protected override void OnTriggerEnter(Collider other)
        {
            using (s_onTriggerEnter.Auto())
            {
                // Skip prisms entirely - they're handled by batch AOE processing
                if (_useBatchProcessing && other.gameObject.layer == _trailBlockLayer)
                {
                    s_onTriggerSkipped.Begin();
                    s_onTriggerSkipped.End();
                    return;
                }

                base.OnTriggerEnter(other);
            }
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {    
            var impactVector = explosion.CalculateImpactVector(impactee.Transform.position);
            
            switch (impactee)
            {
                case VesselImpactor vesselImpactee:
                    if (vesselImpactee.Vessel.VesselStatus.Domain == explosion.Domain && !affectSelf)
                        break;
                    
                    if (!explosionImpactorDataContainer) return;
                    var vesselExplosionEffects = explosionImpactorDataContainer.vesselExplosionEffects;
                    if(!DoesEffectExist(vesselExplosionEffects)) return;
                    foreach (var effect in vesselExplosionEffects)
                    {
                        effect.Execute(vesselImpactee, this);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    ExecuteCommonPrismCommands(prismImpactee.Prism, impactVector);
                    if (!explosionImpactorDataContainer) return;
                    var explosionPrismEffects = explosionImpactorDataContainer.explosionPrismEffects;
                    if(!DoesEffectExist(explosionPrismEffects)) return;
                    foreach (var effect in explosionPrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// The Physics-trigger fallback's per-prism resolution. Mirrors
        /// <c>PrismSpatialIndex.ResolveExplosionHit</c>, INCLUDING the debris ceiling:
        /// the batch path and this path must hand a prism the same impulse, or a blast
        /// throws mass at one speed with the spatial index up and another without it.
        /// </summary>
        void ExecuteCommonPrismCommands(Prism prism, Vector3 impactVector)
        {
            // Super-shielded prisms are fully invulnerable. The explosion is
            // physically blocked by the shield: destroy the explosion VFX so
            // it doesn't visibly expand through the prism, and skip all
            // damage / steal / shield-decay state changes. Mirrors the
            // PrismSpatialIndex Burst path. Ways to break super-shields will
            // be added later as targeted opt-in mechanics.
            if (prism.prismProperties.IsSuperShielded)
            {
                Destroy(gameObject);
                return;
            }

            if ((prism.Domain == explosion.Domain && !affectSelf) || !destructive)
            {
                if (shielding && prism.Domain == explosion.Domain)
                    prism.ActivateShield();
                else 
                    prism.ActivateShield(2f);
                return;
            }
            
            float debrisSpeedLimit = explosion.Impulse.DebrisSpeedLimit;

            if (explosion.AnonymousExplosion) // Vessel Status will be null here
                prism.Damage(impactVector, Domains.Blue, "🔥GuyFawkes🔥", devastating,
                             debrisSpeedLimit: debrisSpeedLimit);
            else
            {
                var shipStatus = explosion.Vessel.VesselStatus;
                prism.Damage(impactVector, shipStatus.Domain, shipStatus.Player.Name, devastating,
                             debrisSpeedLimit: debrisSpeedLimit);
            }
        }
    }
}