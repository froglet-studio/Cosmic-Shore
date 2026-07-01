using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Single gate for "has this player finished the FTUE?".
    ///
    /// Cloud is the source of truth (<see cref="UGSDataService.FtueRepo"/>, synced across
    /// devices), with a local <see cref="PlayerPrefs"/> mirror so first-run gating still
    /// works offline and before the cloud load completes. Writes go to both; a read is
    /// "completed" if either says so.
    /// </summary>
    public static class FTUECompletionStore
    {
        const string LocalCompletedKey = "FTUE_Completed";
        const string LocalPhaseKey = "FTUE_Phase";

        static FTUEProgressRepository Repo => UGSDataService.Instance != null
            ? UGSDataService.Instance.FtueRepo
            : null;

        /// <summary>True if the FTUE is already done (cloud or local mirror).</summary>
        public static bool IsCompleted()
        {
            if (PlayerPrefs.GetInt(LocalCompletedKey, 0) == 1)
                return true;

            var repo = Repo;
            return repo != null && repo.IsLoaded && repo.Data != null && repo.Data.Completed;
        }

        /// <summary>Mark the FTUE finished — writes local mirror immediately and flags the cloud repo dirty.</summary>
        public static void MarkCompleted()
        {
            PlayerPrefs.SetInt(LocalCompletedKey, 1);
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo?.Data != null)
            {
                repo.Data.Completed = true;
                repo.MarkDirty();
            }
        }

        /// <summary>Record progress (phase + last node) for resume/analytics.</summary>
        public static void SaveProgress(TutorialPhase phase, string nodeId)
        {
            PlayerPrefs.SetString(LocalPhaseKey, phase.ToString());
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo?.Data != null)
            {
                repo.Data.CurrentPhase = phase.ToString();
                repo.Data.LastNodeId = nodeId;
                repo.MarkDirty();
            }
        }

        /// <summary>Clears the LOCAL mirror only (debug/testing — cloud reset goes through UGSDataService).</summary>
        public static void ResetLocal()
        {
            PlayerPrefs.DeleteKey(LocalCompletedKey);
            PlayerPrefs.DeleteKey(LocalPhaseKey);
            PlayerPrefs.Save();
        }
    }
}
