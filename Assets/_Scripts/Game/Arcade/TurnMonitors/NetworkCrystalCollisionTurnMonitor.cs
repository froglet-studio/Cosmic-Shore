using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        protected override void UpdateCrystalsRemainingUI()
        {
            FixedString32Bytes message = GetRemainingCrystalsCountToCollect();
            UpdateCrystalsRemainingUI_ClientRpc(message);
        }
        
        [ClientRpc]
        private void UpdateCrystalsRemainingUI_ClientRpc(FixedString32Bytes message) =>
            InvokeUpdateTurnMonitorDisplay(message.ToString());
    }
}