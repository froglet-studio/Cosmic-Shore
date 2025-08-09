using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FillChargeImpactEffect", menuName = "ScriptableObjects/Impact Effects/FillChargeImpactEffectSO")]
    public class FillChargeEffectSO : ImpactEffectSO
    {
        [SerializeField]
        int _boostResourceIndex;

        /*public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_boostResourceIndex, crystalProperties.fuelAmount);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
