using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Utility;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class TimePlayedScoring : BaseScoring
    {
        private readonly float _intervalSeconds;
        private CancellationTokenSource _cts;
        private float _lastUpdateTime;
        private double _networkStartTime;

        public TimePlayedScoring(IScoreTracker tracker, GameDataSO data, float scoreMultiplier, float intervalSeconds = 0.25f)
            : base(tracker, data, scoreMultiplier)
        {
            _intervalSeconds = Mathf.Max(0.01f, intervalSeconds);
        }

        public override void Subscribe()
        {
            OnTurnStarted();
        }

        public override void Unsubscribe()
        {
            OnTurnEnded();
        }

        private void OnTurnStarted()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();

            if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening)
            {
                _networkStartTime = NetworkManager.Singleton.ServerTime.Time;
            }
            else
            {
                _networkStartTime = Time.timeAsDouble;
            }

            _lastUpdateTime = 0f;

            UpdateScoreLoop(_cts.Token).Forget();
        }

        private void OnTurnEnded()
        {
            if (_cts == null) return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private async Task UpdateScoreLoop(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    float currentElapsedTime = GetCurrentElapsedTime();
                    float dt = Mathf.Max(0f, currentElapsedTime - _lastUpdateTime);
                    _lastUpdateTime = currentElapsedTime;

                    if (dt > 0f)
                        AddTimeScore(dt);

                    await GameTask.Delay(TimeSpan.FromSeconds(_intervalSeconds), token);
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }

        private float GetCurrentElapsedTime()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
                return (float)(Time.timeAsDouble - _networkStartTime);
            var currentNetworkTime = NetworkManager.Singleton.ServerTime.Time;
            return (float)(currentNetworkTime - _networkStartTime);

        }

        private void AddTimeScore(float dt)
        {
            var score = dt * scoreMultiplier;
            foreach (var stats in GameData.RoundStatsList)
                stats.Score += score;
        }
    }
}
