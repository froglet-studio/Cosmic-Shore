using System.Collections;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Arcade.Scoring;
using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Unified EndGame cinematic controller for Wildlife Blitz.
    /// Works for both solo (1 player) and co-op multiplayer (2-3 players).
    /// Reads authoritative results from WildlifeBlitzController.
    /// </summary>
    public class WildlifeBlitzEndGameController : EndGameCinematicController
    {
        [Header("Wildlife Blitz References")]
        [SerializeField] private WildlifeBlitzController blitzController;
        [SerializeField] private WildlifeBlitzScoreTracker scoreTracker;

        protected override bool DetermineLocalPlayerWon()
        {
            return blitzController != null
                && blitzController.ResultsReady
                && blitzController.DidWin;
        }

        protected override IEnumerator RunCompleteEndGameSequence(CinematicDefinitionSO cinematic)
        {
            // Stop Sparrow prism spawning before cinematic
            var localPlayer = gameData.LocalPlayer;
            if (localPlayer is { Vessel: { VesselStatus: { VesselType: VesselClassType.Sparrow } } })
            {
                localPlayer.Vessel.VesselStatus.VesselPrismController.StopSpawn();
            }
            yield return base.RunCompleteEndGameSequence(cinematic);
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            if (!blitzController || !blitzController.ResultsReady) yield break;

            bool didWin = blitzController.DidWin;
            float finishTime = blitzController.FinishTime;
            int currentScore = Mathf.Max(0, (int)finishTime);

            // UGS high-score check
            if (UGSStatsManager.Instance)
            {
                float bestFloat = UGSStatsManager.Instance.GetEvaluatedHighScore(
                    GameModes.WildlifeBlitz,
                    gameData.SelectedIntensity.Value,
                    currentScore
                );

                int cachedBest = (int)bestFloat;
                if (currentScore >= Mathf.Max(currentScore, cachedBest) && cachedBest > 0)
                {
                    Debug.Log($"<color=cyan>[WildlifeBlitz Cinematic] New High Score! Old: {cachedBest} New: {currentScore}</color>");
                }
            }

            // Build display — solo shows simple text, multiplayer shows kill summary
            int totalKills = gameData.RoundStatsList.Sum(s => s.BlocksDestroyed);
            bool isMultiplayer = gameData.IsMultiplayerMode || gameData.RoundStatsList.Count > 1;

            string displayText;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                if (isMultiplayer)
                    displayText = cinematic.GetCinematicTextForScore(currentScore) +
                                  $"\n<size=60%>CO-OP CLEAR — {totalKills} KILLS</size>";
                else
                    displayText = cinematic.GetCinematicTextForScore(currentScore);

                displayValue = currentScore;
                formatAsTime = true;
            }
            else
            {
                if (isMultiplayer)
                    displayText = $"DEFEAT\n<size=60%>SURVIVED — {totalKills} KILLS</size>";
                else
                    displayText = "DEFEAT";

                displayValue = isMultiplayer ? totalKills : currentScore;
                formatAsTime = !isMultiplayer;
            }

            Debug.Log($"[WildlifeBlitz EndGame] didWin={didWin} Time={finishTime:F2}s " +
                      $"Kills={totalKills} Multiplayer={isMultiplayer} " +
                      $"Scores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F2}({s.BlocksDestroyed}k)"))}]");

            yield return view.PlayScoreRevealAnimation(
                displayText,
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
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

            if (scoreTracker)
                scoreTracker.ResetScores();

            gameData.ResetPlayers();
            cinematicCameraController?.StopCameraSetup();
        }
    }
}
