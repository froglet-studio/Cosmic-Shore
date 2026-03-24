using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
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

        /// <summary>Fired when an intensity level is newly unlocked for a game mode. Args: (mode, newlyUnlockedIntensity)</summary>
        public event Action<GameModes, int> OnIntensityUnlocked;

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
                gameData.OnMiniGameEnd.OnRaised -= HandleGameEnd;

            var ds = UGSDataService.Instance;
            if (ds != null)
                ds.OnInitialized -= HandleDataServiceReady;
        }

        void Start()
        {
            if (gameData != null)
                gameData.OnMiniGameEnd.OnRaised += HandleGameEnd;

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
        /// Returns true if the Vessel Hangar quest has been reached in the progression chain.
        /// The hangar quest is identified by DisplayName "VESSEL HANGAR" and is unlocked when
        /// every quest before it in the chain is completed.
        /// </summary>
        public bool IsVesselHangarUnlocked()
        {
            if (questList == null) return false;

            int hangarIndex = -1;
            for (int i = 0; i < questList.Quests.Count; i++)
            {
                if (questList.Quests[i] != null && questList.Quests[i].DisplayName == "VESSEL HANGAR")
                {
                    hangarIndex = i;
                    break;
                }
            }

            if (hangarIndex < 0) return false;

            // All quests before the hangar must be completed
            for (int i = 0; i < hangarIndex; i++)
            {
                var quest = questList.Quests[i];
                if (quest == null || quest.IsPlaceholder) continue;
                if (!ProgressionData.IsQuestCompleted(quest.GameMode.ToString()))
                    return false;
            }

            return true;
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

            // Unlock the next mode in the chain and initialize its intensity to 2
            int nextIndex = questIndex + 1;
            if (nextIndex < questList.Quests.Count)
            {
                var nextQuest = questList.Quests[nextIndex];
                string nextModeName = nextQuest.GameMode.ToString();
                ProgressionData.MarkUnlocked(nextModeName);
                ProgressionData.EnsureIntensityInitialized(nextModeName);
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
                ProgressionData.EnsureIntensityInitialized(modeName);
            }
            else
            {
                ProgressionData.UnlockedModes.Remove(modeName);
            }
            OnProgressionChanged?.Invoke(ProgressionData);
        }

        /// <summary>
        /// Debug: Sets the max unlocked intensity for a game mode directly.
        /// Clamped to [2, 4]. Fires OnIntensityUnlocked and saves.
        /// </summary>
        public void DebugSetMaxIntensity(GameModes mode, int maxIntensity)
        {
            maxIntensity = Mathf.Clamp(maxIntensity, 2, 4);
            string modeName = mode.ToString();
            ProgressionData.EnsureIntensityInitialized(modeName);
            ProgressionData.SetMaxUnlockedIntensity(modeName, maxIntensity);
            OnIntensityUnlocked?.Invoke(mode, maxIntensity);
            OnProgressionChanged?.Invoke(ProgressionData);
            ScheduleDebouncedSave();
            CSDebug.Log($"[GameModeProgressionService] Debug: Set {mode} max intensity to {maxIntensity}.");
        }

        // ── Intensity Progression Public API ─────────────────────────────────

        /// <summary>
        /// Returns the highest intensity the player can play for this mode.
        /// Returns 0 if the mode is not unlocked. Returns 2 by default (intensity 1 and 2 available).
        /// </summary>
        public int GetMaxUnlockedIntensity(GameModes mode)
        {
            if (!IsGameModeUnlocked(mode)) return 0;
            return ProgressionData.GetMaxUnlockedIntensity(mode.ToString());
        }

        /// <summary>
        /// Returns true if the given intensity level is unlocked for this mode.
        /// </summary>
        public bool IsIntensityUnlocked(GameModes mode, int intensity)
        {
            return intensity <= GetMaxUnlockedIntensity(mode);
        }

        /// <summary>
        /// Returns how many games the player has completed at the given intensity for this mode.
        /// </summary>
        public int GetIntensityPlayCount(GameModes mode, int intensity)
        {
            return ProgressionData.GetIntensityPlayCount(mode.ToString(), intensity);
        }

        /// <summary>
        /// Returns how many games at the previous intensity are required to unlock the target intensity.
        /// Only meaningful for targetIntensity 3 or 4.
        /// </summary>
        public int GetPlaysRequiredForIntensity(GameModes mode, int targetIntensity)
        {
            var quest = GetQuestForMode(mode);
            if (quest == null) return int.MaxValue;

            return targetIntensity switch
            {
                3 => quest.PlaysToUnlockIntensity3,
                4 => quest.PlaysToUnlockIntensity4,
                _ => 0
            };
        }

        /// <summary>
        /// Returns the number of plays still needed at the previous intensity to unlock the target intensity.
        /// Returns 0 if already unlocked.
        /// </summary>
        public int GetPlaysRemainingForIntensity(GameModes mode, int targetIntensity)
        {
            if (IsIntensityUnlocked(mode, targetIntensity)) return 0;

            int required = GetPlaysRequiredForIntensity(mode, targetIntensity);
            int previousIntensity = targetIntensity - 1;
            int played = GetIntensityPlayCount(mode, previousIntensity);

            return Mathf.Max(0, required - played);
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
                string modeName = quest.GameMode.ToString();
                ProgressionData.MarkUnlocked(modeName);
                ProgressionData.EnsureIntensityInitialized(modeName);
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

            // Intensity-based quests: track play counts and unlock tiers
            if (quest.TargetType == QuestTargetType.IntensityUnlocked)
            {
                int playedIntensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;
                float statValue = ExtractStatForIntensityGoal(quest);
                RecordIntensityPlay(mode, quest, playedIntensity, statValue);
                return;
            }

            // Legacy stat-based quest evaluation
            float legacyStatValue = ExtractStatForQuest(quest);
            CSDebug.Log($"[GameModeProgressionService] HandleGameEnd — mode:{mode}, targetType:{quest.TargetType}, " +
                       $"targetValue:{quest.TargetValue}, extractedStat:{legacyStatValue}");

            if (legacyStatValue > 0f)
                ReportQuestStat(mode, legacyStatValue);
            else
                CSDebug.LogWarning($"[GameModeProgressionService] Extracted stat is 0 for {mode}. " +
                                  $"RoundStatsList count: {gameData.RoundStatsList?.Count ?? 0}, " +
                                  $"LocalPlayer: {gameData.LocalPlayer?.Name ?? "null"}");
        }

        /// <summary>
        /// Records a completed game at the given intensity and checks whether a new intensity tier should unlock.
        /// Uses stat-based checks when IntensityUnlockStatType is configured, otherwise falls back to play counts.
        /// When intensity 4 is unlocked, the quest is marked as completed.
        /// </summary>
        void RecordIntensityPlay(GameModes mode, SO_GameModeQuestData quest, int playedIntensity, float statValue)
        {
            string modeName = mode.ToString();
            ProgressionData.EnsureIntensityInitialized(modeName);

            int newCount = ProgressionData.IncrementIntensityPlayCount(modeName, playedIntensity);
            int maxUnlocked = ProgressionData.GetMaxUnlockedIntensity(modeName);
            bool useStatBased = quest.IntensityUnlockStatType != QuestTargetType.Placeholder;

            CSDebug.Log($"[GameModeProgressionService] RecordIntensityPlay — mode:{mode}, " +
                       $"intensity:{playedIntensity}, playCount:{newCount}, maxUnlocked:{maxUnlocked}, " +
                       $"statBased:{useStatBased}, statValue:{statValue}");

            // Check if playing at intensity 2 should unlock intensity 3
            if (maxUnlocked == 2 && playedIntensity == 2)
            {
                bool shouldUnlock = useStatBased
                    ? EvaluateIntensityStat(quest, statValue, 3)
                    : newCount >= quest.PlaysToUnlockIntensity3;

                if (shouldUnlock)
                {
                    ProgressionData.SetMaxUnlockedIntensity(modeName, 3);
                    CSDebug.Log($"[GameModeProgressionService] Intensity 3 unlocked for {mode}!");
                    OnIntensityUnlocked?.Invoke(mode, 3);
                    OnProgressionChanged?.Invoke(ProgressionData);
                    SaveImmediateAsync();
                    return;
                }
            }

            // Check if playing at intensity 3 should unlock intensity 4 + quest complete
            if (maxUnlocked == 3 && playedIntensity == 3)
            {
                bool shouldUnlock = useStatBased
                    ? EvaluateIntensityStat(quest, statValue, 4)
                    : newCount >= quest.PlaysToUnlockIntensity4;

                if (shouldUnlock)
                {
                    ProgressionData.SetMaxUnlockedIntensity(modeName, 4);
                    CSDebug.Log($"[GameModeProgressionService] Intensity 4 unlocked for {mode}! Quest complete.");
                    OnIntensityUnlocked?.Invoke(mode, 4);

                    // Intensity 4 unlocked = quest completed
                    ProgressionData.MarkQuestCompleted(modeName);
                    quest.IsCompleted = true;
                    OnQuestCompleted?.Invoke(quest);
                    OnProgressionChanged?.Invoke(ProgressionData);
                    SaveImmediateAsync();
                    return;
                }
            }

            // No tier unlock — just save the updated play count
            OnProgressionChanged?.Invoke(ProgressionData);
            ScheduleDebouncedSave();
        }

        /// <summary>
        /// Extracts the relevant stat from the game data for intensity unlock evaluation.
        /// Uses the quest's IntensityUnlockStatType to determine which stat to read.
        /// </summary>
        float ExtractStatForIntensityGoal(SO_GameModeQuestData quest)
        {
            if (quest.IntensityUnlockStatType == QuestTargetType.Placeholder)
                return 0f;

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

            switch (quest.IntensityUnlockStatType)
            {
                case QuestTargetType.CrystalsCollected:
                case QuestTargetType.ScoreAbove:
                case QuestTargetType.SurvivalTime:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.JoustsWon:
                    return localStats?.JoustCollisions ?? 0;

                case QuestTargetType.RaceTimeUnder:
                    float time = localStats?.Score ?? 10000f;
                    return time < 10000f ? time : 0f;

                case QuestTargetType.WinMatch:
                    if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0 &&
                        gameData.RoundStatsList[0].Name == localName)
                        return 1f;
                    return 0f;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Evaluates whether the given stat value meets the intensity unlock target.
        /// </summary>
        bool EvaluateIntensityStat(SO_GameModeQuestData quest, float value, int targetIntensity)
        {
            float target = targetIntensity == 3 ? quest.Intensity3StatTarget : quest.Intensity4StatTarget;

            switch (quest.IntensityUnlockStatType)
            {
                case QuestTargetType.CrystalsCollected:
                case QuestTargetType.ScoreAbove:
                case QuestTargetType.JoustsWon:
                case QuestTargetType.SurvivalTime:
                case QuestTargetType.WinMatch:
                    return value >= target;

                case QuestTargetType.RaceTimeUnder:
                    return value > 0f && value <= target;

                default:
                    return false;
            }
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
                    return localStats?.JoustCollisions ?? 0;

                case QuestTargetType.WinMatch:
                    // Check if the local player is first in the sorted round stats
                    if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0 &&
                        gameData.RoundStatsList[0].Name == localName)
                        return 1f;
                    return 0f;

                case QuestTargetType.SurvivalTime:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.Placeholder:
                case QuestTargetType.IntensityUnlocked:
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

                case QuestTargetType.IntensityUnlocked:
                    // Evaluated via RecordIntensityPlay, not here
                    return ProgressionData.GetMaxUnlockedIntensity(quest.GameMode.ToString()) >= quest.TargetValue;

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
            ProgressionData.EnsureIntensityInitialized(firstMode);
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
