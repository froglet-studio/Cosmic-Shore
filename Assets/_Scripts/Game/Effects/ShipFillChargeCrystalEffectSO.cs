using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipFillChargeCrystalEffect", menuName = "ScriptableObjects/Impact Effects/ShipFillChargeCrystalEffectSO")]
    public class ShipFillChargeCrystalEffectSO : ImpactEffectSO<R_ShipImpactor, R_CrystalImpactor>
    {
        [SerializeField] private int boostResourceIndex;
        
        protected override void ExecuteTyped(R_ShipImpactor impactor, R_CrystalImpactor crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var crystalProperties = crystalImpactee.Crystal.crystalProperties;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, crystalProperties.fuelAmount);
        }
    }
}
