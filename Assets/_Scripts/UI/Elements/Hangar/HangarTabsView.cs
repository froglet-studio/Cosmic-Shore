using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.UI
{
    /// <summary>A page of the Hangar's right-hand panel; told the selected vessel when it changes.</summary>
    public interface IHangarTabPage
    {
        void SetVessel(SO_Vessel vessel, HangarVesselStage stage);

        /// <summary>The page became (or stopped being) the one on screen.</summary>
        void SetActivePage(bool active);
    }

    /// <summary>
    /// The Hangar's right-hand panel: five tabs - Abilities, Upgrades, Games, Pilots, Artifacts (rev 3:
    /// "on the right there should be place for 5 tabs and swapping should be animated pretty cool").
    ///
    /// <para>A swap is one short beat: the underline glides to the new tab (KitTextTab), the old page
    /// slides out to the left and fades, the new one wipes in from the right with a slight skew settling
    /// to square, and a scan line sweeps the panel. Interruptible - a second press mid-swap starts the
    /// next swap from wherever the pages are. LB / RB step through the tabs (pad); a tap or click on a tab
    /// jumps to it. Pages show and hide through their CanvasGroups (KitVisibility).</para>
    /// </summary>
    public class HangarTabsView : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            public KitTextTab button;
            public CanvasGroup page;
            [Tooltip("The page's component that takes the selected vessel (optional for a static page).")]
            public MonoBehaviour pageLogic;
        }

        [Header("Tabs (left to right)")]
        [SerializeField] private Tab[] tabs = System.Array.Empty<Tab>();

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float swapSeconds = 0.32f;
        [Tooltip("How far a page travels in and out, in px.")]
        [SerializeField] private float travel = 60f;
        [Tooltip("Skew the incoming page starts at, in degrees (settles to 0).")]
        [SerializeField] private float skewDegrees = 6f;
        [Tooltip("The scan line that sweeps the panel on every swap (optional).")]
        [SerializeField] private RectTransform scanLine;
        [SerializeField] private CanvasGroup scanGroup;
        [Tooltip("Height the scan line sweeps across, in px.")]
        [SerializeField] private float scanTravel = 820f;

        [Header("Input")]
        [Tooltip("Step tabs with the pad's shoulder buttons (LB / RB). Keyboard players click the tabs: Q and E " +
                 "are ability keys (ControlGlyphSet), so they cannot double as tab keys.")]
        [SerializeField] private bool shoulderInput = true;

        int _current;
        int _previous = -1;
        float _t = 1f;
        SO_Vessel _vessel;
        HangarVesselStage _stage;

        public int Current => _current;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button) tabs[i].button.onClick.AddListener(() => Show(index));
            }
            ShowInstant(0);
        }

        /// <summary>Hands every page the selected vessel and the stage it can drive.</summary>
        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
            foreach (var tab in tabs)
                if (tab.pageLogic is IHangarTabPage page) page.SetVessel(vessel, stage);
        }

        public void Show(int index)
        {
            if (tabs.Length == 0) return;
            index = (index % tabs.Length + tabs.Length) % tabs.Length;
            if (index == _current && _t >= 1f) return;
            _previous = _current;
            _current = index;
            _t = 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i].button) tabs[i].button.IsSelected = i == _current;
                if (tabs[i].pageLogic is IHangarTabPage page) page.SetActivePage(i == _current);
            }
            KitVisibility.SetShown(tabs[_current].page, true);
            if (_previous >= 0 && _previous != _current && tabs[_previous].page)
            {
                tabs[_previous].page.interactable = false;
                tabs[_previous].page.blocksRaycasts = false;
            }
        }

        void ShowInstant(int index)
        {
            _current = index;
            _previous = -1;
            _t = 1f;
            for (int i = 0; i < tabs.Length; i++)
            {
                KitVisibility.SetShown(tabs[i].page, i == index);
                if (tabs[i].button) tabs[i].button.IsSelected = i == index;
                if (tabs[i].page) Place(tabs[i].page.transform as RectTransform, 0f, 0f);
                if (tabs[i].pageLogic is IHangarTabPage page) page.SetActivePage(i == index);
            }
            if (scanGroup) scanGroup.alpha = 0f;
        }

        void Update()
        {
            if (shoulderInput && isActiveAndEnabled)
            {
                var pad = Gamepad.current;
                if (pad != null && pad.rightShoulder.wasPressedThisFrame) Show(_current + 1);
                else if (pad != null && pad.leftShoulder.wasPressedThisFrame) Show(_current - 1);
            }

            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / swapSeconds);
            float e = 1f - Mathf.Pow(1f - _t, 3f);        // OutCubic

            int dir = _previous < 0 ? 1 : (_current > _previous ? 1 : -1);
            var inPage = tabs[_current].page;
            if (inPage)
            {
                inPage.alpha = e;
                Place(inPage.transform as RectTransform, dir * travel * (1f - e), skewDegrees * (1f - e));
            }
            if (_previous >= 0 && _previous != _current && tabs[_previous].page)
            {
                var outPage = tabs[_previous].page;
                outPage.alpha = 1f - e;
                Place(outPage.transform as RectTransform, -dir * travel * e, 0f);
            }
            if (scanLine && scanGroup)
            {
                scanGroup.alpha = Mathf.Sin(e * Mathf.PI) * 0.9f;
                scanLine.anchoredPosition = new Vector2(scanLine.anchoredPosition.x, -scanTravel * e);
            }

            if (_t >= 1f)
            {
                if (_previous >= 0 && _previous != _current) KitVisibility.SetShown(tabs[_previous].page, false);
                if (inPage) Place(inPage.transform as RectTransform, 0f, 0f);
                if (scanGroup) scanGroup.alpha = 0f;
            }
        }

        static void Place(RectTransform rt, float x, float skew)
        {
            if (!rt) return;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            // A skew is a shear; a slight Y-rotation reads the same on a flat UI page and needs no mesh work.
            rt.localRotation = Quaternion.Euler(0f, skew, 0f);
        }
    }
}
