using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FillChargeImpactEffect", menuName = "ScriptableObjects/Impact Effects/FillChargeImpactEffectSO")]
    public class FillChargeEffectSO : ImpactEffectSO, ICrystalImpactEffect
    {
        [SerializeField]
        int _boostResourceIndex;

        public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_boostResourceIndex, crystalProperties.fuelAmount);
        }
    }
}
