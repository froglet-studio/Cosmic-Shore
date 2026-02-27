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

            // Unlock the next mode in the chain
            int nextIndex = questIndex + 1;
            if (nextIndex < questList.Quests.Count)
            {
                var nextQuest = questList.Quests[nextIndex];
                ProgressionData.MarkUnlocked(nextQuest.GameMode.ToString());
                CSDebug.Log($"[GameModeProgressionService] Unlocked next mode: {nextQuest.GameMode}");
            }

            OnProgressionChanged?.Invoke(ProgressionData);
            ScheduleDebouncedSave();
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
                CSDebug.Log($"[GameModeProgressionService] Quest completed for {mode}!");
                OnQuestCompleted?.Invoke(quest);
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
        /// Returns how many quests the player has completed (for slider normalization).
        /// </summary>
        public int GetCompletedQuestCount()
        {
            if (questList == null) return 0;

            int count = 0;
            for (int i = 0; i < questList.Quests.Count; i++)
            {
                var quest = questList.Quests[i];
                string modeName = quest.GameMode.ToString();

                // A mode is "past" if it's unlocked AND either:
                //  - the next mode is also unlocked, OR
                //  - its quest is completed
                if (ProgressionData.IsUnlocked(modeName) || i == 0)
                {
                    if (ProgressionData.IsQuestCompleted(modeName))
                        count++;
                    else if (i + 1 < questList.Quests.Count &&
                             ProgressionData.IsUnlocked(questList.Quests[i + 1].GameMode.ToString()))
                        count++;
                }
            }

            return count;
        }

        // ── Internal ────────────────────────────────────────────────────────────

        void HandleGameEnd()
        {
            if (gameData == null || gameData.LocalPlayer == null) return;

            var mode = gameData.GameMode;
            var quest = GetQuestForMode(mode);
            if (quest == null || quest.IsPlaceholder) return;

            // Already completed this quest? Skip
            if (ProgressionData.IsQuestCompleted(mode.ToString())) return;

            // Extract the relevant stat based on the quest's target type
            float statValue = ExtractStatForQuest(quest);
            if (statValue > 0f)
                ReportQuestStat(mode, statValue);
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
                    }
                }

                EnsureFirstModeUnlocked();
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
                IsInitialized = true;
                OnProgressionChanged?.Invoke(ProgressionData);
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
