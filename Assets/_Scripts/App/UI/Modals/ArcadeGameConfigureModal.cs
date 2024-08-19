using CosmicShore.App.Systems;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using System;
using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class ArcadeGameConfigureModal : MonoBehaviour
    {
        [SerializeField] DailyChallengeGameView GameView;
        [SerializeField] TMP_Text TimeRemaining;

        void Start()
        {
            var gameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
            GameView.AssignModel(Arcade.Instance.GetTrainingGameByMode(gameMode));
        }

        void Update()
        {
            DateTime current = DateTime.UtcNow;
            DateTime tomorrow = current.AddDays(1).Date;
            double secondsUntilMidnight = (tomorrow - current).TotalSeconds;

            if (secondsUntilMidnight > 0)
            {
                TimeSpan timespan = TimeSpan.FromSeconds(secondsUntilMidnight);
                TimeRemaining.text = string.Format("Time left: {0:D2}:{1:D2}:{2:D2}",
                                timespan.Hours,
                                timespan.Minutes,
                                timespan.Seconds);
            }
            else
            {
                var gameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
                GameView.AssignModel(Arcade.Instance.GetTrainingGameByMode(gameMode));
            }
        }

        public void Play()
        {
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}