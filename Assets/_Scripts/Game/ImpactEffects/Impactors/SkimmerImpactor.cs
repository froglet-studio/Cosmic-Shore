using System;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
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

        [Header("Block-Stay effects (tick while skimming)")] [SerializeField]
        SkimmerPrismEffectSO[] skimmerPrismStayEffectsSO; // TODO -> Add to the container

        [Header("Refs")] [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;
        protected override bool isInitialized => Skimmer.IsInitialized;

        // runtime state (moved from Skimmer)
        readonly Dictionary<string, float> _skimStartTimes = new();
        private int ActivelySkimmingBlockCount;
        [HideInInspector]
        public float CombinedWeight; // exposed for effects that need it
    
        // ------------------------------------------------------------------
        // Trigger callbacks moved here

        void OnTriggerStay(Collider other)
        {
            if (!isInitialized)
                return;

            // Crystal: vacuum (delegate to Skimmer helper so it uses its settings)
            if (other.TryGetComponent<Crystal>(out var crystal))
            {
                skimmer.TryVacuumCrystal(crystal); // uses Skimmer's vaccumAmount + flag
                // no return; a Crystal may also have a TrailBlock? (unlikely, safe to continue)
            }

            // TrailBlock: compute combined weight & run stay effects
            if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            var prism = prismImpactor.Prism;
            if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;

            // ensure we started skimming
            StartSkimIfNeeded(prism.ownerID);

            // choose “mature & nearest” block per your old logic
            if (Time.time - prism.prismProperties.TimeCreated <= 4f) return;

            // distance from skimmer to this block
            float sqrDistance = (skimmer.transform.position - other.transform.position).sqrMagnitude;

            // compute weights on-the-fly (same formula you had)
            float scale = skimmer.transform.localScale.x;
            float sqrSweetSpot = (scale * scale) / 16f;
            float sigma = (sqrSweetSpot) / 2.355f; // since FWHM = sqrSweetSpot

            float distanceWeight = Skimmer.ComputeGaussian(sqrDistance, sqrSweetSpot, sigma);
            float directionWeight = Vector3.Dot(skimmer.VesselStatus.Transform.forward, prism.transform.forward);

            CombinedWeight = distanceWeight * Mathf.Abs(directionWeight);
            // tick stay effects (centralized)
            ExecuteBlockStayEffects(CombinedWeight, prismImpactor);
        }

        void OnTriggerExit(Collider other)
        {
            if (skimmer?.VesselStatus == null) return;

            if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            var prism = prismImpactor.Prism;
            if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;

            if (!_skimStartTimes.ContainsKey(prism.ownerID)) return;

            _skimStartTimes.Remove(prism.ownerID);
            ActivelySkimmingBlockCount = Mathf.Max(0, ActivelySkimmingBlockCount - 1);

            if (ActivelySkimmingBlockCount < 1)
                ExecuteBlockStayEffects(0f, prismImpactor); // stop effects when no longer skimming anything
        }

        // ------------------------------------------------------------------

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case VesselImpactor shipImpactor:
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

        void ExecuteBlockStayEffects(float combinedWeight, PrismImpactor prismImpactor)
        {
            CombinedWeight = combinedWeight;

            if (skimmerPrismStayEffectsSO == null || skimmerPrismStayEffectsSO.Length == 0)
                return;

            // Run as self-effects. Effects can cast `impactor` to SkimmerImpactor and read `CombinedWeight`.
            foreach (var t in skimmerPrismStayEffectsSO)
                t?.Execute(this, prismImpactor);
        }

        void StartSkimIfNeeded(string ownerId)
        {
            if (_skimStartTimes.ContainsKey(ownerId)) return;
            _skimStartTimes.Add(ownerId, Time.time);
            ActivelySkimmingBlockCount++;
        }
    }
}