using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShip))]
    public class ShipImpactor : ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] shipPrismEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] shipOmniCrystalEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] shipElementalCrystalEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] shipFakeCrystalEffectsSO;

        IImpactEffect[] shipPrismEffects;
        IImpactEffect[] shipOmniCrystalEffects;
        IImpactEffect[] shipElementalCrystalEffects;
        IImpactEffect[] shipFakeCrystalEffects;

        public IShip Ship;

        private void Awake()
        {
            Ship ??= GetComponent<IShip>();

            shipPrismEffects = Array.ConvertAll(shipPrismEffectsSO, so => (IImpactEffect)so);
            shipOmniCrystalEffects = Array.ConvertAll(shipOmniCrystalEffectsSO, so => (IImpactEffect)so);
            shipElementalCrystalEffects = Array.ConvertAll(shipElementalCrystalEffectsSO, so => (IImpactEffect)so);
            shipFakeCrystalEffects = Array.ConvertAll(shipFakeCrystalEffectsSO, so => (IImpactEffect)so);
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, shipPrismEffects);
                    break;
                case OmniCrystalImpactor omniCrystalImpactor:
                    ExecuteEffect(impactee, shipOmniCrystalEffects);
                    break;
                case ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(impactee, shipElementalCrystalEffects);
                    break;
            }
        }

        private void Reset()
        {
            Ship ??= GetComponent<IShip>();
        }
    }
}