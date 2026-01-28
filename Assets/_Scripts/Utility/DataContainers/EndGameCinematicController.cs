using System.Collections;
using System.Threading;
using CosmicShore.App.UI.Modals;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Comprehensive end-game cinematic system handling:
    /// 1. Victory Lap (player maintains control + toast message)
    /// 2. AI-driven vessel cinematics
    /// 3. Dynamic camera system
    /// 4. Score reveal with vessel images
    /// 5. Transition to scoreboard
    /// </summary>
    public class EndGameCinematicController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;

        [Header("Cinematics")]
        [SerializeField] SceneCinematicLibrarySO sceneCinematicLibrary;

        [Header("Score Reveal UI")]
        [Tooltip("Parent panel that contains all score reveal UI")]
        [SerializeField] Transform scoreRevealPanel;
        
        [Tooltip("Root transform for slide-in animation")]
        [SerializeField] RectTransform animatedRoot;
        
        [SerializeField] TMP_Text cinematicTextDisplay;
        [SerializeField] TMP_Text bestScoreText;
        [SerializeField] TMP_Text highScoreText;
        [SerializeField] Button continueButton;
        [SerializeField] Image backgroundImage;
        
        [Header("⭐ Victory Toast")]
        [Tooltip("Toast message shown during victory lap (e.g., 'GREAT JOB!', 'AMAZING!')")]
        [SerializeField] TMP_Text scoreRevealToastText;
        [SerializeField] CanvasGroup scoreRevealToastCanvasGroup;

        [Header("Vessel Podium Display")]
        [Tooltip("Manager for vessel icon displays")]
        [SerializeField] EndGameVesselDisplayManager vesselDisplayManager;

        [Header("Connecting Panel")]
        [SerializeField] SceneTransitionModal connectingPanel;
        [SerializeField] float connectingPanelDuration = 1f;

        [Header("Animation Settings")]
        [SerializeField] float startX = -1200f;
        [SerializeField] float endX = 0f;
        [SerializeField] float slideDuration = 0.6f;
        [SerializeField] Ease slideEase = Ease.OutCubic;
        [SerializeField] float casinoCounterDuration = 2f;
        
        [Header("Toast Animation Settings")]
        [Tooltip("How high the toast pops up")]
        [SerializeField] float toastYOffset = 5f;
        [Tooltip("Delay before showing toast")]
        [SerializeField] float toastDelay = 0.5f;
        [Tooltip("Duration toast stays visible")]
        [SerializeField] float toastDuration = 1.5f;

        [Header("Camera Components")]
        [SerializeField] CinematicCameraController cinematicCameraController;

        private bool isRunning;
        private Coroutine runningRoutine;
        private CancellationTokenSource _cts;
        private int currentCameraIndex = -1;

        void OnEnable()
        {
            if (gameData == null) return;
            gameData.OnWinnerCalculated += OnWinnerCalculated;
            if (continueButton) continueButton.onClick.AddListener(OnContinueButtonPressed);
            _cts = new CancellationTokenSource();
        }

        void OnDisable()
        {
            if (gameData == null) return;

            gameData.OnWinnerCalculated -= OnWinnerCalculated;

            if (runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                runningRoutine = null;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            isRunning = false;
        }

        void OnWinnerCalculated()
        {
            if (isRunning) return;
            isRunning = true;

            var cinematic = ResolveCinematicForThisScene();
            runningRoutine = StartCoroutine(RunCompleteEndGameSequence(cinematic));
        }

        IEnumerator RunCompleteEndGameSequence(CinematicDefinitionSO cinematic)
        {
            Debug.Log("=== END GAME SEQUENCE STARTED ===");
            
            // Phase 1: Victory Lap (with toast!)
            if (cinematic && cinematic.enableVictoryLap)
            {
                yield return StartCoroutine(RunVictoryLap(cinematic));
            }
            
            // Phase 2: AI Control
            if (cinematic && cinematic.setLocalVesselToAI)
            {
                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);
            }
            
            // Phase 3: Camera Sequence
            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
            {
                yield return StartCoroutine(RunCameraSequence(cinematic));
            }
            else
            {
                float delay = cinematic ? cinematic.delayBeforeEndScreen : 2f;
                yield return new WaitForSeconds(delay);
            }
            
            // Phase 4: Score Reveal Panel
            if (scoreRevealPanel)
                scoreRevealPanel.gameObject.SetActive(true);

            if (continueButton)
                continueButton.gameObject.SetActive(false);

            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));

            // Phase 5: Wait for Continue
            if (continueButton)
            {
                continueButton.gameObject.SetActive(true);
                yield return new WaitUntil(() => !continueButton.gameObject.activeSelf);
            }
            
            // Phase 6: Connecting & Reset
            if (connectingPanel)
            {
                connectingPanel.TransitionDoor(true);
                yield return new WaitForSeconds(connectingPanelDuration);
                ResetGameForNewRound();
            }
            
            // Phase 7: Hide and Show Scoreboard
            if (scoreRevealPanel)
                scoreRevealPanel.gameObject.SetActive(false);

            gameData.InvokeShowGameEndScreen();

            runningRoutine = null;
            isRunning = false;
        }

        #region Victory Lap
        IEnumerator RunVictoryLap(CinematicDefinitionSO cinematic)
        {
            var settings = cinematic.victoryLapSettings;
            
            Debug.Log($"Victory Lap started - Duration: {settings.duration}s, Speed: {settings.speedMultiplier}x");
            
            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel != null)
            {
                float originalSpeed = localPlayer.Vessel.VesselStatus.Speed;
                localPlayer.Vessel.VesselStatus.Speed *= settings.speedMultiplier;
                
                // TODO: Enhance trail renderer here
                if (settings.enhanceTrail)
                {
                    EnhanceTrailRenderer(localPlayer.Vessel);
                }
                
                // ⭐ NEW: Show victory toast animation during victory lap
                if (cinematic.showVictoryToast && !string.IsNullOrEmpty(cinematic.scoreRevealToastString))
                {
                    StartCoroutine(AnimateVictoryToast(cinematic.scoreRevealToastString));
                }
                
                yield return new WaitForSeconds(settings.duration);
                
                // Restore original speed
                localPlayer.Vessel.VesselStatus.Speed = originalSpeed;
            }
            else
            {
                yield return new WaitForSeconds(settings.duration);
            }
            
            // TODO: In multiplayer, fade loser trails
            if (settings.fadeLoserTrail && gameData.IsMultiplayerMode)
            {
                FadeLoserTrails();
            }
        }
        
        /// <summary>
        /// ⭐ NEW: Animate victory toast message with fade in + pop up + fade out
        /// Shows during victory lap to give immediate player feedback
        /// </summary>
        IEnumerator AnimateVictoryToast(string message)
        {
            if (!scoreRevealToastText || !scoreRevealToastCanvasGroup)
            {
                Debug.LogWarning("[EndGameCinematic] Victory toast components not assigned!");
                yield break;
            }
            
            Debug.Log($"[EndGameCinematic] Showing victory toast: '{message}'");
            
            // Set the message
            scoreRevealToastText.text = message;
            
            // Get RectTransform for position animation
            var rectTransform = scoreRevealToastText.GetComponent<RectTransform>();
            if (!rectTransform)
            {
                Debug.LogWarning("[EndGameCinematic] Toast text missing RectTransform!");
                yield break;
            }
            
            // Store original position
            Vector2 originalPos = rectTransform.anchoredPosition;
            Vector2 targetPos = originalPos + new Vector2(0, toastYOffset);
            
            // Reset initial state
            scoreRevealToastCanvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localScale = Vector3.one * 0.8f;
            
            // Wait for delay
            if (toastDelay > 0f)
                yield return new WaitForSeconds(toastDelay);
            
            // Create animation sequence
            Sequence toastSequence = DOTween.Sequence();
            
            // Phase 1: Fade in + Scale up + Pop up (0.3s)
            toastSequence.Append(scoreRevealToastCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));
            toastSequence.Join(rectTransform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            toastSequence.Join(rectTransform.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.OutQuad));
            
            // Phase 2: Hold visible (toastDuration - 0.6s for fade in/out)
            float holdDuration = Mathf.Max(0.1f, toastDuration - 0.6f);
            toastSequence.AppendInterval(holdDuration);
            
            // Phase 3: Fade out + Scale down (0.3s)
            toastSequence.Append(scoreRevealToastCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
            toastSequence.Join(rectTransform.DOScale(0.8f, 0.3f).SetEase(Ease.InQuad));
            
            // Play the animation
            toastSequence.Play();
            
            // Wait for completion
            yield return toastSequence.WaitForCompletion();
            
            // Reset to original position
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localScale = Vector3.one;
            
            Debug.Log("[EndGameCinematic] Victory toast animation complete");
        }

        void EnhanceTrailRenderer(IVessel vessel)
        {
            // TODO: Implement trail enhancement
            Debug.Log("Trail enhancement - To be implemented");
        }

        void FadeLoserTrails()
        {
            // TODO: Implement loser trail fading in multiplayer
            Debug.Log("Loser trail fading - To be implemented");
        }
        #endregion

        #region Camera Sequence
        IEnumerator RunCameraSequence(CinematicDefinitionSO cinematic)
        {
            var localPlayer = gameData.LocalPlayer;
            var mainCamera = Camera.main;

            cinematicCameraController.Initialize(
                mainCamera,
                localPlayer.Vessel.Transform
            );

            for (int i = 0; i < cinematic.cameraSetups.Count; i++)
            {
                var cameraSetup = cinematic.cameraSetups[i];
                Debug.Log($"Camera Shot {i + 1}/{cinematic.cameraSetups.Count}: {cameraSetup.cameraType} - Duration: {cameraSetup.duration}s");
                
                cinematicCameraController.StartCameraSetup(cameraSetup);
                yield return new WaitForSeconds(cameraSetup.duration);
                cinematicCameraController.StopCameraSetup();

                if (i >= cinematic.cameraSetups.Count - 1) continue;
                Debug.Log($"Transition delay: {cinematic.cameraTransitionTime}s");
                yield return new WaitForSeconds(cinematic.cameraTransitionTime);
            }
            
            Debug.Log("Camera sequence complete");
        }
        #endregion

        #region Score Reveal
        IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            // Slide in animation
            if (animatedRoot)
            {
                animatedRoot.anchoredPosition = new Vector2(startX, animatedRoot.anchoredPosition.y);

                yield return animatedRoot
                    .DOAnchorPosX(endX, slideDuration)
                    .SetEase(slideEase)
                    .WaitForCompletion();
            }
            
            // Get score for display
            gameData.IsLocalDomainWinner(out DomainStats stats);
            int score = Mathf.Max(0, (int)stats.Score);

            // Display cinematic text
            if (cinematicTextDisplay && cinematic)
            {
                string displayText = cinematic.GetCinematicTextForScore(score);
                cinematicTextDisplay.text = displayText;
            }

            // Display vessel images
            DisplayVesselImages();

            // Run casino counter
            yield return StartCoroutine(PlayCasinoCounterCoroutine(score));
        }

        IEnumerator PlayCasinoCounterCoroutine(int targetScore)
        {
            var task = UniTask.WhenAll(
                AnimateSingleCounter(bestScoreText, targetScore, casinoCounterDuration, _cts.Token),
                AnimateSingleCounter(highScoreText, targetScore, casinoCounterDuration, _cts.Token)
            );

            while (!task.Status.IsCompleted())
            {
                yield return null;
            }

            if (task.Status == UniTaskStatus.Faulted)
            {
                Debug.LogError($"Casino counter animation failed: {task.AsTask().Exception}");
            }
        }

        async UniTask AnimateSingleCounter(TMP_Text textField, int target, float duration, CancellationToken ct)
        {
            if (!textField) return;

            try
            {
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    int randomDisplay = Random.Range(0, target + 1);
                    textField.text = randomDisplay.ToString("000");

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                textField.text = target.ToString("000");
            }
            catch (System.OperationCanceledException)
            {
                if (textField)
                    textField.text = target.ToString("000");
            }
        }

        void DisplayVesselImages()
        {
            vesselDisplayManager.DisplayVessels();
        }
        #endregion

        #region AI Control
        void SetLocalVesselAI(bool isAI, AICinematicBehaviorType behaviorName = AICinematicBehaviorType.MoveForward)
        {
            var player = gameData.LocalPlayer;
            if (player.InputController)
            {
                player.InputController.enabled = false;
                Debug.Log("[EndGameCinematic] Player input DISABLED");
            }

            player.Vessel.ToggleAIPilot(isAI);
            Debug.Log($"[EndGameCinematic] AI Pilot toggled: {isAI}");

            if (!isAI) return;
            
            var cinematicBehavior = player.Vessel.VesselStatus.AICinematicBehavior;
            var aiPilot = player.Vessel.VesselStatus.AIPilot;

            cinematicBehavior.Initialize(player.Vessel.VesselStatus, aiPilot);
            cinematicBehavior.StartCinematicBehavior(behaviorName);
                
            Debug.Log($"[EndGameCinematic] AICinematicBehavior started with behavior: {behaviorName}");
        }
        #endregion

        #region Reset & Transition
        void ResetGameForNewRound()
        {
            Debug.Log("Resetting game for new round");
            
            var localPlayer = gameData.LocalPlayer;
            localPlayer.Vessel.VesselStatus.BoostMultiplier = 0f;
            
            if (localPlayer.InputController)
            {
                localPlayer.InputController.enabled = true;
                Debug.Log("Player input RE-ENABLED");
            }
            
            if (localPlayer.Vessel != null)
            {
                localPlayer.Vessel.ToggleAIPilot(false);
                Debug.Log("AI Pilot disabled");
            }
            
            gameData.ResetPlayers();
            Debug.Log("Game reset complete");
        }

        public void OnContinueButtonPressed()
        {
            if (continueButton)
                continueButton.gameObject.SetActive(false);
        }
        #endregion

        #region Helpers
        CinematicDefinitionSO ResolveCinematicForThisScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            if (sceneCinematicLibrary &&
                sceneCinematicLibrary.TryGet(sceneName, out var fromLibrary))
            {
                Debug.Log($"Found cinematic definition for scene: {sceneName}");
                return fromLibrary;
            }
            
            Debug.LogWarning($"No cinematic definition found for scene: {sceneName}");
            return null;
        }
        #endregion
    }
}