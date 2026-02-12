using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceEndGameController : HexRaceEndGameController
    {
        [Header("References")]
        [SerializeField] private MultiplayerHexRaceController hexRaceController;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            // ----------------------------
            // 1) Determine TRUE Victory/Defeat from the authoritative winner domain
            //    (computed in MultiplayerHexRaceController.SyncFinalScores_ClientRpc)
            // ----------------------------
            bool didWin = false;

            var localDomain = gameData.LocalPlayer?.Vessel?.VesselStatus?.Domain;
            if (localDomain != null &&
                gameData.DomainStatsList != null &&
                gameData.DomainStatsList.Count > 0)
            {
                // DomainStatsList[0] should be the winner after CalculateDomainStats(+Sort)
                var winnerDomain = gameData.DomainStatsList[0].Domain;
                didWin = winnerDomain == localDomain.Value;
            }

            var headerText = didWin ? "VICTORY" : "DEFEAT";

            // ----------------------------
            // 2) Fetch local player's stats to decide what to display (time vs crystals left)
            // ----------------------------
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;

            // Fallback if your RoundStatsList is keyed by LocalPlayer.Name instead of VesselStatus.PlayerName
            if (string.IsNullOrEmpty(localPlayerName))
                localPlayerName = gameData.LocalPlayer?.Name;

            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s != null && s.Name == localPlayerName);

            // If we still didn't find stats, try the other common key
            if (localStats == null && gameData.LocalPlayer != null)
                localStats = gameData.RoundStatsList.FirstOrDefault(s => s != null && s.Name == gameData.LocalPlayer.Name);

            if (localStats == null)
            {
                Debug.LogError("[MultiplayerHexRaceEndGameController] No local stats found for score reveal!");
                yield break;
            }

            float localScore = localStats.Score;
            bool didLocalPlayerFinish = localScore < 10000f;

            string label;
            int value;
            bool formatAsTime;

            if (didLocalPlayerFinish)
            {
                label = "RACE TIME";
                value = Mathf.Max(0, (int)localScore);
                formatAsTime = true;
            }
            else
            {
                label = "CRYSTALS LEFT";
                value = Mathf.Max(0, (int)(localScore - 10000f));
                formatAsTime = false;
            }

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                value,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }

        private void OnValidate()
        {
            if (hexRaceController == null)
                hexRaceController = GetComponent<MultiplayerHexRaceController>();
        }
    }
}