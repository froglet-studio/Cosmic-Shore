using CosmicShore.Game.UI;
using UnityEngine;


namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "ShipHUDEventChannel", menuName = "ScriptableObjects/Event Channels/ShipHUDEventChannelSO")]
    public class ShipHUDEventChannelSO : GenericEventChannelSO<ShipHUDData>
    { }

    public struct ShipHUDData
    {
        public MiniGameHUD ShipHUD;
    }
}