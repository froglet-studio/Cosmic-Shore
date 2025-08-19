using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class HudSubscriptionSO : ScriptableObject
    {
        protected IShip Ship      { get; private set; }
        protected IShipStatus ShipStatus { get; private set; }
        protected IHUDEffects Effects    { get; private set; }
        protected ShipHUDRefs Refs       { get; private set; }

        public void Initialize(IShip ship, IShipStatus status, IHUDEffects effects, ShipHUDRefs refs)
        {
            Ship = ship;
            ShipStatus = status;
            Effects = effects;
            Refs = refs;
            OnEnableSubscriptions();
        }

        public void Dispose()
        {
            OnDisableSubscriptions();
            Ship = null; ShipStatus = null; Effects = null; Refs = null;
        }

        protected abstract void OnEnableSubscriptions();
        protected abstract void OnDisableSubscriptions();
    }
}