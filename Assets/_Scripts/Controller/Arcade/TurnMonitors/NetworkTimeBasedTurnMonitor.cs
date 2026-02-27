using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    public class NetworkTimeBasedTurnMonitor : TimeBasedTurnMonitor
    {
        protected override void UpdateTimerUI()
        {
            // In party mode, RPCs on scene-placed NetworkObjects may not work
            // reliably after SetActive toggling. Update the display directly
            // via the ScriptableEvent (same path the base class uses).
            if (gameData != null && gameData.IsPartyMode)
            {
                InvokeUpdateTurnMonitorDisplay(GetTimeToDisplay());
                return;
            }

            FixedString32Bytes message = GetTimeToDisplay();
            UpdateTimerUI_ClientRpc(message);
        }

        [ClientRpc]
        private void UpdateTimerUI_ClientRpc(FixedString32Bytes message) =>
            InvokeUpdateTurnMonitorDisplay(message.ToString());
    }
}