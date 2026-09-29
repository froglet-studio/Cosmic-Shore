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
        [SerializeField] MaelstromDataSO tournamentData;

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

        [Tooltip("Seconds the bar takes to catch up to the model. The phases step, and a bar that " +
                 "steps with them reads as broken; easing makes the same numbers read as motion.")]
        [SerializeField, Min(0.01f)] float barSmoothing = 0.25f;

        [Header("Arena preview")]
        [Tooltip("Optional live window onto the arena being built. Without one the panel simply " +
                 "shows no preview.")]
        [SerializeField] ConnectingArenaPreview arenaPreview;

        [Header("Build tempo (while this panel holds the gate)")]
        [Tooltip("Per-frame slice for LAYING prisms, in milliseconds. The load gate's own tempo " +
                 "(250ms) is sized on the premise that the screen is opaque and no frame is worth " +
                 "protecting — which stopped being true the moment this panel started showing the " +
                 "arena being built. Both dials are work-conserving: the same prisms are laid " +
                 "either way, so a smaller slice costs a little load time and buys the frame rate " +
                 "the view is read at. 0 = keep the full covered-screen tempo.")]
        [SerializeField, Min(0f)] float watchedLayBudgetMs = 25f;

        [Tooltip("Per-frame slice for prism CREATION completions, in milliseconds — the second " +
                 "half of the same trade (the gate otherwise drains 512 completions per frame). " +
                 "0 = keep the full covered-screen tempo.")]
        [SerializeField, Min(0f)] float watchedCreationBudgetMs = 18f;

        [Header("Players")]
        [Tooltip("Optional row of pilot chips. With one wired, the panel also waits for every " +
                 "HUMAN player's arena to finish before it comes down — so nobody drops into the " +
                 "cinematic while a teammate is still loading.")]
        [SerializeField] ConnectingPlayerRoster playerRoster;

        [Tooltip("Seconds the panel will wait on a peer that never reports. A player who crashed " +
                 "or dropped mid-load must not be able to hold everyone else on a loading screen " +
                 "forever; the wait releases loud rather than silently.")]
        [SerializeField, Min(1f)] float peerWaitTimeoutSeconds = 45f;

        [Header("Maelstrom rank (tournament only)")]
        [Tooltip("Shows the ranked domains (each coloured); the whole object is hidden outside a tournament.")]
        [SerializeField] TMP_Text maelstromRankText;
        [SerializeField] string rankHeader = "DOMAIN RANK";

        [Header("Timing")]
        [SerializeField, Min(0f)] float dwellSeconds = 2f;

        bool _showing;
        float _dotTimer;
        float _shownProgress;

        // True while this machine's arena is built and the panel is only still up because another
        // human is not. Drives the status line — the roster chips already say WHO.
        bool _waitingForPeers;

        readonly ArenaLoadProgress _progress = new();

        void Awake()
        {
            StyleProgressBar();
            EnsurePlayerRoster();
            Hide();
        }

        /// <summary>
        /// The pilot row is STRUCTURAL, not opt-in: waiting for a teammate's arena is a property
        /// of the load, not of how a particular panel prefab was authored, and a prefab that
        /// carries the art but not the component would show a row that never lights up. So the
        /// panel ensures one and hands it its sources; an authored component with authored
        /// references is left exactly as it is. The roster finds its own container (a descendant
        /// named "PlayerIcons") and its own chip template, so no wiring is implied either way.
        /// </summary>
        void EnsurePlayerRoster()
        {
            if (!playerRoster) playerRoster = GetComponentInChildren<ConnectingPlayerRoster>(true);
            if (!playerRoster) playerRoster = gameObject.AddComponent<ConnectingPlayerRoster>();

            var hud = GetComponentInParent<MiniGameHUD>(true);
            playerRoster.AdoptSources(gameData, hud ? hud.ProfileIcons : null);
        }

        /// <summary>
        /// Make the slider a READOUT rather than a control: no interaction, no handle, no selection
        /// transition, no navigation.
        ///
        /// <para>The LOOK is authored in the prefab (the capsule sprites, their tints, the bar's
        /// height and insets) — this only enforces the behaviour, so an art pass never has to come
        /// back through code. These four are enforced here anyway because each is a way for a stock
        /// UGUI slider to behave like a control, and a progress bar a player can drag is a lie
        /// about who is in charge of the load.</para>
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
            else if (_waitingForPeers && playerRoster)
            {
                statusText.text =
                    $"WAITING FOR PLAYERS{new string('.', dots)}\n" +
                    $"<size=70%>{playerRoster.ReadyHumanCount} / {playerRoster.HumanCount} READY</size>";
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
            _waitingForPeers = false;
            _progress.Reset();

            SetVisible(true);
            if (connectingCamera) connectingCamera.enabled = true;

            // This hold is WATCHED — the panel shows the build. State the tempo that costs.
            if (watchedLayBudgetMs > 0f)
                PrismTrailBuilder.LoadGateLayBudgetOverrideMs = watchedLayBudgetMs;
            if (watchedCreationBudgetMs > 0f)
                PrismTrailBuilder.LoadGateCreationBudgetMsOverride = watchedCreationBudgetMs;

            if (playerRoster) playerRoster.Begin();

            if (arenaPreview)
            {
                // The panel's backdrop camera is SPOKEN FOR. Said before Begin so the preview can
                // correct a wiring that would otherwise take it over and make the backdrop vanish
                // with nothing in the console.
                arenaPreview.ReserveCamera(connectingCamera);
                arenaPreview.Begin();
            }

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

                // THIS machine's arena is complete: the bar finishes here, because the bar
                // measures the build and the build is done. Whatever is waited on next is a
                // different wait and says so in its own words.
                _arenaReady = true;

                await WaitForPeersAsync(ct);
            }
            finally
            {
                Hide();
            }
        }

        /// <summary>
        /// Hold until every HUMAN pilot has reported their own arena built.
        ///
        /// <para>The arena is built independently on every peer — each machine runs its own
        /// spawner off its own clock — so "loaded" is per-player state, not a server fact. Each
        /// owner reports through <see cref="IPlayer.ReportArenaReady"/> and the answer replicates,
        /// which is what lets this panel name the players it is waiting on rather than saying
        /// "connecting" at a pilot who has been sitting in the cinematic for ten seconds.</para>
        ///
        /// <para>Bounded by <see cref="peerWaitTimeoutSeconds"/>: a player who crashed or dropped
        /// during the load must not be able to pin everyone else to a loading screen. Releasing
        /// is loud, because a timeout here means somebody is about to start a match a player
        /// short.</para>
        /// </summary>
        async UniTask WaitForPeersAsync(CancellationToken ct)
        {
            if (!playerRoster) return;

            playerRoster.ReportLocalReady();
            if (playerRoster.AllHumansReady) return;

            _waitingForPeers = true;
            float waited = 0f;
            try
            {
                while (!playerRoster.AllHumansReady)
                {
                    if (waited >= peerWaitTimeoutSeconds)
                    {
                        CSDebug.LogWarning(
                            $"[ConnectingPanel] Released after waiting {peerWaitTimeoutSeconds:F0}s for " +
                            $"{playerRoster.HumanCount - playerRoster.ReadyHumanCount} player(s) who never " +
                            "reported their arena built - starting anyway.");
                        return;
                    }

                    // The local player may spawn after the build finishes on a slow client; keep
                    // asking rather than reporting once into a null LocalPlayer and hanging on
                    // ourselves.
                    playerRoster.ReportLocalReady();

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    waited += Time.unscaledDeltaTime;
                }
            }
            finally
            {
                _waitingForPeers = false;
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

            bool maelstrom = gameData != null && gameData.IsMaelstromMode && tournamentData != null;
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
            if (playerRoster) playerRoster.End();

            // The watched tempo belonged to this hold. SetLoadGateHolding(false) clears it too;
            // clearing it here as well means a cancelled ShowAsync cannot leave the next load
            // running at a slice nobody asked for.
            PrismTrailBuilder.LoadGateLayBudgetOverrideMs = 0f;
            PrismTrailBuilder.LoadGateCreationBudgetMsOverride = 0f;
        }

        void EnsureCanvasGroup()
        {
            if (canvasGroup) return;
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
