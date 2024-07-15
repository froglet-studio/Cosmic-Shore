using CosmicShore.App.Systems;
using CosmicShore.Core;
using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CosmicShore
{
    public class DailyChallengeGameView : View
    {
        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] GameObject PreviewWindow;

        void Start()
        {
            var gameMode = DailyChallengeSystem.Instance.DailyChallenge.GameMode;
            AssignModel(Arcade.Instance.TrainingGames.GameList.Where(x => x.Game.Mode == gameMode).FirstOrDefault().Game);
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
                AssignModel(Arcade.Instance.TrainingGames.GameList.Where(x => x.Game.Mode == gameMode).FirstOrDefault().Game);
            }
        }

        public override void UpdateView()
        {
            var game = SelectedModel as SO_ArcadeGame;
            GameTitle.text = $"{game.DisplayName}";
            // TODO: may need to destroy all children first to prevent multiple clips from playing
            var preview = Instantiate(game.PreviewClip);
            preview.transform.SetParent(PreviewWindow.transform, false);
            preview.GetComponent<RectTransform>().sizeDelta = new Vector2(256, 144);

            Canvas.ForceUpdateCanvases();
        }
    }
}