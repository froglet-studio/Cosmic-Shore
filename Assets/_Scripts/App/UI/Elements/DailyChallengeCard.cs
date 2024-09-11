using CosmicShore.App.Systems;
using CosmicShore.Core;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class DailyChallengeCard : MonoBehaviour
    {
        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] Image BackgroundImage;

        GameModes gameMode;

        public GameModes GameMode
        {
            get { return gameMode; }
            set
            {
                gameMode = value;
                UpdateCardView();
            }
        }

        void Start()
        {
            gameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
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
                gameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
            }
        }

        void UpdateCardView()
        {
            var game = Arcade.Instance.TrainingGames.Games.Where(x => x.Game.Mode == gameMode).FirstOrDefault().Game;
            GameTitle.text = $"Daily Challenge: {game.DisplayName}";
            BackgroundImage.sprite = game.CardBackground;
        }
    }
}