using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceEndGameController : EndGameCinematicController
    {
        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            var winner = gameData.RoundStatsList.Count > 0 ? gameData.RoundStatsList[0] : null;
            bool didWin = winner != null && winner.Name == localStats.Name;

            string headerText = didWin ? "VICTORY" : "DEFEAT";

            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label = "RACE TIME";
                displayValue = (int)localStats.Score;
                formatAsTime = true;
            }
            else
            {
                label = "CRYSTALS LEFT";
                // Encoding: 10000 + crystalsLeft
                int crystalsLeft = Mathf.Max(0, (int)(localStats.Score - 10000f));
                displayValue = crystalsLeft;
                formatAsTime = false;
            }

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}