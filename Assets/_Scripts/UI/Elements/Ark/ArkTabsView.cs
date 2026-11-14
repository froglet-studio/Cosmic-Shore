using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.UI
{
    /// <summary>A page of the Ark's tabs: told when it comes on screen and when it leaves.</summary>
    public interface IArkTabPage
    {
        /// <summary>True while the page is the one on screen AND the Ark is the active screen.</summary>
        void SetPageShown(bool shown);
    }

    /// <summary>A page of the Ark's tabs that has levels of its own (the Pilots tab's PDA, the Artifacts tab's hoard,
    /// artifact and feed). B on a pad, or Escape, steps it one level back (Docs/MENU_REDESIGN_B.md section 205).</summary>
    public interface IArkTabBack
    {
        /// <summary>Steps one level back. False when the page is already at its top level, so the press is not its.</summary>
        bool TryBack();
    }

    /// <summary>
    /// The Ark's tabs (Shombith, 2026-10-27: "add two more tabs along with the converter"): CONVERTER, SQUADS and DECOR
    /// in one row of word tabs (<see cref="KitTextTab"/>), each with its page; one page shows at a time. ARTIFACTS joined
    /// them on 2026-10-27 (Garrett, Docs/Pilots/ARTIFACTS.md §4: crystals raise an artifact's tracks), and PILOTS on
    /// 2026-11-12 (Garrett, Docs/MENU_REDESIGN_B.md §92.1: the crew, each pilot a door to their profile page).
    ///
    /// <para>A tab whose <see cref="Tab.availability"/> is not Available refuses the press the way every menu entry
    /// does (<see cref="MenuAvailabilityView"/>: the sting, the shake, "Not open yet.") and its page never shows. DECOR
    /// ships Locked ("lock it we do not need to open it now"); opening it later is flipping that one state.</para>
    ///
    /// <para>The Ark screen's one <see cref="IScreen"/> is the <see cref="ElementalConverterView"/> above this view, so
    /// it forwards the screen's enter and exit here (<see cref="OnScreenEnter"/>, <see cref="OnScreenExit"/>), and it
    /// reads <see cref="IsShown"/> for the converter tab to stop rendering its 3D picture while another tab is up.</para>
    /// </summary>
    public class ArkTabsView : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            [Tooltip("The tab's word in the row.")]
            public KitTextTab button;
            [Tooltip("The page, shown and hidden through its CanvasGroup.")]
            public CanvasGroup page;
            [Tooltip("The page's component told when it shows (an IArkTabPage). Optional.")]
            public MonoBehaviour pageLogic;
            [Tooltip("Locked or Unavailable refuses the tab. Empty is Available.")]
            public MenuAvailabilityView availability;
        }

        [Tooltip("The tabs, left to right: Converter, Squads, Pilots, Artifacts, Decor.")]
        [SerializeField] private Tab[] tabs = Array.Empty<Tab>();
        [Tooltip("The tab shown the first time the Ark opens.")]
        [SerializeField, Min(0)] private int defaultTab;

        [Header("Page change (the HyperSea pass, Docs/MENU_REDESIGN_B.md section 197)")]
        [Tooltip("Seconds the page left takes to fade out. It takes no input from the press on.")]
        [SerializeField, Min(0f)] private float pageOutSeconds = KitMotion.Micro;
        [Tooltip("Seconds before the page reached starts to come in: the backdrop's membrane is past the middle by then " +
                 "(0.45 of ArkBackdropConfig's pass seconds).")]
        [SerializeField, Min(0f)] private float pageInDelay = 0.22f;
        [Tooltip("Seconds the page reached takes to rise in (the kit's panel token).")]
        [SerializeField, Min(0f)] private float pageInSeconds = KitMotion.Panel;
        [Tooltip("How far the page reached rises as it comes in, in canvas units.")]
        [SerializeField, Min(0f)] private float pageRise = 14f;

        [Header("Back (B on a pad, Escape)")]
        [Tooltip("Leave empty to find the one in the scene. A modal over the Ark (a pilot's profile page) keeps B for itself.")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        int _current = -1;
        bool _screenActive;

        // The pages in motion: the one left fading out, the one reached rising in.
        CanvasGroup _leaving, _arriving;
        RectTransform _arrivingRect;
        Vector2 _arrivingRest;
        float _leaveClock = -1f, _arriveClock = -1f;

        /// <summary>Raised with the new tab's index when the shown tab changes.</summary>
        public event Action<int> Changed;

        public int Current => _current;

        /// <summary>True while tab <paramref name="index"/> is the one shown.</summary>
        public bool IsShown(int index) => index == _current;

        /// <summary>The page logic tab <paramref name="index"/> drives (its <see cref="IArkTabPage"/>), or null. The Ark's 3D
        /// picks each tab's room by WHAT the tab holds, never by its position in the row (tabs get inserted).</summary>
        public MonoBehaviour PageLogic(int index) => index >= 0 && index < tabs.Length ? tabs[index].pageLogic : null;

        /// <summary>Tab <paramref name="index"/>'s word in the row, or null (the backdrop's ripple starts from it).</summary>
        public RectTransform ButtonRect(int index) =>
            index >= 0 && index < tabs.Length && tabs[index].button ? tabs[index].button.transform as RectTransform : null;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button) tabs[i].button.onClick.AddListener(() => Press(index));
            }
            Show(Mathf.Clamp(defaultTab, 0, Mathf.Max(0, tabs.Length - 1)));
        }

        /// <summary>A tab was pressed: shows it, or lets its availability refuse.</summary>
        public void Press(int index)
        {
            if (index < 0 || index >= tabs.Length) return;
            var gate = tabs[index].availability;
            if (gate && !gate.TryPress()) return;
            Show(index);
        }

        /// <summary>Shows tab <paramref name="index"/> (no availability check: the caller decided).</summary>
        public void Show(int index)
        {
            if (index < 0 || index >= tabs.Length || index == _current) return;
            int previous = _current;
            _current = index;
            SettlePages();
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = tabs[i];
                bool on = i == index;
                if (t.button) t.button.IsSelected = on;
                // The tab repaints its word on every state change; a locked tab's dim goes back over it.
                if (t.availability) t.availability.Reapply();
                if (t.page) KitVisibility.SetShown(t.page, on);
            }
            // Pressed from another tab: the page left fades (it took no input from the press on), the page reached
            // rises in once the backdrop's membrane has passed the middle. The first page of all just shows.
            if (previous >= 0 && enabled && gameObject.activeInHierarchy) BeginPageChange(tabs[previous].page, tabs[index].page);
            if (previous >= 0) Tell(previous, false);
            Tell(index, _screenActive);
            Changed?.Invoke(index);
        }

        /// <summary>The Ark became the active screen (forwarded by <see cref="ElementalConverterView"/>).</summary>
        public void OnScreenEnter()
        {
            _screenActive = true;
            if (_current >= 0) Tell(_current, true);
        }

        /// <summary>The player left the Ark.</summary>
        public void OnScreenExit()
        {
            _screenActive = false;
            SettlePages();
            if (_current >= 0) Tell(_current, false);
        }

        void BeginPageChange(CanvasGroup leaving, CanvasGroup arriving)
        {
            if (leaving && leaving != arriving && pageOutSeconds > 0f)
            {
                _leaving = leaving;
                _leaving.alpha = 1f;          // hidden already (no input, no layout); only its look fades
                _leaveClock = 0f;
            }
            if (arriving && (pageInDelay > 0f || pageInSeconds > 0f))
            {
                _arriving = arriving;
                _arrivingRect = arriving.transform as RectTransform;
                _arrivingRest = _arrivingRect ? _arrivingRect.anchoredPosition : Vector2.zero;
                _arriveClock = 0f;
                ApplyArrival(0f);
            }
        }

        void Update()
        {
            if (_screenActive && BackPressed()) TryBack();
            float dt = Time.unscaledDeltaTime;
            if (_leaveClock >= 0f)
            {
                _leaveClock += dt;
                float k = Mathf.Clamp01(_leaveClock / Mathf.Max(0.001f, pageOutSeconds));
                if (_leaving) _leaving.alpha = 1f - k;
                if (k >= 1f) EndLeave();
            }
            if (_arriveClock >= 0f)
            {
                _arriveClock += dt;
                float k = pageInSeconds <= 0f ? 1f : (_arriveClock - pageInDelay) / pageInSeconds;
                if (_arriveClock >= pageInDelay + pageInSeconds) EndArrive();
                else ApplyArrival(KitMotion.Ease(KitEase.OutQuint, Mathf.Max(0f, k)));
            }
        }

        /// <summary>B or Escape: the shown page steps one level back, if it has levels and is not at its top one.
        /// Nothing happens under a modal (it closes itself on B) or on a guided path (only the spotlit control works).</summary>
        public bool TryBack()
        {
            if (!_screenActive || _current < 0 || _current >= tabs.Length) return false;
            if (MenuGuide.IsActive) return false;
            if (!screenSwitcher) screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            if (screenSwitcher && screenSwitcher.HasActiveModal) return false;
            return tabs[_current].pageLogic is IArkTabBack page && page.TryBack();
        }

        static bool BackPressed() =>
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        void ApplyArrival(float e)
        {
            if (_arriving) _arriving.alpha = e;
            if (_arrivingRect) _arrivingRect.anchoredPosition = _arrivingRest + Vector2.down * (pageRise * (1f - e));
        }

        void EndLeave()
        {
            if (_leaving && _leaving != _arriving) _leaving.alpha = 0f;
            _leaving = null;
            _leaveClock = -1f;
        }

        void EndArrive()
        {
            if (_arriving) _arriving.alpha = 1f;
            if (_arrivingRect) _arrivingRect.anchoredPosition = _arrivingRest;
            _arriving = null;
            _arrivingRect = null;
            _arriveClock = -1f;
        }

        /// <summary>Lands any page change under way where it was going (a new press, leaving the screen).</summary>
        void SettlePages()
        {
            if (_leaveClock >= 0f) EndLeave();
            if (_arriveClock >= 0f) EndArrive();
        }

        void OnDisable() => SettlePages();

        void Tell(int index, bool shown)
        {
            if (tabs[index].pageLogic is IArkTabPage page) page.SetPageShown(shown);
        }
    }
}
