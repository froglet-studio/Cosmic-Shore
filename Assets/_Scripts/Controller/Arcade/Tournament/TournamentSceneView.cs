using System.Collections;
using System.Linq;
using System.Text;
using CosmicShore.Utility;
using Obvious.Soap;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// UI view for the Tournament scene, which serves two roles (the persistent
    /// <see cref="TournamentController"/> decides which via its phase):
    ///
    ///   • <b>Lobby / intro</b> (fresh start) — shows the game lineup; on the host, advances into
    ///     the first game (auto after <see cref="autoStartDelaySeconds"/> or via <see cref="hostStartButton"/>).
    ///   • <b>Summary</b> (after the last game's Continue) — shows all results, with host-only
    ///     <see cref="playAgainButton"/> (restart the whole tournament) and <see cref="mainMenuButton"/>
    ///     (take the party back to the lava-lamp menu).
    ///
    /// Runs on every peer; only the host drives transitions — clients follow the Single load.
    /// All buttons are host-only.
    /// </summary>
    public class TournamentSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;

        [Header("Shared")]
        [SerializeField] TMP_Text titleText;

        [Header("Lobby (intro)")]
        [Tooltip("Optional root for the lobby/intro layout — toggled on for a fresh start.")]
        [SerializeField] GameObject lobbyRoot;
        [Tooltip("Shows the ordered game lineup.")]
        [SerializeField] TMP_Text lineupText;
        [Tooltip("Optional host-only Start button. If wired, the host taps it to begin; otherwise " +
                 "the host auto-starts after autoStartDelaySeconds.")]
        [SerializeField] GameObject hostStartButton;
        [Tooltip("Seconds the lobby is shown before the host auto-advances to game 1 (no Start button).")]
        [SerializeField, Min(0f)] float autoStartDelaySeconds = 3f;

        [Header("Summary (results)")]
        [Tooltip("Optional root for the results layout — toggled on after the last game.")]
        [SerializeField] GameObject summaryRoot;
        [Tooltip("Displays the final standings + per-game results.")]
        [SerializeField] TMP_Text resultsText;
        [Tooltip("Host-only. Restarts the whole tournament. Wire onClick → OnPlayAgainPressed().")]
        [SerializeField] GameObject playAgainButton;
        [Tooltip("Host-only. Returns the party to the lava-lamp menu. Wire onClick → OnMainMenuPressed().")]
        [SerializeField] GameObject mainMenuButton;
        [Tooltip("The shared main-menu SOAP event (same asset the Scoreboard's Main Menu uses, " +
                 "that SceneLoader listens to). Raised by Main Menu to return everyone to Menu_Main.")]
        [SerializeField] ScriptableEventNoParam onClickToMainMenu;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        bool HasStartButtonFlow => hostStartButton != null;

        // Player-facing mode name. SINGLE SOURCE: the Arcade card's DisplayName (currently "Shuffle"),
        // wired via TournamentDataSO.ModeCard — so the in-scene title tracks the same string the Arcade
        // grid card shows. Falls back to "Tournament" if ModeCard is unwired. To rename the mode for
        // players, change ONLY that card's DisplayName — see Docs/ShuffleSystem/ARCHITECTURE.md.
        string ModeName =>
            tournamentData != null && tournamentData.ModeCard != null
            && !string.IsNullOrEmpty(tournamentData.ModeCard.DisplayName)
                ? tournamentData.ModeCard.DisplayName
                : "Tournament";

        void Start()
        {
            // Lift the loading splash that the Single load left opaque (no vessel here to raise
            // OnClientReady). SceneLoader.FadeFromSplashOnReady was armed by LaunchGame.
            if (gameData != null)
                gameData.InvokeClientReady();

            bool summary = TournamentController.Instance != null && TournamentController.Instance.IsShowingSummary;
            if (summary)
                ShowSummary();
            else
                ShowLobby();
        }

        // ── Lobby ────────────────────────────────────────────────────────────────

        void ShowLobby()
        {
            if (lobbyRoot) lobbyRoot.SetActive(true);
            if (summaryRoot) summaryRoot.SetActive(false);
            if (playAgainButton) playAgainButton.SetActive(false);
            if (mainMenuButton) mainMenuButton.SetActive(false);

            RenderLineup();

            if (hostStartButton) hostStartButton.SetActive(IsHost && HasStartButtonFlow);
            if (IsHost && !HasStartButtonFlow)
                StartCoroutine(AutoStartRoutine());
        }

        IEnumerator AutoStartRoutine()
        {
            yield return new WaitForSecondsRealtime(autoStartDelaySeconds);
            BeginFirstGame();
        }

        void RenderLineup()
        {
            if (titleText) titleText.text = ModeName.ToUpperInvariant();
            if (lineupText == null || tournamentData == null) return;

            var sb = new StringBuilder();
            for (int i = 0; i < tournamentData.GameQueue.Count; i++)
            {
                var game = tournamentData.GameQueue[i];
                sb.AppendLine($"Game {i + 1}: {(game != null ? game.DisplayName : "—")}");
            }
            lineupText.text = sb.ToString().TrimEnd();
        }

        /// <summary>Host entry point — wire <see cref="hostStartButton"/>'s onClick here if using a button.</summary>
        public void OnHostStartPressed() => BeginFirstGame();

        void BeginFirstGame()
        {
            if (!IsHost) return;
            if (TournamentController.Instance != null)
                TournamentController.Instance.BeginFirstGame();
            else
                CSDebug.LogError("[TournamentSceneView] TournamentController.Instance is null — cannot begin the tournament.");
        }

        // ── Summary (results) ──────────────────────────────────────────────────────

        void ShowSummary()
        {
            if (summaryRoot) summaryRoot.SetActive(true);
            if (lobbyRoot) lobbyRoot.SetActive(false);
            if (hostStartButton) hostStartButton.SetActive(false);

            RenderSummary();

            // Buttons are host-only; clients just follow the host's choice.
            if (playAgainButton) playAgainButton.SetActive(IsHost);
            if (mainMenuButton) mainMenuButton.SetActive(IsHost);
        }

        void RenderSummary()
        {
            if (titleText) titleText.text = $"{ModeName.ToUpperInvariant()} RESULTS";
            if (resultsText == null || tournamentData == null) return;

            var standings = tournamentData.BuildSortedStandings();
            var sb = new StringBuilder();

            sb.AppendLine("<b>FINAL STANDINGS</b>");
            for (int i = 0; i < standings.Count; i++)
                sb.AppendLine($"{i + 1}. {standings[i].Domain} — {standings[i].TotalPoints} pts");

            // Per-game results: order domains by their finishing place in each game.
            for (int g = 0; g < tournamentData.GameQueue.Count; g++)
            {
                var game = tournamentData.GameQueue[g];
                sb.AppendLine();
                sb.AppendLine($"<b>{(game != null ? game.DisplayName : $"Game {g + 1}")}</b>");
                foreach (var s in standings.Where(s => g < s.Placements.Count).OrderBy(s => s.Placements[g]))
                    sb.AppendLine($"  {Ordinal(s.Placements[g])}: {s.Domain}");
            }

            resultsText.text = sb.ToString().TrimEnd();
        }

        /// <summary>Host-only. Wire the summary Play Again button's onClick here. Restarts the tournament.</summary>
        public void OnPlayAgainPressed()
        {
            if (!IsHost) return;
            if (TournamentController.Instance != null)
                TournamentController.Instance.RestartTournament();
            else
                CSDebug.LogError("[TournamentSceneView] TournamentController.Instance is null — cannot restart.");
        }

        /// <summary>Host-only. Wire the summary Main Menu button's onClick here. Returns everyone to Menu_Main.</summary>
        public void OnMainMenuPressed()
        {
            if (!IsHost) return;
            if (onClickToMainMenu != null)
                onClickToMainMenu.Raise();   // → SceneLoader.ReturnToMainMenu (host loads, clients follow)
            else
                CSDebug.LogError("[TournamentSceneView] onClickToMainMenu event not wired — cannot return to menu.");
        }

        static string Ordinal(int n) => n switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{n}th",
        };
    }
}
