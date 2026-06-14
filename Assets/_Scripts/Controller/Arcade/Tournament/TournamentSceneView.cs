using System.Collections;
using System.Text;
using CosmicShore.Utility;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// UI view for the Tournament lobby/intro scene. The persistent
    /// <see cref="TournamentController"/> owns all tournament logic; this view only renders the
    /// lineup, lifts the loading splash so the scene is visible, and (on the host) advances into
    /// the first game — either automatically after <see cref="autoStartDelaySeconds"/> or when the
    /// host presses <see cref="hostStartButton"/>.
    ///
    /// Runs on every peer (the lobby scene is loaded via Netcode on the whole party); only the
    /// host actually begins the first game — clients follow the Single load.
    /// </summary>
    public class TournamentSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;

        [Header("UI")]
        [SerializeField] TMP_Text titleText;
        [Tooltip("Shows the ordered game lineup (and is a natural home for cumulative standings later).")]
        [SerializeField] TMP_Text lineupText;
        [Tooltip("Optional host-only 'Start' button. If wired, the host taps it to begin; otherwise " +
                 "the host auto-starts after autoStartDelaySeconds.")]
        [SerializeField] GameObject hostStartButton;

        [Header("Flow")]
        [Tooltip("Seconds the lobby is shown before the host advances to game 1 (when no host Start " +
                 "button is wired).")]
        [SerializeField, Min(0f)] float autoStartDelaySeconds = 3f;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        void Start()
        {
            // Lift the loading splash that the Single load left opaque (the lobby has no vessel to
            // raise OnClientReady itself). SceneLoader.FadeFromSplashOnReady was armed by LaunchGame.
            if (gameData != null)
                gameData.InvokeClientReady();

            RenderLineup();

            // Host Start button is host-only; clients never see it.
            if (hostStartButton)
                hostStartButton.SetActive(IsHost && HasStartButtonFlow);

            if (IsHost && !HasStartButtonFlow)
                StartCoroutine(AutoStartRoutine());
        }

        bool HasStartButtonFlow => hostStartButton != null;

        IEnumerator AutoStartRoutine()
        {
            yield return new WaitForSecondsRealtime(autoStartDelaySeconds);
            BeginFirstGame();
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

        void RenderLineup()
        {
            if (titleText) titleText.text = "TOURNAMENT";

            if (lineupText == null || tournamentData == null) return;

            var sb = new StringBuilder();
            for (int i = 0; i < tournamentData.GameQueue.Count; i++)
            {
                var game = tournamentData.GameQueue[i];
                string name = game != null ? game.DisplayName : "—";
                sb.AppendLine($"Game {i + 1}: {name}");
            }
            lineupText.text = sb.ToString().TrimEnd();
        }
    }
}
