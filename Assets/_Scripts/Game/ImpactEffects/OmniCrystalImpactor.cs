using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        OmniCrystalExplodeByShipEffectSO[] omniCrystalShipEffects;


        protected virtual void Awake()
        {
            base.Awake();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactee:
                {
                    // ExecuteEffect(shipImpactee, omniCrystalShipEffects);
                    if(!DoesEffectExist(omniCrystalShipEffects)) return;
                    foreach (var effect in omniCrystalShipEffects)
                    {
                        effect.Execute(this, shipImpactee);
                    }
                    break;
                }
            }
        }
    }
}