using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The one friends / party panel, out of every window and into a drawer on the left edge
    /// (rev 3: "make it independent like a popup from the left side we can open, minimise, maximise
    /// whenever we want"). Three states:
    /// <list type="bullet">
    /// <item><b>Closed</b> - only the handle shows; pressing it opens the drawer. Since rev 6 the handle is the
    /// presence orb in the bottom-left corner (<see cref="KitPresenceOrb"/>, Docs/MENU_REDESIGN_B.md §45.4).</item>
    /// <item><b>Open</b> - the window slides in from the left at its normal width.</item>
    /// <item><b>Maximised</b> - the same window, wider; the button restores it.</item>
    /// </list>
    /// The – button minimises back to the handle. The panel inside (PartyPanel) keeps all its own
    /// behaviour; this only moves and sizes its window.
    ///
    /// <para>Where it sits (§64, Shombith 2026-10-25): the window takes the whole left side, from under the top nav to
    /// the bottom, so its height follows the screen it is on. Over a hub window (Arcade, Arena, Mission, Toy Box) it
    /// docks: <see cref="DockWidth"/> tells each window's <see cref="FriendsDrawerDock"/> how much room to make, and
    /// the cards move right. Over Home, the Hangar and the other screens it slides over them and moves nothing. While
    /// any other window is on top (a game's configure, Settings, a leaderboard) it never covers it: the drawer
    /// closes and the orb hides until that window closes.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Friends Drawer")]
    public class FriendsDrawer : MonoBehaviour
    {
        public enum DrawerState { Closed = 0, Open = 1, Maximised = 2 }

        /// <summary>How far (canvas units) the open drawer reaches into a hub window; 0 when it is closed or is over
        /// anything but a hub window. One drawer per menu scene, so one value.</summary>
        public static float DockWidth { get; private set; }

        /// <summary>Raised when <see cref="DockWidth"/> changes, with the new value - during the slide too.</summary>
        public static event Action<float> DockWidthChanged;

        // Enter Play Mode without a domain reload keeps statics: start every session undocked.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DockWidth = 0f;
            DockWidthChanged = null;
        }

        [Header("Wiring")]
        [Tooltip("The handle (shown while closed): the presence orb in the bottom-left corner.")]
        [SerializeField] private CanvasGroup handleGroup;
        [SerializeField] private Button handleButton;
        [Tooltip("The drawer window (holds the party panel). Anchored to the left edge, stretched top to bottom.")]
        [SerializeField] private CanvasGroup windowGroup;
        [SerializeField] private RectTransform window;
        [SerializeField] private Button minimiseButton;
        [SerializeField] private Button maximiseButton;
        [Tooltip("The maximise button's glyph while the window is maximised (shown) or normal (hidden).")]
        [SerializeField] private CanvasGroup restoreGlyph;
        [Tooltip("The maximise button's glyph while the window is normal.")]
        [SerializeField] private CanvasGroup maximiseGlyph;
        [Tooltip("The scene's switcher: which window is on top decides whether the drawer docks, floats or stays shut. " +
                 "Found in the scene when left empty.")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        [Header("Layout")]
        [Tooltip("The open window's width (canvas units).")]
        [SerializeField, Min(0f)] private float normalWidth = 460f;
        [Tooltip("The maximised window's width (canvas units).")]
        [SerializeField, Min(0f)] private float maximisedWidth = 720f;
        [Tooltip("Room left above the window for the top nav (canvas units).")]
        [SerializeField, Min(0f)] private float dockTop = 84f;
        [Tooltip("Room left below the window (canvas units).")]
        [SerializeField, Min(0f)] private float dockBottom;
        [Tooltip("Gap between the screen's left edge and the open window.")]
        [SerializeField] private float openInset;
        [Tooltip("Seconds a slide or resize takes.")]
        [SerializeField, Min(0f)] private float moveSeconds = 0.22f;

        private DrawerState _state = DrawerState.Closed;
        private float _fromX, _toX, _fromWidth, _toWidth;
        private float _t = 1f;
        private bool _blocked;

        public DrawerState State => _state;

        private void Awake()
        {
            if (!screenSwitcher) screenSwitcher = FindAnyObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (handleButton) handleButton.onClick.AddListener(Open);
            if (minimiseButton) minimiseButton.onClick.AddListener(Close);
            if (maximiseButton) maximiseButton.onClick.AddListener(ToggleMaximise);
            if (window)
            {
                window.offsetMin = new Vector2(window.offsetMin.x, dockBottom);
                window.offsetMax = new Vector2(window.offsetMax.x, -dockTop);
            }
            Apply(DrawerState.Closed, instant: true);
        }

        private void OnEnable()
        {
            if (screenSwitcher) screenSwitcher.ModalStackChanged += OnModalStackChanged;
            OnModalStackChanged();
        }

        private void OnDisable()
        {
            if (screenSwitcher) screenSwitcher.ModalStackChanged -= OnModalStackChanged;
            SetDockWidth(0f);
        }

        private void OnDestroy()
        {
            if (handleButton) handleButton.onClick.RemoveListener(Open);
            if (minimiseButton) minimiseButton.onClick.RemoveListener(Close);
            if (maximiseButton) maximiseButton.onClick.RemoveListener(ToggleMaximise);
        }

        public void Open()
        {
            if (!_blocked) Apply(DrawerState.Open, instant: false);
        }

        public void Close() => Apply(DrawerState.Closed, instant: false);

        public void ToggleMaximise()
        {
            if (_blocked) return;
            Apply(_state == DrawerState.Maximised ? DrawerState.Open : DrawerState.Maximised, instant: false);
        }

        /// <summary>True while a hub window is on top: the open drawer makes room in it.</summary>
        private bool OverHubWindow =>
            screenSwitcher && screenSwitcher.ActiveHubWindow != ScreenSwitcher.ModalWindows.NONE;

        /// <summary>True while any other window is on top (a game's configure, Settings...): the drawer stays shut.</summary>
        private bool Blocked => screenSwitcher && screenSwitcher.HasActiveModal && !OverHubWindow;

        private void OnModalStackChanged()
        {
            _blocked = Blocked;
            if (_blocked && _state != DrawerState.Closed) Apply(DrawerState.Closed, instant: true);
            else KitVisibility.SetShown(handleGroup, _state == DrawerState.Closed && !_blocked);
            UpdateDockWidth();
        }

        private void Apply(DrawerState state, bool instant)
        {
            _state = state;
            bool open = state != DrawerState.Closed;
            KitVisibility.SetShown(handleGroup, !open && !_blocked);
            KitVisibility.SetShown(maximiseGlyph, state != DrawerState.Maximised);
            KitVisibility.SetShown(restoreGlyph, state == DrawerState.Maximised);
            // The window stays drawn while it slides out; it stops taking input at once.
            if (open) KitVisibility.SetShown(windowGroup, true);
            else if (windowGroup) { windowGroup.interactable = false; windowGroup.blocksRaycasts = false; }
            if (!window) return;

            float width = state == DrawerState.Maximised ? maximisedWidth : normalWidth;
            _fromX = window.anchoredPosition.x;
            _fromWidth = window.sizeDelta.x;
            _toWidth = width;
            _toX = open ? openInset : -width - 8f;
            _t = instant || moveSeconds <= 0f ? 1f : 0f;
            if (_t >= 1f) Place(1f);
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / moveSeconds);
            Place(1f - (1f - _t) * (1f - _t) * (1f - _t));   // OutCubic
        }

        private void Place(float k)
        {
            window.anchoredPosition = new Vector2(Mathf.LerpUnclamped(_fromX, _toX, k), window.anchoredPosition.y);
            window.sizeDelta = new Vector2(Mathf.LerpUnclamped(_fromWidth, _toWidth, k), window.sizeDelta.y);
            if (k >= 1f && _state == DrawerState.Closed) KitVisibility.SetShown(windowGroup, false);
            UpdateDockWidth();
        }

        /// <summary>The window's right edge while it is over a hub window, so the window's content follows the slide.</summary>
        private void UpdateDockWidth()
        {
            float reach = window ? window.anchoredPosition.x + window.sizeDelta.x : 0f;
            SetDockWidth(OverHubWindow ? Mathf.Max(0f, reach) : 0f);
        }

        private static void SetDockWidth(float width)
        {
            if (Mathf.Approximately(DockWidth, width)) return;
            DockWidth = width;
            DockWidthChanged?.Invoke(width);
        }
    }
}
