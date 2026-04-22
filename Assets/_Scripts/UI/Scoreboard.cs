// Scoreboard.cs — dynamic per-player cards, always multiplayer view
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
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// End-game scoreboard. Instantiates one <see cref="PlayerScoreCard"/> per player
    /// under <see cref="playerCardContainer"/>, tints each card to its player's domain
    /// color, and shows a "{DOMAIN} VICTORY" banner. Always uses the multiplayer layout;
    /// the legacy SinglePlayerView is never shown.
    ///
    /// Subclasses override <see cref="FormatPlayerScore"/> and optionally
    /// <see cref="FormatSecondaryStat"/> / <see cref="SortPlayers"/> to customize display.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("References")]
        [SerializeField] private MultiplayerMiniGameControllerBase multiplayerController;
        [SerializeField] private GameObject endGameObject;

        [Header("UI Containers")]
        [SerializeField] Transform scoreboardPanel;
        [SerializeField] Transform statsContainer;
        [SerializeField] StatRowUI statRowPrefab;

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] protected TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] Color JadeTeamBannerColor    = new Color(0.0f, 0.8f, 0.4f);
        [SerializeField] Color RubyTeamBannerColor    = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] Color GoldTeamBannerColor    = new Color(1.0f, 0.8f, 0.0f);
        [SerializeField] Color BlueTeamBannerColor    = new Color(0.2f, 0.4f, 0.9f);

        [Header("Player Score Cards")]
        [Tooltip("Container transform that will host one PlayerScoreCard per player (e.g. ScrollView/Viewport/Content).")]
        [SerializeField] protected Transform playerCardContainer;
        [Tooltip("Prefab instance to clone for each player row.")]
        [SerializeField] protected PlayerScoreCard playerCardPrefab;
        [Tooltip("Domain color palette for card background tint (falls back to banner colors if unassigned).")]
        [SerializeField] protected DomainColorPaletteSO domainColorPalette;
        [Tooltip("Profile icon list used to resolve player avatars by AvatarId.")]
        [SerializeField] protected SO_ProfileIconList profileIconList;
        [Tooltip("AI profile list used to resolve AI avatars by name.")]
        [SerializeField] protected SO_AIProfileList aiProfileList;

        [Header("Winner Crystal Reward")]
        [Tooltip("Crystals awarded to the winning player's card (+N indicator). Set 0 to disable.")]
        [SerializeField] protected int winnerCrystalReward = 5;

        [Header("Play Again")]
        [Tooltip("Play Again button — host only in multiplayer. Hidden for non-host clients; the host's Play Again forces everyone to replay.")]
        [SerializeField] private GameObject playAgainButton;

        [Header("Host / Client Buttons")]
        [Tooltip("Main Menu button — host only in multiplayer (host-initiated return takes everyone). Always visible in single-player.")]
        [SerializeField] private GameObject mainMenuButton;

        [Tooltip("Leave Lobby button — non-host clients only. Disconnects from the party session and returns to Menu_Main.")]
        [SerializeField] private GameObject leaveLobbyButton;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        #endregion

        #region Private Fields

        private ScoreboardStatsProvider statsProvider;
        private CanvasGroup _scoreboardCanvasGroup;
        private RectTransform _scoreboardRect;
        private Sequence _entranceSeq;
        private readonly List<PlayerScoreCard> _spawnedCards = new();

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            statsProvider = GetComponent<ScoreboardStatsProvider>();
            if (!statsProvider)
                CSDebug.LogWarning("[Scoreboard] No ScoreboardStatsProvider found.");
            HideScoreboard();
        }

        void OnEnable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += ShowScoreboard;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised += HideScoreboard;
        }

        void OnDisable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised -= HideScoreboard;
        }

        #endregion

        #region Core

        void ShowScoreboard()
        {
            if (!gameData) { CSDebug.LogError("[Scoreboard] GameData is null!"); return; }

            ConfigureLobbyButtons();
            ShowMultiplayerView();
            PopulateDynamicStats();

            if (scoreboardPanel)
            {
                scoreboardPanel.gameObject.SetActive(true);
                PlayEntranceAnimation();
            }
        }

        /// <summary>
        /// Shows Main Menu + Play Again for host / single-player, Leave Lobby for non-host clients.
        /// Non-host clients cannot restart the game — the host's Play Again forces everyone to replay,
        /// so exposing the button to clients would be misleading.
        /// </summary>
        void ConfigureLobbyButtons()
        {
            var nm = NetworkManager.Singleton;
            bool isMultiplayer = gameData != null && gameData.IsMultiplayerMode;
            bool isHost = nm != null && nm.IsServer;
            bool isClient = isMultiplayer && !isHost;

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

        void OnDestroy()
        {
            _entranceSeq?.Kill();
        }

        #endregion

        #region Multiplayer View (the only view)

        /// <summary>
        /// Always-on multiplayer presentation. The legacy "SinglePlayerView" is gone —
        /// solo play uses the multiplayer layout with a single card.
        /// </summary>
        protected virtual void ShowMultiplayerView()
        {
            var orderedStats = SortPlayers(gameData.RoundStatsList);

            Domains winnerDomain = DetermineWinnerDomain(orderedStats);
            SetBannerForDomain(winnerDomain);
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
        /// Default: first domain in DomainStatsList → first player's domain → Unassigned.
        /// Subclasses can override if they need different winner-domain logic.
        /// </summary>
        protected virtual Domains DetermineWinnerDomain(List<IRoundStats> orderedStats)
        {
            if (gameData.DomainStatsList is { Count: > 0 })
                return gameData.DomainStatsList[0].Domain;
            if (orderedStats is { Count: > 0 })
                return orderedStats[0].Domain;
            return Domains.Unassigned;
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
                CSDebug.LogWarning($"[Scoreboard] PopulatePlayerCards skipped — " +
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

                // Winner gets the "+N crystals" reward indicator
                if (winnerCrystalReward > 0 && stats.Name == winnerName)
                    card.ShowCrystalReward(winnerCrystalReward);

                _spawnedCards.Add(card);
            }

            // Award crystals once to the local player if they won (same side-effect that
            // EndGameCinematicController used to do — centralized here now)
            AwardCrystalsIfLocalWinner(winnerName);
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

        void AwardCrystalsIfLocalWinner(string winnerName)
        {
            if (winnerCrystalReward <= 0) return;
            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName) || localName != winnerName) return;

            var service = PlayerDataService.Instance;
            if (service == null) return;

            int newBalance = service.AddCrystals(winnerCrystalReward);
            CSDebug.Log($"[Scoreboard] Awarded {winnerCrystalReward} crystals to '{localName}'. New balance: {newBalance}");
        }

        Color GetDomainColor(Domains domain)
        {
            if (domainColorPalette)
                return domainColorPalette.Get(domain);

            return domain switch
            {
                Domains.Jade => JadeTeamBannerColor,
                Domains.Ruby => RubyTeamBannerColor,
                Domains.Gold => GoldTeamBannerColor,
                Domains.Blue => BlueTeamBannerColor,
                _            => SinglePlayerBannerColor,
            };
        }

        Sprite ResolveAvatarSprite(IRoundStats stats)
        {
            // AI players — look up by name in AI profile list (struct, not nullable)
            if (aiProfileList != null && aiProfileList.aiProfiles != null)
            {
                foreach (var p in aiProfileList.aiProfiles)
                {
                    if (p.Name == stats.Name)
                        return p.AvatarSprite;
                }
            }

            // Human players — look up by AvatarId via gameData.Players
            if (profileIconList != null && profileIconList.profileIcons != null && gameData?.Players != null)
            {
                var player = gameData.Players.FirstOrDefault(pl => pl.Name == stats.Name);
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
        /// Play Again is host-only in multiplayer — non-host clients don't see the button
        /// (see <see cref="ConfigureLobbyButtons"/>). A host click forces everyone to replay
        /// through the controller's server-authoritative reset pipeline.
        /// </summary>
        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null)
                UGSStatsManager.Instance.TrackPlayAgain();

            // Prefer the serialized MP reference; fall back to a scene-level lookup
            // so singleplayer scenes (which don't wire this field) also work.
            MiniGameControllerBase controller = multiplayerController;
            if (controller == null)
                controller = FindAnyObjectByType<MiniGameControllerBase>();

            if (controller == null)
            {
                CSDebug.LogError("[Scoreboard] No MiniGameControllerBase in scene — cannot restart.");
                return;
            }

            // Defense in depth: non-host clients don't see the button
            // (ConfigureLobbyButtons gates it), but guard the call path too.
            if (gameData.IsMultiplayerMode)
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsServer)
                {
                    CSDebug.LogWarning("[Scoreboard] Play Again ignored — only the host can restart the game.");
                    return;
                }
            }

            controller.RequestReplay();
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
                CSDebug.LogError("[Scoreboard] PartyInviteController not available — cannot leave lobby.");
                return;
            }

            PartyInviteController.Instance.LeavePartyAndReturnToMenuAsync().Forget();
        }

        #endregion
    }
}
