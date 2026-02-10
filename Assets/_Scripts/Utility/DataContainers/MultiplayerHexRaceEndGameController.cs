using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceEndGameController : HexRaceEndGameController
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
            bool didLocalPlayerFinish = localScore < 10000f;

            var headerText = didLocalPlayerFinish ? "VICTORY" : "DEFEAT";
            
            string label;
            int value;
            bool formatAsTime;

            if (didLocalPlayerFinish)
            {
                label = "RACE TIME";
                value = (int)localScore; 
                formatAsTime = true;
            }
            else
            {
                label = "CRYSTALS LEFT";
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
    }
}