using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// SOAP-style runtime state container for tournament mode.
    /// Holds current round index, cumulative results, and player standings.
    /// Single writer: <see cref="Core.TournamentManager"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TournamentData",
        menuName = "ScriptableObjects/Data Containers/TournamentData")]
    public class TournamentDataSO : ScriptableObject
    {
        [Header("State")]
        [Tooltip("The tournament definition currently being played.")]
        public SO_Tournament ActiveTournament;

        [Tooltip("Zero-based index of the current round.")]
        public int CurrentRoundIndex;

        [Tooltip("Whether a tournament session is currently in progress.")]
        public bool IsTournamentActive;

        [Header("Results")]
        [Tooltip("Completed round results, one per finished round.")]
        public List<TournamentRoundResult> CompletedRounds = new();

        [Header("Player Standings")]
        [Tooltip("Cumulative tournament standings sorted by total points (descending).")]
        public List<TournamentStanding> Standings = new();

        public SO_ArcadeGame CurrentRound =>
            ActiveTournament != null && CurrentRoundIndex < ActiveTournament.Rounds.Count
                ? ActiveTournament.Rounds[CurrentRoundIndex]
                : null;

        public bool IsLastRound =>
            ActiveTournament != null && CurrentRoundIndex >= ActiveTournament.Rounds.Count - 1;

        public int TotalRounds => ActiveTournament?.Rounds?.Count ?? 0;

        public void Reset()
        {
            ActiveTournament = null;
            CurrentRoundIndex = 0;
            IsTournamentActive = false;
            CompletedRounds.Clear();
            Standings.Clear();
        }
    }
}
