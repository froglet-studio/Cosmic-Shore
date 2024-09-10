using CosmicShore.Models;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems
{
    public  static class TrainingGameProgressSystem
    {
        static bool Initialized;
        const string ProgressSaveFileName = "training_progress.data";
        static Dictionary<GameModes, TrainingGameProgress> Progress;

        static void LoadProgress()
        {
            Progress = DataAccessor.Load<Dictionary<GameModes, TrainingGameProgress>>(ProgressSaveFileName);

            Initialized = true;
        }

        static void SaveProgress(GameModes mode, TrainingGameProgress progress)
        {
            Progress[mode] = progress;

            DataAccessor.Save(ProgressSaveFileName, Progress);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="intensityTier"></param>
        /// <param name="score"></param>
        /// <returns>Whether or not current mode and tier were just satisfied</returns>
        public static bool ReportProgress(SO_TrainingGame trainingGame, int intensityTier, int score)
        {
            if (!Progress.ContainsKey(trainingGame.Game.Mode))
            {
                SaveProgress(trainingGame.Game.Mode, new TrainingGameProgress(0, null));
            }

            var gameProgress = Progress[trainingGame.Game.Mode];
            
            if (!gameProgress.Progress[intensityTier-1].Satisfied)
            {
                GameplayReward rewardTier;
                switch (intensityTier)
                {
                    case 1:
                        rewardTier = trainingGame.IntensityOneReward;
                        break;
                    case 2:
                        rewardTier = trainingGame.IntensityTwoReward;
                        break;
                    case 3:
                        rewardTier = trainingGame.IntensityThreeReward;
                        break;
                    case 4:
                    default:
                        rewardTier = trainingGame.IntensityFourReward;
                        break;
                }

                if (score >= rewardTier.ScoreRequirement)
                {
                    SatisfyIntensityTier(trainingGame.Game.Mode, intensityTier);
                    return true;
                }
            }

            return false;
        }

        public static void SatisfyIntensityTier(GameModes mode, int intensityTier)
        {
            var gameProgress = Progress[mode];

            gameProgress.Progress[intensityTier - 1].Satisfied = true;
            gameProgress.CurrentIntensity = Mathf.Min(intensityTier + 1, 4);

            Progress[mode] = gameProgress;

            SaveProgress(mode, Progress[mode]);
        }

        public static void ClaimIntensityTierReward(GameModes mode, int intensityTier)
        {
            var gameProgress = Progress[mode];

            gameProgress.Progress[intensityTier - 1].Claimed = true;

            SaveProgress(mode, Progress[mode]);
        }


        public static TrainingGameProgress GetGameProgress(GameModes mode)
        {
            if (!Initialized)
                LoadProgress();

            if (!Progress.ContainsKey(mode))
            {
                Debug.LogWarning($"GetGameProgress did not contain mode:{mode} ");
                var progress = new TrainingGameProgress(0,null);
                Debug.LogWarning($"GetGameProgress new progress - currentIntensity:{progress.CurrentIntensity}, progress:{progress.Progress} ");
                SaveProgress(mode, progress);
            }

            return Progress[mode];
        }
    }
}