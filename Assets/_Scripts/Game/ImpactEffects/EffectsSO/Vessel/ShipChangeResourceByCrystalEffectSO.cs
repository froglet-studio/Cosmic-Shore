using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeResourceByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/ShipChangeResourceByCrystalEffectSO")]
    public class ShipChangeResourceByCrystalEffectSO : ImpactEffectSO<ShipImpactor, CrystalImpactor>
    {
        [SerializeField] private int resourceIndex;
        
        protected override void ExecuteTyped(ShipImpactor impactor, CrystalImpactor crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var crystalProperties = crystalImpactee.Crystal.crystalProperties;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, crystalProperties.fuelAmount);
        }
    }
}
