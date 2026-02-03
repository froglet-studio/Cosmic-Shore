using System.Collections;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Arcade.Scoring;
using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Wildlife Blitz specific end game controller
    /// Extends base to add score tracking stats display
    /// </summary>
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

            string timeString = FormatTime(stats.elapsedTime);

            string displayText = stats.didWin 
                ? cinematic.GetCinematicTextForScore((int)stats.elapsedTime) 
                : "DEFEAT";

            int displayScore = Mathf.Max(0, (int)stats.elapsedTime);
            
            yield return view.PlayScoreRevealAnimation(
                displayText,
                displayScore,
                cinematic.scoreRevealSettings
            );
        }
        
        protected override void ResetGameForNewRound()
        {

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer == null && gameData.Players.Count > 0)
                localPlayer = gameData.Players[0];

            if (localPlayer != null)
            {
                // Restore boost
                if (localPlayer.Vessel?.VesselStatus != null)
                    localPlayer.Vessel.VesselStatus.BoostMultiplier = cachedBoostMultiplier;

                // Clear trails
                // var trailRenderer = localPlayer.Vessel.Transform.GetComponentInChildren<TrailRenderer>();
                // if (trailRenderer)
                //     trailRenderer.Clear();

                // Force AI OFF
                if (localPlayer.Vessel != null)
                {
                    localPlayer.Vessel.ToggleAIPilot(false);
                    if (localPlayer.Vessel.VesselStatus?.AICinematicBehavior)
                        localPlayer.Vessel.VesselStatus.AICinematicBehavior.StopCinematicBehavior();
                }

                // Force Input ON
                if (localPlayer.InputController)
                    localPlayer.InputController.enabled = true;

                if (localPlayer.Vessel is { VesselStatus: { VesselType: VesselClassType.Sparrow } })
                {
                    localPlayer.Vessel.VesselStatus.VesselPrismController.StopSpawn();
                }
            }

            if (wildlifeBlitzScoreTracker)
                wildlifeBlitzScoreTracker.ResetScores();

            gameData.ResetPlayers();
            cinematicCameraController?.StopCameraSetup();
            
            // Restart prism spawning for Sparrow after reset
            if (localPlayer?.Vessel is { VesselStatus: { VesselType: VesselClassType.Sparrow } })
            {
                localPlayer.Vessel.VesselStatus.VesselPrismController.StartSpawn();
            }
        }
        
        /// <summary>
        /// Get Wildlife Blitz specific stats for display
        /// </summary>
        WildlifeBlitzStats GetWildlifeBlitzStats()
        {
            if (!wildlifeBlitzScoreTracker)
            {
                Debug.LogWarning("[WildlifeBlitzEndGame] Score tracker not assigned!");
                return new WildlifeBlitzStats();
            }
            
            // Get scoring instances
            var lifeFormScoring = wildlifeBlitzScoreTracker.GetScoring<LifeFormsKilledScoring>();
            var crystalScoring = wildlifeBlitzScoreTracker.GetScoring<ElementalCrystalsCollectedBlitzScoring>();
            
            // Get domain stats to check win
            gameData.IsLocalDomainWinner(out DomainStats domainStats);
            
            return new WildlifeBlitzStats
            {
                didWin = domainStats.Score < 999f, // Win time is actual time, loss is 999
                elapsedTime = domainStats.Score,
                lifeFormsKilled = lifeFormScoring?.GetTotalLifeFormsKilled() ?? 0,
                crystalsCollected = crystalScoring?.GetTotalCrystalsCollected() ?? 0
            };
        }
        
        /// <summary>
        /// Format seconds as HH:MM:SS
        /// </summary>
        private string FormatTime(float totalSeconds)
        {
            int hours = Mathf.FloorToInt(totalSeconds / 3600f);
            int minutes = Mathf.FloorToInt((totalSeconds % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(totalSeconds % 60f);
            
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }
    }
}