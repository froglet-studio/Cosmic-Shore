using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DrainHalfAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/DrainHalfAmmoImpactEffectSO")]
    public class DrainHalfAmmoEffectSO : BaseImpactEffectSO
    {
        const int DIVIDE_BY = 2;

        [SerializeField]
        int _ammoResourceIndex;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                                - context.ShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount / DIVIDE_BY);
        }
    }
}
