using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_OmniCrystalImpactor : R_ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] omniCrystalShipEffectsSO;
        
        R_IImpactEffect[] omniCrystalShipEffects;

        private void Awake()
        {
            omniCrystalShipEffects = Array.ConvertAll(omniCrystalShipEffectsSO, so => so as R_IImpactEffect);
        }

        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, omniCrystalShipEffects);
                    break;
            }
        }
    }
}