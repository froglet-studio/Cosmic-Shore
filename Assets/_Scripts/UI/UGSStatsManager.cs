using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using Unity.Services.Leaderboards;
using UnityEngine;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Core
{
    /// <summary>
    /// Domain service for game stats and vessel telemetry.
    /// Delegates all cloud persistence to UGSDataService.ModeStatsRepo and HangarRepo.
    /// Keeps leaderboard submission, analytics, and stat evaluation logic here.
    ///
    /// Per-mode results all land in one uniform <see cref="ModeRecord"/> now - see
    /// Docs/Analytics/DATA_ARCHITECTURE.md §3.3. Adding a mode means calling
    /// <see cref="ReportModeResult"/> and adding one row to <see cref="LowerIsBetter"/>.
    /// </summary>
    public class UGSStatsManager : MonoBehaviour
    {
        public static UGSStatsManager Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] LeaderboardConfigSO leaderboardConfig;

        [Inject] UGSDataService _ugsDataService;
        [Inject] AnalyticsServiceFacade _analytics;

        ModeStatsCloudData _modeStats = new();
        HangarCloudData _hangar = new();
        bool _isReady;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_ugsDataService.IsInitialized)
                HandleDataServiceReady();
            else
                _ugsDataService.OnInitialized += HandleDataServiceReady;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_ugsDataService != null)
                _ugsDataService.OnInitialized -= HandleDataServiceReady;
        }

        void HandleDataServiceReady()
        {
            _ugsDataService.OnInitialized -= HandleDataServiceReady;

            // Use the repo's data directly - no separate cloud load needed
            if (_ugsDataService.ModeStatsRepo != null)
                _modeStats = _ugsDataService.ModeStatsRepo.Data;
            if (_ugsDataService.HangarRepo != null)
                _hangar = _ugsDataService.HangarRepo.Data;

            _isReady = true;
            CSDebug.Log("[UGSStats] Initialized from UGSDataService repositories.");
        }

        #region Scoring direction

        /// <summary>
        /// Whether a lower score is better for this mode (golf rules).
        ///
        /// This mirrors each controller's <c>MiniGameControllerBase.UseGolfRules</c> override.
        /// It is not read from <c>LeaderboardConfigSO</c> (no direction field) nor from the
        /// controller (not reachable at report time, and reporters run outside the controller's
        /// lifetime). Consolidating the direction here is not new duplication - it is exactly
        /// the branching the old GetEvaluatedHighScore already hardcoded - but the real fix is
        /// to publish <c>SO_Game.GolfScoring</c> onto GameDataSO and have both read that.
        /// Tracked in Docs/Analytics/DATA_ARCHITECTURE.md §10.
        /// </summary>
        static bool LowerIsBetter(GameModes mode) => mode switch
        {
            GameModes.HexRace => true,
            GameModes.MultiplayerJoust => true,
            GameModes.MultiplayerCrystalCapture => true,
            _ => false
        };

        #endregion

        #region Public API - Smart High Score Evaluation

        public float GetEvaluatedHighScore(GameModes mode, int intensity, float currentSessionScore)
        {
            if (!_isReady) return currentSessionScore;
            if (!_modeStats.TryGet(mode, intensity, out var record) || !record.HasScore)
                return currentSessionScore;

            if (!LowerIsBetter(mode))
                return Mathf.Max(record.BestScore, currentSessionScore);

            // Golf: a DNF/loser sentinel is not a real result, so the stored best stands.
            return currentSessionScore >= GolfScoreSentinels.DnfThreshold
                ? record.BestScore
                : Mathf.Min(record.BestScore, currentSessionScore);
        }

        #endregion

        #region Public API - Reporting

        /// <summary>
        /// One funnel for every mode result: updates the uniform mode record, submits to the
        /// leaderboard when the score is real, and marks the repository dirty.
        /// </summary>
        /// <param name="isRealResult">
        /// False for a DNF/loser sentinel. Play is still counted; the best score and the
        /// leaderboard submit are skipped.
        /// </param>
        /// <param name="won">
        /// Whether this run was a win. Modes with no win condition (single-player score
        /// attacks) pass false - <see cref="ModeRecord.GamesWon"/> stays 0 there by design.
        /// </param>
        public void ReportModeResult(GameModes mode, int intensity, float score, bool isRealResult, bool won)
        {
            if (!_isReady) return;

            var record = _modeStats.GetOrCreate(mode, intensity);
            record.GamesPlayed++;
            record.LastPlayedUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            record.FlightTimeSeconds += FlightClock.LastGameSeconds;

            if (won)
                record.GamesWon++;

            if (isRealResult)
            {
                record.TryUpdateBest(score, LowerIsBetter(mode));
                SubmitScoreInternal(mode, intensity, score);
            }

            SaveModeStats();
        }


        public void ReportHexRaceStats(GameModes mode, int intensity, int clean, float drift, int jousts, float score)
        {
            bool finished = GolfScoreSentinels.IsFinishTime(score);
            ReportModeResult(mode, intensity, score, isRealResult: finished, won: finished);
        }

        public void ReportJoustStats(GameModes mode, int intensity, int joustsWon, float raceTime)
        {
            bool finished = GolfScoreSentinels.IsFinishTime(raceTime);
            ReportModeResult(mode, intensity, raceTime, isRealResult: finished, won: finished);
        }

        public void ReportCrystalCaptureStats(GameModes mode, int intensity, int crystals)
        {
            // CrystalCaptureStatsReporter is winner-only, so a real finish time is a win.
            bool finished = GolfScoreSentinels.IsFinishTime(crystals);
            ReportModeResult(mode, intensity, crystals, isRealResult: finished, won: finished);
        }

        /// <summary>
        /// Reports per-vessel telemetry at game end. Lands in HANGAR_DATA alongside that
        /// vessel's unlock state - one record per vessel, no cross-key correlation.
        /// </summary>
        public void ReportVesselTelemetry(VesselTelemetry telemetry, string vesselTypeName)
        {
            if (!_isReady || telemetry == null || string.IsNullOrWhiteSpace(vesselTypeName))
            {
                Debug.LogWarning($"[UGSStats] ReportVesselTelemetry skipped - " +
                    $"ready={_isReady}, telemetry={(telemetry != null ? telemetry.GetType().Name : "NULL")}, " +
                    $"vessel='{vesselTypeName}'");
                return;
            }

            Debug.Log($"[UGSStats] ReportVesselTelemetry - {telemetry.GetType().Name} for '{vesselTypeName}', " +
                $"drift={telemetry.MaxDriftTime:F2}s, boost={telemetry.MaxBoostTime:F2}s, " +
                $"prismsDmg={telemetry.PrismsDamaged}");

            var record = _hangar.GetOrCreate(vesselTypeName);
            if (record == null) return; // blank vessel name - authored-data bug, do not persist it

            record.GamesPlayed++;
            record.LastUsedUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Drives HangarCloudData.PreferredVessel ("most hours played").
            record.FlightTimeSeconds += FlightClock.LastGameSeconds;

            // Common stats - keep best values
            if (telemetry.MaxDriftTime > record.BestDriftTimeSeconds)
                record.BestDriftTimeSeconds = telemetry.MaxDriftTime;
            if (telemetry.MaxBoostTime > record.BestBoostTimeSeconds)
                record.BestBoostTimeSeconds = telemetry.MaxBoostTime;
            record.TotalPrismsDamaged += telemetry.PrismsDamaged;

            // Vessel-specific stats
            switch (telemetry)
            {
                case SparrowVesselTelemetry sparrow:
                    Debug.Log($"[UGSStats] Sparrow stats - prismBlocks={sparrow.PrismBlocksShot}, " +
                        $"skyburst={sparrow.SkyburstMissilesShot}, dangerBlocks={sparrow.DangerBlocksSpawned}");
                    record.IncrementCounter("PrismBlocksShot", sparrow.PrismBlocksShot);
                    record.IncrementCounter("SkyburstMissilesShot", sparrow.SkyburstMissilesShot);
                    record.IncrementCounter("DangerBlocksSpawned", sparrow.DangerBlocksSpawned);
                    break;
                case SquirrelVesselTelemetry squirrel:
                    Debug.Log($"[UGSStats] Squirrel stats - jousts={squirrel.JoustsWon}, " +
                        $"stolen={squirrel.PrismsStolen}, cleanStreak={squirrel.MaxCleanStreak}");
                    record.IncrementCounter("JoustsWon", squirrel.JoustsWon);
                    record.IncrementCounter("PrismsStolen", squirrel.PrismsStolen);
                    record.RaiseCounterToBest("BestCleanStreak", squirrel.MaxCleanStreak);
                    break;
                default:
                    Debug.LogWarning($"[UGSStats] No vessel-specific handler for {telemetry.GetType().Name}");
                    break;
            }

            _hangar.RecomputePreferredVessel();
            SaveHangar();
        }

        /// <summary>
        /// Records the vessel the player deliberately selected. Previously nothing wrote
        /// HANGAR_DATA.SelectedVessel, which is why it was always empty.
        /// </summary>
        public void ReportVesselSelected(string vesselTypeName)
        {
            if (!_isReady || string.IsNullOrWhiteSpace(vesselTypeName)) return;
            if (_hangar.SelectedVessel == vesselTypeName) return;

            _hangar.SelectedVessel = vesselTypeName;
            SaveHangar();
        }

        /// <summary>
        /// Banks one freestyle segment measured in Menu_Main. Menu flight has no game and no
        /// turn, so it never reaches <see cref="ReportModeResult"/> or
        /// <see cref="ReportVesselTelemetry"/> - but it is still the player at the stick, so it
        /// counts toward the lifetime total and toward this vessel's "most hours played"
        /// (<see cref="HangarCloudData.PreferredVessel"/>). It deliberately does NOT touch
        /// GamesPlayed or GamesCompleted: no game was played.
        /// </summary>
        public void ReportFreestyleFlight(float seconds, string vesselTypeName)
        {
            if (!_isReady || seconds <= 0f) return;

            PlayerDataService.Instance?.RecordFlightTime(seconds);

            if (string.IsNullOrWhiteSpace(vesselTypeName)) return;

            var record = _hangar.GetOrCreate(vesselTypeName);
            if (record == null) return; // blank vessel name - authored-data bug, do not persist it

            record.FlightTimeSeconds += seconds;
            record.LastUsedUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _hangar.RecomputePreferredVessel();
            SaveHangar();

            // Menu flight raises no game event, so without this the new totals would not reach
            // PostHog until the next pause. Re-identifying is how menu time becomes visible
            // there - it needs no new event name and no new parameter in the UGS Event Manager.
            _analytics?.IdentifyPlayer();
        }

        #endregion

        #region Internal

        public void TrackPlayAgain() => _analytics.RecordPlayAgain();

        async void SubmitScoreInternal(GameModes mode, int intensity, double score)
        {
            try
            {
                string id = leaderboardConfig.GetLeaderboardId(mode, intensity);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[UGSStats] No leaderboard mapping for {mode} intensity {intensity}");
                    return;
                }

                await LeaderboardsService.Instance.AddPlayerScoreAsync(id, score);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UGSStats] Leaderboard submit failed for {mode}/{intensity}: {ex.Message}");
            }
        }

        void SaveModeStats() => _ugsDataService?.ModeStatsRepo?.MarkDirty();

        void SaveHangar() => _ugsDataService?.HangarRepo?.MarkDirty();

        #endregion
    }
}
