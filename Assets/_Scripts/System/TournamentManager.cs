using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Orchestrates tournament lifecycle: starts tournaments, captures round results,
    /// advances between rounds, and computes final standings.
    ///
    /// Pure C# class registered as a Reflex DI lazy singleton in <see cref="AppManager"/>.
    /// Single writer to <see cref="TournamentDataSO"/>.
    /// Raises events via <see cref="TournamentEventsContainerSO"/>.
    ///
    /// Game controllers (HexRace, Joust, CrystalCapture) run completely unmodified.
    /// This manager sits above them, intercepting OnMiniGameEnd to capture scores
    /// and reconfiguring GameDataSO between rounds.
    /// </summary>
    public class TournamentManager
    {
        readonly TournamentDataSO _tournamentData;
        readonly TournamentEventsContainerSO _tournamentEvents;
        readonly GameDataSO _gameData;
        readonly SceneLoader _sceneLoader;

        int _intensity;
        int _humanPlayerCount;
        int _totalPlayerCount;
        bool _subscribed;

        public TournamentManager(
            TournamentDataSO tournamentData,
            TournamentEventsContainerSO tournamentEvents,
            GameDataSO gameData,
            SceneLoader sceneLoader)
        {
            _tournamentData = tournamentData;
            _tournamentEvents = tournamentEvents;
            _gameData = gameData;
            _sceneLoader = sceneLoader;
        }

        /// <summary>
        /// Starts a tournament session. Configures the first round and launches the game.
        /// </summary>
        public void StartTournament(
            SO_Tournament tournament,
            int totalPlayerCount,
            int humanPlayerCount,
            int intensity,
            VesselClassType vesselClass)
        {
            if (tournament == null || tournament.Rounds == null || tournament.Rounds.Count == 0)
            {
                Debug.LogError("[TournamentManager] Cannot start tournament — null or empty rounds list.");
                return;
            }

            _tournamentData.Reset();
            _tournamentData.ActiveTournament = tournament;
            _tournamentData.CurrentRoundIndex = 0;
            _tournamentData.IsTournamentActive = true;

            _intensity = intensity;
            _humanPlayerCount = humanPlayerCount;
            _totalPlayerCount = totalPlayerCount;

            _gameData.IsTournamentMode = true;

            SubscribeToGameEnd();
            ConfigureGameDataForRound(0, vesselClass);

            _tournamentEvents.OnTournamentStarted?.Raise();
            _gameData.InvokeGameLaunch();
        }

        /// <summary>
        /// Advances to the next tournament round with the player's chosen vessel.
        /// If the current round was the last, completes the tournament instead.
        /// </summary>
        public void AdvanceToNextRound(VesselClassType vesselClass)
        {
            if (!_tournamentData.IsTournamentActive) return;

            _tournamentData.CurrentRoundIndex++;

            if (_tournamentData.CurrentRoundIndex >= _tournamentData.TotalRounds)
            {
                CompleteTournament();
                return;
            }

            ConfigureGameDataForRound(_tournamentData.CurrentRoundIndex, vesselClass);
            _tournamentEvents.OnTournamentAdvancing?.Raise();
            _gameData.InvokeGameLaunch();
        }

        /// <summary>
        /// Ends the tournament session and returns to the main menu.
        /// </summary>
        public void EndTournament()
        {
            UnsubscribeFromGameEnd();

            _tournamentData.IsTournamentActive = false;
            _gameData.IsTournamentMode = false;

            _tournamentEvents.OnTournamentEnded?.Raise();
            _sceneLoader.ReturnToMainMenu();
        }

        void ConfigureGameDataForRound(int roundIndex, VesselClassType vesselClass)
        {
            var arcadeGame = _tournamentData.ActiveTournament.Rounds[roundIndex];
            _gameData.SyncFromArcadeGame(arcadeGame);
            _gameData.SelectedIntensity.Value = _intensity;
            _gameData.SelectedPlayerCount.Value = _humanPlayerCount;
            _gameData.selectedVesselClass.Value = vesselClass;
            _gameData.RequestedAIBackfillCount = Math.Max(0, _totalPlayerCount - _humanPlayerCount);
        }

        void CaptureRoundResults()
        {
            if (!_tournamentData.IsTournamentActive) return;

            var tournament = _tournamentData.ActiveTournament;
            var roundStats = _gameData.RoundStatsList;

            if (roundStats == null || roundStats.Count == 0)
            {
                Debug.LogWarning("[TournamentManager] No round stats available to capture.");
                return;
            }

            // RoundStatsList is already sorted by the game controller (golf or normal)
            var playerScores = new List<TournamentPlayerScore>();
            for (int i = 0; i < roundStats.Count; i++)
            {
                int placement = i + 1;
                playerScores.Add(new TournamentPlayerScore
                {
                    PlayerName = roundStats[i].Name,
                    Domain = roundStats[i].Domain,
                    RawScore = roundStats[i].Score,
                    Placement = placement,
                    PointsAwarded = tournament.GetPointsForPlacement(placement),
                });
            }

            var currentRound = _tournamentData.CurrentRound;
            _tournamentData.CompletedRounds.Add(new TournamentRoundResult
            {
                RoundIndex = _tournamentData.CurrentRoundIndex,
                GameMode = currentRound != null ? currentRound.Mode : _gameData.GameMode,
                GameDisplayName = currentRound != null ? currentRound.DisplayName : _gameData.GameMode.ToString(),
                PlayerScores = playerScores,
            });

            UpdateStandings();
            _tournamentEvents.OnTournamentRoundCaptured?.Raise();
        }

        void UpdateStandings()
        {
            var pointsByPlayer = new Dictionary<string, (Domains domain, int total, List<int> perRound)>();

            foreach (var round in _tournamentData.CompletedRounds)
            {
                foreach (var ps in round.PlayerScores)
                {
                    if (!pointsByPlayer.TryGetValue(ps.PlayerName, out var entry))
                    {
                        entry = (ps.Domain, 0, new List<int>());
                        pointsByPlayer[ps.PlayerName] = entry;
                    }

                    entry.total += ps.PointsAwarded;
                    entry.perRound.Add(ps.PointsAwarded);
                    pointsByPlayer[ps.PlayerName] = (entry.domain, entry.total, entry.perRound);
                }
            }

            _tournamentData.Standings.Clear();
            foreach (var kvp in pointsByPlayer.OrderByDescending(kv => kv.Value.total))
            {
                _tournamentData.Standings.Add(new TournamentStanding
                {
                    PlayerName = kvp.Key,
                    Domain = kvp.Value.domain,
                    TotalPoints = kvp.Value.total,
                    PointsPerRound = new List<int>(kvp.Value.perRound),
                });
            }
        }

        void CompleteTournament()
        {
            // Final standings are already sorted by UpdateStandings()
            _tournamentEvents.OnTournamentComplete?.Raise();
        }

        void SubscribeToGameEnd()
        {
            if (_subscribed) return;
            _subscribed = true;
            _gameData.OnMiniGameEnd.OnRaised += CaptureRoundResults;
        }

        void UnsubscribeFromGameEnd()
        {
            if (!_subscribed) return;
            _subscribed = false;
            _gameData.OnMiniGameEnd.OnRaised -= CaptureRoundResults;
        }
    }
}
