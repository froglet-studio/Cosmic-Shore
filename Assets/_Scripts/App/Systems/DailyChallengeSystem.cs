using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Singleton;
using System;
using UnityEngine;

namespace CosmicShore.App.Systems
{
    public class DailyChallengeSystem : SingletonPersistent<DailyChallengeSystem>
    {
        [Tooltip("List of all training games")]
        DateTime initializedDate = DateTime.MinValue;
        DailyChallenge dailyChallenge;

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
                    dailyChallenge.ShipResources = LoadGameResourceCollection(trainingGame);
                    dailyChallenge.ShipClass = trainingGame.ShipClass.Class;
                    dailyChallenge.Intensity = random.Next(4);  // TODO: should this be 0-3 (as it is now) or 1-4?
                }

                return dailyChallenge;
            }
        }

        public void PlayDailyChallenge()
        {
            Arcade.Instance.LaunchTrainingGame(dailyChallenge.GameMode, dailyChallenge.ShipClass, dailyChallenge.ShipResources, dailyChallenge.Intensity, 1, true);
        }

        ResourceCollection LoadGameResourceCollection(SO_TrainingGame game)
        {
            var resourceCollection = new ResourceCollection();
            resourceCollection.Mass = game.ElementOne.Element == Element.Mass || game.ElementTwo.Element == Element.Mass ? 1 : 0;
            resourceCollection.Charge = game.ElementOne.Element == Element.Charge || game.ElementTwo.Element == Element.Charge ? 1 : 0;
            resourceCollection.Space = game.ElementOne.Element == Element.Space || game.ElementTwo.Element == Element.Space ? 1 : 0;
            resourceCollection.Time = game.ElementOne.Element == Element.Time || game.ElementTwo.Element == Element.Time ? 1 : 0;
            return resourceCollection;
        }
    }

    [Serializable]
    public struct DailyChallenge
    {
        public int Intensity;
        public ShipTypes ShipClass;
        public MiniGames GameMode;
        public ResourceCollection ShipResources;
    }
}
