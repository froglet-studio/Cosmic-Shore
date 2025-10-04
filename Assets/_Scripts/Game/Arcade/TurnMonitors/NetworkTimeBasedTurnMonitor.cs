using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkTimeBasedTurnMonitor : TimeBasedTurnMonitor
    {
        protected override void UpdateTimerUI()
        {
            FixedString32Bytes message = GetTimeToDisplay(); 
            UpdateTimerUI_ClientRpc(message);
        }

        [ClientRpc]
        private void UpdateTimerUI_ClientRpc(FixedString32Bytes message) =>
            UpdateTimerUI_2(message.ToString());
    }
}