using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipDamagePrismEffectSO")]
    public class ShipDamagePrismEffectSO : DamagePrismEffectBase<ShipImpactor>
    {
        protected override IShipStatus GetAttackerStatus(ShipImpactor impactor)
            => impactor.Ship?.ShipStatus;
    }
}