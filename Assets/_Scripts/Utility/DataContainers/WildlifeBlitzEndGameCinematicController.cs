using System.Collections;
using CosmicShore.Game.Ship;
using CosmicShore.Game.Arcade;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility.Recording;
using CosmicShore.Models.Enums;
using CosmicShore.Game.UI;
using CosmicShore.Utility;
namespace CosmicShore.Utility.DataContainers
{
    public class WildlifeBlitzEndGameCinematicController : EndGameCinematicController
    {
        [Header("Wildlife Blitz Specific")]
        [SerializeField] private SinglePlayerWildlifeBlitzScoreTracker singlePlayerWildlifeBlitzScoreTracker;

        [Inject] UGSStatsManager ugsStatsManager;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var stats = GetWildlifeBlitzStats();
            int currentScore = Mathf.Max(0, (int)stats.elapsedTime);

            if (ugsStatsManager)
            {
                float bestFloat = ugsStatsManager.GetEvaluatedHighScore(
                    GameModes.WildlifeBlitz,
                    gameData.SelectedIntensity.Value,
                    currentScore
                );
                
                int cachedBest = (int)bestFloat;
                var highScore = Mathf.Max(currentScore, cachedBest);
                
                if (currentScore >= highScore && cachedBest > 0)
                {
                    CSDebug.Log($"<color=cyan>[Cinematic] New High Score Triggered! Old: {cachedBest} New: {currentScore}</color>");
                }
            }

            string displayText = stats.didWin 
                ? cinematic.GetCinematicTextForScore(currentScore) 
                : "DEFEAT";

            yield return view.PlayScoreRevealAnimation(
                displayText,
                currentScore,
                cinematic.scoreRevealSettings,
                formatAsTime: true 
            );
        }

        protected override IEnumerator RunCompleteEndGameSequence(CinematicDefinitionSO cinematic)
        {
            var localPlayer = gameData.LocalPlayer;
            if (localPlayer is { Vessel: { VesselStatus: { VesselType: VesselClassType.Sparrow } } })
            {
                localPlayer.Vessel.VesselStatus.VesselPrismController.StopSpawn();
            }
            yield return base.RunCompleteEndGameSequence(cinematic);
        }
        
        protected override void ResetGameForNewRound()
        {
            var localPlayer = gameData.LocalPlayer;
            if (localPlayer == null && gameData.Players.Count > 0)
                localPlayer = gameData.Players[0];

            if (localPlayer != null)
            {
                if (localPlayer.Vessel?.VesselStatus != null)
                    localPlayer.Vessel.VesselStatus.BoostMultiplier = cachedBoostMultiplier;
                
                if (localPlayer.Vessel != null)
                {
                    localPlayer.Vessel.ToggleAIPilot(false);
                    if (localPlayer.Vessel.VesselStatus?.AICinematicBehavior)
                        localPlayer.Vessel.VesselStatus.AICinematicBehavior.StopCinematicBehavior();
                }
                
                if (localPlayer.InputController)
                    localPlayer.InputController.enabled = true;
            }

            if (singlePlayerWildlifeBlitzScoreTracker)
                singlePlayerWildlifeBlitzScoreTracker.ResetScores();

            gameData.ResetPlayers();
            cinematicCameraController?.StopCameraSetup();
        }
        
        WildlifeBlitzStats GetWildlifeBlitzStats()
        {
            if (!singlePlayerWildlifeBlitzScoreTracker)
            {
                return new WildlifeBlitzStats();
            }
            
            var lifeFormScoring = singlePlayerWildlifeBlitzScoreTracker.GetScoring<LifeFormsKilledScoring>();
            var crystalScoring = singlePlayerWildlifeBlitzScoreTracker.GetScoring<ElementalCrystalsCollectedBlitzScoring>();
            gameData.IsLocalDomainWinner(out DomainStats domainStats);
            
            return new WildlifeBlitzStats
            {
                didWin = domainStats.Score < 999f,
                elapsedTime = domainStats.Score,
                lifeFormsKilled = lifeFormScoring?.GetTotalLifeFormsKilled() ?? 0,
                crystalsCollected = crystalScoring?.GetTotalCrystalsCollected() ?? 0
            };
        }
    }
}