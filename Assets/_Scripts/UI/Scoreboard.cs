// Scoreboard.cs - dynamic per-player cards, always multiplayer view
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Obvious.Soap;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// End-game scoreboard. Instantiates one <see cref="PlayerScoreCard"/> per player
    /// under <see cref="playerCardContainer"/>, tints each card to its player's domain
    /// color, and shows a "{DOMAIN} VICTORY" banner.
    ///
    /// Subclasses override <see cref="FormatPlayerScore"/> and optionally
    /// <see cref="FormatSecondaryStat"/> / <see cref="SortPlayers"/> to customize display.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;
        [Tooltip("Shuffle (Tournament meta) data - source of the per-domain placement crystals when " +
                 "IsTournamentMode. Leave null for non-tournament scenes; the reward then stays the flat winner reward.")]
        [SerializeField] protected TournamentDataSO tournamentData;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("References")]
        [Tooltip("Game controller for this mode. Wire the scene's MiniGameControllerBase subclass (SP or MP).")]
        [FormerlySerializedAs("multiplayerController")]
        [SerializeField] private MiniGameControllerBase gameController;
        [SerializeField] private GameObject endGameObject;

        [Header("UI Containers")]
        [SerializeField] Transform scoreboardPanel;
        [SerializeField] Transform statsContainer;
        [SerializeField] StatRowUI statRowPrefab;

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] protected TMP_Text BannerText;

        [Header("Player Score Cards")]
        [Tooltip("Container transform that will host one PlayerScoreCard per player (e.g. ScrollView/Viewport/Content).")]
        [SerializeField] protected Transform playerCardContainer;
        [Tooltip("Prefab instance to clone for each player row.")]
        [SerializeField] protected PlayerScoreCard playerCardPrefab;
        [Tooltip("Profile icon list used to resolve player avatars by AvatarId.")]
        [SerializeField] protected SO_ProfileIconList profileIconList;
        [Tooltip("AI profile list used to resolve AI avatars by name.")]
        [SerializeField] protected SO_AIProfileList aiProfileList;

        [Header("Crystal Rewards")]
        [Tooltip("Crystals by finishing place, best first: index 0 = 1st, index 1 = 2nd, and so on. " +
                 "Places past the end of the list earn 0, and LAST place always earns 0 regardless " +
                 "of the list. Applies to every mode, tournament included.")]
        [SerializeField] protected List<int> placementCrystalRewards = new() { 200, 50, 0 };

        [Header("Play Again")]
        [Tooltip("Play Again button - host only in multiplayer. Hidden for non-host clients; the host's Play Again forces everyone to replay.")]
        [SerializeField] private GameObject playAgainButton;

        [Header("Host / Client Buttons")]
        [Tooltip("Main Menu button - host only in multiplayer (host-initiated return takes everyone). Always visible in single-player.")]
        [SerializeField] private GameObject mainMenuButton;

        [Tooltip("Leave Lobby button - non-host clients only. Disconnects from the party session and returns to Menu_Main.")]
        [SerializeField] private GameObject leaveLobbyButton;

        [Tooltip("Main-menu SOAP event - the same asset the Main Menu button raises (via PauseMenu.OnClickMainMenu) and SceneLoader listens to. When it fires, the host nav buttons hide so the transition can't be spam-clicked.")]
        [SerializeField] private ScriptableEventNoParam onClickToMainMenu;

        [Header("Tournament")]
        [Tooltip("Continue button - HOST ONLY, shown after EVERY tournament game (incl. the last). " +
                 "Mid-lineup it advances the party to the next game; on the last game it loads the " +
                 "Tournament results screen. Wire its onClick to OnContinueButtonPressed(). " +
                 "Leave unassigned in non-tournament scenes.")]
        [SerializeField] private GameObject continueButton;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        #endregion

        #region Private Fields

        private ScoreboardStatsProvider statsProvider;
        private CanvasGroup _scoreboardCanvasGroup;
        private RectTransform _scoreboardRect;
        private Sequence _entranceSeq;
        private readonly List<PlayerScoreCard> _spawnedCards = new();

        // Mode rule's team-total domain placement for the game just shown (index 0 = 1st);
        // null outside tournament mode. Computed once per ShowScoreboard, consumed by the
        // shuffle crystal badge + wallet award so they match the standings fold exactly.
        private List<Domains> _shufflePlacement;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            statsProvider = GetComponent<ScoreboardStatsProvider>();
            if (!statsProvider)
                CSDebug.LogWarning("[Scoreboard] No ScoreboardStatsProvider found.");

            ResolveGameController();
            HideScoreboard();
        }

        /// <summary>
        /// Finds the scene's game controller when the inspector reference is empty.
        ///
        /// This exists so GameCanvas.prefab can be dropped into a NEW game-mode scene and work
        /// without hand-wiring: there is exactly one <see cref="MiniGameControllerBase"/> per
        /// gameplay scene, and it is always the one Play Again must talk to. An explicit inspector
        /// assignment still wins, so existing scenes are unaffected.
        ///
        /// Leaving this to per-scene wiring is what turned a shared prefab into N hand-maintained
        /// copies - every scene had to carry its own override just to point at its own controller.
        /// </summary>
        void ResolveGameController()
        {
            if (gameController != null) return;

            gameController = FindAnyObjectByType<MiniGameControllerBase>(FindObjectsInactive.Include);

            if (gameController == null)
            {
                // Not an error: menu / tool scenes legitimately host GameCanvas with no controller.
                CSDebug.Log("[Scoreboard] No MiniGameControllerBase in this scene - Play Again is unavailable.");
            }
        }

        void OnEnable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += ShowScoreboard;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised += HideScoreboard;

            // Hide the host nav buttons the moment the main-menu transition is
            // committed (the event only fires after PauseMenu's host guard), so the
            // host can't spam Main Menu / Play Again while the scene unloads.
            if (onClickToMainMenu != null) onClickToMainMenu.OnRaised += HideHostNavButtons;
        }

        void OnDisable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised -= HideScoreboard;

            if (onClickToMainMenu != null) onClickToMainMenu.OnRaised -= HideHostNavButtons;
        }

        #endregion

        #region Core

        void ShowScoreboard()
        {
            if (!gameData) { CSDebug.LogError("[Scoreboard] GameData is null!"); return; }

// Fail OPEN: the panel (with its Main Menu / Play Again buttons) must appear even
            // when a population step throws — otherwise the player has no way back to the menu.
            try
            {
                // Crystal payouts are placement-based in EVERY mode now, so resolve the team-total
                // domain order whenever a rule exists - not only in tournament. Without a rule,
                // CrystalsForPlacement falls back to the rank order already present in Results.
                _shufflePlacement = gameData.ScoringRule != null
                    ? gameData.ScoringRule.ResolvePlacementOrder(gameData)
                    : null;

                ConfigureLobbyButtons();
                ShowMultiplayerView();
                PopulateDynamicStats();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                CSDebug.LogError("[Scoreboard] Population threw — showing the panel anyway so the player can exit.");
            }

            if (scoreboardPanel)
            {
                scoreboardPanel.gameObject.SetActive(true);

                // Entrance animation disabled for now: PlayEntranceAnimation() slid the panel
                // by mutating its own anchoredPosition (read current pos as the rest target,
                // shoved it down by `offset`, tweened back via DOAnchorPos) and never restored
                // it on HideScoreboard(). On a stretch-anchored panel a re-entrant/interrupted
                // show captured the already-displaced position as the new rest target, so the
                // panel drifted off-base in modes that re-show the board (Joust / Crystal Capture).
                // Show at the authored position with no slide. (Re-enable once the rest pos is
                // captured at Awake and restored on hide.)
                ShowScoreboardImmediate();
            }
        }

        /// <summary>
        /// Shows Main Menu + Play Again for the host, Leave Lobby for non-host clients.
        /// Non-host clients cannot restart the game - the host's Play Again forces everyone to replay,
        /// so exposing the button to clients would be misleading.
        /// </summary>
        void ConfigureLobbyButtons()
        {
            var nm = NetworkManager.Singleton;
            bool isClient = nm == null || !nm.IsServer;

            // Tournament mode: the host gets Continue on EVERY game (including the last) and
            // clients see no buttons. Continue on the last game takes the party to the Tournament
            // results screen, which is where Play Again / Main Menu live now - so they are never
            // shown on the per-game scoreboard here.
            if (gameData != null && gameData.IsTournamentMode)
            {
                bool isHost = !isClient;
                if (continueButton)   continueButton.SetActive(isHost);
                if (playAgainButton)  playAgainButton.SetActive(false);
                if (mainMenuButton)   mainMenuButton.SetActive(false);
                if (leaveLobbyButton) leaveLobbyButton.SetActive(false);
                return;
            }

            // Normal (non-tournament) game: host gets Main Menu + Play Again, clients get Leave.
            if (continueButton)   continueButton.SetActive(false);
            if (mainMenuButton)   mainMenuButton.SetActive(!isClient);
            if (leaveLobbyButton) leaveLobbyButton.SetActive(isClient);
            if (playAgainButton)  playAgainButton.SetActive(!isClient);
        }

        void HideScoreboard()
        {
            _entranceSeq?.Kill();
            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(false);
            if (endGameObject) endGameObject.SetActive(false);
            ClearPlayerCards();
        }

        void PlayEntranceAnimation()
        {
            if (!scoreboardPanel) return;

            _entranceSeq?.Kill();

            if (!_scoreboardRect)
                _scoreboardRect = scoreboardPanel.GetComponent<RectTransform>();
            if (!_scoreboardCanvasGroup)
            {
                _scoreboardCanvasGroup = scoreboardPanel.GetComponent<CanvasGroup>();
                if (!_scoreboardCanvasGroup)
                    _scoreboardCanvasGroup = scoreboardPanel.gameObject.AddComponent<CanvasGroup>();
            }

            float duration = animSettings ? animSettings.scoreboardEntranceDuration : 0.35f;
            float offset = animSettings ? animSettings.scoreboardSlideOffset : 120f;
            var ease = animSettings ? animSettings.scoreboardEntranceEase : Ease.OutCubic;
            float bannerPunchDur = animSettings ? animSettings.bannerPunchDuration : 0.3f;
            float bannerPunchScale = animSettings ? animSettings.bannerPunchScale : 1.2f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            var targetPos = _scoreboardRect.anchoredPosition;
            _scoreboardRect.anchoredPosition = new Vector2(targetPos.x, targetPos.y - offset);
            _scoreboardCanvasGroup.alpha = 0f;

            _entranceSeq = DOTween.Sequence()
                .Join(_scoreboardRect.DOAnchorPos(targetPos, duration).SetEase(ease))
                .Join(_scoreboardCanvasGroup.DOFade(1f, duration));

            if (BannerText)
            {
                BannerText.transform.localScale = Vector3.one * 0.5f;
                _entranceSeq.Join(BannerText.transform
                    .DOScale(bannerPunchScale, bannerPunchDur * 0.5f)
                    .SetEase(Ease.OutBack));
                _entranceSeq.Append(BannerText.transform
                    .DOScale(1f, bannerPunchDur * 0.5f)
                    .SetEase(Ease.OutQuad));
            }

            _entranceSeq.SetUpdate(unscaled);
        }

        /// <summary>
        /// Shows the scoreboard at its authored position with no slide/fade. Replaces
        /// <see cref="PlayEntranceAnimation"/> while the entrance slide is disabled - see the
        /// note in <see cref="ShowScoreboard"/>. Forces full alpha and unit banner scale in case
        /// a previously-killed entrance tween left either mid-animation.
        /// </summary>
        void ShowScoreboardImmediate()
        {
            if (!scoreboardPanel) return;

            _entranceSeq?.Kill();

            var cg = scoreboardPanel.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 1f;

            if (BannerText) BannerText.transform.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            _entranceSeq?.Kill();
        }

        #endregion

        #region Multiplayer View (the only view)

        /// <summary>
        /// Builds the per-player card layout. Solo play renders as a single card.
        /// </summary>
        protected virtual void ShowMultiplayerView()
        {
            // Prefer the single source of truth: the mode's ranked + formatted results
            // (GameDataSO.Results, produced once by the ScoringRule). Modes that don't
            // produce results (freestyle, DuelForCell) fall back to the legacy path.
            if (gameData.Results is { Count: > 0 })
            {
                var winnerDomain = gameData.WinnerDomain != Domains.Blue
                    ? gameData.WinnerDomain
                    : gameData.Results[0].Domain;
                SetBannerForDomain(winnerDomain);
                PopulateFromResults(gameData.Results);
                return;
            }

            var orderedStats = SortPlayers(gameData.RoundStatsList);
            SetBannerForDomain(DetermineWinnerDomain(orderedStats));
            PopulatePlayerCards(orderedStats);
        }

        /// <summary>
        /// Default ordering uses DomainStatsList if present (winner first), otherwise
        /// returns the list as-is. Subclasses override to apply mode-specific sorting
        /// (golf rules, crystals, etc).
        /// </summary>
        protected virtual List<IRoundStats> SortPlayers(List<IRoundStats> stats)
        {
            if (stats == null) return new List<IRoundStats>();
            return stats.ToList();
        }

        /// <summary>
        /// Winning domain for the banner. Prefers the server-authoritative
        /// <see cref="GameDataSO.WinnerDomain"/> - the SAME value the end-game
        /// cinematic uses - so the banner and the cinematic can't disagree on a tie
        /// and there is one source of truth for "who won". Modes that don't set it
        /// (single-player / co-op / DuelForCell) leave it <see cref="Domains.Blue"/>
        /// - it is reset on every scene load (SceneLoader → GameDataSO.ResetRuntimeData)
        /// and on replay (ResetRuntimeDataForReplay) - and fall back to the per-domain
        /// sum order exactly as before. Subclasses may override.
        /// (Interim step: R10 will replace WinnerDomain + DomainStatsList with one
        /// synced ranked-results list - see Docs/ScoringSystem/REFACTOR.md.)
        /// </summary>
        protected virtual Domains DetermineWinnerDomain(List<IRoundStats> orderedStats)
        {
            if (gameData.WinnerDomain != Domains.Blue)
                return gameData.WinnerDomain;
            if (gameData.DomainStatsList is { Count: > 0 })
                return gameData.DomainStatsList[0].Domain;
            if (orderedStats is { Count: > 0 })
                return orderedStats[0].Domain;
            return Domains.Blue;
        }

        /// <summary>
        /// Subclasses override to format each player's primary score (time, crystals, etc).
        /// </summary>
        protected virtual string FormatPlayerScore(IRoundStats stats)
        {
            return ((int)stats.Score).ToString();
        }

        /// <summary>
        /// Optional secondary line (e.g. "Jousts: 3"). Return null or empty to hide.
        /// </summary>
        protected virtual string FormatSecondaryStat(IRoundStats stats) => null;

        protected virtual void SetBannerForDomain(Domains domain)
        {
            string domainLabel = domain switch
            {
                Domains.Jade => "JADE",
                Domains.Ruby => "RUBY",
                Domains.Gold => "GOLD",
                Domains.Blue => "BLUE",
                _            => null,
            };

            if (BannerImage) BannerImage.color = GetDomainColor(domain);
            if (BannerText)
            {
                BannerText.text = string.IsNullOrEmpty(domainLabel)
                    ? "GAME OVER"
                    : $"{domainLabel} VICTORY";
            }
        }

        void PopulatePlayerCards(List<IRoundStats> orderedStats)
        {
            ClearPlayerCards();

            if (orderedStats == null || orderedStats.Count == 0) return;
            if (!playerCardContainer || !playerCardPrefab)
            {
                CSDebug.LogWarning($"[Scoreboard] PopulatePlayerCards skipped - " +
                    $"container={(playerCardContainer != null ? "OK" : "NULL")}, " +
                    $"prefab={(playerCardPrefab != null ? "OK" : "NULL")}");
                return;
            }

            string winnerName = orderedStats[0].Name;

            for (int i = 0; i < orderedStats.Count; i++)
            {
                var stats = orderedStats[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);

                Color domainColor = GetDomainColor(stats.Domain);
                card.Setup(stats.Name, FormatPlayerScore(stats), domainColor, i);

                card.SetAvatar(ResolveAvatarSprite(stats));

                string secondary = FormatSecondaryStat(stats);
                if (!string.IsNullOrEmpty(secondary))
                    card.ShowSecondaryStat(secondary);

                // Reward badge: shuffle = this card's DOMAIN per-game {2,1,0}; else winner-only flat.
                int reward = CardCrystalReward(stats.Domain, stats.Name, winnerName);
                if (reward > 0)
                    card.ShowCrystalReward(reward);

                _spawnedCards.Add(card);
            }

            // The scoreboard is the single crystal-award path (local player only).
            AwardCrystalsToLocalPlayer(winnerName);
        }

        /// <summary>
        /// Renders cards from the single source of truth - the mode's ranked, formatted
        /// <see cref="GameDataSO.Results"/>. Order, primary text and secondary line all come
        /// from the ScoringRule, so the scoreboard and the end-game cinematic can't disagree.
        /// </summary>
        void PopulateFromResults(List<ScoreResult> results)
        {
            ClearPlayerCards();

            if (results == null || results.Count == 0) return;
            if (!playerCardContainer || !playerCardPrefab)
            {
                CSDebug.LogWarning($"[Scoreboard] PopulateFromResults skipped - " +
                    $"container={(playerCardContainer != null ? "OK" : "NULL")}, " +
                    $"prefab={(playerCardPrefab != null ? "OK" : "NULL")}");
                return;
            }

            string winnerName = results[0].Name;

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);

                card.Setup(r.Name, r.ScoreText, GetDomainColor(r.Domain), i);
                card.SetAvatar(ResolveAvatarSpriteByName(r.Name));

                if (!string.IsNullOrEmpty(r.Secondary))
                    card.ShowSecondaryStat(r.Secondary);

                int reward = CardCrystalReward(r.Domain, r.Name, winnerName);
                if (reward > 0)
                    card.ShowCrystalReward(reward);

                _spawnedCards.Add(card);
            }

            AwardCrystalsToLocalPlayer(winnerName);
        }

        void ClearPlayerCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card) Destroy(card.gameObject);
            }
            _spawnedCards.Clear();

            // Safety: also destroy any leftover cards in the container (e.g. editor-placed templates)
            if (playerCardContainer)
            {
                foreach (Transform child in playerCardContainer)
                    Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// The single crystal-award path (the Scoreboard is the only writer of the wallet). The
        /// local player earns their DOMAIN's placement crystals from
        /// <see cref="placementCrystalRewards"/> - the same table in every mode, tournament
        /// included - credited on every peer for its own local human, once per game.
        /// </summary>
        void AwardCrystalsToLocalPlayer(string winnerName)
        {
            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName)) return;

            var service = PlayerDataService.Instance;
            if (service == null) return;

            var localDomain = gameData.LocalRoundStats != null ? gameData.LocalRoundStats.Domain : Domains.Blue;
            int amount = CrystalsForPlacement(localDomain);
            string source = gameData.IsTournamentMode ? "tournament_placement" : "game_placement";

            if (amount <= 0) return;   // e.g. a last-place domain earns nothing this game

            // Wallet write is an external-service boundary: it runs mid-way through building the
            // end-game screen (before the panel activates), so a service hiccup must degrade to a
            // lost reward log line - never to a missing scoreboard.
            try
            {
                int newBalance = service.AddCrystals(amount, source);
                CSDebug.Log($"[Scoreboard] Awarded {amount} crystals to '{localName}' ({source}). New balance: {newBalance}");
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[Scoreboard] Crystal award failed for '{localName}' ({source}, {amount}): {e}");
            }
        }

        /// <summary>
        /// The "+N crystals" badge amount for one card. Shuffle/tournament: the card's DOMAIN per-game
        /// placement ({2,1,0}); otherwise the winner-only flat reward (0 for non-winners). 0 = no badge.
        /// </summary>
        int CardCrystalReward(Domains domain, string name, string winnerName) =>
            CrystalsForPlacement(domain);

        /// <summary>
        /// Crystals earned by a domain, from its finishing place. One table for every mode:
        /// 1st and 2nd pay out, last place always pays nothing.
        ///
        /// Placement comes from the mode rule's team-total order when one exists (the same
        /// aggregation that decides WinnerDomain), otherwise from the rank order already baked into
        /// <c>gameData.Results</c>. A domain that never appears earns nothing.
        /// </summary>
        int CrystalsForPlacement(Domains domain)
        {
            if (placementCrystalRewards == null || placementCrystalRewards.Count == 0) return 0;

            var order = _shufflePlacement;
            if (order == null || order.Count == 0)
            {
                // Rank-derived fallback: Results are already sorted best-first by the mode.
                if (gameData.Results == null || gameData.Results.Count == 0) return 0;
                order = new List<Domains>();
                foreach (var r in gameData.Results)
                    if (!order.Contains(r.Domain)) order.Add(r.Domain);
            }

            int index = order.IndexOf(domain);
            if (index < 0) return 0;

            // Last place earns nothing even if the table would pay it - with two domains that makes
            // the runner-up a loser, not a silver medallist, which is the intended read.
            if (order.Count > 1 && index == order.Count - 1) return 0;

            return index < placementCrystalRewards.Count
                ? Mathf.Max(0, placementCrystalRewards[index])
                : 0;
        }

        // Single source of truth for domain color: the same ColorSet the vessels and
        // prisms read from (GameDataSO.ThemeManagerData.ColorSet -> TrailHighlightColor).
        // No per-Scoreboard palette or hardcoded fallbacks - see Docs/ScoringSystem/REFACTOR.md R5.
        Color GetDomainColor(Domains domain)
        {
            return gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.gray;
        }

        Sprite ResolveAvatarSprite(IRoundStats stats) => ResolveAvatarSpriteByName(stats.Name);

        Sprite ResolveAvatarSpriteByName(string playerName)
        {
            // AI players - look up by name in AI profile list (struct, not nullable)
            if (aiProfileList != null && aiProfileList.aiProfiles != null)
            {
                foreach (var p in aiProfileList.aiProfiles)
                {
                    if (p.Name == playerName)
                        return p.AvatarSprite;
                }
            }

            // Human players - look up by AvatarId via gameData.Players
            if (profileIconList != null && profileIconList.profileIcons != null && gameData?.Players != null)
            {
                var player = gameData.Players.FirstOrDefault(pl => pl.Name == playerName);
                if (player != null)
                {
                    foreach (var icon in profileIconList.profileIcons)
                    {
                        if (icon.Id == player.AvatarId) return icon.IconSprite;
                    }
                    if (profileIconList.profileIcons.Count > 0)
                        return profileIconList.profileIcons[0].IconSprite;
                }
            }

            return null;
        }

        #endregion

        #region Dynamic Stats

        void PopulateDynamicStats()
        {
            if (statsContainer)
                foreach (Transform child in statsContainer)
                    Destroy(child.gameObject);

            if (!statsProvider || !statsContainer || !statRowPrefab)
            {
                return;
            }

            var stats = statsProvider.GetStats();
            if (stats == null) return;

            foreach (var stat in stats)
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Initialize(stat.Label, stat.Value, stat.Icon);
            }
        }

        #endregion

        #region Play Again

        /// <summary>
        /// Play Again is host-only in multiplayer - non-host clients don't see the button
        /// (see <see cref="ConfigureLobbyButtons"/>). A host click forces everyone to replay
        /// through the controller's server-authoritative reset pipeline.
        /// </summary>
        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null)
                UGSStatsManager.Instance.TrackPlayAgain();

            // Defense in depth: non-host clients don't see the button
            // (ConfigureLobbyButtons gates it), but guard the call path too.
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                CSDebug.LogWarning("[Scoreboard] Play Again ignored - only the host can restart the game.");
                return;
            }

            // In tournament mode Play Again is not shown on the per-game scoreboard (it lives on
            // the Tournament results screen via TournamentSceneView). So this path is only ever
            // reached by non-tournament games.

            if (gameController == null)
            {
                CSDebug.LogError("[Scoreboard] gameController not assigned - wire the scene's MiniGameControllerBase in the inspector.");
                return;
            }

            HideHostNavButtons();
            gameController.RequestReplay();
        }

        /// <summary>
        /// Host-only "Continue" handler (tournament mode). Shown after every tournament game incl.
        /// the last (see <see cref="ConfigureLobbyButtons"/>): mid-lineup it advances the party to
        /// the next game; on the last game <see cref="TournamentController.AdvanceToNextGame"/> loads
        /// the Tournament results screen instead. Wire the Continue button's onClick here.
        /// </summary>
        public void OnContinueButtonPressed()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsServer)
            {
                CSDebug.LogWarning("[Scoreboard] Continue ignored - only the host advances the tournament.");
                return;
            }

            HideHostNavButtons();
            if (TournamentController.Instance != null)
                TournamentController.Instance.AdvanceToNextGame();
            else
                CSDebug.LogError("[Scoreboard] TournamentController.Instance is null - cannot advance the tournament.");
        }

        /// <summary>
        /// Hides the host-only navigation buttons (Play Again + Main Menu + Continue) once a
        /// navigation action is committed - a button clicked or the main-menu SOAP event raised -
        /// so the host can't spam-click during the scene transition. The next game end
        /// re-activates the right ones via <see cref="ConfigureLobbyButtons"/>.
        /// </summary>
        void HideHostNavButtons()
        {
            if (playAgainButton) playAgainButton.SetActive(false);
            if (mainMenuButton)  mainMenuButton.SetActive(false);
            if (continueButton)  continueButton.SetActive(false);
        }

        /// <summary>
        /// Client-side "Leave Lobby" button handler. Disconnects from the host's party
        /// session and returns to Menu_Main. Host/single-player users see the regular
        /// Main Menu button instead (which is wired to the SOAP main-menu event).
        /// </summary>
        public void OnLeaveLobbyButtonPressed()
        {
            if (leaveLobbyButton) leaveLobbyButton.SetActive(false);

            if (PartyInviteController.Instance == null)
            {
                CSDebug.LogError("[Scoreboard] PartyInviteController not available - cannot leave lobby.");
                return;
            }

            PartyInviteController.Instance.LeavePartyAndReturnToMenuAsync().Forget();
        }

        #endregion
    }
}
