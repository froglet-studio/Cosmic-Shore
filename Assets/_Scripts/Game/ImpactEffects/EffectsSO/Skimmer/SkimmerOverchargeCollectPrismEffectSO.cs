// SkimmerOverchargeCollectPrismEffectSO.cs  (minimal diff)
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerOverchargeCollectPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer/SkimmerOverchargeCollectPrismEffectSO")] 
    public class SkimmerOverchargeCollectPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Overcharge Settings")]
        [SerializeField] private int   maxBlockHits     = 30;
        [SerializeField] private float cooldownDuration = 5f;
        [SerializeField] private bool  verbose;  
        [SerializeField] private Material overchargedMaterial;

        [Header("HUD")]
        [SerializeField] private string textKey = "SkimmerOverchargeText"; 

        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers = new();
        IShipStatus _status;
        
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            _status = impactor.Skimmer.ShipStatus;
            if (prismImpactee.Prism.Team == Teams.Jade) return;

            if (cooldownTimers.TryGetValue(impactor, out var cooldownEnd) && Time.time < cooldownEnd)
            {
                SetHudText(impactor, $"0/{maxBlockHits}");
                if (verbose) Debug.Log("[SkimmerOvercharge] Still on cooldown.", impactor);
                return;
            }

            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            if (!hitSet.Add(prismImpactee)) return;

            var rend = prismImpactee.GetComponent<Renderer>();
            if (rend != null && overchargedMaterial != null) rend.material = overchargedMaterial;

            var count = hitSet.Count;
            SetHudText(impactor, $"{count}/{maxBlockHits}");

            if (count < maxBlockHits) return;

            // threshold reached
            TriggerOvercharge(impactor, hitSet);
            hitSet.Clear();
            SetHudText(impactor, $"{maxBlockHits}/{maxBlockHits}");
            SetHudText(impactor, $"0/{maxBlockHits}"); // immediate reset
            cooldownTimers[impactor] = Time.time + cooldownDuration;
        }

        private void TriggerOvercharge(SkimmerImpactor impactor, HashSet<PrismImpactor> hitSet)
        {
            foreach (var prism in hitSet) prism.gameObject.SetActive(false);
            if (verbose) Debug.Log($"[SkimmerOvercharge] Overcharge triggered! ({hitSet.Count})", impactor);
        }

        private void SetHudText(SkimmerImpactor impactor, string value)
        {
            if (_status?.ShipHUDView is ShipHUDView view)
            {
                view.Effects?.SetText(textKey, value);
            }
        }
    }
}
