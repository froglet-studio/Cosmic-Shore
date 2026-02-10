using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerJoustEndGameController : EndGameCinematicController
    {
        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();
            
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);

            if (localStats == null)
            {
                Debug.LogError("No local stats found!");
                yield break;
            }

            float localScore = localStats.Score;
            bool didLocalPlayerWin = localScore < 10000f;

            var headerText = didLocalPlayerWin ? "VICTORY" : "DEFEAT";
            
            string label;
            int value;
            bool formatAsTime;

            if (didLocalPlayerWin)
            {
                // Winner: Show their time
                label = "RACE TIME";
                value = (int)localScore; 
                formatAsTime = true;
            }
            else
            {
                // Loser: Show jousts remaining
                label = "JOUSTS LEFT";
                value = (int)(localScore - 10000f);
                formatAsTime = false;
            }

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                value,
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

            // Reset joust collision counts
            foreach (var stats in gameData.RoundStatsList)
            {
                stats.JoustCollisions = 0;
            }

            gameData.ResetPlayers();
            cinematicCameraController?.StopCameraSetup();
        }
    }
}