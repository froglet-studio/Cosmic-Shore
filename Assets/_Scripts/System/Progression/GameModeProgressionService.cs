using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
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
        [SerializeField] private SO_UnlockList questList;

        [Header("Progression Config")]
        [Tooltip("Designer-tunable unlock rules (always-unlocked modes, first-free, intensity " +
                 "floor/cap, vessel-hangar quest name). When unset, built-in defaults reproduce " +
                 "the previous hardcoded behavior exactly.")]
        [SerializeField] private SO_ProgressionConfig progressionConfig;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        [Inject] UGSDataService _ugsDataService;
        [Inject] AnalyticsServiceFacade _analytics;

        public GameModeProgressionData ProgressionData { get; private set; } = new();
        public SO_UnlockList QuestList => questList;
        public bool IsInitialized { get; private set; }

        SO_ProgressionConfig _runtimeDefaultConfig;
        /// <summary>
        /// Designer progression config, or a lazily-created default instance whose field values
        /// reproduce the original hardcoded rules so behavior is unchanged when none is wired.
        /// </summary>
        public SO_ProgressionConfig Config
        {
            get
            {
                if (progressionConfig != null) return progressionConfig;
                if (_runtimeDefaultConfig == null)
                    _runtimeDefaultConfig = ScriptableObject.CreateInstance<SO_ProgressionConfig>();
                return _runtimeDefaultConfig;
            }
        }

        /// <summary>Fired when progression data changes (unlock, quest complete, etc.)</summary>
        public event Action<GameModeProgressionData> OnProgressionChanged;

        /// <summary>Fired when a quest is newly completed during gameplay.</summary>
        public event Action<SO_UnlockData> OnQuestCompleted;

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

            if (_ugsDataService != null)
                _ugsDataService.OnInitialized -= HandleDataServiceReady;
        }

        void Start()
        {
            if (gameData != null)
                gameData.OnMiniGameEnd.OnRaised += HandleGameEnd;

            if (_ugsDataService.IsInitialized)
                HandleDataServiceReady();
            else
                _ugsDataService.OnInitialized += HandleDataServiceReady;
        }

        void HandleDataServiceReady()
        {
            _ugsDataService.OnInitialized -= HandleDataServiceReady;

            // Use the repo's data directly — unless the backend gate is closed, in which case
            // progression stays session-local (fresh every launch, ideal for FTUE testing).
            if (ProgressionBackendGate.CloudEnabled && _ugsDataService.ProgressionRepo != null)
                ProgressionData = _ugsDataService.ProgressionRepo.Data;
            else if (!ProgressionBackendGate.CloudEnabled)
                CSDebug.Log("[GameModeProgressionService] ProgressionBackendGate closed — cloud record " +
                            "ignored; progression is session-local and starts fresh each launch.");

            EnsureFirstModeUnlocked();
            SyncSOCompletedFlags();
            IsInitialized = true;
            RaiseProgressionChanged();

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
            // Always-unlocked modes (e.g. Tournament, a session-level meta outside the chain).
            if (Config.IsAlwaysUnlocked(mode))
                return true;

            // First quest mode is free when configured ('the first game is free').
            if (Config.firstQuestAlwaysUnlocked &&
                questList != null && questList.Quests.Count > 0 &&
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
        /// every game-mode quest before it in the chain is done (completed or already claimed).
        /// </summary>
        public bool IsVesselHangarUnlocked()
        {
            if (questList == null) return false;

            string hangarQuestName = Config.vesselHangarQuestDisplayName;
            int hangarIndex = -1;
            for (int i = 0; i < questList.Quests.Count; i++)
            {
                if (questList.Quests[i] != null && questList.Quests[i].DisplayName == hangarQuestName)
                {
                    hangarIndex = i;
                    break;
                }
            }

            if (hangarIndex < 0) return false;

            // Every quest before the hangar must be DONE. Use the persistent done signal
            // (IsUnlockObjectiveDone — a completed quest stays at max intensity) rather than the
            // transient CompletedQuests set: ClaimQuestAndUnlockNext removes a quest from
            // CompletedQuests the moment it is claimed to unlock the next mode, so a conjunction
            // over CompletedQuests can never hold once the player has claimed down the chain.
            for (int i = 0; i < hangarIndex; i++)
            {
                var quest = questList.Quests[i];
                if (quest == null || quest.IsPlaceholder) continue;
                if (!IsUnlockObjectiveDone(quest))
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

            // Mark as claimed (remove from CompletedQuests - it's done)
            ProgressionData.CompletedQuests.Remove(modeName);
            questList.Quests[questIndex].IsCompleted = false;

            // Unlock the next mode in the chain and initialize its intensity to 2
            int nextIndex = questIndex + 1;
            if (nextIndex < questList.Quests.Count)
            {
                var nextQuest = questList.Quests[nextIndex];
                string nextModeName = nextQuest.GameMode.ToString();
                ProgressionData.MarkUnlocked(nextModeName);
                ProgressionData.EnsureIntensityInitialized(nextModeName, Config.defaultMaxIntensity);
                _analytics?.RecordModeUnlocked(nextQuest.GameMode);
                CSDebug.Log($"[GameModeProgressionService] Unlocked next mode: {nextQuest.GameMode}");
            }

            RaiseProgressionChanged();
            SaveImmediateAsync();
        }

        /// <summary>
        /// Unlocks a mode directly (Quest Graph–driven source-of-truth write). Marks it
        /// unlocked, opens the default intensity range, records analytics, refreshes
        /// listeners + breadcrumb, and saves. No-op if already unlocked.
        /// </summary>
        public void UnlockMode(GameModes mode)
        {
            string modeName = mode.ToString();
            if (ProgressionData.IsUnlocked(modeName)) return;

            ProgressionData.MarkUnlocked(modeName);
            ProgressionData.EnsureIntensityInitialized(modeName, Config.defaultMaxIntensity);
            _analytics?.RecordModeUnlocked(mode);
            CSDebug.Log($"[GameModeProgressionService] Quest-graph unlock: {mode}");

            RaiseProgressionChanged();
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
                RaiseProgressionChanged();
                SaveImmediateAsync();
                return;
            }

            RaiseProgressionChanged();
            ScheduleDebouncedSave();
        }

        /// <summary>
        /// Returns the quest data for a given game mode, or null if not found.
        /// </summary>
        public SO_UnlockData GetQuestForMode(GameModes mode)
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
        /// Used by the slider - only advances on claim, not on quest-target completion.
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
            RaiseProgressionChanged();
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
                ProgressionData.EnsureIntensityInitialized(modeName, Config.defaultMaxIntensity);
            }
            else
            {
                ProgressionData.UnlockedModes.Remove(modeName);
            }
            RaiseProgressionChanged();
        }

        /// <summary>
        /// Debug: Sets the max unlocked intensity for a game mode directly.
        /// Clamped to [2, 4]. Fires OnIntensityUnlocked and saves.
        /// </summary>
        public void DebugSetMaxIntensity(GameModes mode, int maxIntensity)
        {
            maxIntensity = Mathf.Clamp(maxIntensity, Config.defaultMaxIntensity, Config.maxIntensity);
            string modeName = mode.ToString();
            ProgressionData.EnsureIntensityInitialized(modeName, Config.defaultMaxIntensity);
            ProgressionData.SetMaxUnlockedIntensity(modeName, maxIntensity);
            OnIntensityUnlocked?.Invoke(mode, maxIntensity);
            RaiseProgressionChanged();
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
            // Full-intensity modes (e.g. Tournament) aren't gated behind progression - the full
            // range is available (one intensity is chosen in the lobby and applied to every game).
            if (Config.HasFullIntensity(mode)) return Config.maxIntensity;

            if (!IsGameModeUnlocked(mode)) return 0;
            return ProgressionData.GetMaxUnlockedIntensity(mode.ToString(), Config.defaultMaxIntensity);
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
            RaiseProgressionChanged();
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
                ProgressionData.EnsureIntensityInitialized(modeName, Config.defaultMaxIntensity);
                quest.IsCompleted = false;
            }

            // Make sure remaining quests have flags cleared
            for (int i = targetIndex; i < questCount; i++)
                questList.Quests[i].IsCompleted = false;

            RaiseProgressionChanged();
            SaveImmediateAsync();
            CSDebug.Log($"[GameModeProgressionService] Progress set to index {targetIndex}/{questCount}.");
        }

        // ── Internal ────────────────────────────────────────────────────────────

        void HandleGameEnd()
        {
            if (gameData == null || gameData.LocalPlayer == null)
            {
                CSDebug.LogWarning("[GameModeProgressionService] HandleGameEnd skipped - gameData or LocalPlayer is null.");
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
            CSDebug.Log($"[GameModeProgressionService] HandleGameEnd - mode:{mode}, targetType:{quest.TargetType}, " +
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
        void RecordIntensityPlay(GameModes mode, SO_UnlockData quest, int playedIntensity, float statValue)
        {
            string modeName = mode.ToString();
            ProgressionData.EnsureIntensityInitialized(modeName, Config.defaultMaxIntensity);

            int newCount = ProgressionData.IncrementIntensityPlayCount(modeName, playedIntensity);
            int maxUnlocked = ProgressionData.GetMaxUnlockedIntensity(modeName, Config.defaultMaxIntensity);
            bool useStatBased = quest.IntensityUnlockStatType != QuestTargetType.Placeholder;

            CSDebug.Log($"[GameModeProgressionService] RecordIntensityPlay - mode:{mode}, " +
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
                    _analytics?.RecordIntensityUnlocked(mode, 3);
                    RaiseProgressionChanged();
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
                    _analytics?.RecordIntensityUnlocked(mode, 4);

                    // Intensity 4 unlocked = quest completed
                    ProgressionData.MarkQuestCompleted(modeName);
                    quest.IsCompleted = true;
                    OnQuestCompleted?.Invoke(quest);
                    RaiseProgressionChanged();
                    SaveImmediateAsync();
                    return;
                }

                // Name the shortfall — "played I3, nothing advanced" must be diagnosable
                // from the console (and the Quest Graph tool surfaces the same goal).
                CSDebug.Log($"[GameModeProgressionService] {mode} intensity-4 goal NOT met this game — " +
                            (useStatBased
                                ? $"{quest.IntensityUnlockStatType} was {statValue}, needs " +
                                  $"{(quest.IntensityUnlockStatType == QuestTargetType.RaceTimeUnder ? "a winning finish ≤" : "≥")} {quest.Intensity4StatTarget}."
                                : $"plays at intensity 3: {newCount}/{quest.PlaysToUnlockIntensity4}."));
            }

            // No tier unlock — just save the updated play count
            RaiseProgressionChanged();
            ScheduleDebouncedSave();
        }

        /// <summary>
        /// Extracts the relevant stat from the game data for intensity unlock evaluation.
        /// Uses the quest's IntensityUnlockStatType to determine which stat to read.
        /// </summary>
        float ExtractStatForIntensityGoal(SO_UnlockData quest)
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
                    // The dedicated crystal counter — NOT Score. Score is mode-defined (finish
                    // time under golf rules, points elsewhere) and silently broke crystal goals.
                    return localStats?.CrystalsCollected ?? 0;

                case QuestTargetType.ScoreAbove:
                case QuestTargetType.SurvivalTime:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.JoustsWon:
                    return localStats?.JoustCollisions ?? 0;

                case QuestTargetType.RaceTimeUnder:
                    float time = localStats?.Score ?? GolfScoreSentinels.DnfThreshold;
                    return GolfScoreSentinels.IsFinishTime(time) ? time : 0f;

                case QuestTargetType.WinMatch:
                    return DidLocalPlayerWin() ? 1f : 0f;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Authoritative local-win check (same semantics as EndGameSequencer's reveal):
        /// domain modes set WinnerDomain server-side; anything else falls back to the
        /// per-domain stats winner. RoundStatsList ORDER is roster order, not rank — never
        /// infer a win from list position.
        /// </summary>
        bool DidLocalPlayerWin()
        {
            if (gameData == null || gameData.LocalPlayer == null) return false;

            if (gameData.WinnerDomain != Domains.Blue)
                return gameData.LocalPlayer.Domain == gameData.WinnerDomain;

            return gameData.IsLocalDomainWinner(out _);
        }

        /// <summary>
        /// Evaluates whether the given stat value meets the intensity unlock target.
        /// </summary>
        bool EvaluateIntensityStat(SO_UnlockData quest, float value, int targetIntensity)
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

        float ExtractStatForQuest(SO_UnlockData quest)
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
                    // The dedicated crystal counter — NOT Score (mode-defined; golf time in races).
                    return localStats?.CrystalsCollected ?? 0;

                case QuestTargetType.ScoreAbove:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.RaceTimeUnder:
                    // For race time, a lower score is better.
                    // A score at/above the DNF threshold means DNF, ignore it.
                    float time = localStats?.Score ?? GolfScoreSentinels.DnfThreshold;
                    return GolfScoreSentinels.IsFinishTime(time) ? time : 0f;

                case QuestTargetType.JoustsWon:
                    return localStats?.JoustCollisions ?? 0;

                case QuestTargetType.WinMatch:
                    return DidLocalPlayerWin() ? 1f : 0f;

                case QuestTargetType.SurvivalTime:
                    return localStats?.Score ?? 0f;

                case QuestTargetType.Placeholder:
                case QuestTargetType.IntensityUnlocked:
                    return 0f;

                default:
                    return 0f;
            }
        }

        bool EvaluateQuestTarget(SO_UnlockData quest, float value)
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
                    return ProgressionData.GetMaxUnlockedIntensity(quest.GameMode.ToString(), Config.defaultMaxIntensity) >= quest.TargetValue;

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
            ProgressionData.EnsureIntensityInitialized(firstMode, Config.defaultMaxIntensity);
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

        // ── Breadcrumb (active-frontier → CallToAction) ───────────────────────
        //
        // THE KEY WIRE: the progression spine is the sole driver of the breadcrumb. Whenever
        // progression changes, it computes the active frontier (the first unlocked, not-yet-
        // completed unlock node) and lights that node's authored Call-to-Action — retracting the
        // previous one. This is the single guidance channel (C2): no other system pushes "go
        // here / do this" hints. Quest completion is the only progression currency (C1).

        CallToAction _activeBreadcrumb;
        bool _breadcrumbSuppressed;

        /// <summary>
        /// While true the service retracts and stops driving the frontier breadcrumb — the
        /// QuestGraphRunner owns guidance for the duration of a running quest and restores
        /// this to false on quest completion (and on its own teardown).
        /// </summary>
        public bool BreadcrumbSuppressed
        {
            get => _breadcrumbSuppressed;
            set
            {
                if (_breadcrumbSuppressed == value) return;
                _breadcrumbSuppressed = value;
                RefreshActiveBreadcrumb();
            }
        }

        /// <summary>
        /// Fires OnProgressionChanged and re-evaluates the active-frontier breadcrumb. This is the
        /// single funnel for every progression mutation, so the breadcrumb can never drift from
        /// the persisted state.
        /// </summary>
        void RaiseProgressionChanged()
        {
            OnProgressionChanged?.Invoke(ProgressionData);
            RefreshActiveBreadcrumb();
        }

        /// <summary>
        /// Lights the breadcrumb for the current frontier unlock and retracts the previous one.
        /// Idempotent — re-lighting the same target is a no-op, and a frontier whose breadcrumb was
        /// dismissed by a user action (e.g. the player played the game) is re-lit if still the frontier.
        /// </summary>
        void RefreshActiveBreadcrumb()
        {
            var cta = CallToActionSystem.Instance;
            if (cta == null) return; // CTA system not alive yet; a later progression change re-lights.

            // A running quest graph owns guidance — retract ours and stand down until released.
            if (_breadcrumbSuppressed)
            {
                if (_activeBreadcrumb != null)
                {
                    cta.RemoveCallToAction(_activeBreadcrumb);
                    _activeBreadcrumb = null;
                }
                return;
            }

            var frontier = GetActiveFrontierUnlock();
            var desiredTarget = frontier != null && frontier.HasBreadcrumb
                ? frontier.CallToActionTargetID
                : CallToActionTargetType.None;

            // Already showing exactly the right breadcrumb (and it's still live)? Nothing to do.
            if (_activeBreadcrumb != null
                && _activeBreadcrumb.CallToActionTargetID == desiredTarget
                && cta.IsCallToActionTargetActive(desiredTarget))
                return;

            // Retract the previous frontier breadcrumb (no-op if a user action already cleared it).
            if (_activeBreadcrumb != null)
            {
                cta.RemoveCallToAction(_activeBreadcrumb);
                _activeBreadcrumb = null;
            }

            // Light the new frontier breadcrumb.
            if (frontier != null && frontier.HasBreadcrumb)
            {
                _activeBreadcrumb = frontier.BuildCallToAction();
                cta.AddCallToAction(_activeBreadcrumb);
            }
        }

        /// <summary>
        /// The actionable frontier: the first unlock node in chain order that the player can reach
        /// but has not yet accomplished, and that carries a breadcrumb. Game-mode nodes resolve to
        /// the mode the player must still finish; feature nodes (e.g. the Vessel Hangar) surface
        /// once they are revealed. Returns null when nothing is pending a player action (a quest is
        /// done but awaiting a claim, or the chain is complete) — the quest track's own claim
        /// affordance covers that in-screen.
        /// </summary>
        SO_UnlockData GetActiveFrontierUnlock()
        {
            if (questList == null) return null;

            foreach (var node in questList.Quests)
            {
                if (node == null || node.IsPlaceholder || !node.HasBreadcrumb) continue;
                if (!IsUnlockReachable(node)) continue;
                if (IsUnlockObjectiveDone(node)) continue;
                return node;
            }

            return null;
        }

        /// <summary>True if the player has progressed far enough to act on this unlock node.</summary>
        bool IsUnlockReachable(SO_UnlockData node)
        {
            switch (node.FeatureKind)
            {
                case FeatureKind.GameMode:
                    return IsGameModeUnlocked(node.GameMode);

                case FeatureKind.Screen:
                    // The Vessel Hangar is revealed through its dedicated gate; any other screen
                    // unlock keys off the persisted record.
                    if (node.DisplayName == Config.vesselHangarQuestDisplayName)
                        return IsVesselHangarUnlocked();
                    return ProgressionData.IsUnlocked(node.UnlockKey);

                default:
                    return ProgressionData.IsUnlocked(node.UnlockKey);
            }
        }

        /// <summary>
        /// True if the player has already accomplished this unlock's objective. Game-mode nodes are
        /// done when their quest is complete or the mode is maxed. Non-mode nodes have no persistent
        /// completion record — their breadcrumb is dismissed transiently when the player performs the
        /// authored CompletionUserAction (e.g. opening the hangar).
        /// </summary>
        bool IsUnlockObjectiveDone(SO_UnlockData node)
        {
            if (node.FeatureKind != FeatureKind.GameMode) return false;

            if (IsQuestCompleted(node.GameMode)) return true;

            // Full-intensity modes (e.g. Maelstrom/Tournament) have no intensity ladder to climb —
            // the raw persisted record stays at the default forever, which made every gate chained
            // AFTER them (the Vessel Hangar) permanently unsatisfiable. Their objective is done
            // once the chain has unlocked them.
            if (Config.HasFullIntensity(node.GameMode))
                return IsGameModeUnlocked(node.GameMode);

            return ProgressionData.GetMaxUnlockedIntensity(node.GameMode.ToString(), Config.defaultMaxIntensity)
                   >= Config.maxIntensity;
        }

        // ── Cloud Save (delegated to UGSDataService.ProgressionRepo) ──

        async void SaveImmediateAsync()
        {
            if (!ProgressionBackendGate.CloudEnabled) return;

            var repo = _ugsDataService?.ProgressionRepo;
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
            if (!ProgressionBackendGate.CloudEnabled) return;
            _ugsDataService?.ProgressionRepo?.MarkDirty();
        }
    }
}
