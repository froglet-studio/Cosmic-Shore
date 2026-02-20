using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Models.Enums;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Tournament
{
    /// <summary>
    /// Persistent singleton that orchestrates a tournament of 5 random games.
    /// Games are randomly chosen from Crystal Capture, Hex Race, and Joust.
    /// Intensity for each game is randomly chosen at or below the selected max.
    /// The scoreboard tracks game wins per domain; the domain with the most wins
    /// after 5 games is the tournament winner.
    /// </summary>
    public class TournamentManager : SingletonPersistent<TournamentManager>
    {
        [SerializeField] GameDataSO gameData;

        public const int TotalGames = 5;

        static readonly GameModes[] TournamentGamePool =
        {
            GameModes.MultiplayerCrystalCapture,
            GameModes.HexRace,
            GameModes.MultiplayerJoust,
        };

        // Tournament state
        public bool IsTournamentActive { get; private set; }
        public int CurrentGameIndex { get; private set; }
        public int MaxIntensity { get; private set; }
        public GameModes[] ScheduledGames { get; private set; }
        public int[] ScheduledIntensities { get; private set; }
        public Dictionary<Domains, int> DomainWins { get; private set; } = new();
        public List<TournamentRoundResult> RoundResults { get; private set; } = new();

        // Events for UI
        public event Action OnTournamentStarted;
        public event Action<TournamentRoundResult> OnRoundComplete;
        public event Action<TournamentFinalResult> OnTournamentComplete;

        bool _waitingForGameEnd;

        void OnEnable()
        {
            if (gameData != null)
                gameData.OnMiniGameEnd += HandleMiniGameEnd;
        }

        void OnDisable()
        {
            if (gameData != null)
                gameData.OnMiniGameEnd -= HandleMiniGameEnd;
        }

        /// <summary>
        /// Start a new tournament. Call this from the Arcade manager.
        /// </summary>
        public void StartTournament(int maxIntensity, GameDataSO data)
        {
            if (data != null)
            {
                // Re-subscribe if gameData changes
                if (gameData != null)
                    gameData.OnMiniGameEnd -= HandleMiniGameEnd;

                gameData = data;
                gameData.OnMiniGameEnd += HandleMiniGameEnd;
            }

            MaxIntensity = Mathf.Max(1, maxIntensity);
            CurrentGameIndex = 0;
            DomainWins = new Dictionary<Domains, int>();
            RoundResults = new List<TournamentRoundResult>();
            IsTournamentActive = true;

            // Generate the 5-game schedule
            ScheduledGames = new GameModes[TotalGames];
            ScheduledIntensities = new int[TotalGames];

            for (int i = 0; i < TotalGames; i++)
            {
                ScheduledGames[i] = TournamentGamePool[UnityEngine.Random.Range(0, TournamentGamePool.Length)];
                ScheduledIntensities[i] = UnityEngine.Random.Range(1, MaxIntensity + 1);
            }

            Debug.Log($"[Tournament] Started! Schedule: {FormatSchedule()}");
            OnTournamentStarted?.Invoke();

            LaunchCurrentGame();
        }

        /// <summary>
        /// Launch the current game in the schedule via the Arcade singleton.
        /// </summary>
        void LaunchCurrentGame()
        {
            if (!IsTournamentActive || CurrentGameIndex >= TotalGames)
                return;

            var mode = ScheduledGames[CurrentGameIndex];
            var intensity = ScheduledIntensities[CurrentGameIndex];

            Debug.Log($"[Tournament] Launching game {CurrentGameIndex + 1}/{TotalGames}: {mode} at intensity {intensity}");

            _waitingForGameEnd = true;

            // Set the intensity for this sub-game on gameData so the game scene reads it
            gameData.SelectedIntensity.Value = intensity;

            // Use the Arcade singleton to launch the game
            // The Arcade.LaunchArcadeGame sets up gameData and loads the scene
            Arcade.Instance.LaunchArcadeGame(
                mode,
                gameData.selectedVesselClass.Value,
                gameData.ResourceCollection,
                intensity,
                gameData.SelectedPlayerCount.Value,
                isMultiplayer: gameData.SelectedPlayerCount.Value > 1
            );
        }

        void HandleMiniGameEnd()
        {
            if (!IsTournamentActive || !_waitingForGameEnd)
                return;

            _waitingForGameEnd = false;

            // Determine the round winner from gameData
            // The RoundStatsList is already sorted by the game controller
            Domains winnerDomain = Domains.Jade;
            string winnerName = "";

            if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0)
            {
                var topStats = gameData.RoundStatsList[0];
                winnerDomain = topStats.Domain;
                winnerName = topStats.Name;
            }

            // Record the win
            if (!DomainWins.ContainsKey(winnerDomain))
                DomainWins[winnerDomain] = 0;
            DomainWins[winnerDomain]++;

            var roundResult = new TournamentRoundResult
            {
                GameIndex = CurrentGameIndex,
                GameMode = ScheduledGames[CurrentGameIndex],
                Intensity = ScheduledIntensities[CurrentGameIndex],
                WinnerDomain = winnerDomain,
                WinnerName = winnerName,
            };
            RoundResults.Add(roundResult);

            Debug.Log($"[Tournament] Game {CurrentGameIndex + 1} complete: {roundResult.GameMode} won by {winnerName} ({winnerDomain}). " +
                      $"Standings: {FormatStandings()}");

            OnRoundComplete?.Invoke(roundResult);

            CurrentGameIndex++;

            if (CurrentGameIndex >= TotalGames)
                FinishTournament();
        }

        void FinishTournament()
        {
            IsTournamentActive = false;

            // Find the domain with the most wins
            Domains tournamentWinner = Domains.Jade;
            int maxWins = 0;
            foreach (var kvp in DomainWins)
            {
                if (kvp.Value > maxWins)
                {
                    maxWins = kvp.Value;
                    tournamentWinner = kvp.Key;
                }
            }

            var finalResult = new TournamentFinalResult
            {
                WinnerDomain = tournamentWinner,
                WinnerWins = maxWins,
                DomainWins = new Dictionary<Domains, int>(DomainWins),
                RoundResults = new List<TournamentRoundResult>(RoundResults),
            };

            Debug.Log($"[Tournament] Complete! Winner: {tournamentWinner} with {maxWins} wins. {FormatStandings()}");

            OnTournamentComplete?.Invoke(finalResult);
        }

        /// <summary>
        /// Advances to the next tournament game. Called from the tournament scoreboard UI
        /// after the player has seen the round results.
        /// </summary>
        public void AdvanceToNextGame()
        {
            if (!IsTournamentActive)
                return;

            LaunchCurrentGame();
        }

        /// <summary>
        /// Cancels the current tournament and resets state.
        /// </summary>
        public void CancelTournament()
        {
            IsTournamentActive = false;
            _waitingForGameEnd = false;
            CurrentGameIndex = 0;
            Debug.Log("[Tournament] Cancelled.");
        }

        public string GetGameDisplayName(GameModes mode)
        {
            return mode switch
            {
                GameModes.MultiplayerCrystalCapture => "Crystal Capture",
                GameModes.HexRace => "Hex Race",
                GameModes.MultiplayerJoust => "Joust",
                _ => mode.ToString(),
            };
        }

        string FormatSchedule()
        {
            var parts = new string[TotalGames];
            for (int i = 0; i < TotalGames; i++)
                parts[i] = $"{GetGameDisplayName(ScheduledGames[i])}(I{ScheduledIntensities[i]})";
            return string.Join(", ", parts);
        }

        string FormatStandings()
        {
            var parts = new List<string>();
            foreach (var kvp in DomainWins)
                parts.Add($"{kvp.Key}:{kvp.Value}");
            return string.Join(", ", parts);
        }
    }

    [Serializable]
    public struct TournamentRoundResult
    {
        public int GameIndex;
        public GameModes GameMode;
        public int Intensity;
        public Domains WinnerDomain;
        public string WinnerName;
    }

    [Serializable]
    public struct TournamentFinalResult
    {
        public Domains WinnerDomain;
        public int WinnerWins;
        public Dictionary<Domains, int> DomainWins;
        public List<TournamentRoundResult> RoundResults;
    }
}
