using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipMissileRechargeByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipMissileRechargeByCrystalEffectSO")]
    public class ShipMissileRechargeByCrystalEffectSO : ShipCrystalEffectSO
    {
        [SerializeField] private int resourceIndex = 0;
        
        public override void Execute(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            var shipStatus = shipImpactor.Ship.ShipStatus;
            var resourceSystem = shipImpactor.GetComponent<ResourceSystem>();
            
            if (shipStatus.ShipType != ShipClassType.Sparrow) return;
            resourceSystem.ResetResource(resourceIndex);
        }
    }
}