using System;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SkimmerImpactor : ImpactorBase
    {
        [Header("Effect lists")]
        [SerializeField, RequireInterface(typeof(IImpactEffect))] ScriptableObject[] skimmerShipEffectsSO;
        [SerializeField, RequireInterface(typeof(IImpactEffect))] ScriptableObject[] skimmerPrismEffectsSO;
        [SerializeField, RequireInterface(typeof(IImpactEffect))] ScriptableObject[] skimmerElementalCrystalEffectsSO;

        [Header("Block-Stay effects (tick while skimming)")]
        [SerializeField, RequireInterface(typeof(IImpactEffect))] ScriptableObject[] skimmerPrismStayEffectsSO;

        [Header("Refs")]
        [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;

        // cached effects
        IImpactEffect[] skimmerShipEffects;
        IImpactEffect[] skimmerPrismEffects;
        IImpactEffect[] skimmerElementalCrystalEffects;
        IImpactEffect[] skimmerPrismStayEffects;

        // runtime state (moved from Skimmer)
        readonly Dictionary<string, float> _skimStartTimes = new();
        public int ActivelySkimmingBlockCount { get; private set; }
        public float CombinedWeight { get; private set; }   // exposed for effects that need it

        void Awake()
        {
            skimmerShipEffects             = Array.ConvertAll(skimmerShipEffectsSO,            so => so as IImpactEffect);
            skimmerPrismEffects            = Array.ConvertAll(skimmerPrismEffectsSO,           so => so as IImpactEffect);
            skimmerElementalCrystalEffects = Array.ConvertAll(skimmerElementalCrystalEffectsSO,so => so as IImpactEffect);
            skimmerPrismStayEffects        = Array.ConvertAll(skimmerPrismStayEffectsSO,       so => so as IImpactEffect);
        }

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
            if (!other.TryGetComponent<TrailBlock>(out var trailBlock)) return;
            if (!skimmer.AffectSelf && trailBlock.Team == skimmer.ShipStatus.Team) return;

            // ensure we started skimming
            StartSkimIfNeeded(trailBlock.ownerID);

            // choose “mature & nearest” block per your old logic
            if (Time.time - trailBlock.TrailBlockProperties.TimeCreated <= 4f) return;

            // distance from skimmer to this block
            float sqrDistance = (skimmer.transform.position - other.transform.position).sqrMagnitude;

            // compute weights on-the-fly (same formula you had)
            float scale = skimmer.transform.localScale.x;
            float sqrSweetSpot = (scale * scale) / 16f;
            float sigma = (sqrSweetSpot) / 2.355f; // since FWHM = sqrSweetSpot

            float distanceWeight  = Skimmer.ComputeGaussian(sqrDistance, sqrSweetSpot, sigma);
            float directionWeight = Vector3.Dot(skimmer.ShipStatus.Transform.forward, trailBlock.transform.forward);

            CombinedWeight = distanceWeight * Mathf.Abs(directionWeight);

            // tick stay effects (centralized)
            ExecuteBlockStayEffects(CombinedWeight);
        }

        void OnTriggerExit(Collider other)
        {
            if (skimmer?.ShipStatus == null) return;

            if (!other.TryGetComponent<TrailBlock>(out var trailBlock)) return;
            if (!skimmer.AffectSelf && trailBlock.Team == skimmer.ShipStatus.Team) return;

            if (!_skimStartTimes.ContainsKey(trailBlock.ownerID)) return;

            _skimStartTimes.Remove(trailBlock.ownerID);
            ActivelySkimmingBlockCount = Mathf.Max(0, ActivelySkimmingBlockCount - 1);

            if (ActivelySkimmingBlockCount < 1)
                ExecuteBlockStayEffects(0f); // stop effects when no longer skimming anything
        }

        // ------------------------------------------------------------------

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, skimmerShipEffects);
                    skimmer.ExecuteImpactOnShip(shipImpactor.Ship);       // secondary call
                    break;

                case PrismImpactor prismImpactor:

                    var prism = prismImpactor.Prism;
                    
                    ExecuteEffect(prismImpactor, skimmerPrismEffects);
                    skimmer.ExecuteImpactOnPrism(prism);    // secondary call (booster viz, etc.)
                    
                    if (!skimmer.AffectSelf && prism.Team == skimmer.ShipStatus.Team)
                        return;
                    StartSkimIfNeeded(prism.ownerID);
                    
                    break;

                case ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(elementalCrystalImpactor, skimmerElementalCrystalEffects);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Internals

        void ExecuteBlockStayEffects(float combinedWeight)
        {
            CombinedWeight = combinedWeight;

            if (skimmerPrismStayEffects == null || skimmerPrismStayEffects.Length == 0)
                return;

            // Run as self-effects. Effects can cast `impactor` to SkimmerImpactor and read `CombinedWeight`.
            for (int i = 0; i < skimmerPrismStayEffects.Length; i++)
                skimmerPrismStayEffects[i]?.Execute(this, this);
        }

        void StartSkimIfNeeded(string ownerId)
        {
            if (_skimStartTimes.ContainsKey(ownerId)) return;
            _skimStartTimes.Add(ownerId, Time.time);
            ActivelySkimmingBlockCount++;
        }
    }
}
