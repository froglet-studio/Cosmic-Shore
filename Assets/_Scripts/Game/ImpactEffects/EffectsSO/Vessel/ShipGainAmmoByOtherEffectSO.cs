using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipGainAmmoByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipGainAmmoByOtherEffectSO")]
    public class ShipGainAmmoByOtherEffectSO : ShipOtherEffectSO
    {
        [SerializeField] private int ammoResourceIndex;
        [SerializeField] private float ammoAmountMultiplier = 1f;

        public override void Execute(ShipImpactor impactor, ImpactorBase impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            
            shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                shipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount * ammoAmountMultiplier);
        }
    }
}
