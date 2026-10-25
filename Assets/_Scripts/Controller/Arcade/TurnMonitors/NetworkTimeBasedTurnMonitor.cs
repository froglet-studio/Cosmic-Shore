using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    public class NetworkTimeBasedTurnMonitor : TimeBasedTurnMonitor
    {
        // The SERVER's clock ends the turn, like every other network monitor. The monitor runs on
        // every peer (TurnMonitorController starts it on OnMiniGameTurnStarted), so without this a
        // client's own clock - which drifts with its frame rate - ended the turn locally ahead of
        // the server (abilities and comeback off while the server still scored), and then
        // SyncTurnEnd_ClientRpc raised the turn end a second time.
        public override bool CheckForEndOfTurn() =>
            (!IsSpawned || IsServer) && base.CheckForEndOfTurn();

        protected override void UpdateTimerUI()
        {
            // Only the server may invoke a ClientRpc; a client's display arrives through it.
            if (IsSpawned && !IsServer) return;

            FixedString32Bytes message = GetTimeToDisplay(); 
            UpdateTimerUI_ClientRpc(message);
        }

        [ClientRpc]
        private void UpdateTimerUI_ClientRpc(FixedString32Bytes message) =>
            InvokeUpdateTurnMonitorDisplay(message.ToString());
    }
}
