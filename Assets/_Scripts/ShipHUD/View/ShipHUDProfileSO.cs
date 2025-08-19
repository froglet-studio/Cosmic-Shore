using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipHUDProfile", menuName = "HUD/Profile")]
    public class ShipHUDProfileSO : ScriptableObject
    {
        public ShipClassType shipType;
        [Tooltip("List of subscription SOs used by this ship's HUD")]
        public HudSubscriptionSO[] subscriptions;
    }
}