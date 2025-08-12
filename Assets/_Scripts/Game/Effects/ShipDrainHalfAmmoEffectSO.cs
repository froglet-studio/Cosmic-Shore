using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDrainHalfAmmoEffect", menuName = "ScriptableObjects/Impact Effects/ShipDrainHalfAmmoEffectSO")]
    public class ShipDrainHalfAmmoEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        private const int DivideBy = 2;

        [SerializeField] private int ammoResourceIndex;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                - shipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / DivideBy);
        }
    }
}
