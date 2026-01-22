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

        public override void Subscribe()
        {
            GameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            GameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
        }

        public override void Unsubscribe()
        {
            GameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            GameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;

            // Important: stop loop if still running
            OnTurnEnded();
        }

        private void OnTurnStarted()
        {
            if (_cts != null) return; // prevent double-start

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
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    if (GameData.TurnStartTime > _lastUpdateTime)
                        _lastUpdateTime = GameData.TurnStartTime;

                    float now = Time.time;
                    float dt = Mathf.Max(0f, now - _lastUpdateTime);
                    _lastUpdateTime = now;

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

        private void AddTimeScore(float dt)
        {
            for (int i = 0; i < GameData.RoundStatsList.Count; i++)
            {
                var playerScore = GameData.RoundStatsList[i];
                playerScore.Score += dt * scoreMultiplier;
                GameData.RoundStatsList[i] = playerScore;
            }
        }
    }
}
