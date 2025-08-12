using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipGainResourceEffect", menuName = "ScriptableObjects/Impact Effects/ShipGainResourceEffectSO")]
    public class ShipGainResourceEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField] private int resourceIndex;

        [SerializeField] private int blockChargeChange;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, blockChargeChange);
        }
    }
}
