using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipGainAmmoByOtherEffect", menuName = "ScriptableObjects/Impact Effects/ShipGainAmmoByOtherEffectSO")]
    public class ShipGainAmmoByOtherEffectSO : ImpactEffectSO<ShipImpactor, ImpactorBase>
    {
        [SerializeField] private int ammoResourceIndex;
        [SerializeField] private float ammoAmountMultiplier = 1f;

        protected override void ExecuteTyped(ShipImpactor impactor, ImpactorBase impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                shipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount * ammoAmountMultiplier);
        }
    }
}
