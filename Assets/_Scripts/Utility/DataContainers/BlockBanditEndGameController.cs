using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class BlockBanditEndGameController : EndGameCinematicController
    {
        protected override bool DetermineLocalPlayerWon()
        {
            var localName = gameData.LocalPlayer?.Name;
            return gameData.RoundStatsList.Count > 0
                && gameData.RoundStatsList[0].Name == localName;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            bool didWin = gameData.RoundStatsList.Count > 0 &&
                          gameData.RoundStatsList[0].Name == localName;

            string headerText = didWin ? "VICTORY" : "DEFEAT";

            int myBlocks = localStats.PrismStolen;
            int opponentBlocks = gameData.RoundStatsList
                .Where(s => s.Name != localName)
                .Select(s => s.PrismStolen)
                .DefaultIfEmpty(0)
                .Max();

            int blockDifference = Mathf.Abs(myBlocks - opponentBlocks);

            string label = didWin
                ? $"STOLE {blockDifference} MORE BLOCK{(blockDifference != 1 ? "S" : "")}"
                : $"BEHIND BY {blockDifference} BLOCK{(blockDifference != 1 ? "S" : "")}";

            CSDebug.Log($"[BlockBandit] Local='{localName}' myBlocks={myBlocks} " +
                      $"opponentBlocks={opponentBlocks} didWin={didWin} diff={blockDifference} " +
                      $"AllStats=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.PrismStolen}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                myBlocks,
                cinematic.scoreRevealSettings,
                false
            );
        }
    }
}
