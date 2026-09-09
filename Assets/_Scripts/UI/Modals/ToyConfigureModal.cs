using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
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
    /// The Toy Box's <b>second window</b>: one toy, what it does, its variants, and two verbs —
    /// <b>Navigate</b> (go and fly it) and <b>Switch</b> (have it here, without flying).
    ///
    /// <para><b>Both verbs, deliberately.</b> This window shipped with Navigate alone, on the
    /// argument that a menu which applied a toy's actions was a second authority on what a toy
    /// does. Half of that argument survives and half of it was wrong. What survives: the menu
    /// still calls the toy's OWN <see cref="ToyShellOption.Apply"/>, so "change your domain" here
    /// is literally <c>DomainChangerToySet.Apply</c> — one implementation with two surfaces, which
    /// is the whole point of <see cref="IToyShellSurface"/> and is not a second authority on
    /// anything. What was wrong: a player who does not want to fly had no way to change their
    /// domain at all, and telling them to go and fly for it is not a design principle, it is a
    /// tax. So this is an <b>in-UI toybox</b>, and Navigate is still there for everyone who would
    /// rather go to the ring. (<c>Docs/HomeHub/ARCHITECTURE.md</c> §4.1.1, which held this open as
    /// a real decision, is closed in the wire-it-in direction.)</para>
    ///
    /// <para><b>A row SELECTS; Switch COMMITS — except where the toy says otherwise.</b> A cell
    /// swap suctions the world away and grows another behind a veil, so firing one from a stray
    /// tap in a scroll list is a multi-second thing nobody asked for. A domain change is instant
    /// and undone by picking another row, and its world form is a flip-set with no commit step
    /// either. Which of the two an option is, is declared by the toy
    /// (<see cref="ToyShellOption.AppliesOnSelect"/>) rather than decided per toy here, for the
    /// reason <see cref="ToyDefinitionSO.Category"/> is declared in code: the cost of applying is
    /// a property of what the option does. Switch is drawn only for a list that has something for
    /// it to commit, so the domain changer never shows a button that can never light up.</para>
    ///
    /// <para><b>A branch opens in place.</b> The Lifeform Matrix is a tree in the world — kingdom,
    /// then species, then element — so it is a tree here, and the way back out is a synthesized
    /// row at the top of the list rather than a control somebody has to author. Only the FIRST
    /// layer is ever rebuilt from the surface: the deeper ones came from an option's
    /// <c>Expand</c>, which no longer exists once its parent list is rebuilt, and they are trees of
    /// authored content rather than live state.</para>
    ///
    /// <para><b>The picture is the live toy</b> (<see cref="ToyPreviewCamera"/>) — a camera on the
    /// object standing out by the cell membrane, not authored art. So it cannot go stale, and the
    /// player recognises the thing they are about to fly at.</para>
    ///
    /// <para><b>Navigate places the vessel BEFORE the transition, not after it — that ordering is
    /// the whole of why the arrival reads smoothly.</b> Entering freestyle is a single eased
    /// camera blend (<see cref="MainMenuCameraController"/>, smootherstep over
    /// <see cref="MenuCrystalClickHandler.TransitionDuration"/>) whose FAR endpoint is recomputed
    /// every frame from the vessel's live pose. Teleport first and that one blend simply arrives at
    /// the toy: the player watches the camera fly there. Teleport after
    /// <c>OnGameStateTransitionEnd</c> — which is what this did at first — and the blend eases all
    /// the way to wherever the autopilot happened to leave the ship, hands over to the gameplay
    /// camera, and only THEN does the world jump: a hard cut at the end of a two-second ease, which
    /// is the most abrupt place a cut can land.</para>
    ///
    /// <para><b>The arrival distance carries the COAST, because the ship is already flying.</b>
    /// <c>TransitionToFreestyle</c> drops the autopilot and holds input paused for the whole blend,
    /// so the vessel cruises forward at its minimum speed for those seconds — pointed, by
    /// construction, straight at the toy. So Navigate stands the vessel off by the intended
    /// distance PLUS that coast, and the pilot is handed the stick at exactly the stand-off. The
    /// coast is the approach. Without it the ship drifts into the ring mid-blend and trips the toy
    /// before the player has ever touched a control — <see cref="Toy"/> arms on
    /// <c>OnGameStateTransitionStart</c>, at the top of the transition, not at its end.</para>
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

        [Header("Variants")]
        [SerializeField, Tooltip("The scroll view holding the toy's variants. Hidden for a toy " +
                 "that offers none, so the window does not draw an empty list.")]
        GameObject variantsRoot;

        [SerializeField, Tooltip("The scroll view's Content. Cards are pooled and reused here.")]
        Transform variantContent;

        [SerializeField, Tooltip("One card per variant.")]
        ToyVariantCard variantCardPrefab;

        [Header("Controls")]
        [SerializeField, Tooltip("Takes the player to the toy. Disabled while the toy cannot " +
                 "answer - mid cell-swap, mid vessel-swap.")]
        Button navigateButton;

        [SerializeField, Tooltip("Commits the SELECTED variant without flying anywhere - the " +
                 "chosen cell, the chosen Ark. Hidden for a list with nothing to commit (the " +
                 "domain changer applies on the row itself), disabled until a row is selected.")]
        Button switchButton;

        [SerializeField, Tooltip("Back to the toy grid.")]
        Button backButton;

        [Header("Freestyle handoff")]
        [SerializeField, Tooltip("The scene's freestyle toggle. REQUIRED - without it Navigate " +
                 "can only warn.")]
        MenuCrystalClickHandler crystalClickHandler;

        [Header("Arrival")]
        [SerializeField, Min(1f), Tooltip("How far in front of the toy the vessel arrives, as a " +
                 "multiple of the toy's own switch-ring radius. Must be > 1 or the player spawns " +
                 "INSIDE the ring and trips the toy on the first frame.")]
        float arrivalDistanceFactor = 2.4f;

        [SerializeField, Min(1f), Tooltip("Seconds to wait for the freestyle transition to " +
                 "finish before giving up on a variant that only means something in flight.")]
        float freestyleHandoffTimeout = 8f;

        // The DI-registered shared asset, not a reach into MenuCrystalClickHandler's private
        // serialized copy - one reader, one registration, per the project's DI pattern.
        [Inject] GameDataSO gameData;

        // The transition bracket a variant that needs the player flying waits on. Injected rather
        // than read off the click handler for the same reason.
        [Inject] MenuFreestyleEventsContainerSO freestyleEvents;

        IToyShellSurface _surface;

        // Captured at bind, and deliberately not re-read from the surface afterwards. A cell swap
        // fired from this very window destroys the toy, and the definition is the one thing about
        // it that survives - it is an ASSET. Everything that still needs to name the toy after
        // that (the back row's accent, finding the toy's replacement) reads this instead.
        ToyDefinitionSO _boundDefinition;

        readonly List<ToyVariantCard> _variantCards = new();

        // The path into a toy: entry 0 is the toy's own top layer, each later entry a layer an
        // option expanded into. Held as BUILT lists rather than as the options that produced them,
        // so going back redraws exactly what was there instead of re-running a surface that has
        // since moved on.
        readonly List<Layer> _stack = new();

        // The rows as DRAWN, which is the layer plus the synthesized back row when there is one.
        // Kept separate from the layer so the back row never has to be excluded from an index.
        readonly List<ToyShellOption> _rows = new();

        int _selected = -1;
        CancellationTokenSource _handoffCts;

        readonly struct Layer
        {
            public readonly List<ToyShellOption> Options;
            public Layer(List<ToyShellOption> options) { Options = options; }
        }

        /// <summary>
        /// The bound surface, or null once it has gone away.
        ///
        /// <para>The plain <c>_surface != null</c> is not enough and the difference is a real trap:
        /// every surface is a MonoBehaviour but the FIELD is typed as the interface, so the null
        /// check is a reference comparison and keeps answering true after a cell swap has destroyed
        /// the toy - at which point reading <c>ShellAvailable</c> throws rather than returning
        /// false. Unity's own lifetime check has to be done against the MonoBehaviour.</para>
        /// </summary>
        IToyShellSurface LiveSurface =>
            _surface != null && (_surface is not MonoBehaviour mb || mb) ? _surface : null;

        // ── Open / close ─────────────────────────────────────────────────────

        /// <summary>
        /// Bind this window to a toy. Called by <see cref="ToyboxModal"/> BEFORE it opens the
        /// window, so the panel never renders a frame of the previous toy.
        /// </summary>
        public void Bind(IToyShellSurface surface)
        {
            _surface = surface;
            _boundDefinition = surface?.ShellDefinition;
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

            // A cell swap tears the toybox down and builds it again, which destroys the very
            // surface this window is bound to - and it is THIS window that fires the swap now, so
            // that is the ordinary path rather than an edge case.
            ToyShellRegistry.OnChanged -= HandleRegistryChanged;
            ToyShellRegistry.OnChanged += HandleRegistryChanged;
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

            if (switchButton)
            {
                switchButton.onClick.RemoveListener(SwitchToSelected);
                switchButton.onClick.AddListener(SwitchToSelected);
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
            if (switchButton) switchButton.onClick.RemoveListener(SwitchToSelected);
            if (backButton) backButton.onClick.RemoveListener(OnCloseModal);
            OnModalClosed -= HandleSelfClosed;
            ToyShellRegistry.OnChanged -= HandleRegistryChanged;

            // The freestyle handoff is deliberately NOT cancelled here. A modal normally closes
            // without being deactivated at all, but SetActive(false) IS a close route in this
            // project (ModalWindowManager.ModalWindowIn carries an externally-deactivated recovery
            // path for it) - and a handoff closes this window as its first act, so a cancel here
            // could kill the deferred variant on exactly that route. It is cancelled on destroy,
            // and superseded when a second handoff starts.
        }

        void OnDestroy() => CancelHandoff();

        void HandleSelfClosed()
        {
            if (preview) preview.Hide();

            // Come back to the toy's own top layer, so reopening never lands the player inside a
            // branch they left behind three windows ago. The rows are REDRAWN rather than just
            // forgotten: a stack and a drawn list that disagree would leave live, pressable cards
            // for a layer this window no longer thinks it is on.
            if (_stack.Count > 1) _stack.RemoveRange(1, _stack.Count - 1);
            _selected = -1;
            DrawRows();
        }

        public void OnCloseModal() => ModalWindowOut();

        // ── Drawing ──────────────────────────────────────────────────────────

        void Redraw()
        {
            var surface = LiveSurface;
            var def = surface?.ShellDefinition;
            bool ready = surface != null && surface.ShellAvailable;

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

            RebuildFromSurface();
        }

        /// <summary>
        /// The live toy behind this surface, or null. Every surface is a MonoBehaviour (a
        /// <see cref="Toy"/>, or a <see cref="SwapToySetCoordinator{T}"/> that speaks for a set of
        /// them), so the transform is always reachable; only a real <c>Toy</c> carries a ring
        /// radius, which is why the preview and the arrival both degrade gracefully without one.
        /// </summary>
        Toy ResolveToy()
        {
            var surface = LiveSurface;
            if (surface is Toy toy) return toy;
            if (surface is MonoBehaviour mb) return mb.GetComponentInChildren<Toy>();
            return null;
        }

        // ── Variants ─────────────────────────────────────────────────────────

        /// <summary>
        /// Re-ask the toy for its top layer and draw it from scratch. Called on every bind, and
        /// whenever the toybox changes underneath the window.
        /// </summary>
        void RebuildFromSurface()
        {
            _stack.Clear();
            _selected = -1;

            var surface = LiveSurface;
            if (surface != null && surface.ShellAvailable)
            {
                var options = new List<ToyShellOption>();
                surface.BuildShellOptions(options);
                if (options.Count > 0) _stack.Add(new Layer(options));
            }

            DrawRows();
        }

        /// <summary>
        /// Re-ask the toy for the layer the player is looking at, after something they did changed
        /// what it says ("current", "flying", a painting's progress).
        ///
        /// <para>Only the FIRST layer can be refreshed this way. A deeper one came from an option's
        /// <c>Expand</c>, and that closure belongs to a list this call would replace - so a deeper
        /// layer is left alone, which is also correct: those are trees of authored content, not
        /// live state. A rebuild that comes back EMPTY is a toy mid-swap rather than a toy with
        /// nothing to offer, so the drawn rows stay and the registry's own event redraws them when
        /// it settles.</para>
        /// </summary>
        void RebuildTopLayer()
        {
            if (_stack.Count != 1) return;

            var surface = LiveSurface;
            if (surface == null || !surface.ShellAvailable) { DrawRows(); return; }

            var options = new List<ToyShellOption>();
            surface.BuildShellOptions(options);
            if (options.Count == 0) { DrawRows(); return; }

            _stack[0] = new Layer(options);
            _selected = -1;
            DrawRows();
        }

        void PushLayer(List<ToyShellOption> options)
        {
            _stack.Add(new Layer(options));
            _selected = -1;
            if (preview) preview.ClearVariant();
            DrawRows();
        }

        void GoBackLayer()
        {
            if (_stack.Count <= 1) return;
            _stack.RemoveAt(_stack.Count - 1);
            _selected = -1;
            if (preview) preview.ClearVariant();
            DrawRows();
        }

        /// <summary>
        /// The way out of a branch, as a row rather than as a control somebody has to author. It
        /// applies on select for the same reason every flip-set row does: going back is the act,
        /// there is nothing to commit.
        /// </summary>
        ToyShellOption MakeBackRow()
        {
            return new ToyShellOption
            {
                Label = "◀  Back",
                Accent = _boundDefinition ? _boundDefinition.AccentColor : Color.white,
                AppliesOnSelect = true,
                Apply = GoBackLayer,
            };
        }

        void DrawRows()
        {
            _rows.Clear();
            if (_stack.Count > 1) _rows.Add(MakeBackRow());
            if (_stack.Count > 0) _rows.AddRange(_stack[^1].Options);

            if (variantsRoot) variantsRoot.SetActive(_rows.Count > 0);

            EnsurePool(_variantCards, variantCardPrefab, variantContent, _rows.Count);
            for (int i = 0; i < _variantCards.Count; i++)
            {
                var card = _variantCards[i];
                if (!card) continue;

                bool used = i < _rows.Count;
                card.gameObject.SetActive(used);
                if (!used) continue;

                int index = i;
                card.Bind(_rows[i], i == _selected);
                card.Button.onClick.RemoveAllListeners();
                card.Button.onClick.AddListener(() => ChooseRow(index));
            }

            UpdateSwitchButton();
        }

        /// <summary>
        /// Switch is drawn only for a list that has something for it to commit, and lit only once
        /// a row is selected. A list where every row applies on its own press - the domain changer
        /// - would otherwise carry a button that can never light up, which reads as broken rather
        /// than as unnecessary.
        /// </summary>
        void UpdateSwitchButton()
        {
            if (!switchButton) return;

            bool anyToCommit = false;
            for (int i = 0; i < _rows.Count && !anyToCommit; i++)
            {
                var row = _rows[i];
                anyToCommit = row is { AppliesOnSelect: false, Apply: not null };
            }

            switchButton.gameObject.SetActive(anyToCommit);
            switchButton.interactable =
                anyToCommit && _selected >= 0 && _selected < _rows.Count && _rows[_selected].Apply != null;
        }

        void ChooseRow(int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            var option = _rows[index];

            if (option.IsBranch)
            {
                var next = option.Expand();
                if (next is not { Count: > 0 })
                {
                    CSDebug.LogWarning($"[ToyConfigureModal] '{option.Label}' expanded to nothing - " +
                                       "staying on this layer.");
                    return;
                }

                PlayMenuAudio(MenuAudioCategory.OptionClick);
                PushLayer(next);
                return;
            }

            if (option.AppliesOnSelect)
            {
                if (option.Apply == null) return;
                PlayMenuAudio(MenuAudioCategory.Confirmed);
                ApplyOption(option);
                return;
            }

            // A row with no Apply is there to be READ - the hull you are flying, the domain you
            // already wear. The card is already non-interactable; this is the belt to that braces.
            if (option.Apply == null) return;

            PlayMenuAudio(MenuAudioCategory.OptionClick);
            Select(index);
        }

        void Select(int index)
        {
            _selected = index;

            // The picture answers "what IS that" for a name the player has never seen. Most
            // options build nothing, and the window then keeps showing the toy rather than
            // blanking - the honest picture, since the toy is still what Navigate would take
            // them to.
            if (preview) preview.ShowVariant(_rows[index].BuildPreview);

            for (int i = 0; i < _variantCards.Count && i < _rows.Count; i++)
                if (_variantCards[i]) _variantCards[i].Bind(_rows[i], i == _selected);

            UpdateSwitchButton();
        }

        void SwitchToSelected()
        {
            if (_selected < 0 || _selected >= _rows.Count) return;

            var option = _rows[_selected];
            if (option.Apply == null) return;

            PlayMenuAudio(MenuAudioCategory.Confirmed);
            ApplyOption(option);
        }

        void ApplyOption(ToyShellOption option)
        {
            if (option.RequiresFreestyle)
            {
                ApplyAfterFreestyle(option);
                return;
            }

            option.Apply();

            // The press changed live state the rows describe, so the layer is re-asked rather than
            // left showing what was true before it.
            RebuildTopLayer();
        }

        // ── The freestyle handoff ────────────────────────────────────────────

        /// <summary>
        /// Close, enter freestyle, then do the thing — for a variant that only means something with
        /// the player at the stick (a wander, a voyage, a painting).
        ///
        /// <para>The wait is on the transition's own END event rather than on
        /// <see cref="MenuCrystalClickHandler.IsInFreestyle"/>: that flag flips at the START of the
        /// transition, while the vessel's input is still paused and the camera is still blending,
        /// and a run begun then would start against a vessel nobody is flying yet.</para>
        /// </summary>
        void ApplyAfterFreestyle(ToyShellOption option)
        {
            if (!crystalClickHandler)
            {
                CSDebug.LogWarning($"[ToyConfigureModal] '{option.Label}' needs the player flying, " +
                                   "but no MenuCrystalClickHandler is wired - nothing happened. " +
                                   "Wire the scene's freestyle toggle on this modal.");
                return;
            }

            // Already flying (the Toy Box was opened mid-freestyle): nothing to wait for.
            if (crystalClickHandler.IsInFreestyle)
            {
                OnCloseModal();
                option.Apply();
                return;
            }

            CancelHandoff();
            _handoffCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());

            OnCloseModal();
            crystalClickHandler.ToggleTransition();
            WaitForFreestyleThenApply(option, _handoffCts.Token).Forget();
        }

        async UniTaskVoid WaitForFreestyleThenApply(ToyShellOption option, CancellationToken ct)
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
                    if (!crystalClickHandler) return;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (!arrived)
                {
                    CSDebug.LogWarning($"[ToyConfigureModal] Freestyle did not settle within " +
                                       $"{freestyleHandoffTimeout:0.#}s - '{option.Label}' was not started.");
                    return;
                }

                option.Apply();
            }
            finally
            {
                if (channel != null) channel.OnRaised -= OnArrived;
            }
        }

        void CancelHandoff()
        {
            if (_handoffCts == null) return;
            _handoffCts.Cancel();
            _handoffCts.Dispose();
            _handoffCts = null;
        }

        // ── The toybox moved underneath us ───────────────────────────────────

        /// <summary>
        /// A toy appeared or went away. Switching cells from this very window is the ordinary way
        /// that happens now: the swap tears the toybox down and builds it again, so the surface
        /// this window holds is destroyed and a NEW one speaks for the same toy.
        ///
        /// <para>Re-bound by DEFINITION, falling back to display name for the code-built default
        /// toybox, whose definitions are <c>CreateInstance</c>d per build and so match no earlier
        /// reference at all. That is the same two-step, for the same reason, that
        /// <see cref="ToyPortraitLibrary"/> uses to find a toy's codex page.</para>
        /// </summary>
        void HandleRegistryChanged()
        {
            if (LiveSurface != null) { RebuildTopLayer(); return; }

            var wanted = _boundDefinition;
            string wantedName = wanted ? wanted.DisplayName : null;
            if (string.IsNullOrEmpty(wantedName)) return;

            foreach (var candidate in ToyShellRegistry.Surfaces)
            {
                var def = candidate?.ShellDefinition;
                if (!def) continue;
                if (def != wanted && !string.Equals(def.DisplayName, wantedName,
                                                    System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Bind(candidate);
                return;
            }
        }

        /// <summary>
        /// Grow <paramref name="pool"/> to at least <paramref name="needed"/>. Cards are reused and
        /// hidden, never destroyed - the list is redrawn on every layer change.
        /// </summary>
        static void EnsurePool<T>(List<T> pool, T prefab, Transform parent, int needed)
            where T : MonoBehaviour
        {
            if (!prefab || !parent) return;
            while (pool.Count < needed) pool.Add(Instantiate(prefab, parent));
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

            // Already flying (the player opened the Toy Box mid-freestyle): there is no blend to
            // ride and no coast to allow for, so this is a straight teleport to the stand-off.
            if (crystalClickHandler.IsInFreestyle)
            {
                OnCloseModal();
                PlaceVesselAt(toy, 0f);
                return;
            }

            OnCloseModal();

            // ToggleTransition runs synchronously up to its first await, and `_isInFreestyle = true`
            // plus OnGameStateTransitionStart are both on that side of it - so by the time this
            // returns, the camera has already started its blend and IsInFreestyle is the honest
            // answer to "did the toggle take?". It refuses while a transition is in flight or
            // before the local vessel exists, and a refusal must not leave the ship teleported
            // across the menu with the autopilot still driving it.
            crystalClickHandler.ToggleTransition();
            if (!crystalClickHandler.IsInFreestyle)
            {
                CSDebug.LogWarning("[ToyConfigureModal] The freestyle toggle declined (already " +
                                   "transitioning, or no local vessel yet) - the player was not " +
                                   $"moved to '{toy.DisplayName}'.");
                return;
            }

            // Placed inside the SAME frame the blend started, and before the camera's LateUpdate,
            // so the very first blend frame already aims at the toy.
            PlaceVesselAt(toy, crystalClickHandler.TransitionDuration);
        }

        /// <summary>
        /// Put the vessel in front of the toy's ring, facing it.
        ///
        /// <para>Placed OUTSIDE the ring (<see cref="arrivalDistanceFactor"/> &gt; 1) and pointed
        /// at it, so the player arrives looking at the thing they chose and flies THROUGH the ring
        /// to use it. Arriving inside the ring would trip the toy on the first frame — the player
        /// would use it without ever seeing it.</para>
        ///
        /// <para>Approached from <b>INSIDE the cell</b> — the toy's INWARD radial — so the arrival
        /// looks back at the toy with the whole environment behind it and the player flies the same
        /// way they would have flown there themselves. The toybox rings its toys around the
        /// membrane facing inward, so this is also the toy's own front. Standing off on the OUTWARD
        /// radial (which this did first) is geometrically the same shot and reads completely
        /// differently: it parks the player <i>outside</i> the membrane looking at a toy against
        /// empty space, with the world they are about to enter hidden behind it.</para>
        ///
        /// <para><paramref name="coastSeconds"/> is how long the vessel will fly itself before the
        /// pilot is handed the stick — the enter-freestyle blend. The ship is pointed at the toy,
        /// so that coast eats straight into the stand-off; it is added back here (measured from the
        /// vessel's own live speed, so a fast hull is not under-allowed and a stationary one costs
        /// nothing) and the pilot takes over at the intended distance.</para>
        /// </summary>
        void PlaceVesselAt(Toy toy, float coastSeconds)
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

            // The toybox places toys on a ring around the cell centre facing inward, so the lane
            // that keeps the player inside the world is the toy's INWARD radial - the direction
            // the toy is already looking. Falls back to the toy's own forward when it sits exactly
            // on the centre, which no placement produces but which would otherwise yield a
            // zero-length direction.
            var cellCentre = ResolveCellCentre(toy);
            var approach = cellCentre - toyPos;
            approach = approach.sqrMagnitude > 0.001f ? approach.normalized : toy.transform.forward;

            float standOff = radius * Mathf.Max(1.1f, arrivalDistanceFactor);
            float speed = player.Vessel.VesselStatus != null
                ? Mathf.Max(0f, player.Vessel.VesselStatus.Speed)
                : 0f;
            float coast = speed * Mathf.Max(0f, coastSeconds);

            // Inward is a BOUNDED direction in a way outward was not: run far enough along it and
            // the arrival is past the core and out the other side, facing the toy across the whole
            // cell. A fast hull's coast is the realistic way to get there, so the whole lane is
            // capped short of the centre rather than trusting the numbers to stay small.
            float lane = Vector3.Distance(toyPos, cellCentre);
            float reach = standOff + coast;
            if (lane > 1f) reach = Mathf.Min(reach, lane * 0.8f);

            var stand = toyPos + approach * reach;
            player.SetPoseOfVessel(new Pose(stand, Quaternion.LookRotation(toyPos - stand, Vector3.up)));

            // The platform's own off-screen arrow, for the frames after the arrival: the toy is
            // dead ahead on the frame the player lands, so the indicator hides itself immediately
            // and only speaks up once they have turned away. It takes itself down on arrival.
            ToyNavigationBeacon.PointAt(toy, player, crystalClickHandler);

            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[ToyBox] placed at {stand} facing '{toy.DisplayName}' (ring {radius:0.#}, " +
                $"stand-off {standOff:0.#} + {coast:0.#} coast at {speed:0.#} u/s, " +
                $"reach {reach:0.#} along a {lane:0.#} inward lane).");
        }

        static Vector3 ResolveCellCentre(Toy toy)
        {
            var cell = Cell.FindNearestActiveCell(toy.transform.position);
            return cell ? cell.transform.position : Vector3.zero;
        }
    }
}
