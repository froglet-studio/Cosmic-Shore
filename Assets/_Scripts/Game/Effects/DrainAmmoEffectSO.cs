using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DrainAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/DrainAmmoImpactEffectSO")]
    public class DrainAmmoEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        [SerializeField]
        int _ammoResourceIndex;

        public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                            - data.ThisShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount);
        }
    }
}
