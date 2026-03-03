using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.App.Systems;
using CosmicShore.Core;
using CosmicShore.Models;
using CosmicShore.Services.Auth;
using CosmicShore.Soap;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Progression
{
    /// <summary>
    /// Manages the game-mode quest progression chain.
    /// Loads/saves GameModeProgressionData to UGS Cloud Save.
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

        private const string CLOUD_KEY = UGSKeys.GameModeProgression;
        private const float SAVE_DEBOUNCE_SECONDS = 1.5f;
        private bool _saveDirty;
        private bool _saveInFlight;

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

            // Ensure the first quest's mode is always unlocked
            EnsureFirstModeUnlocked();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (gameData != null)
                gameData.OnMiniGameEnd -= HandleGameEnd;

            var auth = AuthenticationController.Instance;
            if (auth != null)
                auth.OnSignedIn -= HandleSignedIn;
        }

        async void Start()
        {
            try
            {
                if (gameData != null)
                    gameData.OnMiniGameEnd += HandleGameEnd;

                var auth = AuthenticationController.Instance;
                if (auth != null)
                    auth.OnSignedIn += HandleSignedIn;

                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    auth != null && auth.IsSignedIn)
                {
                    await LoadFromCloudAsync();
                }
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[GameModeProgressionService] Start failed: {e.Message}");
            }
        }

        async void HandleSignedIn(string playerId)
        {
            try
            {
                if (!IsInitialized)
                    await LoadFromCloudAsync();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[GameModeProgressionService] HandleSignedIn failed: {e.Message}");
            }
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
                RecordIntensityPlay(mode, quest, playedIntensity);
                return;
            }

            // Legacy stat-based quest evaluation
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

        /// <summary>
        /// Records a completed game at the given intensity and checks whether a new intensity tier should unlock.
        /// When intensity 4 is unlocked, the quest is marked as completed.
        /// </summary>
        void RecordIntensityPlay(GameModes mode, SO_GameModeQuestData quest, int playedIntensity)
        {
            string modeName = mode.ToString();
            ProgressionData.EnsureIntensityInitialized(modeName);

            int newCount = ProgressionData.IncrementIntensityPlayCount(modeName, playedIntensity);
            int maxUnlocked = ProgressionData.GetMaxUnlockedIntensity(modeName);

            CSDebug.Log($"[GameModeProgressionService] RecordIntensityPlay — mode:{mode}, " +
                       $"intensity:{playedIntensity}, playCount:{newCount}, maxUnlocked:{maxUnlocked}");

            // Check if playing at intensity 2 should unlock intensity 3
            if (maxUnlocked == 2 && playedIntensity == 2 && newCount >= quest.PlaysToUnlockIntensity3)
            {
                ProgressionData.SetMaxUnlockedIntensity(modeName, 3);
                CSDebug.Log($"[GameModeProgressionService] Intensity 3 unlocked for {mode}!");
                OnIntensityUnlocked?.Invoke(mode, 3);
                OnProgressionChanged?.Invoke(ProgressionData);
                ScheduleDebouncedSave();
                return;
            }

            // Check if playing at intensity 3 should unlock intensity 4
            if (maxUnlocked == 3 && playedIntensity == 3 && newCount >= quest.PlaysToUnlockIntensity4)
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

            // No tier unlock — just save the updated play count
            OnProgressionChanged?.Invoke(ProgressionData);
            ScheduleDebouncedSave();
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

        // ── Cloud Save ──────────────────────────────────────────────────────────

        async Task LoadFromCloudAsync()
        {
            try
            {
                var keys = new HashSet<string> { CLOUD_KEY };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (result.TryGetValue(CLOUD_KEY, out var item))
                {
                    var cloudData = item.Value.GetAs<GameModeProgressionData>();
                    if (cloudData != null)
                    {
                        ProgressionData = cloudData;
                        ProgressionData.UnlockedModes ??= new List<string>();
                        ProgressionData.CompletedQuests ??= new List<string>();
                        ProgressionData.BestStats ??= new Dictionary<string, float>();
                        ProgressionData.MaxUnlockedIntensity ??= new Dictionary<string, int>();
                        ProgressionData.IntensityPlayCounts ??= new Dictionary<string, int>();
                    }
                }

                EnsureFirstModeUnlocked();
                SyncSOCompletedFlags();
                IsInitialized = true;
                OnProgressionChanged?.Invoke(ProgressionData);

                CSDebug.Log($"[GameModeProgressionService] Loaded from cloud. " +
                          $"Unlocked: {ProgressionData.UnlockedModes.Count}, " +
                          $"Completed: {ProgressionData.CompletedQuests.Count}");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[GameModeProgressionService] Cloud load failed: {e.Message}. Using local data.");
                EnsureFirstModeUnlocked();
                SyncSOCompletedFlags();
                IsInitialized = true;
                OnProgressionChanged?.Invoke(ProgressionData);
            }
        }

        async void SaveImmediateAsync()
        {
            try
            {
                bool canSave = UnityServices.State == ServicesInitializationState.Initialized &&
                               Unity.Services.Authentication.AuthenticationService.Instance != null &&
                               Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;

                if (!canSave)
                {
                    CSDebug.LogWarning("[GameModeProgressionService] Cannot save immediately — not signed in. Queuing debounced save.");
                    ScheduleDebouncedSave();
                    return;
                }

                var data = new Dictionary<string, object> { { CLOUD_KEY, ProgressionData } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
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
            _saveDirty = true;
            if (!_saveInFlight)
                RunDebouncedSave();
        }

        async void RunDebouncedSave()
        {
            if (_saveInFlight) return;
            _saveInFlight = true;

            try
            {
                await Task.Delay((int)(SAVE_DEBOUNCE_SECONDS * 1000));

                while (_saveDirty)
                {
                    _saveDirty = false;

                    bool canSave = UnityServices.State == ServicesInitializationState.Initialized &&
                                   Unity.Services.Authentication.AuthenticationService.Instance != null &&
                                   Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;

                    if (!canSave) break;

                    var data = new Dictionary<string, object> { { CLOUD_KEY, ProgressionData } };
                    await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                }
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[GameModeProgressionService] Save failed: {e.Message}");
            }
            finally
            {
                _saveInFlight = false;
                if (_saveDirty)
                    RunDebouncedSave();
            }
        }
    }
}
