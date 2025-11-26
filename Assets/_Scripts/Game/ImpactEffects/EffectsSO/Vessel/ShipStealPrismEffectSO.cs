using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipStealPrismEffectSO")]
    public class ShipStealPrismEffectSO : StealPrismEffectBaseSO<ShipImpactor>
    {
        protected override IShipStatus GetShipStatus(ShipImpactor impactor)
            => impactor.Ship?.ShipStatus;
    }
}