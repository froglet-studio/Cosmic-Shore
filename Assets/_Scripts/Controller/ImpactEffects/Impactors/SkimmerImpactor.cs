using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class SkimmerImpactor : ImpactorBase
    {
        [FormerlySerializedAs("shipSkimmerEffectsSO")]
        [Header("Effect lists")]
        // [SerializeField] VesselSkimmerEffectsSO[] vesselSkimmerEffectsSO;
        // [SerializeField] SkimmerPrismEffectSO[] skimmerPrismEffectsSO;
        // [SerializeField] SkimmerCrystalEffectSO[] skimmerCrystalEffectsSO;
        [SerializeField]
        private SkimmerImpactorDataContainerSO skimmerImpactorDataContainer;

        /// <summary>
        /// Effect container for this skimmer - exposed so the vessel-level confirmed-joust
        /// dispatch (VesselImpactor.ExecuteJoustImpact) can locate the joust effect config
        /// wired to this vessel without duplicating the reference.
        /// </summary>
        public SkimmerImpactorDataContainerSO EffectContainer => skimmerImpactorDataContainer;

        //[Header("Block-Stay effects (tick while skimming)")] [SerializeField]
        //SkimmerPrismEffectSO[] skimmerPrismStayEffectsSO; // TODO -> Add to the container

        [Header("Refs")] [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;
        public override Domains OwnDomain => Skimmer.Domain;
        protected override bool isInitialized => Skimmer.IsInitialized;

        // runtime state (moved from Skimmer)
        readonly Dictionary<string, float> _skimStartTimes = new();

        // Prisms currently inside this skimmer's trigger. Maintained on enter/exit so the Rhino
        // energy sword can, on a slash rising-edge, re-run its prism effects against a prism that
        // was already overlapping the blade before the slash began (no fresh OnTriggerEnter fires
        // for it). Generic + cheap; only the Rhino path calls ReapplyPrismEffectsToOverlapping.
        readonly HashSet<PrismImpactor> _overlappingPrisms = new();
        readonly List<PrismImpactor> _reapplyBuffer = new();
        //private int ActivelySkimmingBlockCount;
        //[HideInInspector]
        public float CombinedWeight; // exposed for effects that need it

        // ------------------------------------------------------------------
        // Trigger callbacks moved here

        //float scale;
        public float SqrSweetSpot;
        //float sigma;

        // Cached trigger sphere so the skim feel can scale by how close a prism passed to the
        // skimmer centre without a per-impact GetComponent on a dense-trail hot path.
        SphereCollider _sphereCollider;
        bool _sphereLookedUp;

        /// <summary>
        /// World-space radius of the skimmer's trigger sphere. Tracks the runtime SPACE-reach
        /// resize via lossyScale; falls back to half the scale if no sphere collider is present.
        /// </summary>
        public float SphereWorldRadius
        {
            get
            {
                if (!_sphereLookedUp)
                {
                    _sphereLookedUp = true;
                    TryGetComponent(out _sphereCollider);
                }
                float lossy = transform.lossyScale.x;
                return _sphereCollider != null ? _sphereCollider.radius * lossy : lossy * 0.5f;
            }
        }

        //float minMaturePrismSqrDistance;
        //Prism minMaturePrism;
        //PrismImpactor minPrismImpactor;

        //private void Start()
        //{

        //    scale = skimmer.transform.localScale.x;
        //    SqrSweetSpot = scale * scale / 16f;
        //    sigma = SqrSweetSpot / 2.355f;
        //}

        void OnTriggerStay(Collider other)
        {
            if (!isInitialized)
                return;
            
            if (skimmer.AllowVaccumCrystal && other.TryGetComponent<Crystal>(out var crystal)
                && !crystal.IsEmbedded) // a living lifeform's heart is never vacuumed out of its body
            {
                // NEW -> Vaccum logic transferred from skimmer to crystal, to reduce crystal dependency
                crystal.Vacuum(transform.position, skimmer.VaccumAmount);
                // skimmer.TryVacuumCrystal(crystal);
                // no return; a Crystal may also have a TrailBlock? (unlikely, safe to continue)
            }

            //// TrailBlock: compute combined weight & run stay effects
            //if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            //var prism = prismImpactor.Prism;
            //if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;

            //// ensure we started skimming
            //StartSkimIfNeeded(prism.ownerID);

            //// choose “mature & nearest” block per your old logic
            //// if (Time.time - prism.prismProperties.TimeCreated <= 4f) return;
            // if ((Time.time - prism.prismProperties.TimeCreated) < 0.25f) return;
            
            //float sqrDistance = (skimmer.transform.position - other.transform.position).sqrMagnitude;
            
            //minMaturePrismSqrDistance = Mathf.Min(minMaturePrismSqrDistance, sqrDistance);
            


            //if (sqrDistance != minMaturePrismSqrDistance) return;

            //minMaturePrism = prism;
            //minPrismImpactor = prismImpactor;
        }

        //private void FixedUpdate()
        //{
        //    if (minMaturePrism)
        //    {
        //        float distanceWeight = Skimmer.ComputeGaussian(minMaturePrismSqrDistance, SqrSweetSpot, sigma);
        //        float directionWeight = Vector3.Dot(skimmer.VesselStatus.Transform.forward, minMaturePrism.transform.forward);

        //        ExecuteBlockStayEffects(distanceWeight * Mathf.Abs(directionWeight), minPrismImpactor);
        //    }
        //    minMaturePrism = null;
        //    minPrismImpactor = null;
        //    minMaturePrismSqrDistance = Mathf.Infinity;
        //}

        void OnTriggerExit(Collider other)
        {
            if (!isInitialized)
                return;

            if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            var prism = prismImpactor.Prism;

            _overlappingPrisms.Remove(prismImpactor);

            if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;

            if (!_skimStartTimes.Remove(prism.ownerID)) return;

            //ActivelySkimmingBlockCount = Mathf.Max(0, ActivelySkimmingBlockCount - 1);

            // if (ActivelySkimmingBlockCount < 1)
            //     ExecuteBlockStayEffects(0f, prismImpactor); // stop effects when no longer skimming anything
        }

        // ------------------------------------------------------------------

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (!isInitialized)
                return;
            
            switch (impactee)
            {
                case VesselImpactor shipImpactor:
                    // A skimmer never impacts its own vessel. The Rhino's sword capsule
                    // permanently overlaps its own hull, so without this guard the full
                    // victim-effect chain ran against the pilot themselves (muting their
                    // own RightStickAction and spamming impact SFX).
                    if (ReferenceEquals(shipImpactor.Vessel?.VesselStatus, skimmer.VesselStatus)) return;
                    var evs = skimmerImpactorDataContainer.VesselSkimmerEffects;
                    if (!DoesEffectExist(evs)) return;
                    foreach (var effect in evs)
                    {
                        effect.Execute(shipImpactor, this);
                    }

                    skimmer.ExecuteImpactOnShip(shipImpactor.Vessel); // secondary call
                    break;

                case PrismImpactor prismImpactor:
                    var prism = prismImpactor.Prism;
                    var esp = skimmerImpactorDataContainer.SkimmerPrismEffects;
                    _overlappingPrisms.Add(prismImpactor);
                    skimmer.ExecuteImpactOnPrism(prism); // secondary call (booster viz, etc.)
                    if (!DoesEffectExist(esp)) return;

                    foreach (var effect in esp)
                    {
                        effect.Execute(this, prismImpactor);
                    }


                    if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain)
                        return;
                    StartSkimIfNeeded(prism.ownerID);

                    break;

                case ElementalCrystalImpactor elementalCrystalImpactor:
                    var esc = skimmerImpactorDataContainer.SkimmerCrystalEffects;
                    if (!DoesEffectExist(esc)) return;
                    foreach (var effect in esc)
                    {
                        effect.Execute(this, elementalCrystalImpactor);
                    }

                    break;
            }
        }

        // ------------------------------------------------------------------
        // Internals

        //void ExecuteBlockStayEffects(float combinedWeight, PrismImpactor prismImpactor)
        //{
        //    CombinedWeight = combinedWeight;

        //    if (skimmerPrismStayEffectsSO == null || skimmerPrismStayEffectsSO.Length == 0)
        //        return;

        //    // Run as self-effects. Effects can cast `impactor` to SkimmerImpactor and read `CombinedWeight`.
        //    foreach (var t in skimmerPrismStayEffectsSO)
        //        t?.Execute(this, prismImpactor);
        //}

        void StartSkimIfNeeded(string ownerId)
        {
            if (_skimStartTimes.ContainsKey(ownerId)) return;
            _skimStartTimes.Add(ownerId, Time.time);
            //ActivelySkimmingBlockCount++;
        }

        /// <summary>
        /// Re-runs this skimmer's prism effects against every prism currently inside the trigger.
        /// The Rhino energy sword calls this on a slash rising-edge so a slash pops a prism that
        /// was already overlapping the blade before the slash began (no new OnTriggerEnter fires
        /// for a prism that never left the trigger). The effects themselves gate on the sword's
        /// slash state, so this is a no-op when the sword is not slashing. Stale entries (prisms
        /// pooled/destroyed without an OnTriggerExit) are pruned as they are encountered.
        /// </summary>
        public void ReapplyPrismEffectsToOverlapping()
        {
            if (!isInitialized || _overlappingPrisms.Count == 0) return;
            var esp = skimmerImpactorDataContainer.SkimmerPrismEffects;
            if (!DoesEffectExist(esp)) return;

            _reapplyBuffer.Clear();
            _reapplyBuffer.AddRange(_overlappingPrisms);
            for (int i = 0; i < _reapplyBuffer.Count; i++)
            {
                var prismImpactor = _reapplyBuffer[i];
                if (!prismImpactor || prismImpactor.Prism == null)
                {
                    _overlappingPrisms.Remove(prismImpactor);
                    continue;
                }
                foreach (var effect in esp)
                    effect.Execute(this, prismImpactor);
            }
        }
    }
}