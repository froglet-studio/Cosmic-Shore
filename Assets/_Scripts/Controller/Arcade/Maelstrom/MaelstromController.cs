using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Persistent brain for a Maelstrom / Shuffle session. Pure C# DI singleton (created eagerly by
    /// <c>AppManager</c>, so it is alive from bootstrap and survives every Single scene load).
    /// A static <see cref="Instance"/> is exposed so scene MonoBehaviours (the Scoreboard's
    /// Continue button, the Maelstrom scene view) can reach it without DI injection - mirroring
    /// <c>PartyInviteController.Instance</c> / <c>CameraManager.Instance</c>.
    ///
    /// Design (see Docs/MaelstromSystem/ARCHITECTURE.md):
    ///   • Sequential <c>LoadSceneMode.Single</c> loads - the network session / Relay / Player
    ///     objects already persist across them. The host drives every scene load; clients follow
    ///     via Netcode. No additive loading, no new NetworkBehaviour.
    ///   • <b>Randomized lineup</b> (the "Shuffle" card): each game the host draws a random pool mode
    ///     (from a BAG - no mode repeats until the pool is exhausted) + a random intensity in
    ///     [1..ceiling] at HUB ENTRY, previews it, and launches it when the countdown elapses. The
    ///     pick reaches clients as a replicated <see cref="MaelstromRoundTicket"/> (it has to, now
    ///     that they build its arena BEFORE the load); the intensity still also rides the existing
    ///     config sync at launch. No shared RNG seed anywhere.
    ///   • <b>Race to 6</b>: standings are network-free - on <c>OnMiniGameEnd</c> EVERY peer folds the
    ///     already-synced <see cref="GameDataSO.Results"/> into per-domain crystals identically and
    ///     evaluates <see cref="MaelstromDataSO.IsShuffleComplete"/> (a domain hit the target, or the
    ///     game cap). The host then advances to the next random game or the summary.
    ///   • Phase is driven by scene loads (deterministic on every peer): the lobby scene starts the
    ///     session, each pool game scene marks it in-game, Menu_Main ends it.
    /// </summary>
    public class MaelstromController
    {
        public static MaelstromController Instance { get; private set; }

        // Pure C# Reflex singleton: a new one is constructed per play session, but the old one's
        // SceneManager.sceneLoaded subscription (a Unity static event that also fires in edit
        // mode) survives a domain-reload-free play exit — session N would have N controllers
        // folding standings. Tear the stale instance down before the next session constructs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance?.Teardown();
            Instance = null;
        }

        void Teardown()
        {
            _gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        readonly GameDataSO _gameData;
        readonly MaelstromDataSO _tournament;
        readonly SceneNameListSO _sceneNames;
        readonly MaelstromStateMachine _stateMachine = new();

        public MaelstromPhase Phase => _stateMachine.Current;
        public bool IsActive => _tournament != null && _tournament.IsActive;

        /// <summary>True while the Maelstrom results screen is up (after the last game). Read by the scene view.</summary>
        public bool IsShowingSummary => _stateMachine.Current == MaelstromPhase.Summary;

        /// <summary>
        /// The mode the hub has drawn and is previewing, or null before the draw lands. Read by
        /// the hub's vessel initializer (so pilots warm up in the round's own hull) and by the
        /// preview host. Authoritative on the host; on a client it is the mirror
        /// <c>MaelstromLobby</c> keeps from the replicated ticket.
        /// </summary>
        public SO_ArcadeGame PendingGame => _tournament != null ? _tournament.PendingGame : null;

        /// <summary>The intensity rolled for <see cref="PendingGame"/>, or 0 when nothing is drawn.</summary>
        public int PendingIntensity => _tournament != null ? _tournament.PendingIntensity : 0;

        /// <summary>
        /// True for the between-game transition whose loading splash shows the running standings - a
        /// tournament is active, a game has been played, and the shuffle isn't decided yet. Mirrors the
        /// exact condition <c>BootStatusBroadcaster.HandleLaunchGame</c> uses to render those standings,
        /// so the dwell below applies precisely when - and because - that summary is on screen.
        /// </summary>
        public bool IsBetweenGamesStandingsShown =>
            _tournament != null && _tournament.IsActive
            && !_tournament.IsShuffleComplete && _tournament.GamesPlayed > 0;

        /// <summary>
        /// Minimum seconds the loading splash should hold before the next scene load begins, so the
        /// between-game running standings are readable. Zero outside that window, so normal game launches,
        /// the first game, and the load into the final summary are never slowed. Read by
        /// <c>SceneLoader.LaunchGame</c> (host path) - holding the host's load holds the whole party's splash.
        /// </summary>
        public float MinLoadSplashDwellSeconds =>
            IsBetweenGamesStandingsShown ? Mathf.Max(0f, _tournament.BetweenGameSummaryDwellSeconds) : 0f;

        static bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        public MaelstromController(GameDataSO gameData, MaelstromDataSO tournament, SceneNameListSO sceneNames)
        {
            _gameData = gameData;
            _tournament = tournament;
            _sceneNames = sceneNames;
            Instance = this;

            // OnMiniGameEnd fires on every peer after the mode synced Results; the handler
            // no-ops unless a tournament is active, so non-tournament games are unaffected.
            _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        // ── Scene-driven lifecycle (runs on every peer) ──────────────────────────

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_tournament == null) return;

            // Returning to the menu ends any running tournament (covers the host's Main Menu
            // press and the clients that follow the load).
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            if (scene.name == menuScene)
            {
                if (_tournament.IsActive) EndMaelstrom();
                return;
            }

            // The Maelstrom scene serves FOUR roles, decided here (deterministic on every peer):
            //   • Summary phase → restart  (Play Again from the summary → fresh lobby; reset but KEEP the
            //                               intensity ceiling - see RestartFromSummary). Must be checked
            //                               FIRST: standings still read complete here, so it has to win
            //                               over the IsShuffleComplete branch below.
            //   • shuffle decided → SUMMARY (a domain hit WinTarget / the game cap → show final results).
            //   • mid-run        → HUB      (between rounds: IsActive with games played → standings hub;
            //                               do NOT reset - standings/history must persist).
            //   • otherwise      → fresh start (entered from the arcade/menu → reset + capture the ceiling).
            //
            // Summary-vs-hub keys off the AUTHORITATIVE MaelstromDataSO.IsShuffleComplete (folded
            // identically on every peer from the synced results), NOT the transient Complete phase that
            // HandleMiniGameEnd sets. That phase is only honored when the deciding game ends in the InGame
            // phase; if it is ever missed, the win MUST still surface as the summary instead of silently
            // routing back to the hub for "one more game" (the race-to-6 regression). See EnterSummary.
            if (scene.name == _tournament.LobbySceneName)
            {
                if (_stateMachine.Current == MaelstromPhase.Summary)
                    RestartFromSummary();                                // Play Again → fresh lobby (keep ceiling)
                else if (_tournament.IsActive && _tournament.IsShuffleComplete)
                    EnterSummary();                                      // shuffle decided → results summary
                else if (_tournament.IsActive && _tournament.GamesPlayed > 0)
                    _stateMachine.TransitionTo(MaelstromPhase.Lobby);   // between-round hub (no reset)
                else
                    StartMaelstrom();                                   // fresh start from arcade/menu
                return;
            }

            // A pool game scene loaded. Every game is now launched from the lobby/hub (BeginNextRound),
            // so the restart wipe happens at LOBBY load (RestartFromSummary), not here - a game scene
            // only ever loads while already in Lobby/InGame phase.
            int idx = _tournament.IndexOfSceneName(scene.name);
            if (idx >= 0 && _tournament.IsActive)
            {
                _tournament.CurrentGameIndex = idx;   // which pool mode is loaded (for repeat-avoidance)

                // The pending round has become the current one. The HOST cleared it in
                // LaunchPendingRound; a client only ever mirrors the replicated ticket, so it
                // clears here - otherwise its next hub opens previewing the round just played
                // for the frame before the new ticket lands.
                _tournament.ClearPendingRound();
                _stateMachine.TransitionTo(MaelstromPhase.InGame);
            }
        }

        void HandleMiniGameEnd()
        {
            if (_tournament == null || !_tournament.IsActive) return;

            // Fold this game's ranked, synced results into the cumulative per-domain standings (and
            // bump GamesPlayed) + capture a per-round history snapshot. Runs on every peer with
            // identical input, BEFORE the next Single load clears Results / Players.
            //
            // Domain placement comes from the mode rule's TEAM-TOTAL order (summed metric per
            // domain - the same aggregation that ends the turn and picks WinnerDomain), NOT from
            // per-player ranks: rank-derived placement let a losing team outplace the team that
            // out-collected it whenever its best individual tied the top score. RoundStatsList is
            // still populated and synced here (the ClientRpc that raised OnMiniGameEnd updated it),
            // so the order is identical on every peer.
            var placement = _gameData.ScoringRule != null
                ? _gameData.ScoringRule.ResolvePlacementOrder(_gameData)
                : null;
            var snapshots = BuildPlayerSnapshots(_gameData.Results);
            string modeName = _tournament.CurrentGame != null ? _tournament.CurrentGame.DisplayName : null;
            int intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0;
            _tournament.RecordResults(_gameData.Results, snapshots, modeName, intensity, placement);

            // Race to 6 (or the game cap): once the shuffle is decided, the next Continue loads the
            // summary instead of another game. Evaluated identically on every peer from synced state.
            // The Complete transition is a best-effort signal (it only lands when the game ends in the
            // InGame phase) - it is NOT the source of truth for showing the summary. The authoritative,
            // phase-independent decision is re-made from IsShuffleComplete at the Maelstrom scene load
            // (see HandleSceneLoaded → EnterSummary), so a missed transition here can't swallow the win.
            if (_tournament.IsShuffleComplete)
            {
                _stateMachine.TransitionTo(MaelstromPhase.Complete);
                _tournament.OnMaelstromCompleted.Raise();
            }
        }

        /// <summary>
        /// Builds enriched per-player history snapshots for the just-finished round by merging the
        /// ranked <paramref name="results"/> with avatar / AI metadata from the still-populated
        /// <c>gameData.Players</c> (matched by Name) - captured before the next Single load clears them.
        /// Runs on every peer; avatar/AI fields are display-only so minor cross-peer differences are harmless.
        /// </summary>
        List<MaelstromPlayerSnapshot> BuildPlayerSnapshots(IReadOnlyList<ScoreResult> results)
        {
            var list = new List<MaelstromPlayerSnapshot>();
            if (results == null) return list;

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];

                IPlayer player = null;
                var players = _gameData.Players;
                if (players != null)
                {
                    for (int p = 0; p < players.Count; p++)
                    {
                        var candidate = players[p];
                        if (candidate != null && candidate.Name == r.Name) { player = candidate; break; }
                    }
                }

                list.Add(new MaelstromPlayerSnapshot
                {
                    Name = r.Name,
                    Domain = r.Domain,
                    Rank = r.Rank,
                    ScoreText = r.ScoreText,
                    Secondary = r.Secondary,
                    AvatarId = player != null ? player.AvatarId : -1,
                    IsAI = player != null && player.IsInitializedAsAI,
                });
            }
            return list;
        }

        // ── Session control ──────────────────────────────────────────────────────

        /// <summary>
        /// Fresh start from the arcade/menu - resets standings AND captures the lobby-chosen intensity
        /// as the per-game ceiling. Called on every peer when the lobby scene loads outside a run, so
        /// standings reset identically across the party.
        /// </summary>
        public void StartMaelstrom() => StartMaelstromInternal(captureCeiling: true);

        /// <summary>
        /// Play Again from the summary - same fresh-lobby reset, but PRESERVES the intensity ceiling.
        /// By the summary, <c>gameData.SelectedIntensity</c> holds the last game's rolled value (not the
        /// original ceiling), so re-capturing it would corrupt the ceiling; <see cref="ResetRuntime"/>
        /// already preserves it, so we just skip the re-capture. Runs on every peer (lobby load while
        /// phase is Summary), so the wipe is deterministic across the party.
        /// </summary>
        void RestartFromSummary() => StartMaelstromInternal(captureCeiling: false);

        void StartMaelstromInternal(bool captureCeiling)
        {
            _tournament.ResetRuntime();   // preserves IntensityCeiling
            _tournament.IsActive = true;

            // Resolve the race-to-N win target from the End Game Conditions tool (Resources/EndConditionOverrides,
            // edited via FrogletTools > Game Modes > End Game Conditions) once per shuffle start. Runs on every peer
            // from the same committed asset, so IsShuffleComplete stays deterministic across the party; falls
            // back to the asset's serialized WinTarget if the tool asset is missing.
            var endConditions = EndConditionOverridesSO.Instance;
            _tournament.ResolveWinTarget(endConditions != null ? endConditions.GetMaelstromWinTarget() : _tournament.WinTarget);

            // Capture the lobby-chosen intensity as the per-game CEILING (X); each game then draws a
            // random intensity in [1..X]. Set AFTER ResetRuntime so a fresh start re-captures the
            // player's current choice; skipped on Play Again so the original ceiling survives.
            if (captureCeiling)
                _tournament.IntensityCeiling = _gameData.SelectedIntensity != null
                    ? Mathf.Max(1, _gameData.SelectedIntensity.Value)
                    : 1;
            _gameData.IsMaelstromMode = true;
            _stateMachine.ResetToIdle();
            _stateMachine.TransitionTo(MaelstromPhase.Lobby);
            _tournament.OnMaelstromStarted.Raise();
        }

        /// <summary>
        /// Drives the state machine to the results <see cref="MaelstromPhase.Summary"/> when the shuffle
        /// is decided. Routed through <see cref="MaelstromPhase.Complete"/> so the Complete signal is still
        /// observed, but it does NOT depend on <see cref="HandleMiniGameEnd"/> having already set Complete:
        /// the win is authoritative via <see cref="MaelstromDataSO.IsShuffleComplete"/>, so the summary
        /// must show even if the per-game-end Complete transition was missed (e.g. the deciding game ended
        /// in a phase other than InGame). Idempotent and safe from Lobby / InGame / Complete (the only
        /// phases that occur at a mid-run Maelstrom load); runs on every peer, so it stays deterministic.
        /// </summary>
        void EnterSummary()
        {
            if (_stateMachine.Current == MaelstromPhase.Summary) return;
            if (_stateMachine.Current != MaelstromPhase.Complete)
                _stateMachine.TransitionTo(MaelstromPhase.Complete);
            _stateMachine.TransitionTo(MaelstromPhase.Summary);
        }

        /// <summary>
        /// Host DRAWS the next round (mode + intensity) without launching it, and stamps it on
        /// <see cref="MaelstromDataSO.PendingGameIndex"/> / <c>PendingIntensity</c>. Idempotent -
        /// a hub visit draws once, and every later call while that draw is still pending is a
        /// no-op, so the arena a player is looking at cannot change under them.
        ///
        /// <para>The draw used to happen at LAUNCH, on purpose: the upcoming mode stayed hidden
        /// until its connecting panel, and a client never needed to know it because the loaded
        /// scene told it. The hub now stands that mode's arena and lets the party fly it while
        /// the countdown runs, so the pick has to exist before the load and has to be the same
        /// pick everywhere - it is published by <c>MaelstromLobby</c> as a
        /// <see cref="MaelstromRoundTicket"/>.</para>
        ///
        /// <returns>True when a round is pending (drawn now or already).</returns>
        /// </summary>
        public bool PrepareNextRound()
        {
            if (!IsHost) return false;
            if (_tournament == null || !_tournament.IsActive) return false;
            if (_tournament.PendingGame != null) return true;
            return DrawNextRound();
        }

        /// <summary>
        /// Fix the tournament's FIELD: <see cref="MaelstromDataSO.SeatCount"/> pilots every round,
        /// the party's humans plus AI for the rest - and deal those AI once, here in the hub,
        /// rather than lazily in the first round that happens to backfill.
        ///
        /// <para><b>Why the seat count is not the launch modal's number.</b> The stepper on the
        /// arcade card is a preference for ONE match. A tournament is scored across sixteen of
        /// them, so a field that changed size between rounds would be scoring a different game
        /// each time - a three-player round and a four-player round are not comparable, and the
        /// placement table (<see cref="MaelstromDataSO.PointsByPlace"/>) is per DOMAIN, so the
        /// shape of the teams is the shape of the scoring. Four is the card's own maximum, so a
        /// full party of four brings no AI at all and a solo player brings three.</para>
        ///
        /// <para><b>Why the roster is dealt HERE.</b> It used to be dealt by the first round's
        /// spawner, which made the intro hub honest about nothing: the party readied up against a
        /// field that did not exist yet, and the bots they would race were decided by whichever
        /// game happened to load. Dealing at hub entry makes the roster a property of the
        /// TOURNAMENT, which is what "the same bot across all game modes" actually means. The
        /// spawner still owns the spawn - it simply replays a seat it now always finds already
        /// dealt (<c>ServerPlayerVesselInitializerWithAI.SpawnAIs</c>).</para>
        ///
        /// <para>The placement algorithm is the spawner's own
        /// (<c>ServerPlayerVesselInitializerWithAI.GetBalancedDomain</c>, called against the same
        /// two count dictionaries), deliberately rather than a second copy: identical inputs give
        /// identical seats, so moving WHEN the deal happens cannot change WHAT it deals.</para>
        ///
        /// <para>Host-only, and idempotent: seats already dealt are never re-dealt or re-balanced.
        /// That is the whole point - a bot that changed domain between rounds turned an opponent
        /// into a team-mate mid-tournament.</para>
        /// </summary>
        public void ApplyRoster(IReadOnlyList<IPlayer> humans)
        {
            if (!IsHost || _tournament == null || _gameData == null) return;

            // The caller's list is already human-only (the hub waits on people, not bots), but
            // BuildHumanCounts wants the concrete Player to read NetDomain off, and a null or an
            // AI slipping in would be counted as a seat nobody is sitting in.
            _rosterHumans.Clear();
            if (humans != null)
                for (int i = 0; i < humans.Count; i++)
                    if (humans[i] is Player pl && pl.IsSpawned && !pl.NetIsAI.Value)
                        _rosterHumans.Add(pl);

            int seats = _tournament.SeatCount;
            _gameData.ConfigurePlayerCounts(seats, _rosterHumans.Count);

            DealAISeats(Mathf.Max(0, seats - _rosterHumans.Count), _rosterHumans);
        }

        /// <summary>Scratch list for <see cref="ApplyRoster"/> - it runs on a host tick.</summary>
        readonly List<Player> _rosterHumans = new();

        /// <summary>
        /// Append seats until the roster holds <paramref name="aiWanted"/>. Existing seats are read
        /// back untouched and still bump the placement counts, so a bot dealt now is balanced
        /// against the ones already sitting rather than against an empty board.
        /// </summary>
        void DealAISeats(int aiWanted, List<Player> humans)
        {
            var seats = _tournament.MaelstromAISeats;
            if (seats.Count >= aiWanted) return;

            var activeDomains = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(
                _gameData.RequestedDomainCount);
            var humanCounts = GameDataSO.BuildHumanCounts(humans, activeDomains);
            var totalCounts = new Dictionary<Domains, int>(humanCounts);

            for (int i = 0; i < seats.Count; i++)
                if (totalCounts.ContainsKey(seats[i].Domain)) totalCounts[seats[i].Domain]++;

            // Drawn for the seats still to fill, so a re-deal after a player leaves does not
            // re-roll the names already sitting.
            var profiles = _tournament.AIProfileList != null
                ? _tournament.AIProfileList.PickRandom(aiWanted - seats.Count)
                : null;

            for (int drawn = 0; seats.Count < aiWanted; drawn++)
            {
                string name = profiles != null && drawn < profiles.Count
                    ? profiles[drawn].Name
                    : $"AI {seats.Count + 1}";

                var domain = ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totalCounts, humanCounts);
                totalCounts[domain]++;

                seats.Add(new MaelstromAISeat { Name = name, Domain = domain });
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Maelstrom] Roster: {aiWanted} AI seat(s) - " +
                string.Join(", ", seats.ConvertAll(s => $"{s.Name}/{s.Domain}")));
        }

        /// <summary>
        /// Host launches the round the hub has been previewing. Draws one first if nothing is
        /// pending, so a degraded hub (no preview, no draw) still starts a game rather than
        /// stalling. The party follows the Single load.
        /// </summary>
        public void BeginNextRound()
        {
            if (!IsHost) return;
            if (_tournament == null || !_tournament.IsActive) return;
            if (_tournament.PendingGame == null && !DrawNextRound()) return;
            LaunchPendingRound();
        }

        /// <summary>Back-compat alias for <see cref="BeginNextRound"/> (the first round is just the
        /// next round from a fresh lobby). Retained for the existing lobby view wiring.</summary>
        public void BeginFirstGame() => BeginNextRound();

        /// <summary>
        /// Host advances on the Scoreboard's host-only Continue button. ALWAYS returns the party to the
        /// Maelstrom scene: mid-run it shows the standings HUB (phase InGame → Lobby on load); once the
        /// shuffle is decided it shows the results SUMMARY (phase Complete → Summary on load). The next
        /// random game is drawn later, from the hub's ready-up (<see cref="BeginNextRound"/>), so the
        /// upcoming mode stays hidden in the hub. The party follows the Single load.
        /// </summary>
        public void AdvanceToNextGame()
        {
            if (!IsHost) return;
            if (_tournament == null || !_tournament.IsActive) return;

            LoadMaelstromScene();
        }

        /// <summary>
        /// Host restarts the whole tournament (the summary screen's Play Again). Loads the Maelstrom
        /// scene as a fresh lobby; the reset runs on every peer when it loads while still in Summary
        /// phase (see <see cref="HandleSceneLoaded"/> → <see cref="RestartFromSummary"/>), so standings
        /// clear consistently across the party while keeping the chosen intensity ceiling.
        /// </summary>
        public void RestartMaelstrom()
        {
            if (!IsHost) return;
            LoadMaelstromScene();
        }

        /// <summary>Clears tournament state on every peer (Menu_Main return / exit).</summary>
        public void EndMaelstrom()
        {
            _tournament.IsActive = false;
            _gameData.IsMaelstromMode = false;
            _stateMachine.ResetToIdle();
        }

        // ── Host-only random draw + scene load (reuse the proven SceneLoader path) ─

        /// <summary>
        /// Draws a random (mode, intensity ∈ [1..ceiling]) "experience" from the pool and stamps it
        /// as PENDING. The host draws; the pick reaches clients as a <see cref="MaelstromRoundTicket"/>
        /// (and the intensity still rides the existing <c>SyncGameConfigToClients</c> path at launch),
        /// so no shared RNG/seed is needed. The mode is drawn from a BAG: no mode repeats until every
        /// drawable mode has been played, and a refill still never deals the same mode back-to-back.
        /// </summary>
        bool DrawNextRound()
        {
            if (_tournament == null || _tournament.GameCount == 0) return false;

            int ceiling = Mathf.Clamp(_tournament.IntensityCeiling <= 0 ? 1 : _tournament.IntensityCeiling, 1, 4);

            // The lobby's intensity does TWO things: it caps each game's own intensity, and it
            // decides how wide the pool is (MaelstromDataSO.IntensityTiers). An un-authored
            // ladder returns the whole queue, so this is the legacy draw until tiers are written.
            var drawable = _tournament.GamesForIntensity(ceiling);
            if (drawable.Count == 0) return false;

            // A shuffle is a BAG, not a roll: every mode in the drawable pool is dealt once before
            // ANY mode comes round again. Drawing from the pool minus what has already been dealt
            // makes "no game repeats itself" a property of the draw rather than of a lucky roll -
            // where the old immediate-repeat guard left a 16-mode pool free to deal the same mode
            // on rounds 1, 3 and 5 of a race that is often only four rounds long.
            var candidates = new List<SO_ArcadeGame>();
            for (int i = 0; i < drawable.Count; i++)
                if (!_tournament.HasBeenDrawn(drawable[i])) candidates.Add(drawable[i]);

            // CurrentGameIndex holds the last loaded pool mode as a GameQueue index (set on scene
            // load). It is only needed when the bag REFILLS - inside a bag the previous mode has
            // already been dealt and cannot be a candidate - and it has to be mapped INTO the
            // candidate list, because that list is a subset and the index spaces are not the same.
            SO_ArcadeGame previous = null;
            if (_tournament.GamesPlayed > 0 &&
                _tournament.CurrentGameIndex >= 0 &&
                _tournament.CurrentGameIndex < _tournament.GameCount)
            {
                previous = _tournament.GameQueue[_tournament.CurrentGameIndex];
            }

            if (candidates.Count == 0)
            {
                // Bag empty: a shuffle longer than the pool has to come round again. Refill and
                // fall back to the old rule - still never back-to-back across the seam.
                _tournament.RefillDrawBag();
                candidates.AddRange(drawable);
            }

            int avoid = previous != null ? candidates.IndexOf(previous) : -1;

            var game = candidates[PickRandomIndex(candidates.Count, avoid)];
            if (game == null) return false;

            _tournament.MarkDrawn(game);

            int intensity = Random.Range(1, ceiling + 1);   // inclusive [1..ceiling]

            // Stamp the pick as PENDING. The GameQueue index is what travels (an SO_ArcadeGame has
            // no network identity), so it is taken from the queue rather than from `drawable`,
            // which is an intensity-filtered subset with a different index space.
            _tournament.PendingGameIndex = _tournament.GameQueue.IndexOf(game);
            _tournament.PendingIntensity = intensity;

            // Name what is coming so the between-game splash can show "up next: <mode> · Intensity N"
            // (see MaelstromStandingsFormatter.FormatRunning) and the hub can title its preview.
            _tournament.NextGameName = game.DisplayName;
            _tournament.NextGameIntensity = intensity;
            return true;
        }

        /// <summary>
        /// Host-only launch of whatever <see cref="DrawNextRound"/> left pending. Everything that
        /// used to sit at the tail of the draw lives here, so the pick and the load are separable:
        /// the hub draws early to preview the arena, and launches minutes later.
        /// </summary>
        void LaunchPendingRound()
        {
            var game = _tournament.PendingGame;
            if (game == null) return;

            int intensity = Mathf.Clamp(_tournament.PendingIntensity <= 0 ? 1 : _tournament.PendingIntensity, 1, 4);

            // Per-game intensity: set BEFORE SyncFromArcadeGame (which doesn't touch intensity) and
            // before launch, so the game scene's config sync replicates it to clients.
            if (_gameData.SelectedIntensity != null)
                _gameData.SelectedIntensity.Value = intensity;

            _tournament.NextGameName = game.DisplayName;
            _tournament.NextGameIntensity = intensity;

            // The pending round is now the CURRENT one. Cleared before the load rather than after,
            // because the load is what tears this scene down - a pending index surviving it would
            // have the next hub preview the round just played.
            _tournament.ClearPendingRound();

            _gameData.SyncFromArcadeGame(game);        // scene / mode / multiplayer
            _gameData.IsMaelstromMode = true;         // SyncFromArcadeGame doesn't set it; keep it on.

            // AFTER SyncFromArcadeGame, which republishes the card's own player range - the
            // tournament's field is fixed at SeatCount and does not take a per-mode preference.
            // The hub already applied this; re-applying is what covers a degraded launch (a
            // BeginNextRound that never went through a hub tick) rather than trusting it.
            ApplyRoster(_gameData.Players);
            _gameData.InvokeGameLaunch();              // → SceneLoader.LaunchGame (host loads; clients follow)
        }

        // Uniform random index into a list of `count`, optionally excluding `avoid`
        // (-1 or out of range = no exclusion).
        static int PickRandomIndex(int count, int avoid)
        {
            if (count <= 1) return 0;
            if (avoid < 0 || avoid >= count) return Random.Range(0, count);

            // Pick uniformly among the (count - 1) indices that are not `avoid`.
            int r = Random.Range(0, count - 1);
            return r < avoid ? r : r + 1;
        }

        // Loads the Maelstrom scene (the intro lobby on a fresh start, the results summary after
        // the shuffle is decided - the scene view picks the layout from the phase).
        void LoadMaelstromScene()
        {
            _gameData.SceneName = _tournament.LobbySceneName;
            _gameData.GameMode = CosmicShore.Data.GameModes.Maelstrom;
            _gameData.IsMultiplayerMode = true;
            _gameData.IsMaelstromMode = true;
            _gameData.InvokeGameLaunch();
        }
    }
}
