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
using UnityEngine.UI;

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

        [Header("Progress bar")]
        [Tooltip("Progress of the arena build, 0..1. Forced non-interactable and stripped of its " +
                 "handle at Awake - it is a READOUT, and a slider a player can drag is a lie about " +
                 "who is in control of the load.")]
        [SerializeField] Slider progressSlider;

        [Tooltip("Optional 9-sliced capsule for the empty channel. Authored by " +
                 "Tools/Build/author_loading_bar_sprites.py; white, tinted below.")]
        [SerializeField] Sprite trackSprite;

        [Tooltip("Optional 9-sliced capsule for the filled part.")]
        [SerializeField] Sprite fillSprite;

        [SerializeField] Color trackColor = new(0.10f, 0.18f, 0.28f, 0.85f);
        [SerializeField] Color fillColor = new(0.29f, 0.72f, 1f, 1f);

        [Tooltip("Seconds the bar takes to catch up to the model. The phases step, and a bar that " +
                 "steps with them reads as broken; easing makes the same numbers read as motion.")]
        [SerializeField, Min(0.01f)] float barSmoothing = 0.25f;

        [Header("Arena preview")]
        [Tooltip("Optional live window onto the arena being built. Without one the panel simply " +
                 "shows no preview.")]
        [SerializeField] ConnectingArenaPreview arenaPreview;

        [Header("Maelstrom rank (tournament only)")]
        [Tooltip("Shows the ranked domains (each coloured); the whole object is hidden outside a tournament.")]
        [SerializeField] TMP_Text maelstromRankText;
        [SerializeField] string rankHeader = "DOMAIN RANK";

        [Header("Timing")]
        [SerializeField, Min(0f)] float dwellSeconds = 2f;

        bool _showing;
        float _dotTimer;
        float _shownProgress;

        readonly ArenaLoadProgress _progress = new();

        void Awake()
        {
            StyleProgressBar();
            Hide();
        }

        /// <summary>
        /// Make the slider a readout: no interaction, no handle, no selection transition, and the
        /// authored capsule art if it is wired. Done in code rather than left to the prefab because
        /// every one of these is a way for a stock UGUI slider to look and behave like a control.
        /// </summary>
        void StyleProgressBar()
        {
            if (!progressSlider) return;

            progressSlider.interactable = false;
            progressSlider.transition = Selectable.Transition.None;
            progressSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.wholeNumbers = false;
            progressSlider.value = 0f;

            // A handle is the affordance that says "drag me". There is nothing to drag.
            if (progressSlider.handleRect)
                progressSlider.handleRect.gameObject.SetActive(false);
            progressSlider.handleRect = null;

            ApplyBarImage(progressSlider.targetGraphic as Image, trackSprite, trackColor);
            ApplyBarImage(progressSlider.fillRect ? progressSlider.fillRect.GetComponent<Image>() : null,
                          fillSprite, fillColor);
        }

        static void ApplyBarImage(Image image, Sprite sprite, Color color)
        {
            if (!image) return;

            if (sprite)
            {
                image.sprite = sprite;
                // Sliced, so the capsule's round caps ride the border instead of stretching into
                // ellipses as the bar grows - which is the whole reason the sprite has one.
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            image.color = color;
            image.raycastTarget = false;
        }

        void Update()
        {
            if (!_showing) return;

            TickProgress();

            if (!statusText) return;
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

        /// <summary>
        /// Advance the bar. The model owns the phase arithmetic and monotonicity; this only eases
        /// the drawn value toward it, because the phases STEP and a bar that steps with them reads
        /// as broken.
        /// </summary>
        void TickProgress()
        {
            if (!progressSlider) return;

            float target = _progress.Tick(
                Time.unscaledDeltaTime,
                PrismTrailBuilder.IsLayingInProgress,
                PrismTrailBuilder.LayProgress,
                PrismTrailBuilder.GrowRemainingCount,
                _arenaReady);

            _shownProgress = Mathf.Lerp(_shownProgress, target,
                                        Mathf.Clamp01(Time.unscaledDeltaTime / barSmoothing));
            progressSlider.value = _shownProgress;
        }

        /// <summary>
        /// Latched true once the caller's hold predicate is satisfied, so the bar can finish at
        /// exactly 1 rather than vanishing at 0.9 - which reads as an abandoned load rather than a
        /// completed one.
        /// </summary>
        bool _arenaReady;

        /// <param name="ct">Cancellation (HUD lifecycle).</param>
        /// <param name="holdUntil">Optional extra hold: after the dwell, the panel stays up until
        /// this returns true (checked once per frame). Used to keep the connecting screen covering
        /// the world until the arena is ready (every build executed, every lay drained, every
        /// prism fully grown — PrismTrailBuilder.PollArenaReady) so the player never sees the
        /// structure lay or bloom in.</param>
        public async UniTask ShowAsync(CancellationToken ct, Func<bool> holdUntil = null)
        {
            _dotTimer = 0f;
            _shownProgress = 0f;
            _arenaReady = false;
            _progress.Reset();

            SetVisible(true);
            if (connectingCamera) connectingCamera.enabled = true;
            if (arenaPreview) arenaPreview.Begin();

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

                _arenaReady = true;
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

            // The preview owns a RenderTexture and (when it made one) a camera. Left running it is
            // both a GPU allocation nobody frees and a second camera rendering the world for the
            // whole match - so it comes down with the panel, on every exit including a cancelled
            // load.
            if (arenaPreview) arenaPreview.End();
        }

        void EnsureCanvasGroup()
        {
            if (canvasGroup) return;
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
