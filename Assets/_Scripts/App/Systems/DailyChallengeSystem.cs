using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.CloudScripts;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Singleton;
using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Integrations.PlayFab.PlayerData;
using PlayFab.ClientModels;
using UnityEngine;
using CosmicShore.Integrations.PlayFab.Economy;

namespace CosmicShore.App.Systems
{
    public class DailyChallengeSystem : SingletonPersistent<DailyChallengeSystem>
    {
        [SerializeField] int DailyAttempts = 3;

        /// <summary>
        /// Tracking for player's progress on the daily challenge reward ladder
        /// </summary>
        DailyChallengeRewardState rewardState;
        public DailyChallengeRewardState RewardState { get => rewardState;
            private set => rewardState = value;
        }
        
        /// <summary>
        /// Today's challenge
        /// </summary>
        DailyChallenge dailyChallenge;
        public DailyChallenge DailyChallenge => dailyChallenge;

        /// <summary>
        /// Date that the current challenge is valid for
        /// </summary>
        DateTime ChallengeDate = DateTime.MinValue;
        
        /// <summary>
        /// Ship's initial resource values to pass to arcade
        /// </summary>
        ResourceCollection ShipResources;

        /// <summary>
        /// Additional info about the game for today's challenge
        /// </summary>
        SO_TrainingGame DailyGame;

        // TODO: All this goes away once state is tracked on the backend
        readonly string LastTicketIssuedDatePrefKey = "DailyChallengeTicketIssuedDate";
        readonly string InitializedDatePrefKey = "DailyChallengeInitializedDate";
        readonly string TicketBalancePrefKey = "DailyChallengeTicketBalance";
        readonly string HighScorePrefKey = "DailyChallengeHighScore";
        readonly string RewardTierOneClaimedPrefKey = "RewardTierOneClaimed";
        readonly string RewardTierTwoClaimedPrefKey = "RewardTierTwoClaimed";
        readonly string RewardTierThreeClaimedPrefKey = "RewardTierThreeClaimed";
        readonly string RewardTierOneSatisfiedPrefKey = "RewardTierOneSatisfied";
        readonly string RewardTierTwoSatisfiedPrefKey = "RewardTierTwoSatisfied";
        readonly string RewardTierThreeSatisfiedPrefKey = "RewardTierThreeSatisfied";

        private readonly List<string> PrefKeys = new ();

        private void AddKeysToList()
        {
            // Put all the keys into a list for query purpose
            PrefKeys.Add(LastTicketIssuedDatePrefKey);
            PrefKeys.Add(InitializedDatePrefKey);
            PrefKeys.Add(TicketBalancePrefKey);
            PrefKeys.Add(HighScorePrefKey);
            PrefKeys.Add(RewardTierOneClaimedPrefKey);
            PrefKeys.Add(RewardTierTwoClaimedPrefKey);
            PrefKeys.Add(RewardTierThreeClaimedPrefKey);
            PrefKeys.Add(RewardTierTwoSatisfiedPrefKey);
            PrefKeys.Add(RewardTierThreeSatisfiedPrefKey);
        }
        
        void Start()
        {
            InitializePlayerPrefs();
            LoadRewardState();

            ChallengeDate = DateTime.Parse(PlayerPrefs.GetString(InitializedDatePrefKey), null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

            var lastTickedIssuedDate = DateTime.Parse(PlayerPrefs.GetString(LastTicketIssuedDatePrefKey), null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            if (lastTickedIssuedDate < DateTime.UtcNow.Date)
            {
                var ticketBalance = PlayerPrefs.GetInt(TicketBalancePrefKey);
                PlayerPrefs.SetInt(TicketBalancePrefKey, Mathf.Max(ticketBalance, DailyAttempts));
                PlayerPrefs.SetString(LastTicketIssuedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
                PlayerPrefs.Save();
            }

            if (ChallengeDate < DateTime.UtcNow.Date)
            {
                SelectDailyGame();
                ResetRewardState();
            }

            // TODO: this goes away once the daily challenge selection logic is moved to the backend
            // If it's a new launch of the app on the same day, need to reinit this, but not reset other tracking
            if (DailyGame == null)
                SelectDailyGame();

            AddKeysToList();
        }

        private void OnEnable()
        {
            // Subscribe update and pull data logic to getting data event 
            PlayerDataController.OnGettingPlayerData += SaveToPref;
        }

        /// <summary>
        /// Save user data result to PlayerPref
        /// Everytime daily challenge data is updated, pull data from PlayFab immediately and save to PlayerPref
        /// So that the client only need to access PlayerPref for data
        /// Not anti-cheating, but anyways, the updating logic goes to the server side.
        /// </summary>
        /// <param name="result">User data result form PlayFab Player Data</param>
        private void SaveToPref(GetUserDataResult result)
        {
            foreach (var key in PrefKeys)
            {
                if (!result.Data.TryGetValue(key, out var value)) continue;
                
                if (key == InitializedDatePrefKey || key == LastTicketIssuedDatePrefKey)
                {
                    PlayerPrefs.SetString(key, value.Value);
                }
                else
                {
                    PlayerPrefs.SetInt(key, int.Parse(value.Value));
                }
            }
        }

        void SelectDailyGame()
        {
            ChallengeDate = DateTime.UtcNow.Date;
            PlayerPrefs.SetString(InitializedDatePrefKey, DateTime.UtcNow.Date.ToString("o"));
            PlayerPrefs.Save();

            dailyChallenge = FetchDailyChallenge();
            DailyGame = Arcade.Instance.GetTrainingGameByMode(dailyChallenge.GameMode);
            ShipResources = LoadGameResourceCollection(DailyGame);
        }

        DailyChallenge FetchDailyChallenge()
        {
            // Use the 32 least significant bits (& 0xFFFFFFFF) of the tick count from today's date in GMT as the random seed 
            DateTime currentDate = DateTime.UtcNow.Date;
            long dateTicks = currentDate.Ticks;
            var random = new System.Random((int)(dateTicks & 0xFFFFFFFF));

            var trainingGames = Arcade.Instance.TrainingGames.Games;
            var index = random.Next(trainingGames.Count);
            var dailyGame = trainingGames[index];
            var challenge = new DailyChallenge();
            challenge.GameMode = dailyGame.Game.Mode;
            challenge.Intensity = random.Next(4);

            return challenge;
        }

        public void PlayDailyChallenge()
        {
            var remainingAttempts = CatalogManager.Instance.GetDailyChallengeTicketBalance();//PlayerPrefs.GetInt(TicketBalancePrefKey);
            if (remainingAttempts > 0)
            {
                Debug.Log($"DailyChallenge - Remaining Attempts:{remainingAttempts - 1}");
                CatalogManager.Instance.UseDailyChallengeTicket();
                Arcade.Instance.LaunchTrainingGame(dailyChallenge.GameMode, DailyGame.ShipClass.Class, ShipResources, dailyChallenge.Intensity, 1, true);
            }
            else
            {
                Debug.LogError("Attempt to play Daily Challenge without remaining tickets");
            }
        }

        public void ReportScore(int score)
        {
            rewardState.HighScore = Mathf.Max(rewardState.HighScore, score);

            if (rewardState.HighScore >= DailyGame.DailyChallengeTierOneReward.ScoreRequirement)
                rewardState.RewardTierOneSatisfied = true;

            if (rewardState.HighScore >= DailyGame.DailyChallengeTierTwoReward.ScoreRequirement)
                rewardState.RewardTierTwoSatisfied = true;

            if (rewardState.HighScore >= DailyGame.DailyChallengeTierThreeReward.ScoreRequirement)
                rewardState.RewardTierThreeSatisfied = true;

            SaveRewardState();
        }

        public bool ClaimReward(int tier)
        {
            Debug.Log($"ClaimRewardTierOne - dailyGame:{DailyGame}, tier:{tier}");
            switch (tier)
            {
                case 1:
                    if (rewardState is { RewardTierOneSatisfied: true, RewardTierOneClaimed: false })
                    {
                        rewardState.RewardTierOneClaimed = true;
                        DailyRewardHandler.Instance.ClaimDailyChallengeReward(1, DailyGame.DailyChallengeTierOneReward.Value);
                        SaveRewardState();
                        return true;
                    }
                    return false;
                case 2:
                    if (rewardState is { RewardTierTwoSatisfied: true, RewardTierTwoClaimed: false })
                    {
                        rewardState.RewardTierTwoClaimed = true;
                        DailyRewardHandler.Instance.ClaimDailyChallengeReward(2, DailyGame.DailyChallengeTierTwoReward.Value);
                        SaveRewardState();
                        return true;
                    }
                    return false;
                case 3:
                    if (rewardState is { RewardTierThreeSatisfied: true, RewardTierThreeClaimed: false })
                    {
                        rewardState.RewardTierThreeClaimed = true;
                        DailyRewardHandler.Instance.ClaimDailyChallengeReward(3, DailyGame.DailyChallengeTierThreeReward.Value);
                        SaveRewardState();
                        return true;
                    }
                    return false;
            }

            return false;
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

        void LoadRewardState()
        {
            rewardState.RewardTierOneClaimed = PlayerPrefs.GetInt(RewardTierOneClaimedPrefKey) == 1;
            rewardState.RewardTierTwoClaimed = PlayerPrefs.GetInt(RewardTierTwoClaimedPrefKey) == 1;
            rewardState.RewardTierThreeClaimed = PlayerPrefs.GetInt(RewardTierThreeClaimedPrefKey) == 1;
            rewardState.RewardTierOneSatisfied = PlayerPrefs.GetInt(RewardTierOneSatisfiedPrefKey) == 1;
            rewardState.RewardTierTwoSatisfied = PlayerPrefs.GetInt(RewardTierTwoSatisfiedPrefKey) == 1;
            rewardState.RewardTierThreeSatisfied = PlayerPrefs.GetInt(RewardTierThreeSatisfiedPrefKey) == 1;
            rewardState.HighScore = PlayerPrefs.GetInt(HighScorePrefKey);
        }

        void SaveRewardState()
        {
            PlayerPrefs.SetInt(RewardTierOneClaimedPrefKey, RewardState.RewardTierOneClaimed ? 1 : 0);
            PlayerPrefs.SetInt(RewardTierTwoClaimedPrefKey, RewardState.RewardTierTwoClaimed ? 1 : 0);
            PlayerPrefs.SetInt(RewardTierThreeClaimedPrefKey, RewardState.RewardTierThreeClaimed ? 1 : 0);
            PlayerPrefs.SetInt(RewardTierOneSatisfiedPrefKey, RewardState.RewardTierOneSatisfied ? 1 : 0);
            PlayerPrefs.SetInt(RewardTierTwoSatisfiedPrefKey, RewardState.RewardTierTwoSatisfied ? 1 : 0);
            PlayerPrefs.SetInt(RewardTierThreeSatisfiedPrefKey, RewardState.RewardTierThreeSatisfied ? 1 : 0);
            PlayerPrefs.SetInt(HighScorePrefKey, RewardState.HighScore);

            PlayerPrefs.Save();
        }

        void ResetRewardState()
        {
            RewardState = new();
            SaveRewardState();
        }

        void InitializePlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(InitializedDatePrefKey))
                PlayerPrefs.SetString(InitializedDatePrefKey, DateTime.MinValue.Date.ToString("o"));

            if (!PlayerPrefs.HasKey(LastTicketIssuedDatePrefKey))
                PlayerPrefs.SetString(LastTicketIssuedDatePrefKey, DateTime.MinValue.Date.ToString("o"));
            if (!PlayerPrefs.HasKey(TicketBalancePrefKey))
                PlayerPrefs.SetInt(TicketBalancePrefKey, 0);
            if (!PlayerPrefs.HasKey(HighScorePrefKey))
                PlayerPrefs.SetInt(HighScorePrefKey, 0);

            if (!PlayerPrefs.HasKey(RewardTierOneClaimedPrefKey))
                PlayerPrefs.SetInt(RewardTierOneClaimedPrefKey, 0);
            if (!PlayerPrefs.HasKey(RewardTierTwoClaimedPrefKey))
                PlayerPrefs.SetInt(RewardTierTwoClaimedPrefKey, 0);
            if (!PlayerPrefs.HasKey(RewardTierThreeClaimedPrefKey))
                PlayerPrefs.SetInt(RewardTierThreeClaimedPrefKey, 0);

            if (!PlayerPrefs.HasKey(RewardTierOneSatisfiedPrefKey))
                PlayerPrefs.SetInt(RewardTierOneSatisfiedPrefKey, 0);
            if (!PlayerPrefs.HasKey(RewardTierTwoSatisfiedPrefKey))
                PlayerPrefs.SetInt(RewardTierTwoSatisfiedPrefKey, 0);
            if (!PlayerPrefs.HasKey(RewardTierThreeSatisfiedPrefKey))
                PlayerPrefs.SetInt(RewardTierThreeSatisfiedPrefKey, 0);

            PlayerPrefs.Save();
        }
    }
}