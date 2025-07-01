using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DrainAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/DrainAmmoImpactEffectSO")]
    public class DrainAmmoEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        int _ammoResourceIndex;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                            - context.ShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount);
        }
    }
}
