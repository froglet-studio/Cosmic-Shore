using System;
using System.Threading;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
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

        private async UniTaskVoid UpdateScoreLoop(CancellationToken token)
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

                    await UniTask.Delay(TimeSpan.FromSeconds(_intervalSeconds),
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);
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