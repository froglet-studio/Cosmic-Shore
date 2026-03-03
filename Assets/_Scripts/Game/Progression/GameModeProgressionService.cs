using System;
using CosmicShore.App.Systems.CloudData;
using CosmicShore.Core;
using CosmicShore.Models;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Progression
{
    /// <summary>
    /// Manages the game-mode quest progression chain.
    /// Delegates cloud persistence to UGSDataService.ProgressionRepo.
    /// Evaluates quest completion after each game and exposes
    /// unlock state for the Arcade screen and Quest track UI.
    /// </summary>
    public class GameModeProgressionService : MonoBehaviour
    {
        public static GameModeProgressionService Instance { get; private set; }

        [Header("Quest Data")]
        [SerializeField] private SO_GameModeQuestList questList;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        public GameModeProgressionData ProgressionData { get; private set; } = new();
        public SO_GameModeQuestList QuestList => questList;
        public bool IsInitialized { get; private set; }

        /// <summary>Fired when progression data changes (unlock, quest complete, etc.)</summary>
        public event Action<GameModeProgressionData> OnProgressionChanged;

        /// <summary>Fired when a quest is newly completed during gameplay.</summary>
        public event Action<SO_GameModeQuestData> OnQuestCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            EnsureFirstModeUnlocked();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (gameData != null)
                gameData.OnMiniGameEnd -= HandleGameEnd;

            var ds = UGSDataService.Instance;
            if (ds != null)
                ds.OnInitialized -= HandleDataServiceReady;
        }

        void Start()
        {
            if (gameData != null)
                gameData.OnMiniGameEnd += HandleGameEnd;

            var ds = UGSDataService.Instance;
            if (ds != null)
            {
                if (ds.IsInitialized)
                    HandleDataServiceReady();
                else
                    ds.OnInitialized += HandleDataServiceReady;
            }
        }

        void HandleDataServiceReady()
        {
            var ds = UGSDataService.Instance;
            if (ds != null)
                ds.OnInitialized -= HandleDataServiceReady;

            // Use the repo's data directly
            if (ds?.ProgressionRepo != null)
                ProgressionData = ds.ProgressionRepo.Data;

            EnsureFirstModeUnlocked();
            SyncSOCompletedFlags();
            IsInitialized = true;
            OnProgressionChanged?.Invoke(ProgressionData);

            CSDebug.Log($"[GameModeProgressionService] Initialized from UGSDataService. " +
                       $"Unlocked: {ProgressionData.UnlockedModes.Count}, " +
                       $"Completed: {ProgressionData.CompletedQuests.Count}");
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the given game mode is unlocked for the player.
        /// </summary>
        public bool IsGameModeUnlocked(GameModes mode)
        {
            // First quest mode is always unlocked
            if (questList != null && questList.Quests.Count > 0 &&
                questList.Quests[0].GameMode == mode)
                return true;

            return ProgressionData.IsUnlocked(mode.ToString());
        }

        /// <summary>
        /// Returns true if the given mode is gated behind the quest progression chain.
        /// </summary>
        public bool IsGameModeInQuestChain(GameModes mode)
        {
            if (questList == null) return false;

            foreach (var quest in questList.Quests)
            {
                if (quest.GameMode == mode)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the quest for this game mode has been completed
        /// (target met) but not yet claimed by the player.
        /// </summary>
        public bool IsQuestCompleted(GameModes mode)
        {
            return ProgressionData.IsQuestCompleted(mode.ToString());
        }

        /// <summary>
        /// Called from the Quest UI when the player taps the unlock button
        /// after completing a quest. Unlocks the next game mode in the chain.
        /// </summary>
        public void ClaimQuestAndUnlockNext(GameModes completedMode)
        {
            if (questList == null) return;

            string modeName = completedMode.ToString();

            // Find the completed quest's index
            int questIndex = -1;
            for (int i = 0; i < questList.Quests.Count; i++)
            {
                if (questList.Quests[i].GameMode == completedMode)
                {
                    questIndex = i;
                    break;
                }
            }

            if (questIndex < 0) return;

            // Mark as claimed (remove from CompletedQuests — it's done)
            ProgressionData.CompletedQuests.Remove(modeName);
            questList.Quests[questIndex].IsCompleted = false;

            // Unlock the next mode in the chain
            int nextIndex = questIndex + 1;
            if (nextIndex < questList.Quests.Count)
            {
                var nextQuest = questList.Quests[nextIndex];
                ProgressionData.MarkUnlocked(nextQuest.GameMode.ToString());
                CSDebug.Log($"[GameModeProgressionService] Unlocked next mode: {nextQuest.GameMode}");
            }

            OnProgressionChanged?.Invoke(ProgressionData);
            SaveImmediateAsync();
        }

        /// <summary>
        /// Manually reports a stat for quest evaluation.
        /// Called by game-mode-specific score trackers at game end.
        /// </summary>
        public void ReportQuestStat(GameModes mode, float value)
        {
            string modeName = mode.ToString();

            // Already completed? Skip
            if (ProgressionData.IsQuestCompleted(modeName))
                return;

            ProgressionData.TryUpdateBestStat(modeName, value);

            // Check if this meets the quest target
            var quest = GetQuestForMode(mode);
            if (quest == null || quest.IsPlaceholder) return;

            if (EvaluateQuestTarget(quest, value))
            {
                ProgressionData.MarkQuestCompleted(modeName);
                quest.IsCompleted = true;
                CSDebug.Log($"[GameModeProgressionService] Quest completed for {mode}! stat={value} target={quest.TargetValue}");
                OnQuestCompleted?.Invoke(quest);
                OnProgressionChanged?.Invoke(ProgressionData);
                SaveImmediateAsync();
                return;
            }

            OnProgressionChanged?.Invoke(ProgressionData);
            ScheduleDebouncedSave();
        }

        /// <summary>
        /// Returns the quest data for a given game mode, or null if not found.
        /// </summary>
        public SO_GameModeQuestData GetQuestForMode(GameModes mode)
        {
            if (questList == null) return null;

            foreach (var quest in questList.Quests)
            {
                if (quest.GameMode == mode)
                    return quest;
            }

            return null;
        }

        /// <summary>
        /// Returns how many quests have been claimed (next mode unlocked).
        /// Used by the slider — only advances on claim, not on quest-target completion.
        /// </summary>
        public int GetClaimedQuestCount()
        {
            if (questList == null) return 0;

            int count = 0;
            for (int i = 0; i + 1 < questList.Quests.Count; i++)
            {
                if (ProgressionData.IsUnlocked(questList.Quests[i + 1].GameMode.ToString()))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Forces the OnProgressionChanged event to fire, refreshing all listeners.
        /// Used by editor tools when toggling debug overrides.
        /// </summary>
        public void InvokeProgressionChanged()
        {
            OnProgressionChanged?.Invoke(ProgressionData);
        }

        /// <summary>
        /// Debug toggle for a single game mode. Unlocks or locks it and refreshes UI.
        /// </summary>
        public void DebugSetModeUnlocked(GameModes mode, bool unlocked)
        {
            string modeName = mode.ToString();
            if (unlocked)
            {
                ProgressionData.MarkUnlocked(modeName);
            }
            else
            {
                ProgressionData.UnlockedModes.Remove(modeName);
            }
            OnProgressionChanged?.Invoke(ProgressionData);
        }

        // ── Debug / Editor ────────────────────────────────────────────────────

        /// <summary>
        /// Resets all progression data and re-locks every mode except the first.
        /// </summary>
        public void ResetAllProgress()
        {
            ProgressionData = new GameModeProgressionData();

            // Reset runtime SO flags
            if (questList != null)
                foreach (var quest in questList.Quests)
                    quest.IsCompleted = false;

            EnsureFirstModeUnlocked();
            OnProgressionChanged?.Invoke(ProgressionData);
            SaveImmediateAsync();
            CSDebug.Log("[GameModeProgressionService] All quest progress reset.");
        }

        /// <summary>
        /// Sets progression to a specific quest index (1-based).
        /// Index 1 = only first mode unlocked (fresh state).
        /// Index N = first N modes unlocked, everything after locked.
        /// Clamped to [1, questCount].
        /// </summary>
        public void DebugSetProgressToIndex(int targetIndex)
        {
            if (questList == null || questList.Quests.Count == 0) return;

            int questCount = questList.Quests.Count;
            targetIndex = Mathf.Clamp(targetIndex, 1, questCount);

            // Reset everything first
            ProgressionData = new GameModeProgressionData();

            // Unlock modes 0..targetIndex-1
            for (int i = 0; i < targetIndex; i++)
            {
                var quest = questList.Quests[i];
                ProgressionData.MarkUnlocked(quest.GameMode.ToString());
                quest.IsCompleted = false;
            }

            // Make sure remaining quests have flags cleared
            for (int i = targetIndex; i < questCount; i++)
                questList.Quests[i].IsCompleted = false;

            OnProgressionChanged?.Invoke(ProgressionData);
            SaveImmediateAsync();
            CSDebug.Log($"[GameModeProgressionService] Progress set to index {targetIndex}/{questCount}.");
        }

        // ── Internal ────────────────────────────────────────────────────────────

        void HandleGameEnd()
        {
            if (gameData == null || gameData.LocalPlayer == null)
            {
                CSDebug.LogWarning("[GameModeProgressionService] HandleGameEnd skipped — gameData or LocalPlayer is null.");
                return;
            }

            var mode = gameData.GameMode;
            var quest = GetQuestForMode(mode);
            if (quest == null || quest.IsPlaceholder)
            {
                CSDebug.Log($"[GameModeProgressionService] No quest found for mode {mode}, skipping.");
                return;
            }

            if (ProgressionData.IsQuestCompleted(mode.ToString()))
            {
                CSDebug.Log($"[GameModeProgressionService] Quest for {mode} already completed, skipping.");
                return;
            }

            float statValue = ExtractStatForQuest(quest);
            CSDebug.Log($"[GameModeProgressionService] HandleGameEnd — mode:{mode}, targetType:{quest.TargetType}, " +
                       $"targetValue:{quest.TargetValue}, extractedStat:{statValue}");

            if (statValue > 0f)
                ReportQuestStat(mode, statValue);
            else
                CSDebug.LogWarning($"[GameModeProgressionService] Extracted stat is 0 for {mode}. " +
                                  $"RoundStatsList count: {gameData.RoundStatsList?.Count ?? 0}, " +
                                  $"LocalPlayer: {gameData.LocalPlayer?.Name ?? "null"}");
        }

        float ExtractStatForQuest(SO_GameModeQuestData quest)
        {
            if (gameData.LocalPlayer == null) return 0f;

            var localName = gameData.LocalPlayer.Name;
            IRoundStats localStats = null;
            if (gameData.RoundStatsList != null)
            {
                foreach (var stats in gameData.RoundStatsList)
                {
                    if (stats.Name == localName)
                    {
                        localStats = stats;
                        break;
                    }
                }
            }

            switch (quest.TargetType)
            {
                case QuestTargetType.CrystalsCollected:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.ScoreAbove:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.RaceTimeUnder:
                    // For race time, a lower score is better.
                    // Score of 10000+ means DNF, ignore it.
                    float time = localStats?.Score ?? 10000f;
                    return time < 10000f ? time : 0f;

                case QuestTargetType.JoustsWon:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.WinMatch:
                    // Check if the local player is first in the sorted round stats
                    if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0 &&
                        gameData.RoundStatsList[0].Name == localName)
                        return 1f;
                    return 0f;

                case QuestTargetType.SurvivalTime:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.Placeholder:
                    return 0f;

                default:
                    return 0f;
            }
        }

        bool EvaluateQuestTarget(SO_GameModeQuestData quest, float value)
        {
            switch (quest.TargetType)
            {
                case QuestTargetType.CrystalsCollected:
                case QuestTargetType.ScoreAbove:
                case QuestTargetType.JoustsWon:
                case QuestTargetType.SurvivalTime:
                case QuestTargetType.WinMatch:
                    return value >= quest.TargetValue;

                case QuestTargetType.RaceTimeUnder:
                    // Must be under the target (lower is better)
                    return value > 0f && value <= quest.TargetValue;

                case QuestTargetType.Placeholder:
                    return false;

                default:
                    return false;
            }
        }

        void EnsureFirstModeUnlocked()
        {
            if (questList == null || questList.Quests.Count == 0) return;

            string firstMode = questList.Quests[0].GameMode.ToString();
            ProgressionData.MarkUnlocked(firstMode);
        }

        /// <summary>
        /// Syncs the runtime IsCompleted flag on each quest SO from ProgressionData.
        /// Called after loading from cloud or resetting so the SO flags match persisted state.
        /// </summary>
        void SyncSOCompletedFlags()
        {
            if (questList == null) return;
            foreach (var quest in questList.Quests)
                quest.IsCompleted = ProgressionData.IsQuestCompleted(quest.GameMode.ToString());
        }

        // ── Cloud Save (delegated to UGSDataService.ProgressionRepo) ──

        async void SaveImmediateAsync()
        {
            var repo = UGSDataService.Instance?.ProgressionRepo;
            if (repo == null)
            {
                CSDebug.LogWarning("[GameModeProgressionService] ProgressionRepo not available, cannot save.");
                return;
            }

            try
            {
                await repo.SaveAsync();
                CSDebug.Log("[GameModeProgressionService] Saved progression data immediately.");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[GameModeProgressionService] Immediate save failed: {e.Message}. Queuing debounced save.");
                ScheduleDebouncedSave();
            }
        }

        void ScheduleDebouncedSave()
        {
            UGSDataService.Instance?.ProgressionRepo?.MarkDirty();
        }
    }
}
