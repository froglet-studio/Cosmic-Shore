namespace CosmicShore.Game.Arcade
{
    public class NetworkTurnMonitorController : TurnMonitorController
    {
        protected override void OnEnable()
        {
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