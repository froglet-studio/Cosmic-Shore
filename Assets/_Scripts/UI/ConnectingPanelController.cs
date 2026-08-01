using System;
using System.Text;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// In-game connecting panel - lives under the MiniGameHUD in every game scene. Shown at the start of
    /// each game, BEFORE the pre-game cinematic:
    ///   • enables its own embedded <see cref="connectingCamera"/> (posed in the prefab) and turns it off
    ///     again when done, so the gameplay camera takes over;
    ///   • animates the "CONNECTING TO SHORE…." status dots (., .., …, …. on a loop);
    ///   • shows the game mode + intensity ("HEX RACE - INTENSITY 4");
    ///   • in a Maelstrom run, also shows the per-domain rank (each domain coloured) - hidden otherwise.
    /// Holds for <see cref="dwellSeconds"/> (2s), then hides. MiniGameHUD awaits <see cref="ShowAsync"/>.
    /// </summary>
    public class ConnectingPanelController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;

        [Header("References")]
        [Tooltip("Optional CanvasGroup used to show/hide the panel UI (auto-added if missing).")]
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("Embedded camera, posed in the prefab. Enabled only while the panel is up; disabled on " +
                 "hide so the gameplay camera takes over.")]
        [SerializeField] Camera connectingCamera;

        [Header("Status (\"CONNECTING TO SHORE….\")")]
        [SerializeField] TMP_Text statusText;
        [SerializeField] string statusBaseText = "CONNECTING TO SHORE";
        [Tooltip("Seconds per dot step in the ., .., …, …. loop.")]
        [SerializeField, Min(0.05f)] float dotInterval = 0.35f;

        [Header("Game mode + intensity")]
        [SerializeField] TMP_Text gameModeText;

        [Header("Maelstrom rank (tournament only)")]
        [Tooltip("Shows the ranked domains (each coloured); the whole object is hidden outside a tournament.")]
        [SerializeField] TMP_Text maelstromRankText;
        [SerializeField] string rankHeader = "DOMAIN RANK";

        [Header("Timing")]
        [SerializeField, Min(0f)] float dwellSeconds = 2f;

        bool _showing;
        float _dotTimer;

        void Awake() => Hide();

        void Update()
        {
            if (!_showing || !statusText) return;
            _dotTimer += Time.unscaledDeltaTime;
            int dots = 1 + (int)(_dotTimer / dotInterval) % 4;   // 1..4 on a loop

            // While the arena build holds this panel up, run a live readout under the status
            // line — elapsed clock + progress — so the wait reads as a loading bar, not a hang.
            // Phases mirror the arena-ready gate: laying (prisms being placed) → growing
            // (placed prisms finishing their grow-in behind the covered screen). Rendered into
            // statusText so no scene/prefab rewiring is needed.
            if (PrismTrailBuilder.IsLayingInProgress)
            {
                statusText.text =
                    $"{statusBaseText}{new string('.', dots)}\n" +
                    $"<size=70%>BUILDING ARENA  {PrismTrailBuilder.LayProgress:P0}  " +
                    $"({PrismTrailBuilder.LayDoneCount:N0} / {PrismTrailBuilder.LayQueuedCount:N0})  ·  {_dotTimer:F0}s</size>";
            }
            else if (PrismTrailBuilder.GrowRemainingCount > 0)
            {
                statusText.text =
                    $"{statusBaseText}{new string('.', dots)}\n" +
                    $"<size=70%>GROWING ARENA  ({PrismTrailBuilder.GrowRemainingCount:N0} settling)  ·  {_dotTimer:F0}s</size>";
            }
            else
            {
                statusText.text = statusBaseText + new string('.', dots);
            }
        }

        /// <param name="ct">Cancellation (HUD lifecycle).</param>
        /// <param name="holdUntil">Optional extra hold: after the dwell, the panel stays up until
        /// this returns true (checked once per frame). Used to keep the connecting screen covering
        /// the world until the arena is ready (every build executed, every lay drained, every
        /// prism fully grown — PrismTrailBuilder.PollArenaReady) so the player never sees the
        /// structure lay or bloom in.</param>
        public async UniTask ShowAsync(CancellationToken ct, Func<bool> holdUntil = null)
        {
            _dotTimer = 0f;
            SetVisible(true);
            if (connectingCamera) connectingCamera.enabled = true;

            RenderGameMode();
            RenderRank();

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(dwellSeconds), ignoreTimeScale: true, cancellationToken: ct);
                if (holdUntil != null)
                {
                    while (!holdUntil())
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                Hide();
            }
        }

        void RenderGameMode()
        {
            if (!gameModeText) return;
            string mode = ResolveModeName(gameData != null ? gameData.GameMode : default);
            int intensity = gameData != null && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 0;
            gameModeText.text = intensity > 0 ? $"{mode} - INTENSITY {intensity}" : mode;
        }

        void RenderRank()
        {
            if (!maelstromRankText) return;

            bool maelstrom = gameData != null && gameData.IsTournamentMode && tournamentData != null;
            maelstromRankText.gameObject.SetActive(maelstrom);
            if (!maelstrom) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(rankHeader)) sb.Append(rankHeader);
            var sorted = tournamentData.BuildSortedStandings();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sb.Length > 0) sb.Append('\n');
                var d = sorted[i].Domain;
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(DomainColor(d))}>{d.ToString().ToUpperInvariant()}</color>");
            }
            maelstromRankText.text = sb.ToString();
        }

        string ResolveModeName(GameModes mode)
        {
            if (tournamentData != null && tournamentData.GameQueue != null)
                foreach (var card in tournamentData.GameQueue)
                    if (card != null && card.Mode == mode && !string.IsNullOrEmpty(card.DisplayName))
                        return card.DisplayName.ToUpperInvariant();
            return mode.ToString().ToUpperInvariant();
        }

        // Theme per-domain UI accent (same source as the Maelstrom cards); white when no theme is wired.
        Color DomainColor(Domains d) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIAccentColor(d)
                : Color.white;

        void SetVisible(bool visible)
        {
            _showing = visible;
            EnsureCanvasGroup();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        void Hide()
        {
            SetVisible(false);
            if (connectingCamera) connectingCamera.enabled = false;
        }

        void EnsureCanvasGroup()
        {
            if (canvasGroup) return;
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
