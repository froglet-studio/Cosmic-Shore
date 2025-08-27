using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShip))]
    public class ShipImpactor : ImpactorBase
    {
        ShipPrismEffectSO[] shipPrismEffects;
        
        ShipCrystalEffectSO[] shipOmniCrystalEffects;
        
        ShipCrystalEffectSO[] shipElementalCrystalEffects;
        
        ShipCrystalEffectSO[] shipFakeCrystalEffects;
        
        public IShip Ship { get; private set; }
        
        private void Awake()
        {
            Ship ??= GetComponent<IShip>();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case PrismImpactor prismImpactee:
                   // ExecuteEffect(impactee, shipPrismEffects);
                   if(!DoesEffectExist(shipPrismEffects)) return;
                   foreach (var effect in shipPrismEffects)
                   {
                       effect.Execute(this, prismImpactee);
                   }
                   break;
                case OmniCrystalImpactor omniCrystalImpactee:
                    //ExecuteEffect(impactee, shipOmniCrystalEffects);
                    if(!DoesEffectExist(shipOmniCrystalEffects)) return;
                    foreach (var effect in shipOmniCrystalEffects)
                    {
                        effect.Execute(this, omniCrystalImpactee);
                    }
                    break;
                case ElementalCrystalImpactor elementalCrystalImpactee:
                    //ExecuteEffect(impactee, shipElementalCrystalEffects);
                    if(!DoesEffectExist(shipElementalCrystalEffects)) return;
                    foreach (var effect in shipElementalCrystalEffects)
                    {
                        effect.Execute(this, elementalCrystalImpactee);
                    }
                    break;
            }
        }

        private void Reset()
        { 
            Ship ??= GetComponent<IShip>();
        }
    }
}