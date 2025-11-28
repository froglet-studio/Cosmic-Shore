using System;
using CosmicShore.SOAP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Game.UI
{
    public class DuelCellStatsRoundUIController : MonoBehaviour
    {
        [SerializeField] GameDataSO gameData;

        [Header("Own Player Rows")]
        [SerializeField] DuellCellStatsRowUIController ownRound1Row;
        [SerializeField] DuellCellStatsRowUIController ownRound2Row;

        [Header("Opponent Rows")]
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
            gameData.OnWinnerCalculated += OnWinnerCalculated;
            
            Hide();
        }

        void OnDisable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= OnMiniGameTurnStarted;
            gameData.OnMiniGameRoundEnd.OnRaised -= OnMiniGameRoundEnd;
            gameData.OnWinnerCalculated -= OnWinnerCalculated;
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
            // You can keep hiding here if you want it auto-hidden per turn
            Hide();
            TrySubscribeToRoundStats();
        }

        void OnMiniGameRoundEnd()
        {
            // At the end of a round, we snapshot round 1 results
            if (gameData.RoundsPlayed == 1)
                CaptureRound1Snapshots();

            RefreshAllRows();
            Show();
        }

        void OnWinnerCalculated()
        {
            // At end of duel, just ensure UI is visible with final data
            RefreshAllRows();
            Show();
            UnsubscribeFromRoundStats();
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
            if (gameData.Players.Count != 2 || gameData.RoundStatsList.Count != 2)
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

                // RoundsPlayed semantics:
                //  - 0 or 1: we are in Round1 or just finished Round1
                //  - 2: we are in Round2 or just finished Round2
                if (gameData.RoundsPlayed <= 1)
                {
                    // Live / final stats for Round 1
                    rowRound1.Data = BuildRound1Data(stats);
                    rowRound1.UpdateRow();

                    // Round 2 row empty (or keep last, depending on your preference)
                    rowRound2.CleanupUI();
                }
                else // RoundsPlayed >= 2
                {
                    // Row1 shows frozen round1 snapshot (captured at Round1 end)
                    if (round1SnapshotCaptured)
                    {
                        rowRound1.Data = round1Snapshots[i];
                        rowRound1.UpdateRow();
                    }
                    else
                    {
                        // Fallback: if no snapshot, derive from current (shouldn't normally happen)
                        rowRound1.Data = BuildRound1Data(stats);
                        rowRound1.UpdateRow();
                    }

                    // Row2 shows round2-only delta = total - round1Snapshot
                    var baseData = round1SnapshotCaptured ? round1Snapshots[i] : default;
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
