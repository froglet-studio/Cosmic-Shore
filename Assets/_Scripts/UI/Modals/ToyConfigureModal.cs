using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Toy Box's <b>second window</b>: one toy, what it does, and the button that takes you to
    /// it. The arcade's card opens a lobby because a match needs configuring; a toy needs nothing
    /// configured, so this window asks nothing and offers one verb — <b>Navigate</b>.
    ///
    /// <para><b>It hands the player to the toy rather than operating it.</b> That is the whole
    /// difference from the shell surface this replaced: the menu used to be a remote control that
    /// applied a toy's actions from a list, which made it a second authority on what a toy does.
    /// Navigate drops the player in the lava lamp in front of the real ring, and from there the
    /// toy is the only thing that acts. A toy authored tomorrow is reachable here with no menu
    /// work at all, because the catalogue is <see cref="ToyShellRegistry"/> and the destination is
    /// the toy's own transform.</para>
    ///
    /// <para><b>The picture is the live toy</b> (<see cref="ToyPreviewCamera"/>) — a camera on the
    /// object standing out by the cell membrane, not authored art. So it cannot go stale, and the
    /// player recognises the thing they are about to fly at.</para>
    ///
    /// <para><b>Navigate is a three-step handoff and every step is load-bearing:</b> close this
    /// window, enter freestyle through the menu's own <see cref="MenuCrystalClickHandler"/>, and —
    /// only once the transition has actually ENDED — place the vessel. The wait is on
    /// <c>OnGameStateTransitionEnd</c> rather than on <c>IsInFreestyle</c>, which flips at the
    /// START of the transition while input is still paused and the camera is still blending: a
    /// pose written then is overwritten by the tail of the blend, and the player arrives somewhere
    /// else. Same trap, same fix, as the Wanderway handoff.</para>
    /// </summary>
    public class ToyConfigureModal : ModalWindowManager
    {
        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField, Tooltip("The toy's own authored description (ToyDefinitionSO.Description).")]
        TMP_Text descriptionText;
        [SerializeField, Tooltip("Which fundamental this toy changes - Pilot / World / Creation.")]
        TMP_Text categoryText;

        [SerializeField, Tooltip("Live window onto the toy standing in the lava lamp. Sits where " +
                 "the arcade card puts its arena preview.")]
        ToyPreviewCamera preview;

        [Header("Controls")]
        [SerializeField, Tooltip("Takes the player to the toy. Disabled while the toy cannot " +
                 "answer - mid cell-swap, mid vessel-swap.")]
        Button navigateButton;

        [SerializeField, Tooltip("Back to the toy grid.")]
        Button backButton;

        [Header("Freestyle handoff")]
        [SerializeField, Tooltip("The scene's freestyle toggle. REQUIRED - without it Navigate " +
                 "can only warn.")]
        MenuCrystalClickHandler crystalClickHandler;

        [SerializeField, Tooltip("Raises OnGameStateTransitionEnd, which is what Navigate waits " +
                 "on before placing the vessel.")]
        MenuFreestyleEventsContainerSO freestyleEvents;

        [SerializeField, Min(1f)]
        float freestyleHandoffTimeout = 8f;

        [Header("Arrival")]
        [SerializeField, Min(1f), Tooltip("How far in front of the toy the vessel arrives, as a " +
                 "multiple of the toy's own switch-ring radius. Must be > 1 or the player spawns " +
                 "INSIDE the ring and trips the toy on the first frame.")]
        float arrivalDistanceFactor = 2.4f;

        // The DI-registered shared asset, not a reach into MenuCrystalClickHandler's private
        // serialized copy - one reader, one registration, per the project's DI pattern.
        [Inject] GameDataSO gameData;

        IToyShellSurface _surface;
        CancellationTokenSource _handoffCts;

        // ── Open / close ─────────────────────────────────────────────────────

        /// <summary>
        /// Bind this window to a toy. Called by <see cref="ToyboxModal"/> BEFORE it opens the
        /// window, so the panel never renders a frame of the previous toy.
        /// </summary>
        public void Bind(IToyShellSurface surface)
        {
            _surface = surface;
            Redraw();
        }

        protected override void Start()
        {
            base.Start();
            // Bound HERE as well as in OnEnable, and idempotently. A modal that is already active
            // at scene load runs its OnEnable before anything has bound it, and this window is
            // wired by a tool rather than by hand - so "the button did nothing" must not be able to
            // come down to which of the two ran first.
            BindControls();
        }

        void OnEnable()
        {
            BindControls();

            // Whatever closes this window - the back button, the Navigate handoff, gamepad B, or
            // ScreenSwitcher.CloseAllModals before a flight - the preview camera has to stop
            // rendering. Hooking the modal's OWN close event is one subscription instead of one
            // rule per caller, and it is the only place that covers the routes this class is not
            // on. (A modal closes by fading its CanvasGroup and stays ACTIVE, so OnDisable does
            // not fire on close and cannot be used for this.)
            OnModalClosed -= HandleSelfClosed;
            OnModalClosed += HandleSelfClosed;
        }

        void BindControls()
        {
            // A bound button that is INACTIVE in the hierarchy can never be pressed, and the
            // symptom is indistinguishable from "the listener did not fire" - so say so, once.
            if (navigateButton && !navigateButton.gameObject.activeInHierarchy)
                CSDebug.LogWarning($"[ToyConfigureModal] navigateButton '{navigateButton.name}' is " +
                                   "inactive in the hierarchy - the lit button on screen is a " +
                                   "different object. Re-run FrogletTools > Interface > Home Hub Wiring.");

            if (navigateButton)
            {
                navigateButton.onClick.RemoveListener(Navigate);
                navigateButton.onClick.AddListener(Navigate);
            }

            if (backButton)
            {
                backButton.onClick.RemoveListener(OnCloseModal);
                backButton.onClick.AddListener(OnCloseModal);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (navigateButton) navigateButton.onClick.RemoveListener(Navigate);
            if (backButton) backButton.onClick.RemoveListener(OnCloseModal);
            OnModalClosed -= HandleSelfClosed;

            // The handoff is deliberately NOT cancelled here: Navigate CLOSES this window as its
            // first act, and SetActive(false) is a close route in this project, so cancelling here
            // could kill the arrival on exactly the path that needs it. Cancelled on destroy.
        }

        void OnDestroy() => CancelHandoff();

        void HandleSelfClosed()
        {
            if (preview) preview.Hide();
        }

        public void OnCloseModal() => ModalWindowOut();

        // ── Drawing ──────────────────────────────────────────────────────────

        void Redraw()
        {
            var def = _surface?.ShellDefinition;
            bool ready = _surface != null && _surface.ShellAvailable;

            if (titleText) titleText.text = def ? def.DisplayName : "";
            if (categoryText) categoryText.text = def ? ToyPortraitLibrary.Section(def) : "";

            // The codex's authored BODY copy - a paragraph, where the card gets the one-line
            // tagline - falling back to the toy definition's own line for a toy the codex has not
            // been scanned for. Never a string table in the UI layer: that would be a second place
            // to describe a toy, and it would drift from the toy's own assets.
            if (descriptionText) descriptionText.text = ToyPortraitLibrary.Body(def);

            if (preview) preview.Show(ResolveToy());

            // A toy that cannot answer right now is shown, not hidden - the player should see the
            // Toy Box has this toy in it, and that it is momentarily busy.
            if (navigateButton) navigateButton.interactable = ready;
        }

        /// <summary>
        /// The live toy behind this surface, or null. Every surface is a MonoBehaviour (a
        /// <see cref="Toy"/>, or a <see cref="SwapToySetCoordinator{T}"/> that speaks for a set of
        /// them), so the transform is always reachable; only a real <c>Toy</c> carries a ring
        /// radius, which is why the preview and the arrival both degrade gracefully without one.
        /// </summary>
        Toy ResolveToy()
        {
            if (_surface is Toy toy) return toy;
            if (_surface is MonoBehaviour mb) return mb.GetComponentInChildren<Toy>();
            return null;
        }

        // ── Navigate ─────────────────────────────────────────────────────────

        void Navigate()
        {
            CSDebug.LogVerbose(CSLogChannel.ToyBox, "[ToyBox] Navigate pressed.");

            var toy = ResolveToy();
            if (!toy)
            {
                CSDebug.LogWarning("[ToyConfigureModal] Navigate pressed with no live toy behind " +
                                   "this card - the toybox has not built it, or it was torn down " +
                                   "by a cell swap. Nothing happened.");
                return;
            }

            if (!crystalClickHandler)
            {
                CSDebug.LogWarning("[ToyConfigureModal] No MenuCrystalClickHandler is wired, so " +
                                   "Navigate cannot enter freestyle. Wire the scene's freestyle " +
                                   "toggle on this modal.");
                return;
            }

            // Already flying (the player opened the Toy Box mid-freestyle): nothing to wait for.
            if (crystalClickHandler.IsInFreestyle)
            {
                OnCloseModal();
                PlaceVesselAt(toy);
                return;
            }

            CancelHandoff();
            _handoffCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());

            OnCloseModal();
            crystalClickHandler.ToggleTransition();
            WaitForFreestyleThenPlace(toy, _handoffCts.Token).Forget();
        }

        async UniTaskVoid WaitForFreestyleThenPlace(Toy toy, CancellationToken ct)
        {
            bool arrived = false;
            void OnArrived() => arrived = true;

            var channel = freestyleEvents ? freestyleEvents.OnGameStateTransitionEnd : null;
            if (channel != null) channel.OnRaised += OnArrived;

            try
            {
                float deadline = Time.unscaledTime + Mathf.Max(1f, freestyleHandoffTimeout);
                while (!arrived && Time.unscaledTime < deadline)
                {
                    if (!crystalClickHandler || !toy) return;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (!arrived)
                {
                    CSDebug.LogWarning($"[ToyConfigureModal] Freestyle did not settle within " +
                                       $"{freestyleHandoffTimeout:0.#}s - the player was not moved " +
                                       $"to '{toy.DisplayName}'.");
                    return;
                }

                PlaceVesselAt(toy);
            }
            finally
            {
                if (channel != null) channel.OnRaised -= OnArrived;
            }
        }

        /// <summary>
        /// Put the vessel in front of the toy's ring, facing it.
        ///
        /// <para>Placed OUTSIDE the ring (<see cref="arrivalDistanceFactor"/> &gt; 1) and pointed
        /// at it, so the player arrives looking at the thing they chose and flies THROUGH the ring
        /// to use it. Arriving inside the ring would trip the toy on the first frame — the player
        /// would use it without ever seeing it.</para>
        ///
        /// <para>Approached from the CELL's side (the toy's outward radial), because the toybox
        /// rings its toys around the membrane facing inward: coming from anywhere else puts the
        /// membrane between the player and the toy.</para>
        /// </summary>
        void PlaceVesselAt(Toy toy)
        {
            var player = gameData ? gameData.LocalPlayer : null;
            if (player?.Vessel == null)
            {
                CSDebug.LogWarning("[ToyConfigureModal] Freestyle started but there is no local " +
                                   "vessel to move - the player is flying, just not repositioned.");
                return;
            }

            var toyPos = toy.transform.position;
            float radius = Mathf.Max(1f, toy.SwitchRingRadius);

            // The toybox places toys on a ring around the cell centre facing inward, so the
            // vessel's approach lane is the toy's own outward radial. Falls back to the toy's
            // forward when the toy sits exactly on the centre, which no placement produces but
            // which would otherwise yield a zero-length direction.
            var cellCentre = ResolveCellCentre(toy);
            var outward = toyPos - cellCentre;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : toy.transform.forward;

            var stand = toyPos + outward * (radius * Mathf.Max(1.1f, arrivalDistanceFactor));
            player.SetPoseOfVessel(new Pose(stand, Quaternion.LookRotation(toyPos - stand, Vector3.up)));

            // The platform's own off-screen arrow, for the frames after the arrival: the toy is
            // dead ahead on the frame the player lands, so the indicator hides itself immediately
            // and only speaks up once they have turned away. It takes itself down on arrival.
            ToyNavigationBeacon.PointAt(toy, player, crystalClickHandler);

            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[ToyBox] placed at {stand} facing '{toy.DisplayName}' (ring {radius:0.#}).");
        }

        static Vector3 ResolveCellCentre(Toy toy)
        {
            var cell = Cell.FindNearestActiveCell(toy.transform.position);
            return cell ? cell.transform.position : Vector3.zero;
        }

        void CancelHandoff()
        {
            if (_handoffCts == null) return;
            _handoffCts.Cancel();
            _handoffCts.Dispose();
            _handoffCts = null;
        }
    }
}
