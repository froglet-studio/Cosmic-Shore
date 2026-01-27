using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Game.UI;
using Cysharp.Threading.Tasks;
using CosmicShore.Soap;
using DG.Tweening;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace CosmicShore.Game
{
    public class Scoreboard : MonoBehaviour
    {
        [FormerlySerializedAs("miniGameData")]
        [SerializeField] protected GameDataSO gameData;

        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [SerializeField] Transform gameOverPanel;
        [SerializeField] private Transform endGameStatsPanel;
        [SerializeField] RectTransform animatedRoot;

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor;
        [SerializeField] Color JadeTeamBannerColor;
        [SerializeField] Color RubyTeamBannerColor;
        [SerializeField] Color GoldTeamBannerColor;

        [Header("Single Player")]
        [SerializeField] Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multi Player")]
        [SerializeField] Transform MultiplayerView;
        [SerializeField] List<TMP_Text> PlayerNameTextFields;
        [SerializeField] List<TMP_Text> PlayerScoreTextFields;

        [Header("End Screen Sequence")]
        [SerializeField] TMP_Text bestScoreText;
        [SerializeField] TMP_Text highScoreText;
        [SerializeField] GameObject continueButton;
        [Header("Slide Animation")]
        [SerializeField] float startX = -1200f;
        [SerializeField] float endX = 0f;
        [SerializeField] float slideDuration = 0.6f;
        [SerializeField] Ease slideEase = Ease.OutCubic;
        
        CancellationTokenSource _cts;
        bool _sequenceRunning;

        void Awake()
        {
            ResetForReplay();
        }

        void OnEnable()
        {
            EnsureCts();

            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += BeginEndScreenFlow;

            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised += ResetForReplay;
        }

        void OnDisable()
        {
            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= BeginEndScreenFlow;

            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised -= ResetForReplay;

            CancelAndDisposeCts();
        }

        void EnsureCts()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
        }

        void CancelAndDisposeCts()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        void RestartCts()
        {
            CancelAndDisposeCts();
            _cts = new CancellationTokenSource();
        }

        void BeginEndScreenFlow()
        {
            if (_sequenceRunning) return;
            _sequenceRunning = true;

            EnsureCts();
            RunEndScreenSequenceAsync(_cts.Token).Forget();
        }

        async UniTaskVoid RunEndScreenSequenceAsync(CancellationToken ct)
        {
            try
            {
                endGameStatsPanel.gameObject.SetActive(true);
                continueButton.SetActive(false);

                animatedRoot.anchoredPosition =
                    new Vector2(startX, animatedRoot.anchoredPosition.y);

                await animatedRoot
                    .DOAnchorPosX(endX, slideDuration)
                    .SetEase(slideEase)
                    .AsyncWaitForCompletion();

                gameData.IsLocalDomainWinner(out DomainStats stats);
                int score = Mathf.Max(0, (int)stats.Score);

                await UniTask.WhenAll(
                    PlayCasinoCounter(bestScoreText, score, 2f, ct),
                    PlayCasinoCounter(highScoreText, score, 2f, ct)
                );

                continueButton.SetActive(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _sequenceRunning = false;
            }
        }
        
        async UniTask PlayCasinoCounter(
            TMP_Text text,
            int target,
            float duration,
            CancellationToken ct)
        {
            float t = 0f;

            while (t < duration)
            {
                ct.ThrowIfCancellationRequested();

                t += Time.deltaTime;
                int display = Random.Range(0, target + 1);
                text.text = display.ToString("000");

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            text.text = target.ToString("000");
        }


        public void OnContinueButtonPressed()
        {
            endGameStatsPanel.gameObject.SetActive(false);
            ShowSinglePlayerView();
        }

        void ResetForReplay()
        {
            _sequenceRunning = false;
            RestartCts();
            
            if (gameOverPanel) gameOverPanel.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
        }

        protected virtual void ShowSinglePlayerView()
        {
            bool won = gameData.IsLocalDomainWinner(out DomainStats localDomainStats);

            var bannerText = won ? "WON" : "DEFEAT";
            if (BannerImage) BannerImage.color = SinglePlayerBannerColor;
            if (BannerText) BannerText.text = bannerText;

            var playerScore = localDomainStats.Score;
            if (SinglePlayerScoreTextField) SinglePlayerScoreTextField.text = ((int)playerScore).ToString();
            if (SinglePlayerHighscoreTextField) SinglePlayerHighscoreTextField.text = ((int)playerScore).ToString();

            if (MultiplayerView) MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(true);
            if (gameOverPanel) gameOverPanel.gameObject.SetActive(true);
        }

        public void ShowMultiplayerView()
        {
            var winningTeam = Domains.Jade;

            switch (winningTeam)
            {
                case Domains.Jade:
                    BannerImage.color = JadeTeamBannerColor;
                    BannerText.text = "JADE VICTORY";
                    break;
                case Domains.Ruby:
                    BannerImage.color = RubyTeamBannerColor;
                    BannerText.text = "RUBY VICTORY";
                    break;
                case Domains.Gold:
                    BannerImage.color = GoldTeamBannerColor;
                    BannerText.text = "GOLD VICTORY";
                    break;
                default:
                    Debug.LogWarning($"{winningTeam} does not have assigned banner image color and banner text preset.");
                    break;
            }

            var playerScores = gameData.RoundStatsList;

            for (var i = 0; i < playerScores.Count; i++)
            {
                var playerScore = playerScores[i];
                PlayerNameTextFields[i].text = playerScore.Name;
                PlayerScoreTextFields[i].text = ((int)playerScore.Score).ToString();
            }

            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                PlayerNameTextFields[i].text = "";
                PlayerScoreTextFields[i].text = "";
            }

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
            if (gameOverPanel) gameOverPanel.gameObject.SetActive(true);
        }
    }
}
