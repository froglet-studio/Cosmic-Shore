using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipGainResourceByVolumeEffect", menuName = "ScriptableObjects/Impact Effects/ShipGainResourceByVolumeEffectSO")]
    public class ShipGainResourceByVolumeEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField] private int boostResourceIndex;
        [SerializeField] private float blockChargeChange;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, blockChargeChange);
        }
    }
}
