using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] elementalCrystalShipEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] elementalCrystalSkimmerEffectsSO;

        IImpactEffect[] elementalCrystalShipEffects;
        IImpactEffect[] elementalCrystalSkimmerEffects;

        protected virtual void Awake()
        {
            base.Awake();
            elementalCrystalShipEffects = Array.ConvertAll(elementalCrystalShipEffectsSO, so => so as IImpactEffect);
            elementalCrystalSkimmerEffects =
                Array.ConvertAll(elementalCrystalSkimmerEffectsSO, so => so as IImpactEffect);
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, elementalCrystalShipEffects);
                    break;
                case SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, elementalCrystalSkimmerEffects);
                    break;
            }
        }
    }
}