using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// When a ship impacts any R_ImpactorBase that has/ is on a Crystal,
    /// mark that Crystal so the NEXT vessel-impact behaves as a "decoy":
    /// skip explosion + audio, and spawn a fake crystal instead.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipDecoyCrystalEffect", menuName = "ScriptableObjects/Impact Effects/ShipDecoyCrystalEffectSO")]
    public class ShipDecoyCrystalEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField] private float cooldownDuration = 5f;
        [SerializeField] private bool verbose = false;
        private static readonly Dictionary<Crystal, float> s_cooldownUntil = new();
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            if (verbose) Debug.Log("[ShipDecoy] Impacted : ", impactee);
            
            var crystal = impactee.GetComponent<Crystal>();
            if (!crystal)
            {
                if (verbose) Debug.Log("[Decoy] Impacted has no Crystal in hierarchy.", impactee);
                return;
            }

            if (s_cooldownUntil.TryGetValue(crystal, out var until) && Time.time < until)
            {
                if (verbose) Debug.Log("[Decoy] On cooldown; skipping.", crystal);
                return;
            }

            crystal.MarkNextImpactAsDecoy();

            s_cooldownUntil[crystal] = Time.time + cooldownDuration;
        }
    }
}