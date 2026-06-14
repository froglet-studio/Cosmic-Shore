using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One cumulative tournament standing, keyed by player display name. Carries the
    /// running placement-point total plus the per-game finishing places so the
    /// scoreboard/summary can show history and break ties.
    /// </summary>
    [System.Serializable]
    public class TournamentStanding
    {
        public string Name;
        public Domains Domain;
        public bool IsAI;

        /// <summary>Running sum of placement points across the games played so far.</summary>
        public int TotalPoints;

        /// <summary>1-based finishing place for each completed game, in play order.</summary>
        public List<int> Placements = new();

        /// <summary>Best (lowest) finishing place achieved so far; int.MaxValue if none yet.</summary>
        public int BestPlacement
        {
            get
            {
                int best = int.MaxValue;
                for (int i = 0; i < Placements.Count; i++)
                    if (Placements[i] < best) best = Placements[i];
                return best;
            }
        }
    }

    /// <summary>
    /// SOAP data container for a Tournament session — the single source of truth for
    /// the game lineup, the cumulative per-player standings, and the placement-points
    /// table. Authored once as an asset (lineup + points table); the runtime fields
    /// (<see cref="IsActive"/>, <see cref="CurrentGameIndex"/>, <see cref="Standings"/>,
    /// <see cref="TournamentAINames"/>) are reduced locally on every peer by
    /// <c>TournamentController</c> from the already-synced <see cref="GameDataSO.Results"/>,
    /// so no extra networking is needed (identical inputs → identical standings).
    ///
    /// See Docs/TournamentSystem/ARCHITECTURE.md.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DataContainer_" + nameof(TournamentDataSO),
        menuName = "ScriptableObjects/Data Containers/" + nameof(TournamentDataSO))]
    public class TournamentDataSO : ScriptableObject
    {
        [Header("Lineup")]
        [Tooltip("The minigames played in order, one per round. Fixed set for the MVP: " +
                 "HexRace, Joust, Crystal Capture.")]
        public List<SO_ArcadeGame> GameQueue = new();

        [Tooltip("Scene loaded as the tournament lobby/intro (the SO_ArcadeGame card for " +
                 "the tournament points here). Its load is the per-peer 'tournament started' signal.")]
        public string LobbySceneName = "Tournament";

        [Header("Scoring")]
        [Tooltip("Placement points by finishing place: element 0 = 1st place, 1 = 2nd, … " +
                 "Places beyond the table score 0.")]
        public List<int> PointsByPlace = new() { 10, 6, 3, 1 };

        [Header("SOAP Events")]
        public ScriptableEventNoParam OnTournamentStarted;
        public ScriptableEventNoParam OnGameResultRecorded;
        public ScriptableEventNoParam OnStandingsChanged;
        public ScriptableEventNoParam OnTournamentCompleted;

        // ── Runtime state (never serialized into the asset) ──────────────────────

        /// <summary>True from tournament start until it ends / is exited.</summary>
        [System.NonSerialized] public bool IsActive;

        /// <summary>Index into <see cref="GameQueue"/> of the game currently loaded/just finished.</summary>
        [System.NonSerialized] public int CurrentGameIndex;

        /// <summary>
        /// AI display names seeded once at tournament start and reused for every game, so
        /// name-keyed bot standings attribute correctly across the lineup (see
        /// <c>ServerPlayerVesselInitializerWithAI</c>).
        /// </summary>
        [System.NonSerialized] public List<string> TournamentAINames = new();

        /// <summary>Cumulative standings, keyed by player name.</summary>
        [System.NonSerialized] public List<TournamentStanding> Standings = new();

        public int GameCount => GameQueue?.Count ?? 0;

        public bool IsLastGame =>
            GameQueue != null && GameQueue.Count > 0 && CurrentGameIndex >= GameQueue.Count - 1;

        public bool IsComplete =>
            GameQueue != null && CurrentGameIndex >= GameQueue.Count;

        public SO_ArcadeGame CurrentGame =>
            (GameQueue != null && CurrentGameIndex >= 0 && CurrentGameIndex < GameQueue.Count)
                ? GameQueue[CurrentGameIndex]
                : null;

        /// <summary>
        /// Resolves the queue index of a scene by name (used by the controller to keep
        /// <see cref="CurrentGameIndex"/> in lock-step with the loaded scene on every peer).
        /// Returns -1 if the scene is not one of the queued games.
        /// </summary>
        public int IndexOfSceneName(string sceneName)
        {
            if (GameQueue == null || string.IsNullOrEmpty(sceneName)) return -1;
            for (int i = 0; i < GameQueue.Count; i++)
                if (GameQueue[i] != null && GameQueue[i].SceneName == sceneName)
                    return i;
            return -1;
        }

        /// <summary>Placement points for a 1-based finishing place (out-of-table = 0).</summary>
        public int PointsForPlace(int oneBasedPlace)
        {
            if (PointsByPlace == null) return 0;
            int idx = oneBasedPlace - 1;
            if (idx < 0 || idx >= PointsByPlace.Count) return 0;
            return PointsByPlace[idx];
        }

        /// <summary>
        /// Clears all runtime state for a fresh tournament. Lineup + points table (the
        /// authored fields) are untouched.
        /// </summary>
        public void ResetRuntime()
        {
            IsActive = false;
            CurrentGameIndex = 0;
            Standings.Clear();
            TournamentAINames.Clear();
        }

        /// <summary>
        /// Folds one finished game's ranked, per-player results into the cumulative
        /// standings — awarding placement points by finishing place and appending each
        /// player's place to their history. Called on EVERY peer from the controller's
        /// <c>OnMiniGameEnd</c> handler; <paramref name="results"/> is the already-synced
        /// <see cref="GameDataSO.Results"/>, so all peers converge on the same standings.
        /// </summary>
        public void RecordResults(IReadOnlyList<ScoreResult> results)
        {
            if (results == null || results.Count == 0) return;

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var standing = FindOrCreate(r.Name);
                standing.Domain = r.Domain;
                standing.IsAI = TournamentAINames != null && TournamentAINames.Contains(r.Name);
                standing.TotalPoints += PointsForPlace(r.Rank);
                standing.Placements.Add(r.Rank);
            }

            OnGameResultRecorded.Raise();
            OnStandingsChanged.Raise();
        }

        /// <summary>
        /// Standings sorted best-first: highest total points, tie-broken by best single
        /// placement (lower place number wins), then by name for cross-peer determinism.
        /// </summary>
        public List<TournamentStanding> BuildSortedStandings()
        {
            var sorted = new List<TournamentStanding>(Standings);
            sorted.Sort((a, b) =>
            {
                int byPoints = b.TotalPoints.CompareTo(a.TotalPoints);
                if (byPoints != 0) return byPoints;

                int byBest = a.BestPlacement.CompareTo(b.BestPlacement);
                if (byBest != 0) return byBest;

                return string.CompareOrdinal(a.Name ?? "", b.Name ?? "");
            });
            return sorted;
        }

        TournamentStanding FindOrCreate(string name)
        {
            for (int i = 0; i < Standings.Count; i++)
                if (Standings[i].Name == name)
                    return Standings[i];

            var created = new TournamentStanding { Name = name };
            Standings.Add(created);
            return created;
        }
    }
}
