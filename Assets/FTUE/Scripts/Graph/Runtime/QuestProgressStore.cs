using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Single gate + recorder for Quest Graph player progress.
    ///
    /// Cloud is the source of truth (<see cref="UGSDataService.QuestGraphRepo"/>, synced
    /// across devices) with a <see cref="PlayerPrefs"/> mirror so first-run gating and
    /// resume still work offline and before the cloud load completes. Writes go to both;
    /// a read prefers the loaded cloud record and falls back to the mirror.
    /// </summary>
    public static class QuestProgressStore
    {
        static QuestProgressRepository Repo => UGSDataService.Instance != null
            ? UGSDataService.Instance.QuestGraphRepo
            : null;

        static string CompletedKey(string questId) => $"QUEST_{questId}_Completed";
        static string PhaseKey(string questId) => $"QUEST_{questId}_Phase";
        static string NodeKey(string questId) => $"QUEST_{questId}_Node";

        static QuestProgressRecord LoadedRecord(string questId)
        {
            var repo = Repo;
            if (repo == null || !repo.IsLoaded || repo.Data == null) return null;
            return repo.Data.TryGet(questId);
        }

        /// <summary>True if the quest is already done (cloud record or local mirror).</summary>
        public static bool IsCompleted(string questId)
        {
            if (PlayerPrefs.GetInt(CompletedKey(questId), 0) == 1)
                return true;

            var record = LoadedRecord(questId);
            return record != null && record.Completed;
        }

        /// <summary>Phase index to resume at (cloud preferred, mirror fallback, 0 default).</summary>
        public static int GetPhaseIndex(string questId)
        {
            var record = LoadedRecord(questId);
            if (record != null) return record.CurrentPhaseIndex;
            return PlayerPrefs.GetInt(PhaseKey(questId), 0);
        }

        /// <summary>Node id to resume at within the current phase (may be empty = phase entry).</summary>
        public static string GetCurrentNodeId(string questId)
        {
            var record = LoadedRecord(questId);
            if (record != null) return record.CurrentNodeId;
            return PlayerPrefs.GetString(NodeKey(questId), string.Empty);
        }

        /// <summary>Record a node completion + advance the resume cursor. Debounce-saved to UGS.</summary>
        public static void ReportNodeCompleted(string questId, int phaseIndex, string completedNodeId, string nextNodeId)
        {
            PlayerPrefs.SetInt(PhaseKey(questId), phaseIndex);
            PlayerPrefs.SetString(NodeKey(questId), nextNodeId ?? string.Empty);
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo?.Data == null) return;

            var record = repo.Data.GetOrCreate(questId);
            record.RecordNode(phaseIndex, completedNodeId);
            record.CurrentPhaseIndex = phaseIndex;
            record.CurrentNodeId = nextNodeId ?? string.Empty;
            repo.MarkDirty();
        }

        /// <summary>Record a phase boundary: cursor moves to the next phase's entry.</summary>
        public static void ReportPhaseCompleted(string questId, int completedPhaseIndex)
        {
            PlayerPrefs.SetInt(PhaseKey(questId), completedPhaseIndex + 1);
            PlayerPrefs.SetString(NodeKey(questId), string.Empty);
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo?.Data == null) return;

            var record = repo.Data.GetOrCreate(questId);
            record.CurrentPhaseIndex = completedPhaseIndex + 1;
            record.CurrentNodeId = string.Empty;
            repo.MarkDirty();
        }

        /// <summary>Mark the whole quest finished — local mirror immediately, cloud debounced.</summary>
        public static void MarkQuestCompleted(string questId)
        {
            PlayerPrefs.SetInt(CompletedKey(questId), 1);
            PlayerPrefs.Save();

            var repo = Repo;
            if (repo?.Data == null) return;

            repo.Data.GetOrCreate(questId).Completed = true;
            repo.MarkDirty();
        }

        /// <summary>Clears the LOCAL mirror only.</summary>
        public static void ResetLocal(string questId)
        {
            PlayerPrefs.DeleteKey(CompletedKey(questId));
            PlayerPrefs.DeleteKey(PhaseKey(questId));
            PlayerPrefs.DeleteKey(NodeKey(questId));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Removes this quest's CLOUD record and saves immediately. Requires Play mode with a
        /// signed-in session (the repo must be loaded). Returns true if a record was removed
        /// or the cloud slate was already clean.
        /// </summary>
        public static bool ResetCloud(string questId)
        {
            var repo = Repo;
            if (repo == null || !repo.IsLoaded || repo.Data == null)
                return false;

            repo.Data.Quests?.Remove(questId);
            _ = repo.SaveAsync();
            return true;
        }

        /// <summary>
        /// FULL gameplay-progress wipe for FTUE testing (Play mode, signed in): this quest's
        /// local + cloud record, game-mode progression (unlocks + intensity tiers + play counts),
        /// every vessel unlock (SO state + hangar cloud record), and any active quest-graph
        /// arcade constraints. The Froglet Toolbox reads all of this live, so its toggles show
        /// the locked state immediately — and can still manually re-unlock anything afterwards.
        /// Returns true when the cloud-side reset ran (repos loaded).
        /// </summary>
        public static bool ResetAllGameplayProgress(string questId)
        {
            ResetLocal(questId);
            QuestArcadeConstraints.Clear();

            bool cloud = ResetCloud(questId);

            var progression = GameModeProgressionService.Instance;
            if (progression != null) progression.ResetAllProgress();
            else cloud = false;

            var vesselList = Resources.FindObjectsOfTypeAll<SO_VesselList>();
            VesselUnlockSystem.ResetAllUnlocks(vesselList != null && vesselList.Length > 0 ? vesselList[0] : null);

            // Vessel reset only marks the hangar repo dirty — flush so it lands in the backend now.
            var ds = UGSDataService.Instance;
            if (ds != null) _ = ds.FlushAllAsync();
            else cloud = false;

            return cloud;
        }
    }
}
