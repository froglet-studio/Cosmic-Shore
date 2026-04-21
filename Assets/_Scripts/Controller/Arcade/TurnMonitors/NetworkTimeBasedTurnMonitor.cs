using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    public class NetworkTimeBasedTurnMonitor : TimeBasedTurnMonitor
    {
        protected override void UpdateTimerUI()
        {
            if (!IsSpawned || !IsServer) return;
            FixedString32Bytes message = GetTimeToDisplay();
            UpdateTimerUI_ClientRpc(message);
        }

        [ClientRpc]
        private void UpdateTimerUI_ClientRpc(FixedString32Bytes message) =>
            InvokeUpdateTurnMonitorDisplay(message.ToString());
    }
}