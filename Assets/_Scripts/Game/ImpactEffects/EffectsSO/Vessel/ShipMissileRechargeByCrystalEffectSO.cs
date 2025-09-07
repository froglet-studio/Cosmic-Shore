using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipMissileRechargeByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipMissileRechargeByCrystalEffectSO")]
    public class ShipMissileRechargeByCrystalEffectSO : ImpactEffectSO<ShipImpactor, CrystalImpactor>
    {
        [SerializeField] private int resourceIndex = 0;
        
        protected override void ExecuteTyped(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            var shipStatus = shipImpactor.Ship.ShipStatus;
            var resourceSystem = shipImpactor.GetComponent<ResourceSystem>();
            
            if (shipStatus.ShipType != ShipClassType.Sparrow) return;
            resourceSystem.ResetResource(resourceIndex);
        }
    }
}