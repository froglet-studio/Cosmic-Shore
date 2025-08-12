using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipGainAmmoEffect", menuName = "ScriptableObjects/Impact Effects/ShipGainAmmoEffectSO")]
    public class ShipGainAmmoEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField] private int ammoResourceIndex;
        [SerializeField] private float ammoAmountMultiplier = 1f;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                shipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount * ammoAmountMultiplier);
        }
    }
}
