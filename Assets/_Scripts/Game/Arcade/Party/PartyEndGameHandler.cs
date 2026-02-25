using CosmicShore.Game.Cinematics;
using CosmicShore.Game.UI.Party;
using CosmicShore.Game.XP;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Overrides the end-game cinematic flow for party mode.
    /// After each round: cinematic → score reveal → party pause panel (Ready button).
    /// After the final round: cinematic → score reveal → party pause panel (Next button → PartyScoreboard).
    /// XP is only awarded on the final round.
    /// </summary>
    public class PartyEndGameHandler : EndGameCinematicController
    {
        [Header("Party")]
        [SerializeField] PartyGameController partyController;
        [SerializeField] PartyPausePanel partyPausePanel;

        [Header("Party Components")]
        [Tooltip("Root GameObject for party components. Re-enabled after cinematic so the panel can show.")]
        [SerializeField] GameObject partyComponentsRoot;

        bool IsFinalRound => partyController != null &&
                             partyController.CurrentRound + 1 >= partyController.TotalRounds;

        protected override void OnWinnerCalculated()
        {
            if (isRunning) return;
            isRunning = true;

            CSDebug.Log($"[PartyEndGame] OnWinnerCalculated. Round={partyController?.CurrentRound}, IsFinal={IsFinalRound}");

            // Only award XP on the final round
            if (IsFinalRound)
            {
                var xpService = XPRewardService.Instance;
                if (xpService != null)
                {
                    int xp = xpService.AwardXP();
                    CSDebug.Log($"[PartyEndGame] Final round XP awarded: {xp}");
                }
            }

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel?.VesselStatus != null)
                cachedBoostMultiplier = localPlayer.Vessel.VesselStatus.BoostMultiplier;

            var cinematic = ResolveCinematicForThisScene();
            runningRoutine = StartCoroutine(RunPartyEndGameSequence(cinematic));
        }

        System.Collections.IEnumerator RunPartyEndGameSequence(CinematicDefinitionSO cinematic)
        {
            localPlayerWon = DetermineLocalPlayerWon();

            if (cinematic && cinematic.enableVictoryLap)
                yield return StartCoroutine(RunVictoryLap(cinematic));

            if (cinematic && cinematic.setLocalVesselToAI)
                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);

            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
                yield return StartCoroutine(RunCameraSequence(cinematic));
            else
            {
                var delay = cinematic ? cinematic.delayBeforeEndScreen : 0.1f;
                yield return new WaitForSeconds(delay);
            }

            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));

            if (IsFinalRound && view)
                view.ShowXPEarned();

            if (view)
            {
                view.ShowContinueButton();
                yield return new WaitUntil(() => !view.IsContinueButtonActive());
            }

            ResetGameForNewRound();

            if (view)
            {
                view.HideXPEarned();
                view.HideScoreRevealPanel();
            }

            // Intercept the normal scoreboard: re-enable party components and show
            // the party panel instead of calling gameData.InvokeShowGameEndScreen().
            if (partyComponentsRoot)
                partyComponentsRoot.SetActive(true);

            if (partyPausePanel)
                partyPausePanel.ForceShow();

            CSDebug.Log($"[PartyEndGame] Cinematic complete → party panel shown (isFinal={IsFinalRound}).");

            runningRoutine = null;
            isRunning = false;
        }
    }
}
