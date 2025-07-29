using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DrainHalfAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/DrainHalfAmmoImpactEffectSO")]
    public class DrainHalfAmmoEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        const int DIVIDE_BY = 2;

        [SerializeField]
        int _ammoResourceIndex;

        public void Execute(ImpactEffectData context)
        {
            context.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                                - context.ThisShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount / DIVIDE_BY);
        }
    }
}
