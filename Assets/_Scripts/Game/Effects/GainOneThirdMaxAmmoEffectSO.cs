using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainOneThirdMaxAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainOneThirdMaxAmmoImpactEffectSO")]
    public class GainOneThirdMaxAmmoEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        int _ammoResourceIndex;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                            context.ShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount / 3f);
        }
    }
}
