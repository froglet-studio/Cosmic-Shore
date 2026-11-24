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
    /// The in-game loading screen - lives under the MiniGameHUD in every game scene, shown at the start of each game
    /// BEFORE the pre-game intro. Since Rev 91 it is "the tour" (Docs/MENU_REDESIGN_B.md section 173, board F) on
    /// the Rev 33 cover (section 99): the parts of the game shown in turn over one bar cut into the load's real steps,
    /// drawn by <see cref="ConnectingGateView"/> and <see cref="KitStepBar"/>.
    ///
    /// <para>It COVERS the arena. Nothing renders the world while it is up: it has no camera and no live preview, and
    /// it parks the gameplay camera (no layers, no post, no shadows) until the gate opens. Covered, the build runs
    /// at the cell-change screen's tempo and the grow-in is skipped (<see cref="PrismTrailBuilder.LoadGateCovered"/>)
    /// - the two extra world renders, the small watched build slices and the waited-out bloom were the measured
    /// mode-entry lag (section 99.1-99.2).</para>
    ///
    /// Holds for at least <see cref="dwellSeconds"/>, then until the arena is ready and every human pilot's arena is
    /// built; then a hex opens from the centre into the arena. MiniGameHUD awaits <see cref="ShowAsync"/>.
    /// </summary>
    public class ConnectingPanelController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] MaelstromDataSO tournamentData;

        [Header("References")]
        [Tooltip("Optional CanvasGroup used to show/hide the panel UI (auto-added if missing).")]
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("The screen's look: the cover, the brief, the tour and the gate that opens into the arena.")]
        [SerializeField] ConnectingGateView gate;

        [Header("Mode")]
        [Tooltip("The mode's title, under ENTERING.")]
        [SerializeField] TMP_Text gameModeText;
        [Tooltip("Where a mode's display name is looked up (the arcade's game list).")]
        [SerializeField] SO_GameList games;
        [Tooltip("The fallback for a mode's name: the title its intro shows.")]
        [SerializeField] IntroCameraLibrarySO introWords;

        [Header("Progress bar")]
        [Tooltip("The one bar, cut into the load's real steps (laying, growing, pilots). It draws the eased value " +
                 "below and names the step that value is in; its words are its own authored fields.")]
        [SerializeField] KitStepBar stepBar;

        [Tooltip("Seconds the bar takes to catch up to the model. The phases step, and a bar that " +
                 "steps with them reads as broken; easing makes the same numbers read as motion.")]
        [SerializeField, Min(0.01f)] float barSmoothing = 0.25f;

        [Header("Build tempo (the screen is covered)")]
        [Tooltip("Per-frame slice for LAYING prisms, in milliseconds - the cell-change screen's 80 ms. Nothing is " +
                 "rendered behind this screen, so a frame is only worth the screen's own motion (about 10 fps).")]
        [SerializeField, Min(0f)] float coveredLayBudgetMs = 80f;

        [Tooltip("Per-frame slice for prism CREATION completions, in milliseconds. 0 = the gate's own 512 " +
                 "completions a frame, as under the cell-change screen.")]
        [SerializeField, Min(0f)] float coveredCreationBudgetMs;

        [Tooltip("What the parked gameplay camera clears to while the screen covers the arena.")]
        [SerializeField] Color parkedBackdrop = Color.black;

        [Header("Players")]
        [Tooltip("The pilots this load waits on (the tour screen does not draw them). The panel waits for every " +
                 "HUMAN player's arena to finish before it comes down, so nobody drops into the intro while a " +
                 "teammate is still loading; their count is the bar's last share.")]
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
        [Tooltip("The least time the screen stays up: long enough to read the mode. The arena-ready gate holds it " +
                 "longer when the build needs longer.")]
        [SerializeField, Min(0f)] float dwellSeconds = 1f;

        bool _showing;
        float _shownProgress;
        bool _arenaReady;
        bool _opening;

        readonly ArenaLoadProgress _progress = new();
        readonly PilotCameraPark _park = new();

        void Awake()
        {
            EnsurePlayerRoster();
            SetVisible(false);
        }

        void OnDestroy() => _park.Release();

        /// <summary>
        /// The pilot list is STRUCTURAL, not opt-in: waiting for a teammate's arena is a property
        /// of the load, not of how a particular panel prefab was authored. So the panel ensures one and
        /// hands it its sources; an authored component with authored references is left as it is.
        /// </summary>
        void EnsurePlayerRoster()
        {
            if (!playerRoster) playerRoster = GetComponentInChildren<ConnectingPlayerRoster>(true);
            if (!playerRoster) playerRoster = gameObject.AddComponent<ConnectingPlayerRoster>();

            var hud = GetComponentInParent<MiniGameHUD>(true);
            playerRoster.AdoptSources(gameData, hud ? hud.ProfileIcons : null);
        }

        void Update()
        {
            if (!_showing || _opening) return;

            ParkGameplayCamera();
            float bar = TickProgress();
            if (gate) gate.Tick(bar);
        }

        /// <summary>
        /// The world is covered: the gameplay camera draws nothing until the gate opens. Retried each frame, because
        /// the camera controller is only active once the local vessel is up. A camera something else already parked
        /// (a crew seat view) is left to its owner.
        /// </summary>
        void ParkGameplayCamera()
        {
            if (_park.Parked) return;
            var manager = CameraManager.Instance;
            if (manager == null || manager.GetActiveController() is not CustomCameraController controller || !controller)
                return;
            var cam = controller.Camera;
            if (!cam || cam.cullingMask == 0) return;
            _park.Park(cam, parkedBackdrop);
        }

        /// <summary>Advance the bar; returns the drawn value. The model owns the phases and monotonicity, this only
        /// eases toward it.</summary>
        float TickProgress()
        {
            float target = _progress.Tick(
                PrismTrailBuilder.IsLayingInProgress,
                PrismTrailBuilder.LayProgress,
                PrismTrailBuilder.GrowRemainingCount,
                _arenaReady,
                playerRoster ? playerRoster.ReadyHumanCount : 0,
                playerRoster ? playerRoster.HumanCount : 0);

            _shownProgress = Mathf.Lerp(_shownProgress, target, Mathf.Clamp01(Time.unscaledDeltaTime / barSmoothing));
            if (stepBar) stepBar.Show(_shownProgress, false);
            return _shownProgress;
        }

        /// <param name="ct">Cancellation (HUD lifecycle).</param>
        /// <param name="holdUntil">Optional extra hold: after the dwell, the panel stays up until this returns true
        /// (checked once per frame) - the arena-ready gate (PrismTrailBuilder.PollArenaReady), so the player never
        /// sees the structure lay or bloom in.</param>
        public async UniTask ShowAsync(CancellationToken ct, Func<bool> holdUntil = null)
        {
            _shownProgress = 0f;
            _arenaReady = false;
            _opening = false;
            _progress.Reset();

            SetVisible(true);

            // Covered from the first frame: the build may take the screen's tempo and skip the grow-in.
            PrismTrailBuilder.LoadGateLayBudgetOverrideMs = coveredLayBudgetMs;
            PrismTrailBuilder.LoadGateCreationBudgetMsOverride = coveredCreationBudgetMs;
            PrismTrailBuilder.LoadGateCovered = true;
            ParkGameplayCamera();

            if (playerRoster) playerRoster.Begin();
            RenderGameMode();
            RenderRank();
            if (gate) gate.BeginHold();
            if (stepBar) stepBar.Show(0f, false);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(dwellSeconds), ignoreTimeScale: true, cancellationToken: ct);
                if (holdUntil != null)
                {
                    while (!holdUntil())
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // THIS machine's arena is complete: the bar moves into its last share, the pilots who have reported
                // their own arena built (ArenaLoadProgress.PilotsFloor). Alone, that is all of them at once.
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
        /// <para>The arena is built independently on every peer, so "loaded" is per-player state, not a server fact.
        /// Each owner reports through <see cref="IPlayer.ReportArenaReady"/> and the answer replicates.</para>
        ///
        /// <para>Bounded by <see cref="peerWaitTimeoutSeconds"/>: a player who crashed or dropped during the load must
        /// not be able to pin everyone else to a loading screen. Releasing is loud.</para>
        /// </summary>
        async UniTask WaitForPeersAsync(CancellationToken ct)
        {
            if (!playerRoster) return;

            playerRoster.ReportLocalReady();
            if (playerRoster.AllHumansReady) return;

            float waited = 0f;
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

                // The local player may spawn after the build finishes on a slow client; keep asking rather than
                // reporting once into a null LocalPlayer and hanging on ourselves.
                playerRoster.ReportLocalReady();

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                waited += Time.unscaledDeltaTime;
            }
        }

        void RenderGameMode()
        {
            var mode = gameData != null ? gameData.GameMode : default;
            if (gameModeText) gameModeText.text = ResolveModeName(mode);
            if (gate) gate.Bind();
        }

        SO_Game FindCard(GameModes mode)
        {
            if (games != null && games.Games != null)
                foreach (var g in games.Games)
                    if (g != null && g.Mode == mode) return g;
            if (tournamentData != null && tournamentData.GameQueue != null)
                foreach (var g in tournamentData.GameQueue)
                    if (g != null && g.Mode == mode) return g;
            return null;
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

        // The mode's authored display name: its arcade card's, else its tournament card's, else the intro library's
        // title. The enum's name is the last resort for a mode no list knows.
        string ResolveModeName(GameModes mode)
        {
            var card = FindCard(mode);
            if (card != null && !string.IsNullOrEmpty(card.DisplayName)) return card.DisplayName;
            var words = introWords ? introWords.WordsFor(mode) : null;
            if (words != null && !string.IsNullOrEmpty(words.Title)) return words.Title;
            return mode.ToString();
        }

        // Theme per-domain UI accent (same source as the Maelstrom cards); white when no theme is wired.
        Color DomainColor(Domains d) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIAccentColor(d)
                : Color.white;

        void SetVisible(bool visible)
        {
            _showing = visible;
            // A load cancelled because its scene unloaded reaches Hide() from ShowAsync's finally AFTER this panel
            // was destroyed. There is no canvas left to fade.
            if (!this) return;
            EnsureCanvasGroup();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
        }

        // The load is over: the world draws again, the tempo goes back, the bar reads done and the gate opens.
        void Hide()
        {
            // The covered tempo belonged to this hold. SetLoadGateHolding(false) clears it too; clearing it here as
            // well means a cancelled ShowAsync cannot leave the next load at a slice nobody asked for.
            PrismTrailBuilder.LoadGateLayBudgetOverrideMs = 0f;
            PrismTrailBuilder.LoadGateCreationBudgetMsOverride = 0f;
            PrismTrailBuilder.LoadGateCovered = false;
            _park.Release();

            if (!this) return;
            if (playerRoster) playerRoster.End();

            if (gate && _showing && isActiveAndEnabled)
            {
                _opening = true;
                _shownProgress = 1f;
                if (stepBar) stepBar.Show(1f, true);
                gate.Tick(1f);
                EnsureCanvasGroup();
                canvasGroup.blocksRaycasts = false;
                gate.Open(() =>
                {
                    _opening = false;
                    SetVisible(false);
                });
                return;
            }
            SetVisible(false);
        }

        void EnsureCanvasGroup()
        {
            if (canvasGroup) return;
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
