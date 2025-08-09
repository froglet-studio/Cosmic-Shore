using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DrainAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/DrainAmmoImpactEffectSO")]
    public class DrainAmmoEffectSO : ImpactEffectSO
    {
        [SerializeField]
        int _ammoResourceIndex;

        /*public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                            - data.ThisShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
