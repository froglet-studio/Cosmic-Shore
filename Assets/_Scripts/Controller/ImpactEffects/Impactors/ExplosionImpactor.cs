using System.Collections.Generic;
using CosmicShore.Gameplay;
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

        // Batch AOE processing — bypasses Physics for prisms entirely
        private bool _useBatchProcessing;
        private static int _trailBlockLayer = -1;
        private HashSet<int> _batchHitTracker;

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
            var registry = PrismAOERegistry.Instance;
            if (registry == null || !registry.IsAvailable) return;
            _useBatchProcessing = true;
            // Reuse cached HashSet to avoid GC allocation per explosion
            if (_batchHitTracker == null)
                _batchHitTracker = new HashSet<int>(256);
            else
                _batchHitTracker.Clear();
        }

        /// <summary>
        /// Processes one frame of batch AOE damage via the PrismAOERegistry.
        /// Called from AOEExplosion.ExplodeAsync each frame instead of relying on Physics.
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism).
        /// </summary>
        public bool ProcessBatchFrame(Vector3 center, float radius, float speed, float inertia)
        {
            if (!_useBatchProcessing) return true;
            var registry = PrismAOERegistry.Instance;
            if (registry == null) return true;

            return registry.ProcessExplosionFrame(
                center, radius, speed, inertia,
                explosion.Domain,
                affectSelf, destructive, devastating, shielding,
                explosion.AnonymousExplosion,
                explosion.Vessel,
                _batchHitTracker);
        }

        /// <summary>
        /// Ends batch processing and cleans up tracking data.
        /// </summary>
        public void EndBatchProcessing()
        {
            _useBatchProcessing = false;
            // Keep HashSet allocated for reuse — just clear on next BeginBatchProcessing
        }

        protected override void OnTriggerEnter(Collider other)
        {
            // Skip prisms entirely — they're handled by batch AOE processing
            if (_useBatchProcessing && other.gameObject.layer == _trailBlockLayer)
                return;

            base.OnTriggerEnter(other);
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
                        if (effect == null) continue;
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
                        if (effect == null) continue;
                        effect.Execute(this, prismImpactee);
                    }
                    break;
            }
        }
        
        void ExecuteCommonPrismCommands(Prism prism, Vector3 impactVector)
        {
            if ((prism.Domain != explosion.Domain || affectSelf) && prism.prismProperties.IsSuperShielded)
            {
                prism.DeactivateShields();
                Destroy(gameObject);    // TODO: This seems wrong...
            } 
            if ((prism.Domain == explosion.Domain && !affectSelf) || !destructive)
            {
                if (shielding && prism.Domain == explosion.Domain)
                    prism.ActivateShield();
                else 
                    prism.ActivateShield(2f);
                return;
            }
            
            if (explosion.AnonymousExplosion) // Vessel Status will be null here
                prism.Damage(impactVector, Domains.None, "🔥GuyFawkes🔥", devastating);
            else
            {
                var shipStatus = explosion.Vessel.VesselStatus;
                prism.Damage(impactVector, shipStatus.Domain, shipStatus.Player.Name, devastating);
            }
        }
    }
}