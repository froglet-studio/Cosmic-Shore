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
    /// The <b>Toy Box</b> — the app-shell face of the freestyle toybox, one of the four things the
    /// home screen opens (Mission, Toy Box, Arena, Arcade).
    ///
    /// <para><b>It drives the LIVE toys, not a copy of them.</b> Every card is a
    /// <see cref="IToyShellSurface"/> registered by a real toy standing out by the cell membrane,
    /// and every row's press is the call that toy's ring makes — "change your domain" here is
    /// literally <c>DomainChangerToySet.Apply</c>. That is what makes the two surfaces one system:
    /// a toy authored tomorrow appears here by implementing one interface, and no table of toy
    /// actions exists to fall out of step. The 2D art is the encyclopedia's own baked emblem
    /// portrait (<see cref="ToyPortraitLibrary"/>), so the flat card is a picture of the thing the
    /// player flies at.</para>
    ///
    /// <para><b>Layers, not a flat list.</b> A toy is already a tree in the world — a
    /// <see cref="MatrixToy"/> unfolds into stations and the Lifeform Matrix unfolds again into
    /// species and elements — so an option may EXPAND instead of acting, and the modal keeps a
    /// breadcrumb stack. Flattening it would flatten something the player already reads as
    /// nested.</para>
    ///
    /// <para><b>Some toys need the player at the stick.</b> A wander, a voyage, a painting is not a
    /// state you set — it is a thing you fly. Those options are marked
    /// <see cref="ToyShellOption.RequiresFreestyle"/>, and the modal answers by closing, entering
    /// freestyle through the menu's own <see cref="MenuCrystalClickHandler"/>, waiting for the
    /// transition to actually finish, and only then applying — so one press still gets the player
    /// playing it.</para>
    /// </summary>
    public class ToyboxModal : ModalWindowManager
    {
        [Header("Toy grid (level 0)")]
        [SerializeField, Tooltip("Parent for the toy cards. Children are pooled and reused.")]
        Transform cardGrid;

        [SerializeField, Tooltip("One card per live toy.")]
        ToyboxCard cardPrefab;

        [SerializeField, Tooltip("Shown when no toy is registered - freestyle has not built the " +
                                 "toybox yet, or the scene has no ToyboxController.")]
        GameObject emptyState;

        [Header("Option list (levels 1+)")]
        [SerializeField, Tooltip("Root shown while a toy is open. Hidden on the toy grid.")]
        GameObject optionView;

        [SerializeField, Tooltip("Root shown while the toy grid is up. Hidden inside a toy.")]
        GameObject gridView;

        [SerializeField] Transform optionGrid;
        [SerializeField] ToyOptionCard optionPrefab;

        [SerializeField, Tooltip("Breadcrumb: the toy, then the layer inside it.")]
        TMP_Text optionTitle;

        [SerializeField, Tooltip("Back one layer - to the previous layer, or to the toy grid.")]
        Button backButton;

        [Header("Freestyle handoff")]
        [Tooltip("The menu's freestyle toggle. Required for options that only mean something with " +
                 "the player flying (Wanderway, Arkway, Connect the Dots) - without it those " +
                 "options report that they cannot run rather than half-running.")]
        [SerializeField] MenuCrystalClickHandler crystalClickHandler;

        [SerializeField, Min(1f), Tooltip("Seconds to wait for the freestyle transition to finish " +
                                          "before giving up on a deferred toy action.")]
        float freestyleHandoffTimeout = 8f;

        [Inject] MenuFreestyleEventsContainerSO freestyleEvents;

        readonly List<ToyboxCard> _cards = new();
        readonly List<ToyOptionCard> _optionCards = new();

        // The path into a toy: entry 0 is the toy itself, each later entry a layer it expanded
        // into. Held as built lists rather than as the options that produced them, so going back
        // redraws exactly what was there instead of re-running a surface that may have moved on.
        readonly List<Layer> _stack = new();

        readonly struct Layer
        {
            public readonly string Title;
            public readonly List<ToyShellOption> Options;
            public Layer(string title, List<ToyShellOption> options) { Title = title; Options = options; }
        }

        IToyShellSurface _openSurface;
        CancellationTokenSource _handoffCts;

        protected override void Start()
        {
            base.Start();
            if (backButton) backButton.onClick.AddListener(GoBack);
        }

        void OnEnable()
        {
            ToyShellRegistry.OnChanged += HandleRegistryChanged;
            ShowGrid();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ToyShellRegistry.OnChanged -= HandleRegistryChanged;

            // The handoff is deliberately NOT cancelled here. Closing the modal is the FIRST thing
            // a freestyle handoff does, and closing disables this object - so cancelling on
            // disable would cancel every deferred toy action before it could run. It is cancelled
            // on destroy, and superseded when a second handoff starts.
        }

        void OnDestroy()
        {
            if (backButton) backButton.onClick.RemoveListener(GoBack);
            CancelHandoff();
        }

        /// <summary>
        /// A toy appeared or went away (the toybox rebuilt, a cell swap tore it down). Only the
        /// GRID is redrawn: a player part-way into a toy keeps their layer, and the card list is
        /// rebuilt underneath them for when they come back.
        /// </summary>
        void HandleRegistryChanged()
        {
            if (_stack.Count == 0) RefreshGrid();
        }

        // ── Level 0: the toys ────────────────────────────────────────────────

        /// <summary>Back to the toy grid from wherever the player is.</summary>
        public void ShowGrid()
        {
            _stack.Clear();
            _openSurface = null;
            RefreshGrid();

            if (gridView) gridView.SetActive(true);
            if (optionView) optionView.SetActive(false);
        }

        void RefreshGrid()
        {
            var surfaces = ToyShellRegistry.Surfaces;

            EnsurePool(_cards, cardPrefab, cardGrid, surfaces.Count);

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (!card) continue;

                bool used = i < surfaces.Count;
                card.gameObject.SetActive(used);
                if (!used) continue;

                var surface = surfaces[i];
                card.Bind(surface);
                card.Button.onClick.RemoveAllListeners();
                card.Button.onClick.AddListener(() => OpenToy(surface));
            }

            if (emptyState) emptyState.SetActive(surfaces.Count == 0);
        }

        // ── Levels 1+: a toy's choices ───────────────────────────────────────

        void OpenToy(IToyShellSurface surface)
        {
            if (surface == null || !surface.ShellAvailable) return;

            if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);

            var options = new List<ToyShellOption>();
            surface.BuildShellOptions(options);

            if (options.Count == 0)
            {
                CSDebug.LogWarning($"[ToyboxModal] '{surface.ShellDefinition?.DisplayName}' " +
                                   "offered nothing - staying on the toy grid.");
                return;
            }

            _openSurface = surface;
            _stack.Clear();
            PushLayer(surface.ShellDefinition ? surface.ShellDefinition.DisplayName : "Toy", options);
        }

        void PushLayer(string title, List<ToyShellOption> options)
        {
            _stack.Add(new Layer(title, options));
            DrawTopLayer();

            if (gridView) gridView.SetActive(false);
            if (optionView) optionView.SetActive(true);
        }

        void DrawTopLayer()
        {
            if (_stack.Count == 0) { ShowGrid(); return; }

            var layer = _stack[_stack.Count - 1];

            if (optionTitle) optionTitle.text = BuildBreadcrumb();

            EnsurePool(_optionCards, optionPrefab, optionGrid, layer.Options.Count);

            for (int i = 0; i < _optionCards.Count; i++)
            {
                var row = _optionCards[i];
                if (!row) continue;

                bool used = i < layer.Options.Count;
                row.gameObject.SetActive(used);
                if (!used) continue;

                var option = layer.Options[i];
                row.Bind(option);
                row.Button.onClick.RemoveAllListeners();
                row.Button.onClick.AddListener(() => ChooseOption(option));
            }
        }

        string BuildBreadcrumb()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _stack.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(_stack[i].Title);
            }
            return sb.ToString();
        }

        void ChooseOption(ToyShellOption option)
        {
            if (option == null) return;

            if (option.IsBranch)
            {
                var next = option.Expand();
                if (next == null || next.Count == 0)
                {
                    CSDebug.LogWarning($"[ToyboxModal] '{option.Label}' expanded to nothing.");
                    return;
                }

                if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);
                PushLayer(option.Label, next);
                return;
            }

            if (option.Apply == null) return;

            if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.Confirmed);

            if (option.RequiresFreestyle)
            {
                ApplyAfterFreestyle(option);
                return;
            }

            option.Apply();

            // The choice changed live state the rows describe ("current", "flying", progress), so
            // the layer is rebuilt from the surface rather than left showing what was true before
            // the press.
            RebuildOpenLayer();
        }

        /// <summary>
        /// Re-ask the open toy for its top layer. Only the FIRST layer can be refreshed from the
        /// surface - deeper ones came from an option's Expand, which no longer exists once its
        /// parent list is rebuilt - so a deeper layer is left as it is, which is correct: those
        /// are trees of authored content (species, elements), not live state.
        /// </summary>
        void RebuildOpenLayer()
        {
            if (_openSurface == null || _stack.Count != 1) return;

            var options = new List<ToyShellOption>();
            _openSurface.BuildShellOptions(options);
            if (options.Count == 0) { ShowGrid(); return; }

            _stack[0] = new Layer(_stack[0].Title, options);
            DrawTopLayer();
        }

        public void GoBack()
        {
            if (_stack.Count <= 1) { ShowGrid(); return; }

            _stack.RemoveAt(_stack.Count - 1);
            DrawTopLayer();
        }

        // ── The freestyle handoff ────────────────────────────────────────────

        /// <summary>
        /// Close, enter freestyle, then do the thing. The wait is on the transition's own END
        /// event rather than on <see cref="MenuCrystalClickHandler.IsInFreestyle"/>: that flag
        /// flips at the START of the transition, while the vessel's input is still paused and the
        /// camera is still blending - a run begun then would start against a vessel nobody is
        /// flying yet.
        /// </summary>
        void ApplyAfterFreestyle(ToyShellOption option)
        {
            if (!crystalClickHandler)
            {
                CSDebug.LogWarning($"[ToyboxModal] '{option.Label}' needs the player flying, but no " +
                                   "MenuCrystalClickHandler is wired - nothing happened. Wire the " +
                                   "scene's freestyle toggle on this modal.");
                return;
            }

            // Already flying (the player opened the Toy Box mid-freestyle): nothing to wait for.
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
                    CSDebug.LogWarning($"[ToyboxModal] Freestyle did not settle within " +
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

        // ── Close ────────────────────────────────────────────────────────────

        /// <summary>Wire every close/back-out control here rather than to ModalWindowOut.</summary>
        public void OnCloseModal()
        {
            ShowGrid();
            ModalWindowOut();
        }

        // ── Pooling ──────────────────────────────────────────────────────────

        /// <summary>
        /// Grow <paramref name="pool"/> to at least <paramref name="needed"/>. Rows are reused and
        /// hidden, never destroyed: the toy grid is redrawn whenever the toybox changes, and the
        /// option list on every press.
        /// </summary>
        static void EnsurePool<T>(List<T> pool, T prefab, Transform parent, int needed)
            where T : MonoBehaviour
        {
            if (!prefab || !parent) return;

            while (pool.Count < needed)
                pool.Add(Instantiate(prefab, parent));
        }
    }
}
