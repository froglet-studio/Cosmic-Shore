using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FillChargeImpactEffect", menuName = "ScriptableObjects/Impact Effects/FillChargeImpactEffectSO")]
    public class FillChargeEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        int _boostResourceIndex;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_boostResourceIndex, context.CrystalProperties.fuelAmount);
        }
    }
}
