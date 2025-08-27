using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeResourceByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeResourceByCrystalEffectSO")]
    public class ShipChangeResourceByCrystalEffectSO : ShipCrystalEffectSO
    {
        [SerializeField] private int resourceIndex;
        
        public override void Execute(ShipImpactor impactor, CrystalImpactor crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var crystalProperties = crystalImpactee.Crystal.crystalProperties;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, crystalProperties.fuelAmount);
        }
    }
}
