namespace CosmicShore.Utility.Timers
{
    public class NetworkTimer
    {
        private float _timer;
        public float ServerDeltaTime { get; }
        public int CurrentTick { get; private set; }

        public NetworkTimer(float serverTickRate)
        {
            ServerDeltaTime = 1.0f / serverTickRate;
        }

        public void Update(float deltaTime)
        {
            _timer += deltaTime;
        }

        public bool ShouldTick()
        {
            if (_timer >= ServerDeltaTime)
            {
                _timer -= ServerDeltaTime;
                CurrentTick++;
                return true;
            }

            return false;
        }
    }
}