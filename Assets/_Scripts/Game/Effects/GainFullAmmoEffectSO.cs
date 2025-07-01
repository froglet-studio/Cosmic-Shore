using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainFullAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainFullAmmoImpactEffectSO")]
    public class GainFullAmmoEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        int _ammoResourceIndex;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                            context.ShipStatus.ResourceSystem.Resources[_ammoResourceIndex].MaxAmount);
        }
    }
}
