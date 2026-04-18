// Scoreboard.cs — dynamic per-player cards, always multiplayer view
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using Obvious.Soap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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

        [Header("Multiplayer Rematch")]
        [Tooltip("Shown to the player who SENT the request — auto-dismisses after 2s if no response")]
        [SerializeField] private GameObject rematchInvitedPanel;

        [Tooltip("Shown to the player who RECEIVED the request — stays until Yes/No pressed")]
        [SerializeField] private GameObject rematchReceivedPanel;
        [SerializeField] private TMP_Text rematchReceivedText;

        [Tooltip("Shown to the requester when DENIED — auto-dismisses after 2s")]
        [SerializeField] private GameObject rematchDeniedPanel;
        [SerializeField] private TMP_Text rematchDeniedText;

        [Tooltip("Play Again button — hidden while rematch request is pending")]
        [SerializeField] private GameObject playAgainButton;

        [Tooltip("Seconds before invited/denied panels auto-dismiss")]
        [SerializeField] private float rematchPanelAutoDismissSeconds = 2f;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        #endregion

        #region Private Fields

        private ScoreboardStatsProvider statsProvider;
        private Coroutine _invitedAutoDismiss;
        private Coroutine _deniedAutoDismiss;
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

            HideAllRematchPanels();
            ShowMultiplayerView();
            PopulateDynamicStats();

            if (scoreboardPanel)
            {
                scoreboardPanel.gameObject.SetActive(true);
                PlayEntranceAnimation();
            }
        }

        void HideScoreboard()
        {
            _entranceSeq?.Kill();
            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(false);
            if (endGameObject) endGameObject.SetActive(false);
            HideAllRematchPanels();
            ClearPlayerCards();
        }

        void HideAllRematchPanels()
        {
            StopAutoDismiss(ref _invitedAutoDismiss);
            StopAutoDismiss(ref _deniedAutoDismiss);

            if (rematchInvitedPanel)  rematchInvitedPanel.SetActive(false);
            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(false);
            if (rematchDeniedPanel)   rematchDeniedPanel.SetActive(false);
            if (playAgainButton)      playAgainButton.SetActive(true);
        }

        void StopAutoDismiss(ref Coroutine coroutine)
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
            coroutine = null;
        }

        IEnumerator AutoDismissPanel(GameObject panel, float delay, Action onDismiss = null)
        {
            yield return new WaitForSeconds(delay);
            if (panel) panel.SetActive(false);
            onDismiss?.Invoke();
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
            // AI players — look up by name in AI profile list
            if (aiProfileList != null && aiProfileList.aiProfiles != null)
            {
                foreach (var p in aiProfileList.aiProfiles)
                {
                    if (p != null && p.Name == stats.Name)
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

        #region Play Again / Rematch

        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null)
                UGSStatsManager.Instance.TrackPlayAgain();

            if (gameData.IsMultiplayerMode)
            {
                if (multiplayerController == null)
                {
                    CSDebug.LogError("[Scoreboard] multiplayerController not assigned!");
                    return;
                }

                if (playAgainButton)     playAgainButton.SetActive(false);
                if (rematchInvitedPanel) rematchInvitedPanel.SetActive(true);

                StopAutoDismiss(ref _invitedAutoDismiss);
                _invitedAutoDismiss = StartCoroutine(AutoDismissPanel(
                    rematchInvitedPanel,
                    rematchPanelAutoDismissSeconds,
                    onDismiss: () => { if (playAgainButton) playAgainButton.SetActive(true); }
                ));

                multiplayerController.RequestRematch(gameData.LocalPlayer.Name);
            }
            else if (multiplayerController != null)
            {
                multiplayerController.RequestReplay();
            }
            else
            {
                gameData.ResetForReplay();
            }
        }

        public void ShowRematchRequest(string requesterName)
        {
            if (rematchReceivedText)
                rematchReceivedText.text = $"{requesterName} wants a rematch!";

            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(true);
            if (playAgainButton)      playAgainButton.SetActive(false);
        }

        public void OnAcceptRematch()
        {
            HideAllRematchPanels();
            multiplayerController?.RequestReplay();
        }

        public void OnDeclineRematch()
        {
            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(false);
            if (playAgainButton)      playAgainButton.SetActive(true);
            multiplayerController?.NotifyRematchDeclined(gameData.LocalPlayer.Name);
        }

        public void ShowRematchDeclined(string declinerName)
        {
            StopAutoDismiss(ref _invitedAutoDismiss);
            if (rematchInvitedPanel) rematchInvitedPanel.SetActive(false);

            if (rematchDeniedText)
                rematchDeniedText.text = $"{declinerName} declined the rematch.";

            if (rematchDeniedPanel) rematchDeniedPanel.SetActive(true);

            StopAutoDismiss(ref _deniedAutoDismiss);
            _deniedAutoDismiss = StartCoroutine(AutoDismissPanel(
                rematchDeniedPanel,
                rematchPanelAutoDismissSeconds,
                onDismiss: () => { if (playAgainButton) playAgainButton.SetActive(true); }
            ));
        }

        #endregion
    }
}
