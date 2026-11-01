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
    /// The in-game loading screen - lives under the MiniGameHUD in every game scene, shown at the start of each game
    /// BEFORE the pre-game intro. Since Rev 33 it is the "cell gate" (Docs/MENU_REDESIGN_B.md section 99, board A):
    /// the cell-change screen grown into the mode entry, drawn by <see cref="ConnectingGateView"/>.
    ///
    /// <para>It COVERS the arena. Nothing renders the world while it is up: it has no camera and no live preview, and
    /// it parks the gameplay camera (no layers, no post, no shadows) until the gate opens. Covered, the build runs
    /// at the cell-change screen's tempo and the grow-in is skipped (<see cref="PrismTrailBuilder.LoadGateCovered"/>)
    /// - the two extra world renders, the small watched build slices and the waited-out bloom were the measured
    /// mode-entry lag (section 99.1-99.2).</para>
    ///
    /// Holds for at least <see cref="dwellSeconds"/>, then until the arena is ready and every human pilot's arena is
    /// built; then the crystal's hex opens into the arena. MiniGameHUD awaits <see cref="ShowAsync"/>.
    /// </summary>
    public class ConnectingPanelController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] MaelstromDataSO tournamentData;

        [Header("References")]
        [Tooltip("Optional CanvasGroup used to show/hide the panel UI (auto-added if missing).")]
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("The cell-gate look: crystal, steps, brief, footer and the gate that opens into the arena.")]
        [SerializeField] ConnectingGateView gate;

        [Header("Mode")]
        [Tooltip("The mode's title, big, under ENTERING.")]
        [SerializeField] TMP_Text gameModeText;
        [Tooltip("Where a mode's display name and tips are looked up (the arcade's game list).")]
        [SerializeField] SO_GameList games;
        [Tooltip("Where a mode's tagline and objective are looked up - the same words its intro shows.")]
        [SerializeField] IntroCameraLibrarySO introWords;

        [Header("Phase (the steps panel's header)")]
        [SerializeField] TMP_Text phaseTitle;
        [Tooltip("Optional. Replays on the header whenever the phase changes.")]
        [SerializeField] KitEntrance phaseTitleEntrance;
        [SerializeField] string connectingTitle = "Connecting to shore";
        [SerializeField] string buildingTitle = "Building the arena";
        [SerializeField] string settlingTitle = "Settling the arena";
        [SerializeField] string waitingTitle = "Waiting for pilots";

        [Header("Progress bar")]
        [Tooltip("Progress of the arena build, 0..1. Forced non-interactable and stripped of its " +
                 "handle at Awake - it is a READOUT, and a slider a player can drag is a lie about " +
                 "who is in control of the load.")]
        [SerializeField] Slider progressSlider;

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
        [Tooltip("The pilots panel. The panel also waits for every HUMAN player's arena to finish before it " +
                 "comes down, so nobody drops into the intro while a teammate is still loading.")]
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

        // True while this machine's arena is built and the panel is only still up because another
        // human is not. Drives the header - the pilots panel already says WHO.
        bool _waitingForPeers;

        // The build's stage, as the cell-change screen reads it (EnvironmentLoadVeil.UpdateStage).
        int _step = -1;
        float _level;
        int _growPeak;
        bool _sawBuild;

        readonly ArenaLoadProgress _progress = new();
        readonly PilotCameraPark _park = new();

        void Awake()
        {
            StyleProgressBar();
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

        /// <summary>
        /// Make the slider a READOUT rather than a control: no interaction, no handle, no selection
        /// transition, no navigation. The LOOK is authored in the prefab; this only enforces the behaviour.
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

            if (progressSlider.handleRect)
                progressSlider.handleRect.gameObject.SetActive(false);
            progressSlider.handleRect = null;
        }

        void Update()
        {
            if (!_showing || _opening) return;

            ParkGameplayCamera();
            float bar = TickProgress();
            UpdateStage();
            if (gate)
                gate.Tick(_step, _level, bar, PrismTrailBuilder.LayDoneCount, PrismTrailBuilder.LayQueuedCount);
            DrawPhase();
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
                Time.unscaledDeltaTime,
                PrismTrailBuilder.IsLayingInProgress,
                PrismTrailBuilder.LayProgress,
                PrismTrailBuilder.GrowRemainingCount,
                _arenaReady);

            _shownProgress = Mathf.Lerp(_shownProgress, target, Mathf.Clamp01(Time.unscaledDeltaTime / barSmoothing));
            if (progressSlider) progressSlider.value = _shownProgress;
            return _shownProgress;
        }

        // The four steps and the crystal, read the way the cell-change screen reads them. The panel's gate is held
        // throughout; "released" is the gate opening, so a peer wait sits on the all-clear step.
        void UpdateStage()
        {
            bool laying = PrismTrailBuilder.IsLayingInProgress;
            int grow = PrismTrailBuilder.GrowRemainingCount;
            if (laying || grow > 0) _sawBuild = true;
            _growPeak = Mathf.Max(_growPeak, grow);
            _step = EnvironmentLoadVeil.StepFor(_step, true, false, laying, grow, _sawBuild || _arenaReady);
            if (_arenaReady) _step = Mathf.Max(_step, 2);
            _level = EnvironmentLoadVeil.CrystalLevel(_level, _step, PrismTrailBuilder.LayProgress, grow, _growPeak);
        }

        /// <param name="ct">Cancellation (HUD lifecycle).</param>
        /// <param name="holdUntil">Optional extra hold: after the dwell, the panel stays up until this returns true
        /// (checked once per frame) - the arena-ready gate (PrismTrailBuilder.PollArenaReady), so the player never
        /// sees the structure lay or bloom in.</param>
        public async UniTask ShowAsync(CancellationToken ct, Func<bool> holdUntil = null)
        {
            _shownProgress = 0f;
            _arenaReady = false;
            _waitingForPeers = false;
            _opening = false;
            _step = -1;
            _level = 0f;
            _growPeak = 0;
            _sawBuild = false;
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

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(dwellSeconds), ignoreTimeScale: true, cancellationToken: ct);
                if (holdUntil != null)
                {
                    while (!holdUntil())
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // THIS machine's arena is complete: the bar finishes here, because the bar measures the build and
                // the build is done. Whatever is waited on next is a different wait and says so in its own words.
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

                    // The local player may spawn after the build finishes on a slow client; keep asking rather than
                    // reporting once into a null LocalPlayer and hanging on ourselves.
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

        // The steps panel's header: the phase, from authored words.
        void DrawPhase()
        {
            if (!phaseTitle) return;
            string title = _waitingForPeers ? waitingTitle
                : PrismTrailBuilder.IsLayingInProgress ? buildingTitle
                : PrismTrailBuilder.GrowRemainingCount > 0 ? settlingTitle
                : _sawBuild ? settlingTitle
                : connectingTitle;
            if (phaseTitle.text == title) return;
            phaseTitle.text = title;
            if (phaseTitleEntrance) phaseTitleEntrance.Play();
        }

        void RenderGameMode()
        {
            var mode = gameData != null ? gameData.GameMode : default;
            if (gameModeText) gameModeText.text = ResolveModeName(mode);

            var words = introWords ? introWords.WordsFor(mode) : null;
            int intensity = gameData != null && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 0;
            if (gate) gate.Bind(words?.Tagline, words?.Objective, intensity, ResolveTip(mode));
        }

        // One of the mode's own play tips (its arcade card's), or none.
        string ResolveTip(GameModes mode)
        {
            var card = FindCard(mode) as SO_ArcadeGame;
            if (card == null || card.Tips == null || card.Tips.Count == 0) return string.Empty;
            return card.Tips[UnityEngine.Random.Range(0, card.Tips.Count)];
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

        // The load is over: the world draws again, the tempo goes back, and the crystal's hex opens into the arena.
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
                _step = 3;
                _level = 1f;
                gate.Tick(_step, _level, 1f, PrismTrailBuilder.LayDoneCount, PrismTrailBuilder.LayQueuedCount);
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
