using System;
using System.Collections;
using CosmicShore.App.Profile;
using CosmicShore.App.Systems.Audio;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Progression;
using CosmicShore.Models;
using CosmicShore.Soap;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Utility;

namespace CosmicShore.Game.Cinematics
{
    public class EndGameCinematicController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected SceneCinematicLibrarySO sceneCinematicLibrary;
        [SerializeField] protected CinematicCameraController cinematicCameraController;
        
        [Header("View")]
        [SerializeField] protected EndGameCinematicView view;

        [Header("Crystal Reward")]
        [Tooltip("Amount of crystals awarded per game.")]
        [SerializeField] private int crystalsPerGame = 5;
        [Tooltip("Root GameObject to enable/disable for the crystal reward display.")]
        [SerializeField] private GameObject crystalRewardRoot;
        [Tooltip("Text showing how many crystals were earned.")]
        [SerializeField] private TMP_Text crystalRewardText;
        [Tooltip("Duration of the fade-in animation.")]
        [SerializeField] private float crystalFadeDuration = 0.5f;

        protected bool isRunning;
        protected bool localPlayerWon;
        protected Coroutine runningRoutine;
        protected float cachedBoostMultiplier;
        
        protected virtual void OnEnable()
        {
            if (!gameData) return;
            gameData.OnWinnerCalculated += OnWinnerCalculated;

            if (crystalRewardRoot)
                crystalRewardRoot.SetActive(false);

            if (!view) return;
            view.Initialize();
            view.OnContinuePressed += HandleContinuePressed;
        }

        protected virtual void OnDisable()
        {
            if (!gameData) return;
            gameData.OnWinnerCalculated -= OnWinnerCalculated;

            if (view)
                view.OnContinuePressed -= HandleContinuePressed;

            if (runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                runningRoutine = null;
            }

            if (view)
                view.Cleanup();

            isRunning = false;
        }

        protected virtual void OnWinnerCalculated()
        {
            if (isRunning) return;
            isRunning = true;

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel?.VesselStatus != null)
            {
                cachedBoostMultiplier = localPlayer.Vessel.VesselStatus.BoostMultiplier;
            }

            var cinematic = ResolveCinematicForThisScene();
            runningRoutine = StartCoroutine(RunCompleteEndGameSequence(cinematic));
        }

        protected virtual IEnumerator RunCompleteEndGameSequence(CinematicDefinitionSO cinematic)
        {
            localPlayerWon = DetermineLocalPlayerWon();

            if (cinematic && cinematic.enableVictoryLap)
                yield return StartCoroutine(RunVictoryLap(cinematic));

            if (cinematic && cinematic.setLocalVesselToAI)
                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);

            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
                yield return StartCoroutine(RunCameraSequence(cinematic));
            else
            {
                var delay = cinematic ? cinematic.delayBeforeEndScreen : 0.1f;
                yield return new WaitForSeconds(delay);
            }
            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));
            yield return StartCoroutine(AwardCrystalReward());
            yield return StartCoroutine(ShowIntensityUnlockSequence());
            yield return StartCoroutine(ShowQuestCompletionSequence());

            if (view)
            {
                view.ShowContinueButton();
                yield return new WaitUntil(() => !view.IsContinueButtonActive());
            }
            
            if (view && cinematic)
            {
                view.ShowConnectingPanel();
                yield return new WaitForSeconds(cinematic.connectingPanelDuration);
                ResetGameForNewRound();
            }

            if (view)
            {
                view.HideScoreRevealPanel();
            }

            gameData.InvokeShowGameEndScreen();

            runningRoutine = null;
            isRunning = false;
        }

        #region Reset & Transition
        
        /// <summary>
        /// Virtual method - can be overridden by game-specific controllers
        /// </summary>
        protected virtual void ResetGameForNewRound()
        {
            CSDebug.Log("[EndGameCinematic] Resetting Game State...");

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer == null && gameData.Players.Count > 0)
                localPlayer = gameData.Players[0];

            if (localPlayer != null)
            {
                // Restore cached boost
                if (localPlayer.Vessel?.VesselStatus != null)
                    localPlayer.Vessel.VesselStatus.BoostMultiplier = cachedBoostMultiplier;

                // if (localPlayer.Vessel != null)
                // {
                //     var trailRenderer = localPlayer.Vessel.Transform.GetComponentInChildren<TrailRenderer>();
                //     if (trailRenderer)
                //         trailRenderer.Clear();
                // }

                if (localPlayer.Vessel != null)
                {
                    localPlayer.Vessel.ToggleAIPilot(false);
                    if (localPlayer.Vessel.VesselStatus?.AICinematicBehavior)
                        localPlayer.Vessel.VesselStatus.AICinematicBehavior.StopCinematicBehavior();
                }

                if (localPlayer.InputController)
                    localPlayer.InputController.enabled = true;
            }

            gameData.ResetPlayers();

            if (cinematicCameraController)
                cinematicCameraController.StopCameraSetup();

            // Snap player camera back to follow target after cinematic
            // moved it to a cinematic position.
            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
        
        protected virtual void HandleContinuePressed()
        {
        }
        
        #endregion

        #region Victory Lap
        
        protected virtual IEnumerator RunVictoryLap(CinematicDefinitionSO cinematic)
        {
            var settings = cinematic.victoryLapSettings;
            var localPlayer = gameData.LocalPlayer;
            
            if (localPlayer?.Vessel != null)
            {
                float originalSpeed = localPlayer.Vessel.VesselStatus.Speed;
                localPlayer.Vessel.VesselStatus.Speed *= settings.speedMultiplier;
                
                if (settings.enhanceTrail)
                {
                    EnhanceTrailRenderer(localPlayer.Vessel);
                }
                
                if (cinematic.showVictoryToast)
                {
                    var toastMessage = localPlayerWon
                        ? cinematic.GetRandomVictoryToast()
                        : cinematic.GetRandomDefeatToast();

                    if (!string.IsNullOrEmpty(toastMessage))
                        view?.ShowVictoryToast(toastMessage, cinematic.toastSettings);
                }
                
                yield return new WaitForSeconds(settings.duration);
                localPlayer.Vessel.VesselStatus.Speed = originalSpeed;
            }
            else
            {
                yield return new WaitForSeconds(settings.duration);
            }
            
            if (settings.fadeLoserTrail && gameData.IsMultiplayerMode)
            {
                FadeLoserTrails();
            }
        }

        protected virtual void EnhanceTrailRenderer(IVessel vessel)
        {
        }

        protected virtual void FadeLoserTrails()
        {
        }
        
        #endregion

        #region Camera Sequence
        
        protected virtual IEnumerator RunCameraSequence(CinematicDefinitionSO cinematic)
        {
            var localPlayer = gameData.LocalPlayer;
            var mainCamera = Camera.main;

            cinematicCameraController.Initialize(mainCamera, localPlayer.Vessel.Transform);

            for (int i = 0; i < cinematic.cameraSetups.Count; i++)
            {
                var cameraSetup = cinematic.cameraSetups[i];
                cinematicCameraController.StartCameraSetup(cameraSetup);
                yield return new WaitForSeconds(cameraSetup.duration);
                cinematicCameraController.StopCameraSetup();

                if (i >= cinematic.cameraSetups.Count - 1) continue;
                yield return new WaitForSeconds(cinematic.cameraTransitionTime);
            }
        }
        
        #endregion

        #region Score Reveal
        
        /// <summary>
        /// VIRTUAL - Override in game-specific controllers for custom score display
        /// </summary>
        protected virtual IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ScoreReveal);

            gameData.IsLocalDomainWinner(out DomainStats stats);
            int score = Mathf.Max(0, (int)stats.Score); 
            
            string displayText = cinematic.GetCinematicTextForScore(score);
            
            yield return view.PlayScoreRevealAnimation(
                displayText,
                score,
                cinematic.scoreRevealSettings
            );
        }
        
        protected virtual IEnumerator AwardCrystalReward()
        {
            if (crystalsPerGame <= 0) yield break;

            var service = PlayerDataService.Instance;
            if (service != null)
            {
                int newBalance = service.AddCrystals(crystalsPerGame);
                CSDebug.Log($"[EndGameCinematic] Awarded {crystalsPerGame} crystals. New balance: {newBalance}");
            }

            if (crystalRewardRoot && crystalRewardText)
            {
                crystalRewardText.text = $"+{crystalsPerGame}";
                crystalRewardRoot.SetActive(true);

                var cg = crystalRewardRoot.GetComponent<CanvasGroup>();
                if (cg)
                {
                    cg.alpha = 0f;
                    yield return cg.DOFade(1f, crystalFadeDuration)
                        .SetEase(Ease.OutQuad)
                        .WaitForCompletion();
                }

                yield return new WaitForSeconds(1.5f);
            }
        }

        /// <summary>
        /// Checks whether the just-finished game unlocked a new intensity level (3 or 4).
        /// If so, shows a brief message via the quest-completion text panel before moving on.
        /// Must run after RecordIntensityPlay has already updated the progression data.
        /// </summary>
        protected virtual IEnumerator ShowIntensityUnlockSequence()
        {
            if (!view || !gameData) yield break;

            var service = GameModeProgressionService.Instance;
            if (service == null) yield break;

            var mode = gameData.GameMode;
            int maxUnlocked = service.GetMaxUnlockedIntensity(mode);

            // Only show if intensity 3 or 4 was just unlocked this game
            // We detect this by comparing remaining plays — 0 remaining means it was just unlocked
            // (the quest completion sequence handles the full-quest-complete case separately)
            if (maxUnlocked >= 3 && service.GetPlaysRemainingForIntensity(mode, 3) == 0 &&
                maxUnlocked < 4 && service.GetPlaysRemainingForIntensity(mode, 4) > 0)
            {
                // Intensity 3 was recently unlocked, intensity 4 still locked
                view.ShowQuestCompletion("Intensity 3 Unlocked!");
                yield return new WaitForSeconds(2f);
                view.HideQuestCompletion();
            }
            else if (maxUnlocked >= 4 && !service.IsQuestCompleted(mode))
            {
                // Intensity 4 unlocked but quest not yet flagged as complete
                view.ShowQuestCompletion("Intensity 4 Unlocked!");
                yield return new WaitForSeconds(2f);
                view.HideQuestCompletion();
            }
        }

        /// <summary>
        /// After the score reveal, checks if the current game mode's quest was completed.
        /// If so, sets the SO runtime flag and shows a completion message in the cinematic view.
        /// Relies on GameModeProgressionService having already evaluated the quest via HandleGameEnd.
        /// </summary>
        protected virtual IEnumerator ShowQuestCompletionSequence()
        {
            if (!view || !gameData) yield break;

            var service = GameModeProgressionService.Instance;
            if (service == null) yield break;

            var mode = gameData.GameMode;
            var quest = service.GetQuestForMode(mode);
            if (quest == null || quest.IsPlaceholder) yield break;

            if (service.IsQuestCompleted(mode))
            {
                quest.IsCompleted = true;
                view.ShowQuestCompletion($"Quest Complete!\n{quest.DisplayName}");
                CSDebug.Log($"[EndGameCinematic] Quest completed for {mode}: {quest.DisplayName}");
                yield return new WaitForSeconds(2f);
            }
        }

        #endregion

        #region AI Control
        
        protected virtual void SetLocalVesselAI(bool isAI, AICinematicBehaviorType behaviorName = AICinematicBehaviorType.MoveForward)
        {
            var player = gameData.LocalPlayer;
            if (player == null) return;

            if (player.InputController)
            {
                player.InputController.enabled = !isAI;
            }

            player.Vessel.ToggleAIPilot(isAI);
            
            // Stop prism spawning for Sparrow when AI takes over
            if (player.Vessel.VesselStatus.VesselType == VesselClassType.Sparrow)
                player.Vessel.VesselStatus.VesselPrismController.StopSpawn();

            if (!isAI) return;
            
            var cinematicBehavior = player.Vessel.VesselStatus.AICinematicBehavior;
            var aiPilot = player.Vessel.VesselStatus.AIPilot;

            cinematicBehavior.Initialize(player.Vessel.VesselStatus, aiPilot);
            cinematicBehavior.StartCinematicBehavior(behaviorName);
        }
        
        #endregion

        #region Helpers

        /// <summary>
        /// Override in game-specific controllers to provide win/loss detection.
        /// Called at the start of the end-game sequence to determine which toast strings to use.
        /// </summary>
        protected virtual bool DetermineLocalPlayerWon()
        {
            return gameData != null && gameData.IsLocalDomainWinner(out _);
        }

        protected virtual CinematicDefinitionSO ResolveCinematicForThisScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            if (sceneCinematicLibrary && sceneCinematicLibrary.TryGet(sceneName, out var fromLibrary))
            {
                CSDebug.Log($"Found cinematic definition for scene: {sceneName}");
                return fromLibrary;
            }
            
            CSDebug.LogWarning($"No cinematic definition found for scene: {sceneName}");
            return null;
        }
        
        #endregion
    }
}