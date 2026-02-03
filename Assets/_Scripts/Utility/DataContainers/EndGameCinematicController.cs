using System;
using System.Collections;
using CosmicShore.Game.Arcade;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        protected bool isRunning;
        protected Coroutine runningRoutine;
        protected float cachedBoostMultiplier;
        
        protected virtual void OnEnable()
        {
            if (!gameData) return;
            gameData.OnWinnerCalculated += OnWinnerCalculated;

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
                view.HideScoreRevealPanel();

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
            Debug.Log("[EndGameCinematic] Resetting Game State...");

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
                
                if (cinematic.showVictoryToast && !string.IsNullOrEmpty(cinematic.scoreRevealToastString))
                {
                    view?.ShowVictoryToast(cinematic.scoreRevealToastString, cinematic.toastSettings);
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

            gameData.IsLocalDomainWinner(out DomainStats stats);
            int score = Mathf.Max(0, (int)stats.Score); 
            
            string displayText = cinematic.GetCinematicTextForScore(score);
            
            yield return view.PlayScoreRevealAnimation(
                displayText,
                score,
                cinematic.scoreRevealSettings
            );
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
        
        protected virtual CinematicDefinitionSO ResolveCinematicForThisScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            if (sceneCinematicLibrary && sceneCinematicLibrary.TryGet(sceneName, out var fromLibrary))
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