namespace CosmicShore.Gameplay
{
    public class NetworkTurnMonitorController : TurnMonitorController
    {
        protected override void OnEnable()
        {
            // Re-subscribe when the environment is reactivated (e.g., party mode
            // toggling SetActive). OnDisable (base) already unsubscribes + stops
            // monitors, so we must re-subscribe here. Guard on IsSpawned to avoid
            // subscribing before OnNetworkSpawn has fired during initial scene load.
            if (IsSpawned)
                SubscribeToEvents();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            SubscribeToEvents();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            UnsubscribeFromEvents();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }
    }
}
