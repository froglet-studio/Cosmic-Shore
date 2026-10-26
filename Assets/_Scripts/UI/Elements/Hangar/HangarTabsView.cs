using TMPro;
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
    /// A page that names what it shows in the Hangar's header while it is on screen (Rev 24: the PILOTS tab puts the
    /// selected pilot's name and line where the vessel's were).
    /// </summary>
    public interface IHangarHeader
    {
        /// <summary>False while the page has nothing to name (the vessel's header stays).</summary>
        bool TryGetHeader(out string kicker, out string title, out string line);

        /// <summary>Raised when what the page names changes (another pilot picked).</summary>
        event System.Action HeaderChanged;
    }

    /// <summary>
    /// The Hangar's right-hand panel. Rev 25 (Shombith, 2026-10-26: "Overview, Abilities, Upgrades, Games, Pilots and
    /// artifacts all are on the same tab nav bars"): one row of tabs, each page its own tab (a group of one), LB / RB
    /// stepping through all of them. Rev 24 grouped them - OVERVIEW's parts on LB / RB and the groups on Y - and a group
    /// of several pages still works that way. The header above the vessel follows the tab: the vessel on its own pages,
    /// the pilot on PILOTS, the slot on ARTIFACTS ("the header text can change as per the tab we are in").
    ///
    /// <para>Underneath it is still one strip of pages (<see cref="tabs"/>, left to right), each group a run of them
    /// (<see cref="groups"/>). A swap is one short beat: the underline glides to the new tab (KitTextTab), the old page
    /// slides out and fades, the new one wipes in with a slight skew settling to square. Interruptible. Pages show and
    /// hide through their CanvasGroups (KitVisibility). With no groups authored, LB / RB step every page (the rev-3
    /// panel).</para>
    /// </summary>
    public class HangarTabsView : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            [Tooltip("The page's own button: an OVERVIEW part in the parts row. Empty for a group's only page.")]
            public KitTextTab button;
            public CanvasGroup page;
            [Tooltip("The page's component that takes the selected vessel (optional for a static page).")]
            public MonoBehaviour pageLogic;
        }

        [System.Serializable]
        public class Group
        {
            [Tooltip("The tab in the top row (OVERVIEW, PILOTS, ARTIFACTS).")]
            public KitTextTab button;
            [Tooltip("The group's first page in Tabs.")]
            public int firstPage;
            [Tooltip("How many pages the group has; LB / RB step them when it is more than one.")]
            [Min(1)] public int pageCount = 1;
            [Tooltip("Shown while the group is on screen: OVERVIEW's parts row with its LB / RB keycaps (optional).")]
            public CanvasGroup partsRow;
            [Tooltip("The header's kicker on this tab; {vessel} is filled in. Empty (with an empty title) keeps the vessel's header, unless the page names its own.")]
            public string kickerTemplate;
            [Tooltip("The header's title on this tab; {vessel} is filled in.")]
            public string titleTemplate;
            [Tooltip("The header's line on this tab; {vessel} is filled in.")]
            public string lineTemplate;
        }

        [Header("Pages (left to right)")]
        [SerializeField] private Tab[] tabs = System.Array.Empty<Tab>();

        [Header("Tabs (Rev 24)")]
        [SerializeField] private Group[] groups = System.Array.Empty<Group>();

        [Header("Header (follows the tab)")]
        [SerializeField] private TMP_Text headerKickerText;
        [SerializeField] private TMP_Text headerTitleText;
        [SerializeField] private TMP_Text headerLineText;

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
        [Tooltip("Step OVERVIEW's parts with the pad's shoulder buttons (LB / RB). Keyboard players click the tabs: Q and E " +
                 "are ability keys (ControlGlyphSet), so they cannot double as tab keys.")]
        [SerializeField] private bool shoulderInput = true;
        [Tooltip("Step the tabs (OVERVIEW, PILOTS, ARTIFACTS) with the pad's Y. On Home Y opens the profile; on the Hangar it is free.")]
        [SerializeField] private bool northInput = true;

        int _current;
        int _previous = -1;
        float _t = 1f;
        int _group = -1;
        int[] _groupPage = System.Array.Empty<int>();
        SO_Vessel _vessel;
        HangarVesselStage _stage;
        string _vesselKicker, _vesselTitle, _vesselLine;

        public int Current => _current;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button) tabs[i].button.onClick.AddListener(() => Show(index));
                if (tabs[i].pageLogic is IHangarHeader header) header.HeaderChanged += RefreshHeader;
            }
            _groupPage = new int[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                int index = g;
                _groupPage[g] = groups[g].firstPage;
                if (groups[g].button) groups[g].button.onClick.AddListener(() => ShowGroup(index));
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
            RefreshHeader();
        }

        /// <summary>The vessel's own header (kicker, name, line): what OVERVIEW shows, and any tab that names nothing.</summary>
        public void SetVesselHeader(string kicker, string title, string line)
        {
            _vesselKicker = kicker;
            _vesselTitle = title;
            _vesselLine = line;
            RefreshHeader();
        }

        public void ShowGroup(int group)
        {
            if (groups.Length == 0) return;
            group = (group % groups.Length + groups.Length) % groups.Length;
            Show(_groupPage[group]);
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
            ApplyGroup();
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
            _group = -1;
            ApplyGroup();
        }

        int GroupOf(int page)
        {
            for (int g = 0; g < groups.Length; g++)
                if (page >= groups[g].firstPage && page < groups[g].firstPage + groups[g].pageCount) return g;
            return -1;
        }

        /// <summary>The tab row, the parts row and the header for the page now on screen.</summary>
        void ApplyGroup()
        {
            int group = GroupOf(_current);
            if (group >= 0) _groupPage[group] = _current;
            if (group != _group)
            {
                _group = group;
                for (int g = 0; g < groups.Length; g++)
                {
                    if (groups[g].button) groups[g].button.IsSelected = g == group;
                    if (groups[g].partsRow) KitVisibility.SetShown(groups[g].partsRow, g == group);
                }
            }
            RefreshHeader();
        }

        void RefreshHeader()
        {
            string kicker = _vesselKicker, title = _vesselTitle, line = _vesselLine;
            var group = _group >= 0 && _group < groups.Length ? groups[_group] : null;
            var named = _current >= 0 && _current < tabs.Length ? tabs[_current].pageLogic as IHangarHeader : null;
            if (named != null && named.TryGetHeader(out var k, out var t, out var l))
            {
                kicker = k;
                title = t;
                line = l;
            }
            else if (group != null && !string.IsNullOrEmpty(group.titleTemplate))
            {
                string vessel = _vessel ? _vessel.CardName.ToUpperInvariant() : string.Empty;
                kicker = TemplateText.Fill(group.kickerTemplate, "vessel", vessel);
                title = TemplateText.Fill(group.titleTemplate, "vessel", vessel);
                line = TemplateText.Fill(group.lineTemplate, "vessel", vessel);
            }
            if (headerKickerText) headerKickerText.text = kicker ?? string.Empty;
            if (headerTitleText) headerTitleText.text = title ?? string.Empty;
            if (headerLineText) headerLineText.text = line ?? string.Empty;
        }

        void Update()
        {
            // While a preview holds the stick, LB / RB / Y belong to the vessel.
            if (isActiveAndEnabled && !ModePreviewWindow.AnyHasFocus)
            {
                var pad = Gamepad.current;
                if (pad != null)
                {
                    if (northInput && groups.Length > 0 && pad.buttonNorth.wasPressedThisFrame) ShowGroup(_group + 1);
                    else if (shoulderInput && pad.rightShoulder.wasPressedThisFrame) StepPart(+1);
                    else if (shoulderInput && pad.leftShoulder.wasPressedThisFrame) StepPart(-1);
                }
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

        /// <summary>LB / RB: the next part of the tab on screen, wrapping inside it; from a one-page tab, the next tab.</summary>
        void StepPart(int step)
        {
            if (groups.Length == 0) { Show(_current + step); return; }
            if (_group < 0) return;
            var g = groups[_group];
            if (g.pageCount <= 1) { ShowGroup(_group + step); return; }
            int offset = ((_current - g.firstPage + step) % g.pageCount + g.pageCount) % g.pageCount;
            Show(g.firstPage + offset);
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
