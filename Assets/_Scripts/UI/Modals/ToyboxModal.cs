using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The <b>Toy Box</b> — the app-shell face of the freestyle toybox, one of the four things the
    /// home screen opens (Mission, Toy Box, Arena, Arcade). This window is the <b>catalogue</b>:
    /// every toy currently standing out by the cell membrane, one card each. Picking one opens
    /// <see cref="ToyConfigureModal"/>, which describes it and takes the player to it.
    ///
    /// <para><b>It lists the LIVE toys, not a copy of them.</b> Every card is an
    /// <see cref="IToyShellSurface"/> registered by a real toy in Menu_Main's own cell, so a toy
    /// authored tomorrow appears here by implementing one interface and no table of toys exists to
    /// fall out of step. The 2D art is the encyclopedia's own baked emblem portrait
    /// (<see cref="ToyPortraitLibrary"/>), so the flat card is a picture of the thing the player
    /// flies at.</para>
    ///
    /// <para><b>It is a grid and nothing else — that is a deliberate narrowing.</b> This modal used
    /// to drill into a toy's options in place (a breadcrumb stack over
    /// <see cref="IToyShellSurface.BuildShellOptions"/>), which made the menu a second authority on
    /// what a toy does: "change your domain" was applied from here rather than by flying the ring.
    /// The two-window shape replaces that with one verb — go to the toy — and the toy stays the
    /// only thing that acts on the world. <c>BuildShellOptions</c> is deliberately LEFT on the
    /// interface: it is the seam an in-menu option list would plug back into, and it is what the
    /// configure window would show if a toy is ever given menu-side choices. It has no consumer
    /// today; see <c>Docs/HomeHub/ARCHITECTURE.md</c> §4.1.1.</para>
    /// </summary>
    public class ToyboxModal : ModalWindowManager
    {
        [Header("Toy grid")]
        [SerializeField, Tooltip("Parent for the toy cards. Children are pooled and reused.")]
        Transform cardGrid;

        [SerializeField, Tooltip("One card per live toy.")]
        ToyboxCard cardPrefab;

        [SerializeField, Tooltip("Shown when no toy is registered - freestyle has not built the " +
                                 "toybox yet, or the scene has no ToyboxController.")]
        GameObject emptyState;

        [Header("Detail window")]
        [SerializeField, Tooltip("The window a card opens: one toy, its description, and Navigate. " +
                 "Leave empty to find it in the scene at Start.")]
        ToyConfigureModal configureModal;

        readonly List<ToyboxCard> _cards = new();

        // NOT serialized, and deliberately not named `screenSwitcher`: the base already serializes
        // a field by that name, and Unity refuses to serialize the same field name twice in a class
        // and its parent ("The same field name is serialized multiple times"). The authored slot is
        // the base's; this only caches the resolved value, falling back to a scene lookup.
        ScreenSwitcher _switcher;

        protected override void Start()
        {
            base.Start();
            _switcher = Switcher
                ? Switcher
                : FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (!configureModal)
                configureModal = FindFirstObjectByType<ToyConfigureModal>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            ToyShellRegistry.OnChanged += Refresh;
            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ToyShellRegistry.OnChanged -= Refresh;
        }

        /// <summary>
        /// Redraw the grid from the live registry. Called on open, and again whenever a toy appears
        /// or goes away (the toybox rebuilt, a cell swap tore it down) so the catalogue can never
        /// offer a card for a toy that is no longer standing.
        /// </summary>
        public void Refresh()
        {
            var toys = ToyShellRegistry.Surfaces;

            if (emptyState) emptyState.SetActive(toys.Count == 0);

            EnsurePool(_cards, cardPrefab, cardGrid, toys.Count);
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool used = i < toys.Count;
                card.gameObject.SetActive(used);
                if (!used) continue;

                var surface = toys[i];
                card.Bind(surface);
                card.Button.onClick.RemoveAllListeners();
                card.Button.onClick.AddListener(() => OpenToy(surface));
            }
        }

        /// <summary>
        /// Hand this toy to the detail window. Bound BEFORE the window opens, so it never renders
        /// a frame of the previously-selected toy — the same ordering the arcade's launch panel
        /// uses, and the reason the preview camera can render immediately on bind.
        /// </summary>
        void OpenToy(IToyShellSurface surface)
        {
            if (surface == null) return;

            if (!configureModal)
            {
                CSDebug.LogWarning("[ToyboxModal] No ToyConfigureModal is wired or findable, so a " +
                                   "toy card opens nothing. Wire the Toy Box's detail window.");
                return;
            }

            configureModal.Bind(surface);

            // Through the switcher, never ModalWindowIn directly: it owns the modal stack, so
            // gamepad B out of the toy pops back to this grid instead of closing the Toy Box.
            if (_switcher)
                _switcher.OpenModal(ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE);
            else
                configureModal.ModalWindowIn();
        }

        /// <summary>Wire every close/back-out control here rather than to ModalWindowOut.</summary>
        public void OnCloseModal() => ModalWindowOut();

        /// <summary>
        /// Grow <paramref name="pool"/> to at least <paramref name="needed"/>. Cards are reused and
        /// hidden, never destroyed: the grid is redrawn whenever the toybox changes.
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
