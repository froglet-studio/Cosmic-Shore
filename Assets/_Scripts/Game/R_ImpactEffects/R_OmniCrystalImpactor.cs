using UnityEngine;

namespace CosmicShore.Game
{
    public class R_OmniCrystalImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] omniCrystalShipEffects;
        
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