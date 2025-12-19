using System;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Game.UI
{
    public class DuelCellStatsRoundUIController : MonoBehaviour
    {
        [SerializeField] GameDataSO gameData;

        [Header("Own Player Rows (Local User)")]
        [SerializeField] DuellCellStatsRowUIController ownRound1Row;
        [SerializeField] DuellCellStatsRowUIController ownRound2Row;

        [Header("Opponent Rows (Other User)")]
        [SerializeField] DuellCellStatsRowUIController opponentRound1Row;
        [SerializeField] DuellCellStatsRowUIController opponentRound2Row;

        [SerializeField] CanvasGroup canvasGroup;
        private bool isVisible;

        // Cached references to IRoundStats for both players
        private IRoundStats[] roundStats = new IRoundStats[2];

        // Snapshots of ROUND 1 results (per player), used to compute ROUND 2 deltas
        private StatsRowData[] round1Snapshots = new StatsRowData[2];
        private bool round1SnapshotCaptured;

        void OnEnable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += OnMiniGameTurnStarted;
            gameData.OnMiniGameRoundEnd.OnRaised += OnMiniGameRoundEnd;

            ResetStateAndUI();   // full cleanup when this UI is enabled
        }

        void OnDisable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= OnMiniGameTurnStarted;
            gameData.OnMiniGameRoundEnd.OnRaised -= OnMiniGameRoundEnd;

            UnsubscribeFromRoundStats();
        }

        private void Update()
        {
            HandleInput();
        }

        void HandleInput()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return;

            if (!gamepad.dpad.up.wasPressedThisFrame) return;

            isVisible = !isVisible;
            if (isVisible) Show();
            else Hide();
        }

        // ─────────────────────────────────────────
        // ROUND FLOW
        // ─────────────────────────────────────────

        void OnMiniGameTurnStarted()
        {
            // New duel starting? Do a FULL cleanup once, at the very beginning.
            if (gameData.RoundsPlayed == 0)
            {
                ResetStateAndUI();
            }

            TrySubscribeToRoundStats();
            RefreshAllRows();   // make sure we always draw with latest data at turn start
        }

        void OnMiniGameRoundEnd()
        {
            // At the end of the FIRST round, snapshot round 1 results.
            // NOTE: we do NOT clear any rows here; we just capture data.
            if (gameData.RoundsPlayed == 1)
                CaptureRound1Snapshots();

            RefreshAllRows();
            Show(); // make sure it's visible at round end
        }

        // ─────────────────────────────────────────
        // STATE RESET
        // ─────────────────────────────────────────

        void ResetStateAndUI()
        {
            // logical state
            round1SnapshotCaptured = false;

            for (int i = 0; i < round1Snapshots.Length; i++)
                round1Snapshots[i] = default;

            UnsubscribeFromRoundStats();

            // UI rows
            if (ownRound1Row) ownRound1Row.CleanupUI();
            if (ownRound2Row) ownRound2Row.CleanupUI();
            if (opponentRound1Row) opponentRound1Row.CleanupUI();
            if (opponentRound2Row) opponentRound2Row.CleanupUI();

            Hide();     // scoreboard starts hidden; player can toggle it
        }

        // ─────────────────────────────────────────
        // ROUND STATS SUBSCRIPTION
        // ─────────────────────────────────────────

        void TrySubscribeToRoundStats()
        {
            UnsubscribeFromRoundStats();

            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count != 2)
            {
                Debug.LogWarning("DuelCellStatsRoundUIController: RoundStatsList not ready or not exactly 2 players.");
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var stats = gameData.RoundStatsList[i];
                if (stats == null)
                {
                    Debug.LogWarning($"DuelCellStatsRoundUIController: RoundStatsList[{i}] is null.");
                    continue;
                }

                roundStats[i] = stats;
                roundStats[i].OnAnyStatChanged += OnAnyStatChanged;
            }
        }

        void UnsubscribeFromRoundStats()
        {
            for (int i = 0; i < roundStats.Length; i++)
            {
                if (roundStats[i] != null)
                    roundStats[i].OnAnyStatChanged -= OnAnyStatChanged;

                roundStats[i] = null;
            }
        }

        void OnAnyStatChanged(IRoundStats stats)
        {
            // Any stat change -> recompute all rows.
            RefreshAllRows();
        }

        // ─────────────────────────────────────────
        // SNAPSHOTS
        // ─────────────────────────────────────────

        void CaptureRound1Snapshots()
        {
            if (gameData.Players.Count != 2 || gameData.RoundStatsList.Count != 2)
            {
                Debug.LogError("DuelCellStatsRoundUIController: Cannot snapshot Round 1. Players/RoundStatsList != 2.");
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var stats = gameData.RoundStatsList[i];
                round1Snapshots[i] = BuildRound1Data(stats);
            }

            round1SnapshotCaptured = true;
        }

        // ─────────────────────────────────────────
        // MAIN UI UPDATE
        // ─────────────────────────────────────────

        void RefreshAllRows()
        {
            if (gameData.Players == null ||
                gameData.RoundStatsList == null ||
                gameData.Players.Count != 2 ||
                gameData.RoundStatsList.Count != 2)
            {
                // In case called too early
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                bool isLocal = gameData.Players[i].IsLocalUser;
                IRoundStats stats = gameData.RoundStatsList[i];

                var rowRound1 = isLocal ? ownRound1Row : opponentRound1Row;
                var rowRound2 = isLocal ? ownRound2Row : opponentRound2Row;

                if (!rowRound1 || !rowRound2)
                {
                    Debug.LogError("DuelCellStatsRoundUIController: Row references missing.");
                    continue;
                }

                if (!round1SnapshotCaptured)
                {
                    // We are in ROUND 1 (or between start and end of round 1):
                    //  - Round 1 row shows live data
                    //  - Round 2 row stays empty
                    rowRound1.Data = BuildRound1Data(stats);
                    rowRound1.UpdateRow();

                    rowRound2.CleanupUI();
                }
                else
                {
                    // ROUND 2 (or after round 1 is finished):
                    //  - Round 1 row shows frozen snapshot from end of round 1
                    //  - Round 2 row shows "round-2 only" delta = current cumulative – round1 snapshot
                    var baseData = round1Snapshots[i];

                    rowRound1.Data = baseData;
                    rowRound1.UpdateRow();

                    rowRound2.Data = BuildRound2DeltaData(stats, baseData);
                    rowRound2.UpdateRow();
                }
            }
        }

        // ─────────────────────────────────────────
        // HELPERS TO BUILD ROW DATA
        // ─────────────────────────────────────────

        // Round1 = direct mapping from stats
        StatsRowData BuildRound1Data(IRoundStats stats)
        {
            return new StatsRowData
            {
                PlayerName = stats.Name,

                // PRISMS
                BlocksCreated = stats.BlocksCreated,
                BlocksDestroyed = stats.BlocksDestroyed,
                HostilePrismsDestroyed = stats.HostilePrismsDestroyed,
                FriendlyPrismsDestroyed = stats.FriendlyPrismsDestroyed,
                PrismsRemaining = stats.PrismsRemaining,

                // VOLUMES
                VolumeCreated = stats.VolumeCreated,
                TotalVolumeDestroyed = stats.TotalVolumeDestroyed,
                HostileVolumeDestroyed = stats.HostileVolumeDestroyed,
                FriendlyVolumeDestroyed = stats.FriendlyVolumeDestroyed,
                VolumeRestored = stats.VolumeRestored,
                VolumeStolen = stats.VolumeStolen,
                VolumeRemaining = stats.VolumeRemaining,

                // SCORE
                Score = stats.Score
            };
        }

        // Round2 = current cumulative - round1 snapshot
        StatsRowData BuildRound2DeltaData(IRoundStats stats, StatsRowData round1Snapshot)
        {
            return new StatsRowData
            {
                PlayerName = stats.Name,

                // PRISMS (delta)
                BlocksCreated = stats.BlocksCreated - round1Snapshot.BlocksCreated,
                BlocksDestroyed = stats.BlocksDestroyed - round1Snapshot.BlocksDestroyed,
                HostilePrismsDestroyed = stats.HostilePrismsDestroyed - round1Snapshot.HostilePrismsDestroyed,
                FriendlyPrismsDestroyed = stats.FriendlyPrismsDestroyed - round1Snapshot.FriendlyPrismsDestroyed,
                PrismsRemaining = stats.PrismsRemaining - round1Snapshot.PrismsRemaining,

                // VOLUMES (delta)
                VolumeCreated = stats.VolumeCreated - round1Snapshot.VolumeCreated,
                TotalVolumeDestroyed = stats.TotalVolumeDestroyed - round1Snapshot.TotalVolumeDestroyed,
                HostileVolumeDestroyed = stats.HostileVolumeDestroyed - round1Snapshot.HostileVolumeDestroyed,
                FriendlyVolumeDestroyed = stats.FriendlyVolumeDestroyed - round1Snapshot.FriendlyVolumeDestroyed,
                VolumeRestored = stats.VolumeRestored - round1Snapshot.VolumeRestored,
                VolumeStolen = stats.VolumeStolen - round1Snapshot.VolumeStolen,
                VolumeRemaining = stats.VolumeRemaining - round1Snapshot.VolumeRemaining,

                // SCORE (delta)
                Score = stats.Score - round1Snapshot.Score
            };
        }

        // ─────────────────────────────────────────
        // UI VISIBILITY
        // ─────────────────────────────────────────

        void Show()
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        void Hide()
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // ─────────────────────────────────────────
        // DATA STRUCT
        // ─────────────────────────────────────────

        public struct StatsRowData
        {
            public string PlayerName;

            // PRISM COUNTS
            public int BlocksCreated;
            public int BlocksDestroyed;
            public int HostilePrismsDestroyed;
            public int FriendlyPrismsDestroyed;
            public int PrismsRemaining;

            // VOLUME COUNTS
            public float VolumeCreated;
            public float TotalVolumeDestroyed;
            public float HostileVolumeDestroyed;
            public float FriendlyVolumeDestroyed;
            public float VolumeRestored;
            public float VolumeStolen;
            public float VolumeRemaining;

            // SCORE
            public float Score;
        }
    }
}
