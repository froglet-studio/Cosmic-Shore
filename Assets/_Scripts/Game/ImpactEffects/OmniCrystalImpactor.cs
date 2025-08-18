using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class OmniCrystalImpactor : CrystalImpactor
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] omniCrystalShipEffectsSO;

        private IImpactEffect[] omniCrystalShipEffects;

        protected virtual void Awake()
        {
            base.Awake();
            omniCrystalShipEffects = Array.ConvertAll(omniCrystalShipEffectsSO, so => so as IImpactEffect);
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactee:
                {
                    ExecuteEffect(shipImpactee, omniCrystalShipEffects);
                    break;
                }
            }
        }
    }
}