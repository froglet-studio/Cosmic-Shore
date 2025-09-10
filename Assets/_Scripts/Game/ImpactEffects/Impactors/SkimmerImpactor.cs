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
        [SerializeField] VesselSkimmerEffectsSO[] vesselSkimmerEffectsSO;
        [SerializeField] SkimmerPrismEffectSO[] skimmerPrismEffectsSO;
        [SerializeField] SkimmerCrystalEffectSO[] skimmerCrystalEffectsSO;

        [Header("Block-Stay effects (tick while skimming)")]
        [SerializeField] SkimmerPrismEffectSO[] skimmerPrismStayEffectsSO;

        [Header("Refs")]
        [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;

        // runtime state (moved from Skimmer)
        readonly Dictionary<string, float> _skimStartTimes = new();
        private int ActivelySkimmingBlockCount;
        private float CombinedWeight;  // exposed for effects that need it

        // ------------------------------------------------------------------
        // Trigger callbacks moved here

        void OnTriggerStay(Collider other)
        {
            if (skimmer?.ShipStatus == null) return;

            // Crystal: vacuum (delegate to Skimmer helper so it uses its settings)
            if (other.TryGetComponent<Crystal>(out var crystal))
            {
                skimmer.TryVacuumCrystal(crystal); // uses Skimmer's vaccumAmount + flag
                // no return; a Crystal may also have a TrailBlock? (unlikely, safe to continue)
            }

            // TrailBlock: compute combined weight & run stay effects
            if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            var prism = prismImpactor.Prism;
            if (!skimmer.AffectSelf && prism.Team == skimmer.ShipStatus.Team) return;

            // ensure we started skimming
            StartSkimIfNeeded(prism.ownerID);

            // choose “mature & nearest” block per your old logic
            if (Time.time - prism.TrailBlockProperties.TimeCreated <= 4f) return;

            // distance from skimmer to this block
            float sqrDistance = (skimmer.transform.position - other.transform.position).sqrMagnitude;

            // compute weights on-the-fly (same formula you had)
            float scale = skimmer.transform.localScale.x;
            float sqrSweetSpot = (scale * scale) / 16f;
            float sigma = (sqrSweetSpot) / 2.355f; // since FWHM = sqrSweetSpot

            float distanceWeight  = Skimmer.ComputeGaussian(sqrDistance, sqrSweetSpot, sigma);
            float directionWeight = Vector3.Dot(skimmer.ShipStatus.Transform.forward, prism.transform.forward);

            CombinedWeight = distanceWeight * Mathf.Abs(directionWeight);

            // tick stay effects (centralized)
            ExecuteBlockStayEffects(CombinedWeight, prismImpactor);
        }

        void OnTriggerExit(Collider other)
        {
            if (skimmer?.ShipStatus == null) return;

            if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
            var prism = prismImpactor.Prism;
            if (!skimmer.AffectSelf && prism.Team == skimmer.ShipStatus.Team) return;

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
                    ExecuteEffect(impactee, vesselSkimmerEffectsSO);
                    skimmer.ExecuteImpactOnShip(shipImpactor.Ship);       // secondary call
                    break;

                case PrismImpactor prismImpactor:

                    var prism = prismImpactor.Prism;
                    
                    ExecuteEffect(prismImpactor, skimmerPrismEffectsSO);
                    skimmer.ExecuteImpactOnPrism(prism);    // secondary call (booster viz, etc.)
                    
                    if (!skimmer.AffectSelf && prism.Team == skimmer.ShipStatus.Team)
                        return;
                    StartSkimIfNeeded(prism.ownerID);
                    
                    break;

                case ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(elementalCrystalImpactor, skimmerCrystalEffectsSO);
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
