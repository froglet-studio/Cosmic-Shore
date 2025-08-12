using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDrainAmmoEffect", menuName = "ScriptableObjects/Impact Effects/ShipDrainAmmoEffectSO")]
    public class ShipDrainAmmoEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField] private int ammoResourceIndex;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            - shipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount);
        }
    }
}
