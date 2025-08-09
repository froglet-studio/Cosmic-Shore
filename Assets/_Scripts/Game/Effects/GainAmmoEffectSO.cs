using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainAmmoImpactEffectSO")]
    public class GainAmmoEffectSO : ImpactEffectSO
    {
        [SerializeField]
        int _ammoResourceIndex;

        [SerializeField]
        float _ammoAmountMultiplier = 1f;

        /*public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                            data.ThisShipStatus.ResourceSystem.Resources[_ammoResourceIndex].MaxAmount * _ammoAmountMultiplier);
        }*/

        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
