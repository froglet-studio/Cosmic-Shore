using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Singleton;
using System;
using UnityEngine;

namespace CosmicShore.App.Systems
{
    public class DailyChallengeSystem : SingletonPersistent<DailyChallengeSystem>
    {
        [SerializeField] int DailyAttempts = 3;
        DateTime initializedDate = DateTime.MinValue;
        DailyChallenge dailyChallenge;
        ResourceCollection ShipResources;
        ShipTypes ShipClass;

        string LastTicketIssuedDatePrefKey = "DailyChallengeTicketIssuedDate";
        string TicketBalancePrefKey = "DailyChallengeTicketBalance";
        

        void Start()
        {
            if (!PlayerPrefs.HasKey(LastTicketIssuedDatePrefKey))
            {
                PlayerPrefs.SetString(LastTicketIssuedDatePrefKey, DateTime.MinValue.Date.ToString("o"));
                PlayerPrefs.Save();
            }

            if (!PlayerPrefs.HasKey(TicketBalancePrefKey))
            {
                PlayerPrefs.SetInt(TicketBalancePrefKey, 0);
                PlayerPrefs.Save();
            }

            var lastTickedIssuedDate = DateTime.Parse(PlayerPrefs.GetString(LastTicketIssuedDatePrefKey));

            if (lastTickedIssuedDate < DateTime.UtcNow.Date)
            {
                var ticketBalance = PlayerPrefs.GetInt(TicketBalancePrefKey);
                PlayerPrefs.SetInt(TicketBalancePrefKey, Mathf.Max(ticketBalance, DailyAttempts));
                PlayerPrefs.SetString(LastTicketIssuedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
            }
        }

        public DailyChallenge DailyChallenge { 
            get
            {
                if (initializedDate != DateTime.UtcNow.Date)
                {
                    initializedDate = DateTime.UtcNow.Date;

                    // Use the 32 least significant bits (& 0xFFFFFFFF) of the tick count from today's date in GMT as the random seed 
                    DateTime currentDate = DateTime.UtcNow.Date;
                    long dateTicks = currentDate.Ticks;
                    System.Random random = new System.Random((int)(dateTicks & 0xFFFFFFFF));
                    var trainingGames = Arcade.Instance.TrainingGames.GameList;
                    var trainingGame = trainingGames[random.Next(trainingGames.Count)] as SO_TrainingGame;
                    dailyChallenge = new DailyChallenge();
                    dailyChallenge.GameMode = trainingGame.Game.Mode;
                    dailyChallenge.Intensity = random.Next(4);  // TODO: should this be 0-3 (as it is now) or 1-4?
                    ShipResources = LoadGameResourceCollection(trainingGame);
                    ShipClass = trainingGame.ShipClass.Class;
                }

                return dailyChallenge;
            }
        }

        public void PlayDailyChallenge()
        {
            var remainingAttempts = PlayerPrefs.GetInt(TicketBalancePrefKey);
            if (remainingAttempts > 0)
            {
                Debug.Log($"DailyChallenge - Remaining Attempts:{remainingAttempts - 1}");
                PlayerPrefs.SetInt(TicketBalancePrefKey, remainingAttempts - 1);
                Arcade.Instance.LaunchTrainingGame(dailyChallenge.GameMode, ShipClass, ShipResources, dailyChallenge.Intensity, 1, true);
            }
            else
            {
                Debug.LogError("Attempt to play Daily Challenge without remaining tickets");
            }
        }

        ResourceCollection LoadGameResourceCollection(SO_TrainingGame game)
        {
            var resourceCollection = new ResourceCollection
            {
                Mass = game.ElementOne.Element == Element.Mass || game.ElementTwo.Element == Element.Mass ? 1 : 0,
                Charge = game.ElementOne.Element == Element.Charge || game.ElementTwo.Element == Element.Charge ? 1 : 0,
                Space = game.ElementOne.Element == Element.Space || game.ElementTwo.Element == Element.Space ? 1 : 0,
                Time = game.ElementOne.Element == Element.Time || game.ElementTwo.Element == Element.Time ? 1 : 0
            };
            return resourceCollection;
        }
    }

    [Serializable]
    public struct DailyChallenge
    {
        public int Intensity;
        public MiniGames GameMode;
    }
}