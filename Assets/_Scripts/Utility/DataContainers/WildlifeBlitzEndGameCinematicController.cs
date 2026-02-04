using System.Collections;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Arcade.Scoring;
using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    public class WildlifeBlitzEndGameCinematicController : EndGameCinematicController
    {
        [Header("Wildlife Blitz Specific")]
        [SerializeField] private WildlifeBlitzScoreTracker wildlifeBlitzScoreTracker;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var stats = GetWildlifeBlitzStats();

            int displayScore = Mathf.Max(0, (int)stats.elapsedTime);

            string displayText = stats.didWin 
                ? cinematic.GetCinematicTextForScore(displayScore) 
                : "DEFEAT";

            yield return view.PlayScoreRevealAnimation(
                displayText,
                displayScore,
                cinematic.scoreRevealSettings,
                formatAsTime: true 
            );
        }

        protected override IEnumerator RunCompleteEndGameSequence(CinematicDefinitionSO cinematic)
        {
            var localPlayer = gameData.LocalPlayer;
            
            // [Visual Note] Stop Sparrow Prism Spawning immediately on sequence start
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

            if (wildlifeBlitzScoreTracker)
                wildlifeBlitzScoreTracker.ResetScores();

            gameData.ResetPlayers();
            cinematicCameraController?.StopCameraSetup();
        }
        
        WildlifeBlitzStats GetWildlifeBlitzStats()
        {
            if (!wildlifeBlitzScoreTracker)
            {
                Debug.LogWarning("[WildlifeBlitzEndGame] Score tracker not assigned!");
                return new WildlifeBlitzStats();
            }
            
            var lifeFormScoring = wildlifeBlitzScoreTracker.GetScoring<LifeFormsKilledScoring>();
            var crystalScoring = wildlifeBlitzScoreTracker.GetScoring<ElementalCrystalsCollectedBlitzScoring>();
            
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