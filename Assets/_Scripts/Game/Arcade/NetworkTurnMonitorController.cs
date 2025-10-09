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

            if (!IsServer)
                return;
            
            SubscribeToEvents();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsServer)
                return;
            
            UnsubscribeFromEvents();
        }

        protected virtual void OnDisable()
        {
        }
    }
}