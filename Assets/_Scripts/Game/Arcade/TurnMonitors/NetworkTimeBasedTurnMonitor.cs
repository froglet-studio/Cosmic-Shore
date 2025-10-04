using Unity.Netcode;

namespace CosmicShore.Game.Arcade
{
    public class NetworkTimeBasedTurnMonitor : TimeBasedTurnMonitor
    {
        protected override void UpdateTimerUI()
        {
            UpdateTimerUI_ClientRpc();
        }

        [ClientRpc]
        private void UpdateTimerUI_ClientRpc()
        {
            UpdateTimerUI_2();
        }
    }
}