using System;
using System.Threading;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TimePlayedScoring : BaseScoring
    {
        private readonly float _intervalSeconds;
        private CancellationTokenSource _cts;
        private float _lastUpdateTime;
        
        public TimePlayedScoring(GameDataSO data, float scoreMultiplier, float intervalSeconds = 0.25f)
            : base(data, scoreMultiplier)
        {
            _intervalSeconds = Mathf.Max(0.01f, intervalSeconds);
        }

        public override void Subscribe() => OnTurnStarted();
        public override void Unsubscribe() => OnTurnEnded();

        private void OnTurnStarted()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            _lastUpdateTime = Mathf.Max(GameData.TurnStartTime, Time.time);

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
                while (!token.IsCancellationRequested)
                {
                    float now = Time.time;
                    float dt = Mathf.Max(0f, now - _lastUpdateTime);

                    if (dt > 0f)
                    {
                        _lastUpdateTime = now;
                        AddTimeScore(dt);
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(_intervalSeconds), 
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void AddTimeScore(float dt)
        {
            float amountToAdd = dt * scoreMultiplier; 

            foreach (var stats in GameData.RoundStatsList)
            {
                stats.Score += amountToAdd;
            }
        }
    }
}