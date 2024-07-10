using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace CosmicShore.App.UI.Elements
{
    public class DailyChallengeCard : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] SO_GameList AllGames;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] Image BackgroundImage;

        MiniGames gameMode = MiniGames.Random;
        public MiniGames GameMode
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
            // Use the 32 least significant bits (& 0xFFFFFFFF) of the tick count from today's date in GMT as the random seed 
            DateTime currentDate = DateTime.UtcNow.Date;
            long dateTicks = currentDate.Ticks;
            Random random = new Random((int)(dateTicks & 0xFFFFFFFF));

            while (gameMode == MiniGames.Random)
            {
                gameMode = AllGames.GameList[random.Next(AllGames.GameList.Count)].Mode;
                Debug.Log($"Daily Challenge GameMode: {gameMode}");
            }

            UpdateCardView();
        }

        void Update()
        {
            DateTime current = DateTime.Now;
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
                // Do something here - player watched the clock roll over
            }
        }

        void UpdateCardView()
        {
            SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
            GameTitle.text = $"Daily Challenge: {game.DisplayName}";
            BackgroundImage.sprite = game.CardBackground;
        }

        public void OnCardClicked()
        {
            // Add highlight boarder

            // Set active and show details
            // LoadoutView.ExpandLoadout(Index);

            Debug.Log($"GameCard - Clicked: Gamemode: {gameMode}");
        }
    }
}