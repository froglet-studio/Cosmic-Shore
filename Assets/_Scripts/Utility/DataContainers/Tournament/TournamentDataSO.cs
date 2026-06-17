using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One cumulative tournament standing, keyed by <see cref="Domains"/> (team). Carries the
    /// running placement-crystal total plus the per-game finishing places so the scoreboard/summary
    /// can show history and break ties. Scoring is per-DOMAIN: each game ranks the active domains
    /// and awards <see cref="TournamentDataSO.PointsByPlace"/> by place.
    /// </summary>
    [System.Serializable]
    public class TournamentDomainStanding
    {
        public Domains Domain;

        /// <summary>Running sum of placement crystals across the games played so far.</summary>
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
    /// the game lineup, the cumulative per-domain standings, and the placement-points
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

        [Tooltip("This meta-mode's OWN Arcade card (ArcadeGameTournament.asset). Its DisplayName is the " +
                 "player-facing name of the mode (currently \"Shuffle\") and is the SINGLE SOURCE of that " +
                 "name: the Arcade grid card AND the in-scene title (TournamentSceneView) both read it. To " +
                 "rename the mode for players, change ONLY that card's DisplayName. See " +
                 "Docs/ShuffleSystem/ARCHITECTURE.md (\"Renaming the mode\").")]
        public SO_ArcadeGame ModeCard;

        [Header("Scoring")]
        [Tooltip("Placement crystals by DOMAIN finishing place: element 0 = 1st-place domain, 1 = 2nd, " +
                 "2 = 3rd. Places beyond the table score 0. Shuffle awards {2,1,0}.")]
        public List<int> PointsByPlace = new() { 2, 1, 0 };

        [Tooltip("Race target: the first DOMAIN whose cumulative placement crystals reach this total " +
                 "wins the shuffle (with {2,1,0} per game, 6 ≈ three dominant finishes).")]
        public int WinTarget = 6;

        [Tooltip("Hard cap on games played so a stalemate still ends (e.g. a 6-6-2 split). The shuffle " +
                 "ends when a domain hits WinTarget OR this many games have been played, whichever first.")]
        public int MaxGames = 7;

        [Header("SOAP Events")]
        public ScriptableEventNoParam OnTournamentStarted;
        public ScriptableEventNoParam OnGameResultRecorded;
        public ScriptableEventNoParam OnStandingsChanged;
        public ScriptableEventNoParam OnTournamentCompleted;

        // ── Runtime state (never serialized into the asset) ──────────────────────

        /// <summary>True from tournament start until it ends / is exited.</summary>
        [System.NonSerialized] public bool IsActive;

        /// <summary>
        /// Pool index (into <see cref="GameQueue"/>) of the game currently loaded / just finished.
        /// With the randomized lineup this tracks WHICH mode is loaded (for repeat-avoidance), not
        /// progress — game count is <see cref="GamesPlayed"/>.
        /// </summary>
        [System.NonSerialized] public int CurrentGameIndex;

        /// <summary>Number of games whose results have been folded so far this shuffle.</summary>
        [System.NonSerialized] public int GamesPlayed;

        /// <summary>
        /// Per-game intensity ceiling (X) captured from the lobby-chosen intensity at tournament
        /// start; each game draws a random intensity in [1..X]. Persists across <see cref="ResetRuntime"/>
        /// so Play Again keeps the same ceiling (it is re-captured only on a fresh start from the lobby).
        /// </summary>
        [System.NonSerialized] public int IntensityCeiling;

        /// <summary>
        /// AI display names seeded once at tournament start and reused for every game, so AI
        /// identities stay stable across the lineup (see <c>ServerPlayerVesselInitializerWithAI</c>).
        /// </summary>
        [System.NonSerialized] public List<string> TournamentAINames = new();

        /// <summary>Cumulative standings, keyed by domain (team).</summary>
        [System.NonSerialized] public List<TournamentDomainStanding> Standings = new();

        public int GameCount => GameQueue?.Count ?? 0;

        /// <summary>
        /// Player-facing mode name — the mode card's DisplayName (e.g. "Shuffle"); falls back to
        /// "Tournament" if <see cref="ModeCard"/> is unwired. Single source for titles/headers, used by
        /// the scene view and <see cref="TournamentStandingsFormatter"/>.
        /// </summary>
        public string ModeName =>
            ModeCard != null && !string.IsNullOrEmpty(ModeCard.DisplayName) ? ModeCard.DisplayName : "Tournament";

        public bool IsLastGame =>
            GameQueue != null && GameQueue.Count > 0 && CurrentGameIndex >= GameQueue.Count - 1;

        public bool IsComplete =>
            GameQueue != null && CurrentGameIndex >= GameQueue.Count;

        /// <summary>
        /// True once the shuffle is over: a domain reached <see cref="WinTarget"/> (race to 6), or the
        /// <see cref="MaxGames"/> cap was hit. Reduced from the synced standings + <see cref="GamesPlayed"/>,
        /// so it evaluates identically on every peer. Drives the controller's switch from "next random
        /// game" to "load the summary".
        /// </summary>
        public bool IsShuffleComplete
        {
            get
            {
                if (MaxGames > 0 && GamesPlayed >= MaxGames) return true;
                for (int i = 0; i < Standings.Count; i++)
                    if (Standings[i].TotalPoints >= WinTarget) return true;
                return false;
            }
        }

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
            GamesPlayed = 0;
            Standings.Clear();
            TournamentAINames.Clear();
            // IntensityCeiling is intentionally NOT cleared — it is a config value captured at the
            // fresh start (lobby load) and must survive Play Again's reset (which routes through here).
        }

        /// <summary>
        /// Folds one finished game's ranked, per-player results into the cumulative PER-DOMAIN
        /// standings. Each active domain's finishing place this game is its best (lowest) player
        /// <see cref="ScoreResult.Rank"/>; the domains are then ordered by that best rank (ties broken
        /// by enum order for cross-peer determinism) and awarded <see cref="PointsByPlace"/> by place.
        /// Called on EVERY peer from the controller's <c>OnMiniGameEnd</c> handler;
        /// <paramref name="results"/> is the already-synced <see cref="GameDataSO.Results"/>, so all
        /// peers converge on identical standings with no extra networking.
        /// </summary>
        public void RecordResults(IReadOnlyList<ScoreResult> results)
        {
            if (results == null || results.Count == 0) return;

            var ordered = GetDomainPlacementOrder(results);

            // Award placement crystals by domain place (1st = PointsByPlace[0], …) + append history.
            for (int place = 0; place < ordered.Count; place++)
            {
                var standing = FindOrCreate(ordered[place]);
                standing.TotalPoints += PointsForPlace(place + 1);
                standing.Placements.Add(place + 1);
            }

            GamesPlayed++;

            OnGameResultRecorded.Raise();
            OnStandingsChanged.Raise();
        }

        /// <summary>
        /// Orders the active domains by their finishing place in one game's ranked, per-player
        /// <paramref name="results"/>: a domain's place = the best (lowest) player
        /// <see cref="ScoreResult.Rank"/>; domains are sorted by that ascending, ties broken by enum
        /// order (Jade→Ruby→Gold) so every peer agrees. Element 0 is 1st place. Shared by
        /// <see cref="RecordResults"/> (the cumulative fold) and <see cref="CrystalsForDomain"/>
        /// (the per-game reward), so both read identical placements.
        /// </summary>
        public List<Domains> GetDomainPlacementOrder(IReadOnlyList<ScoreResult> results)
        {
            var ordered = new List<Domains>();
            if (results == null || results.Count == 0) return ordered;

            // Each domain's place = the best (lowest) Rank among its players.
            var bestRankByDomain = new Dictionary<Domains, int>();
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (!bestRankByDomain.TryGetValue(r.Domain, out var best) || r.Rank < best)
                    bestRankByDomain[r.Domain] = r.Rank;
            }

            ordered.AddRange(bestRankByDomain.Keys);
            ordered.Sort((a, b) =>
            {
                int byRank = bestRankByDomain[a].CompareTo(bestRankByDomain[b]);
                return byRank != 0 ? byRank : ((int)a).CompareTo((int)b);
            });
            return ordered;
        }

        /// <summary>
        /// The placement crystals a domain earns from one game's ranked <paramref name="results"/> —
        /// i.e. its per-game <c>{2,1,0}</c> via <see cref="PointsByPlace"/>. Returns 0 if the domain
        /// did not play. Computed straight from <paramref name="results"/> (no dependency on
        /// <see cref="RecordResults"/> having run first), so the Scoreboard can read the local
        /// player's per-game reward regardless of event ordering.
        /// </summary>
        public int CrystalsForDomain(IReadOnlyList<ScoreResult> results, Domains domain)
        {
            var ordered = GetDomainPlacementOrder(results);
            int idx = ordered.IndexOf(domain);
            return idx >= 0 ? PointsForPlace(idx + 1) : 0;
        }

        /// <summary>
        /// Standings sorted best-first: highest total points, tie-broken by best single
        /// placement (lower place number wins), then by domain enum order (Jade→Ruby→Gold)
        /// for cross-peer determinism.
        /// </summary>
        public List<TournamentDomainStanding> BuildSortedStandings()
        {
            var sorted = new List<TournamentDomainStanding>(Standings);
            sorted.Sort((a, b) =>
            {
                int byPoints = b.TotalPoints.CompareTo(a.TotalPoints);
                if (byPoints != 0) return byPoints;

                int byBest = a.BestPlacement.CompareTo(b.BestPlacement);
                if (byBest != 0) return byBest;

                return ((int)a.Domain).CompareTo((int)b.Domain);
            });
            return sorted;
        }

        TournamentDomainStanding FindOrCreate(Domains domain)
        {
            for (int i = 0; i < Standings.Count; i++)
                if (Standings[i].Domain == domain)
                    return Standings[i];

            var created = new TournamentDomainStanding { Domain = domain };
            Standings.Add(created);
            return created;
        }
    }
}
