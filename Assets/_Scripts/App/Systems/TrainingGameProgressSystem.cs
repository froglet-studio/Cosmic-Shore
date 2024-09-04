using CosmicShore.Models;
using System.Collections.Generic;

namespace CosmicShore.App.Systems
{
    public  static class TrainingGameProgressSystem
    {
        static bool Initialized;
        const string ProgressSaveFileName = "training_progress.data";
        static Dictionary<MiniGames, TrainingGameProgress> Progress;

        static void LoadProgress()
        {
            Progress = DataAccessor.Load<Dictionary<MiniGames, TrainingGameProgress>>(ProgressSaveFileName);
            Initialized = true;
        }

        public static void SatisfyIntensityTier(MiniGames mode, int intensityTier)
        {

        }

        public static void ClaimIntensityTierReward(MiniGames mode, int intensityTier)
        {

        }


        public static void SaveGameProgress(MiniGames mode, TrainingGameProgress progress)
        {
            if (!Progress.ContainsKey(mode))
                Progress.Add(mode, progress);
            else
                Progress[mode] = progress;

            DataAccessor.Save(ProgressSaveFileName, progress);
        }

        public static TrainingGameProgress GetGameProgress(MiniGames mode)
        {
            if (!Initialized)
                LoadProgress();

            if (Progress.ContainsKey(mode))
                return Progress[mode];

            return new TrainingGameProgress();
        }
    }
}