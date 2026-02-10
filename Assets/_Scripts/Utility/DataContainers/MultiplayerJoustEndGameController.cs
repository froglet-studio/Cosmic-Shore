using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerJoustEndGameController : EndGameCinematicController
    {
        [Header("References")]
        [SerializeField] private MultiplayerJoustController joustController;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();
    
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);

            if (localStats == null) yield break;

            // --- THE LOGIC ---
            int needed = joustController.joustTurnMonitor.CollisionsNeeded;
            int current = localStats.JoustCollisions;
            
            bool didWin = current >= needed;

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label = "RACE TIME";
                displayValue = (int)localStats.Score; // Score is Time
                formatAsTime = true;
            }
            else
            {
                label = "JOUSTS LEFT";
                displayValue = Mathf.Max(0, needed - current); // Calc Jousts Left
                formatAsTime = false;
            }

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
        
        protected override void ResetGameForNewRound()
        {
            base.ResetGameForNewRound();
            foreach (var stats in gameData.RoundStatsList)
                stats.JoustCollisions = 0;
        }
    }
}