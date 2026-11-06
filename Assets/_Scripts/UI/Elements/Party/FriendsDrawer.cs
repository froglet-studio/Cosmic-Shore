using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The one friends / party panel, out of every window and into a drawer on the left edge
    /// (rev 3: "make it independent like a popup from the left side we can open, minimise, maximise
    /// whenever we want"). Two states (the maximised third went 2026-10-26, Shombith: "remove the maximize button from
    /// the friends panel"):
    /// <list type="bullet">
    /// <item><b>Closed</b> - only the handle shows; pressing it opens the drawer. Since rev 6 the handle is the
    /// presence orb in the bottom-left corner (<see cref="KitPresenceOrb"/>, Docs/MENU_REDESIGN_B.md §45.4).</item>
    /// <item><b>Open</b> - the window slides in from the left at its normal width.</item>
    /// </list>
    /// The – button minimises back to the handle. The panel inside (PartyPanel) keeps all its own
    /// behaviour; this only moves and sizes its window.
    ///
    /// <para>Where it sits (§68, Shombith 2026-10-25): the window takes the whole left side, from under the top nav to
    /// the bottom, so its height follows the screen it is on. Over a hub window (Arcade, Arena, Mission, Toy Box) it
    /// docks: <see cref="DockWidth"/> tells each window's <see cref="FriendsDrawerDock"/> how much room to make, and
    /// the cards move right. Over Home, the Hangar and the other screens it slides over them and moves nothing. While
    /// any other window is on top (a game's configure, Settings, a leaderboard) it never covers it: the drawer
    /// closes and the orb hides until that window closes.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Friends Drawer")]
    public class FriendsDrawer : MonoBehaviour
    {
        public enum DrawerState { Closed = 0, Open = 1 }

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
        [Tooltip("The scene's switcher: which window is on top decides whether the drawer docks, floats or stays shut. " +
                 "Found in the scene when left empty.")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        [Header("Layout")]
        [Tooltip("The open window's width (canvas units).")]
        [SerializeField, Min(0f)] private float normalWidth = 460f;
        [Tooltip("Room left above the window for the top nav (canvas units).")]
        [SerializeField, Min(0f)] private float dockTop = 84f;
        [Tooltip("Room left below the window (canvas units).")]
        [SerializeField, Min(0f)] private float dockBottom;
        [Tooltip("Gap between the screen's left edge and the open window.")]
        [SerializeField] private float openInset;
        [Tooltip("Seconds a slide or resize takes.")]
        [SerializeField, Min(0f)] private float moveSeconds = 0.22f;

        [Header("On a phone")]
        [Tooltip("On a phone (its canvas's AdaptiveCanvasScaler at the phone layout; Docs/MENU_REDESIGN_B.md section 129) " +
                 "the drawer is a sheet over the window instead of docking beside it: the open window covers the left of " +
                 "the screen at this scale, top to bottom, and nothing is pushed. The window's PhoneRect applies the scale; " +
                 "keep the two equal (author_menu_friends_drawer.py writes both).")]
        [SerializeField, Range(0.3f, 1f)] private float phoneScale = 0.7f;

        private DrawerState _state = DrawerState.Closed;
        private AdaptiveCanvasScaler _scaler;
        private bool _phone;
        private Vector2 _pcAnchorMin, _pcAnchorMax, _pcPivot;
        private bool _pcRead;
        private float _fromX, _toX, _fromWidth, _toWidth;
        private float _t = 1f;
        private bool _blocked;

        public DrawerState State => _state;

        private void Awake()
        {
            if (!screenSwitcher) screenSwitcher = FindAnyObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (handleButton) handleButton.onClick.AddListener(Open);
            if (minimiseButton) minimiseButton.onClick.AddListener(Close);
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
            _scaler = GetComponentInParent<AdaptiveCanvasScaler>(true);
            if (_scaler) _scaler.PhoneLayoutChanged += OnPhoneLayout;
            OnPhoneLayout(_scaler && _scaler.UsesPhoneLayout);
            OnModalStackChanged();
        }

        private void OnDisable()
        {
            if (screenSwitcher) screenSwitcher.ModalStackChanged -= OnModalStackChanged;
            if (_scaler) _scaler.PhoneLayoutChanged -= OnPhoneLayout;
            SetDockWidth(0f);
        }

        /// <summary>Switch between the PC drawer (docks beside the window) and the phone sheet (covers it, scaled), closed.</summary>
        private void OnPhoneLayout(bool phone)
        {
            if (!window || phone == _phone) return;
            if (!_pcRead)
            {
                _pcAnchorMin = window.anchorMin; _pcAnchorMax = window.anchorMax; _pcPivot = window.pivot;
                _pcRead = true;
            }
            _phone = phone;
            if (phone)
            {
                window.anchorMin = Vector2.zero;
                window.anchorMax = Vector2.zero;
                window.pivot = Vector2.zero;
                window.anchoredPosition = new Vector2(window.anchoredPosition.x, 0f);
                FitPhoneSheet();
            }
            else
            {
                window.anchorMin = _pcAnchorMin; window.anchorMax = _pcAnchorMax; window.pivot = _pcPivot;
                window.offsetMin = new Vector2(window.offsetMin.x, dockBottom);
                window.offsetMax = new Vector2(window.offsetMax.x, -dockTop);
            }
            Apply(DrawerState.Closed, instant: true);
        }

        /// <summary>
        /// Size the phone sheet to the parent's height: the window's PhoneRect draws it at phoneScale, so it is
        /// parentHeight / phoneScale tall unscaled. The canvas takes its phone size a frame AFTER the layout switches, so
        /// this runs again whenever the drawer's rect changes; sized once at the switch, the sheet took the PC height
        /// (1080) and its title and minimise button sat above the screen (2026-11-06, in the Device Simulator).
        /// </summary>
        private void FitPhoneSheet()
        {
            if (!_phone || !window || window.parent is not RectTransform p) return;
            float h = p.rect.height / Mathf.Max(0.01f, phoneScale);
            var size = new Vector2(normalWidth, h);
            if ((window.sizeDelta - size).sqrMagnitude > 0.01f) window.sizeDelta = size;
        }

        private void OnRectTransformDimensionsChange() => FitPhoneSheet();

        private void OnDestroy()
        {
            if (handleButton) handleButton.onClick.RemoveListener(Open);
            if (minimiseButton) minimiseButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (!_blocked) Apply(DrawerState.Open, instant: false);
        }

        public void Close() => Apply(DrawerState.Closed, instant: false);

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
            // The window stays drawn while it slides out; it stops taking input at once.
            if (open) KitVisibility.SetShown(windowGroup, true);
            else if (windowGroup) { windowGroup.interactable = false; windowGroup.blocksRaycasts = false; }
            if (!window) return;

            float width = normalWidth;
            _fromX = window.anchoredPosition.x;
            _fromWidth = window.sizeDelta.x;
            _toWidth = width;
            _toX = open ? openInset : -width * (_phone ? phoneScale : 1f) - 8f;
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
            // On a phone the drawer covers the window as a sheet: nothing docks beside it.
            SetDockWidth(OverHubWindow && !_phone ? Mathf.Max(0f, reach) : 0f);
        }

        private static void SetDockWidth(float width)
        {
            if (Mathf.Approximately(DockWidth, width)) return;
            DockWidth = width;
            DockWidthChanged?.Invoke(width);
        }
    }
}
