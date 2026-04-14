// Scoreboard.cs — rematch panels auto-dismiss after 2s (except received panel)
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using DG.Tweening;
using Obvious.Soap;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using CosmicShore.Utility;

namespace CosmicShore.Game.UI
{
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

        [Header("Single Player View")]
        [SerializeField] protected Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multiplayer View — Team Scorecards")]
        [SerializeField] protected Transform MultiplayerView;
        [Tooltip("Assign the 3 TeamScorecard components from the MultiplayerView hierarchy (winner displayed first).")]
        [SerializeField] private TeamScorecard[] teamScorecards;
        [SerializeField] private DomainColorPaletteSO domainColorPalette;

        [Header("Multiplayer View — Legacy (used by game-mode subclasses)")]
        [SerializeField] protected List<TMP_Text> PlayerNameTextFields;
        [SerializeField] protected List<TMP_Text> PlayerScoreTextFields;

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
        private Tween _scoreCounterTween;
        private Tween _highScoreCounterTween;

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

            // Show team-based multiplayer view when there are multiple teams
            // (online multiplayer, solo-with-AI in multiplayer scenes, or
            // single-player minigames with AI opponents filling team slots).
            bool hasMultipleTeams = gameData.DomainStatsList is { Count: > 1 };
            if (gameData.IsMultiplayerMode || multiplayerController != null || hasMultipleTeams)
                ShowMultiplayerView();
            else
                ShowSinglePlayerView();

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
            if(endGameObject) endGameObject.SetActive(false);
            HideAllRematchPanels();
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

            // Panel slide + fade
            var targetPos = _scoreboardRect.anchoredPosition;
            _scoreboardRect.anchoredPosition = new Vector2(targetPos.x, targetPos.y - offset);
            _scoreboardCanvasGroup.alpha = 0f;

            _entranceSeq = DOTween.Sequence()
                .Join(_scoreboardRect.DOAnchorPos(targetPos, duration).SetEase(ease))
                .Join(_scoreboardCanvasGroup.DOFade(1f, duration));

            // Banner text punch
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

        private void AnimateCounter(TMP_Text field, int target, ref Tween tween)
        {
            if (!field) return;

            tween?.Kill();
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            field.text = "0";
            float current = 0f;
            tween = DOTween.To(() => current, x => current = x, target, 0.6f)
                .SetDelay(0.15f)
                .SetEase(Ease.OutCubic)
                .OnUpdate(() => field.text = Mathf.RoundToInt(current).ToString())
                .SetUpdate(unscaled);
        }

        void OnDestroy()
        {
            _entranceSeq?.Kill();
            _scoreCounterTween?.Kill();
            _highScoreCounterTween?.Kill();
        }

        #endregion

        #region Single Player View

        protected virtual void ShowSinglePlayerView()
        {
            bool won = gameData.IsLocalDomainWinner(out DomainStats localDomainStats);
            if (BannerImage) BannerImage.color = SinglePlayerBannerColor;
            if (BannerText)  BannerText.text   = won ? "VICTORY" : "DEFEAT";

            int playerScore = (int)localDomainStats.Score;
            AnimateCounter(SinglePlayerScoreTextField, playerScore, ref _scoreCounterTween);

            if (SinglePlayerHighscoreTextField)
            {
                float highScore = playerScore;
                if (UGSStatsManager.Instance &&
                    Enum.TryParse(gameData.GameMode.ToString(), out GameModes modeEnum))
                {
                    highScore = UGSStatsManager.Instance.GetEvaluatedHighScore(
                        modeEnum, gameData.SelectedIntensity.Value, playerScore);
                }
                AnimateCounter(SinglePlayerHighscoreTextField, (int)highScore, ref _highScoreCounterTween);
            }

            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(true);
        }

        #endregion

        #region Multiplayer View

        protected virtual void ShowMultiplayerView()
        {
            // Show the actual winning team in the banner, not the local player's team
            if (gameData.TryGetWinningDomain(out DomainStats winnerStats))
                SetBannerForDomain(winnerStats.Domain);
            else if (BannerText)
                BannerText.text = "GAME OVER";

            PopulateTeamScorecards();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(true);
        }

        protected virtual void SetBannerForDomain(Domains domain)
        {
            Color bannerColor = domain switch
            {
                Domains.Jade => JadeTeamBannerColor,
                Domains.Ruby => RubyTeamBannerColor,
                Domains.Gold => GoldTeamBannerColor,
                Domains.Blue => BlueTeamBannerColor,
                _            => SinglePlayerBannerColor,
            };

            string bannerLabel = domain switch
            {
                Domains.Jade => "JADE VICTORY",
                Domains.Ruby => "RUBY VICTORY",
                Domains.Gold => "GOLD VICTORY",
                Domains.Blue => "BLUE VICTORY",
                _            => "GAME OVER",
            };

            if (BannerImage) BannerImage.color = bannerColor;
            if (BannerText)  BannerText.text   = bannerLabel;
        }

        private void PopulateTeamScorecards()
        {
            if (teamScorecards == null || teamScorecards.Length == 0)
                return;

            // Group players by domain
            var teamGroups = new Dictionary<Domains, List<IRoundStats>>();
            foreach (var rs in gameData.RoundStatsList)
            {
                if (!teamGroups.TryGetValue(rs.Domain, out var list))
                {
                    list = new List<IRoundStats>(2);
                    teamGroups[rs.Domain] = list;
                }
                list.Add(rs);
            }

            // Sort teams in the same order as DomainStatsList (winner first)
            var sortedDomains = gameData.DomainStatsList
                .Select(ds => ds.Domain)
                .Where(d => teamGroups.ContainsKey(d))
                .ToList();

            // Include any domains that are in teamGroups but not in DomainStatsList
            foreach (var domain in teamGroups.Keys)
            {
                if (!sortedDomains.Contains(domain))
                    sortedDomains.Add(domain);
            }

            bool isGolfRules = gameData.IsGolfRules;

            for (int i = 0; i < teamScorecards.Length; i++)
            {
                if (i >= sortedDomains.Count)
                {
                    teamScorecards[i].Show(false);
                    continue;
                }

                teamScorecards[i].Show(true);

                var domain = sortedDomains[i];
                var players = teamGroups[domain];

                // Team score: sum for normal modes, min (best time) for golf rules
                float teamScoreValue;
                if (isGolfRules)
                    teamScoreValue = players.Min(p => p.Score);
                else
                    teamScoreValue = players.Sum(p => p.Score);

                // Build player display data
                var playerDisplays = new List<PlayerDisplayData>(players.Count);
                foreach (var p in players)
                {
                    playerDisplays.Add(new PlayerDisplayData
                    {
                        Name  = p.Name,
                        Score = FormatScore(p.Score, isGolfRules),
                    });
                }

                // Domain color from palette, fallback to banner colors
                Color domainColor = GetDomainColor(domain);

                string teamName = domain.ToString().ToUpper();
                string teamScore = FormatScore(teamScoreValue, isGolfRules);

                teamScorecards[i].Populate(teamName, teamScore, domainColor, playerDisplays);
            }
        }

        private string FormatScore(float score, bool isGolfRules)
        {
            if (isGolfRules)
            {
                // Time-based: format as MM:SS.f
                var ts = TimeSpan.FromSeconds(score);
                return ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 100}"
                    : $"{ts.Seconds}.{ts.Milliseconds / 100}s";
            }

            return ((int)score).ToString();
        }

        /// <summary>
        /// Legacy per-player score display used by game-mode-specific subclasses.
        /// Base implementation populates PlayerNameTextFields / PlayerScoreTextFields flat lists.
        /// </summary>
        protected virtual void DisplayPlayerScores()
        {
            if (PlayerNameTextFields == null || PlayerScoreTextFields == null) return;

            var playerScores = gameData.RoundStatsList;

            for (int i = 0; i < playerScores.Count && i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = ((int)playerScores[i].Score).ToString();
            }

            for (int i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = "";
            }
        }

        private Color GetDomainColor(Domains domain)
        {
            if (domainColorPalette)
                return domainColorPalette.Get(domain);

            return domain switch
            {
                Domains.Jade => JadeTeamBannerColor,
                Domains.Ruby => RubyTeamBannerColor,
                Domains.Gold => GoldTeamBannerColor,
                Domains.Blue => BlueTeamBannerColor,
                _            => Color.white,
            };
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
                CSDebug.LogWarning($"[Scoreboard] PopulateDynamicStats skipped — " +
                    $"provider={(statsProvider != null ? "OK" : "NULL")}, " +
                    $"container={(statsContainer != null ? "OK" : "NULL")}, " +
                    $"rowPrefab={(statRowPrefab != null ? "OK" : "NULL")}");
                return;
            }

            var stats = statsProvider.GetStats();
            CSDebug.Log($"[Scoreboard] Populating {stats.Count} dynamic stat row(s)");

            foreach (var stat in stats)
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Initialize(stat.Label, stat.Value, stat.Icon);
            }
        }

        #endregion

        #region Return to Main Menu

        public void OnReturnToMainMenuButtonPressed()
        {
            if (multiplayerController != null)
                multiplayerController.LeaveSessionAndReturnToMenu();
            else
                CSDebug.LogWarning("[Scoreboard] No multiplayerController — cannot leave session.");
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

                // Invited panel auto-dismisses after 2s if opponent doesn't respond
                // Restores play again button so local player isn't stuck waiting
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
                // Solo-with-AI: the game runs on the network stack, so go through the
                // controller's replay flow to properly reset race state (_raceEnded, etc.)
                // without showing the multiplayer rematch invitation UI.
                multiplayerController.RequestReplay();
            }
            else
            {
                gameData.ResetForReplay();
            }
        }

        /// <summary>
        /// Called by MultiplayerMiniGameControllerBase when the OPPONENT requests a rematch.
        /// Received panel stays until the player responds — no auto-dismiss.
        /// </summary>
        public void ShowRematchRequest(string requesterName)
        {
            if (rematchReceivedText)
                rematchReceivedText.text = $"{requesterName} wants a rematch!";

            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(true);
            if (playAgainButton)      playAgainButton.SetActive(false);
            // No auto-dismiss — player must actively accept or decline
        }

        /// <summary>
        /// Bound to YES button inside rematchReceivedPanel.
        /// </summary>
        public void OnAcceptRematch()
        {
            HideAllRematchPanels();
            multiplayerController?.RequestReplay();
        }

        /// <summary>
        /// Bound to NO button inside rematchReceivedPanel.
        /// </summary>
        public void OnDeclineRematch()
        {
            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(false);
            if (playAgainButton)      playAgainButton.SetActive(true);
            multiplayerController?.NotifyRematchDeclined(gameData.LocalPlayer.Name);
        }

        /// <summary>
        /// Called by MultiplayerMiniGameControllerBase when the OPPONENT declined our request.
        /// Denied panel auto-dismisses after 2s, then restores play again button.
        /// </summary>
        public void ShowRematchDeclined(string declinerName)
        {
            StopAutoDismiss(ref _invitedAutoDismiss); // cancel invited panel if still running
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
