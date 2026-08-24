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
    /// Display snapshot of one player's finish in a single tournament round, captured at game-end
    /// into <see cref="TournamentDataSO.History"/>. Needed because the Maelstrom hub / summary is a
    /// UI-only scene where <c>GameDataSO.Players</c> and <c>GameDataSO.Results</c> are already cleared
    /// by the per-scene reset - the snapshot preserves everything the round cards and the lobby player
    /// list need to render without those live objects.
    /// </summary>
    [System.Serializable]
    public class TournamentPlayerSnapshot
    {
        public string Name;
        public Domains Domain;

        /// <summary>Profile-icon id for humans (resolve via SO_ProfileIconList); AI use the AI profile list by Name (-1 = none).</summary>
        public int AvatarId = -1;
        public bool IsAI;

        /// <summary>1-based finishing rank within this round (1 = best).</summary>
        public int Rank;

        /// <summary>Mode-formatted primary score string for this round (e.g. "01:24:30", "12 Crystals").</summary>
        public string ScoreText;

        /// <summary>Optional mode-formatted secondary stat for this round, or null.</summary>
        public string Secondary;
    }

    /// <summary>
    /// One completed tournament round, captured at game-end. Drives the Maelstrom hub's per-round
    /// scroll cards and the final summary. <see cref="DomainOrder"/> is the per-domain placement
    /// (index 0 = 1st), so the points each domain earned this round are
    /// <c>PointsForPlacement(i + 1, DomainOrder.Count)</c> (last place always 0).
    /// </summary>
    [System.Serializable]
    public class TournamentRoundRecord
    {
        /// <summary>1-based round number, equal to GamesPlayed at the time it was recorded.</summary>
        public int RoundNumber;

        /// <summary>Player-facing name of the mode that was played (revealed after the fact).</summary>
        public string ModeDisplayName;

        /// <summary>The intensity this round was played at.</summary>
        public int Intensity;

        /// <summary>Players who finished this round, ranked best-first.</summary>
        public List<TournamentPlayerSnapshot> Players = new();

        /// <summary>Active domains ordered by placement this round (index 0 = 1st place).</summary>
        public List<Domains> DomainOrder = new();

        /// <summary>The domain that placed 1st this round (Blue if unknown).</summary>
        public Domains WinningDomain =>
            DomainOrder != null && DomainOrder.Count > 0 ? DomainOrder[0] : Domains.Blue;
    }

    /// <summary>
    /// SOAP data container for a Tournament session - the single source of truth for
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
        [Tooltip("The POOL a round is drawn from - the host picks a random entry per round (no " +
                 "immediate repeat), so serialized order is display order only (the hub's pool " +
                 "string), never play order. Currently seven: Skim Race, Joust, Crystal Capture, " +
                 "Rampage, Peel the Cage, Scarab Scramble, The Bends.\n\n" +
                 "Adding a mode: it must be a domain-scored multiplayer mode whose ScoringRuleSO " +
                 "can ResolvePlacementOrder, its scene must be in Build Settings, and its card's " +
                 "player/domain range must contain the Maelstrom card's (2-4 players, 2+ domains). " +
                 "A VESSEL-LOCKED mode is fine and needs no extra wiring - GameDataSO." +
                 "SyncFromArcadeGame publishes the card's Vessels list and clamps the lobby pick, " +
                 "so the round forces its own hull.")]
        public List<SO_ArcadeGame> GameQueue = new();

        [Tooltip("Scene loaded as the tournament lobby/intro (the SO_ArcadeGame card for " +
                 "the tournament points here). Its load is the per-peer 'tournament started' signal.")]
        public string LobbySceneName = "Maelstrom";

        [Tooltip("This meta-mode's OWN Arcade card (ArcadeGameTournament.asset). Its DisplayName is the " +
                 "player-facing name of the mode (currently \"Maelstrom\") and is the SINGLE SOURCE of that " +
                 "name: the Arcade grid card AND the in-scene title (TournamentSceneView) both read it. To " +
                 "rename the mode for players, change ONLY that card's DisplayName. See " +
                 "Docs/ShuffleSystem/ARCHITECTURE.md (\"Renaming the mode\").")]
        public SO_ArcadeGame ModeCard;

        [Header("Scoring")]
        [Tooltip("Placement crystals by DOMAIN finishing place: element 0 = 1st-place domain, 1 = 2nd, " +
                 "2 = 3rd. Places beyond the table score 0, and the LAST-placed domain of a round " +
                 "always earns the last entry (0) whatever the domain count - so a 2-domain game pays " +
                 "{2,0}, never {2,1} (losing must not pay toward the race target). Shuffle awards {2,1,0}.")]
        public List<int> PointsByPlace = new() { 2, 1, 0 };

        [Tooltip("FALLBACK race target only - used when the End Game Conditions tool asset is missing. " +
                 "The authority is FrogletTools > Game Modes > End Game Conditions (Maelstrom - Win Target); " +
                 "read EffectiveWinTarget, not this field. First DOMAIN whose cumulative placement " +
                 "crystals reach the target wins the shuffle (with {2,1,0} per game, 6 ≈ three dominant finishes).")]
        public int WinTarget = 6;

        [Tooltip("Hard cap on games played so a stalemate still ends (e.g. a 6-6-2 split). The shuffle " +
                 "ends when a domain hits WinTarget OR this many games have been played, whichever first.")]
        public int MaxGames = 7;

        [Header("Pacing")]
        [Tooltip("Minimum seconds the between-game loading splash holds the running standings before the " +
                 "next game begins loading, so players can actually read the summary. The host drives the " +
                 "dwell; clients follow the held scene load. Applies ONLY mid-run (a game has finished and " +
                 "the shuffle isn't decided) - the first launch and the load into the final results summary " +
                 "are never delayed. See TournamentController.MinLoadSplashDwellSeconds.")]
        public float BetweenGameSummaryDwellSeconds = 2f;

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
        /// progress - game count is <see cref="GamesPlayed"/>.
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

        /// <summary>
        /// Per-round completed-game history, captured at game-end by <see cref="RecordResults"/>.
        /// Survives the per-scene <c>GameDataSO.ResetRuntimeData</c>, so the Maelstrom hub/summary
        /// can render the full per-player roster and per-round cards.
        /// </summary>
        [System.NonSerialized] public List<TournamentRoundRecord> History = new();

        /// <summary>
        /// The upcoming pool game's display name and the intensity rolled for it, stamped by the host's
        /// random draw (<c>TournamentController.LoadRandomGame</c>) just before launch so the between-game
        /// loading splash can name what's loading. Host-side only (clients never draw); left empty on
        /// peers/games where no draw has run, so the splash simply omits the "up next" line.
        /// </summary>
        [System.NonSerialized] public string NextGameName;
        [System.NonSerialized] public int NextGameIntensity;

        public int GameCount => GameQueue?.Count ?? 0;

        /// <summary>
        /// Player-facing mode name - the mode card's DisplayName (e.g. "Maelstrom"); falls back to
        /// "Tournament" if <see cref="ModeCard"/> is unwired. Single source for titles/headers, used by
        /// the scene view and <see cref="TournamentStandingsFormatter"/>.
        /// </summary>
        public string ModeName =>
            ModeCard != null && !string.IsNullOrEmpty(ModeCard.DisplayName) ? ModeCard.DisplayName : "Tournament";

        /// <summary>
        /// Runtime win target stamped by <see cref="ResolveWinTarget"/> at tournament start (0 until then).
        /// Never serialized - re-resolved on every fresh shuffle. See <see cref="EffectiveWinTarget"/>.
        /// </summary>
        [System.NonSerialized] int _resolvedWinTarget;

        /// <summary>
        /// The win target actually used at runtime ("race to N"): the value resolved from the End Game
        /// Conditions tool at tournament start (see <see cref="ResolveWinTarget"/> /
        /// <c>TournamentController.StartTournamentInternal</c>), falling back to the serialized
        /// <see cref="WinTarget"/> until then (and in pure unit tests). Use this everywhere - the win
        /// check (<see cref="IsShuffleComplete"/>) and the UI race-rule text - so the displayed target
        /// and the actual target can't drift. See the /EndGameConditions skill.
        /// </summary>
        public int EffectiveWinTarget => _resolvedWinTarget > 0 ? _resolvedWinTarget : WinTarget;

        /// <summary>
        /// Stamp the runtime win target, resolved by <c>TournamentController</c> from the End Game
        /// Conditions tool (Tools &gt; Cosmic Shore &gt; End Game Conditions). Called once per shuffle
        /// start on every peer from the same committed asset, so <see cref="IsShuffleComplete"/> stays
        /// deterministic across the party. A non-positive value clears the override (falls back to
        /// <see cref="WinTarget"/>).
        /// </summary>
        public void ResolveWinTarget(int target) => _resolvedWinTarget = Mathf.Max(0, target);

        public bool IsLastGame =>
            GameQueue != null && GameQueue.Count > 0 && CurrentGameIndex >= GameQueue.Count - 1;

        public bool IsComplete =>
            GameQueue != null && CurrentGameIndex >= GameQueue.Count;

        /// <summary>
        /// True once the shuffle is over: a domain reached <see cref="EffectiveWinTarget"/> (race to N,
        /// authored in the End Game Conditions tool), or the <see cref="MaxGames"/> cap was hit. Reduced
        /// from the synced standings + <see cref="GamesPlayed"/>, so it evaluates identically on every
        /// peer. Drives the controller's switch from "next random game" to "load the summary".
        /// </summary>
        public bool IsShuffleComplete
        {
            get
            {
                if (MaxGames > 0 && GamesPlayed >= MaxGames) return true;
                int target = EffectiveWinTarget;
                for (int i = 0; i < Standings.Count; i++)
                    if (Standings[i].TotalPoints >= target) return true;
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
        /// Placement points for a 1-based place among <paramref name="placedDomainCount"/> ranked
        /// domains. Identical to <see cref="PointsForPlace"/> except that the LAST-placed domain
        /// always earns the table's last entry (0 with {2,1,0}), whatever the domain count - losing
        /// must never pay. With 3 domains that's the unchanged {2,1,0}; with 2 domains the loser
        /// earns 0 (not the 2nd-place 1, which let a team race to the win target on losses alone
        /// and broke "win a game = 2 points, lose = nothing").
        /// </summary>
        public int PointsForPlacement(int oneBasedPlace, int placedDomainCount)
        {
            if (PointsByPlace == null || PointsByPlace.Count == 0) return 0;
            if (placedDomainCount > 1 && oneBasedPlace == placedDomainCount)
                return PointsByPlace[PointsByPlace.Count - 1];
            return PointsForPlace(oneBasedPlace);
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
            _resolvedWinTarget = 0;   // re-resolved from the End Game Conditions tool at the next start
            Standings.Clear();
            History.Clear();
            TournamentAINames.Clear();
            // IntensityCeiling is intentionally NOT cleared - it is a config value captured at the
            // fresh start (lobby load) and must survive Play Again's reset (which routes through here).
        }

        /// <summary>
        /// Folds one finished game's ranked, per-player results into the cumulative PER-DOMAIN
        /// standings. Domain placement comes from <paramref name="domainPlacementOrder"/> when the
        /// caller supplies it - <c>TournamentController</c> passes the mode's authoritative
        /// TEAM-total order (<c>ScoringRuleSO.ResolvePlacementOrder</c>, summed metric per domain) so
        /// a team can never outplace a team that out-collected it via one strong individual. Without
        /// it (edit-mode tests / legacy callers) placement falls back to
        /// <see cref="GetDomainPlacementOrder"/> (best player rank per domain). Places are awarded
        /// <see cref="PointsByPlace"/> via <see cref="PointsForPlacement"/> (last place always earns
        /// the table's last entry - 0). Called on EVERY peer from the controller's
        /// <c>OnMiniGameEnd</c> handler; <paramref name="results"/> is the already-synced
        /// <see cref="GameDataSO.Results"/>, so all peers converge on identical standings with no
        /// extra networking.
        /// </summary>
        /// <param name="playerSnapshots">Optional per-player display snapshot for this round's history
        /// (carries avatar/AI metadata the bare <paramref name="results"/> lacks). When null, the
        /// snapshot is rebuilt from <paramref name="results"/> alone (no avatar/AI info).</param>
        /// <param name="modeDisplayName">Player-facing name of the mode just played (for the round card).</param>
        /// <param name="intensity">Intensity this round was played at (for the round card).</param>
        /// <param name="domainPlacementOrder">Authoritative per-domain finishing order (index 0 = 1st),
        /// typically the mode rule's team-total placement. Sanitized against <paramref name="results"/>:
        /// domains that fielded no player are dropped, domains missing from the order are appended.</param>
        public void RecordResults(IReadOnlyList<ScoreResult> results,
                                  List<TournamentPlayerSnapshot> playerSnapshots = null,
                                  string modeDisplayName = null,
                                  int intensity = 0,
                                  IReadOnlyList<Domains> domainPlacementOrder = null)
        {
            if (results == null || results.Count == 0) return;

            var ordered = BuildPlacement(results, domainPlacementOrder);

            // Award placement crystals by domain place (1st = PointsByPlace[0], …, last = 0) + history.
            for (int place = 0; place < ordered.Count; place++)
            {
                var standing = FindOrCreate(ordered[place]);
                standing.TotalPoints += PointsForPlacement(place + 1, ordered.Count);
                standing.Placements.Add(place + 1);
            }

            GamesPlayed++;

            // Per-round history snapshot - survives the per-scene reset so the Maelstrom hub/summary
            // (a UI-only scene where gameData.Players/Results are already cleared) can render the full
            // roster and per-round cards. Falls back to building from `results` alone when the caller
            // supplies no snapshots, so history is always recorded (incl. from edit-mode tests).
            History.Add(new TournamentRoundRecord
            {
                RoundNumber = GamesPlayed,
                ModeDisplayName = modeDisplayName ?? string.Empty,
                Intensity = intensity,
                DomainOrder = new List<Domains>(ordered),
                Players = playerSnapshots ?? BuildSnapshotsFromResults(results),
            });

            OnGameResultRecorded.Raise();
            OnStandingsChanged.Raise();
        }

        /// <summary>
        /// Builds bare per-player snapshots from ranked <paramref name="results"/> alone (no avatar/AI
        /// metadata). Used as the fallback when <see cref="RecordResults"/> is called without an
        /// enriched snapshot list (e.g. edit-mode tests).
        /// </summary>
        static List<TournamentPlayerSnapshot> BuildSnapshotsFromResults(IReadOnlyList<ScoreResult> results)
        {
            var list = new List<TournamentPlayerSnapshot>(results.Count);
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                list.Add(new TournamentPlayerSnapshot
                {
                    Name = r.Name,
                    Domain = r.Domain,
                    Rank = r.Rank,
                    ScoreText = r.ScoreText,
                    Secondary = r.Secondary,
                    AvatarId = -1,
                    IsAI = false,
                });
            }
            return list;
        }

        /// <summary>
        /// Resolves the per-domain finishing order for one game: the caller-supplied authoritative
        /// order (team-total placement from the mode rule) sanitized against <paramref name="results"/>,
        /// falling back to <see cref="GetDomainPlacementOrder"/> when none is supplied. Sanitizing
        /// keeps the fold robust to a stale/foreign order: only domains that actually fielded a
        /// player stay, and any result-domain the order missed is appended (in fallback order) so
        /// no team is ever dropped from the standings.
        /// </summary>
        List<Domains> BuildPlacement(IReadOnlyList<ScoreResult> results, IReadOnlyList<Domains> supplied)
        {
            var fallback = GetDomainPlacementOrder(results);
            if (supplied == null || supplied.Count == 0) return fallback;

            var ordered = new List<Domains>(fallback.Count);
            for (int i = 0; i < supplied.Count; i++)
                if (fallback.Contains(supplied[i]) && !ordered.Contains(supplied[i]))
                    ordered.Add(supplied[i]);

            for (int i = 0; i < fallback.Count; i++)
                if (!ordered.Contains(fallback[i]))
                    ordered.Add(fallback[i]);

            return ordered;
        }

        /// <summary>
        /// FALLBACK ordering of the active domains from one game's ranked, per-player
        /// <paramref name="results"/> alone: a domain's place = the best (lowest) player
        /// <see cref="ScoreResult.Rank"/>; domains are sorted by that ascending, ties broken by enum
        /// order (Jade→Ruby→Gold) so every peer agrees. Element 0 is 1st place. Used when no
        /// authoritative team-total order is supplied (edit-mode tests / legacy callers) - the live
        /// game path passes <c>ScoringRuleSO.ResolvePlacementOrder</c> instead, because best player
        /// rank can misplace teams in modes whose results rank individuals (a losing team's player
        /// tying the top score outplaced the team that actually won).
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
        /// The placement crystals a domain earns from one game's ranked <paramref name="results"/> -
        /// i.e. its per-game <c>{2,1,0}</c> via <see cref="PointsByPlace"/> (last place always 0,
        /// see <see cref="PointsForPlacement"/>). Returns 0 if the domain did not play. Computed
        /// straight from <paramref name="results"/> (no dependency on <see cref="RecordResults"/>
        /// having run first), so the Scoreboard can read the local player's per-game reward
        /// regardless of event ordering. Pass <paramref name="domainPlacementOrder"/> (the mode
        /// rule's team-total placement) whenever available so the reward matches the standings fold.
        /// </summary>
        public int CrystalsForDomain(IReadOnlyList<ScoreResult> results, Domains domain,
                                     IReadOnlyList<Domains> domainPlacementOrder = null)
        {
            if (results == null || results.Count == 0) return 0;
            var ordered = BuildPlacement(results, domainPlacementOrder);
            int idx = ordered.IndexOf(domain);
            return idx >= 0 ? PointsForPlacement(idx + 1, ordered.Count) : 0;
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
