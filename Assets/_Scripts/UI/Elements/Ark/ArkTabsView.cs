using System;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>A page of the Ark's tabs: told when it comes on screen and when it leaves.</summary>
    public interface IArkTabPage
    {
        /// <summary>True while the page is the one on screen AND the Ark is the active screen.</summary>
        void SetPageShown(bool shown);
    }

    /// <summary>
    /// The Ark's tabs (Shombith, 2026-10-27: "add two more tabs along with the converter"): CONVERTER, SQUADS and DECOR
    /// in one row of word tabs (<see cref="KitTextTab"/>), each with its page; one page shows at a time.
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

        [Tooltip("The tabs, left to right: Converter, Squads, Decor.")]
        [SerializeField] private Tab[] tabs = Array.Empty<Tab>();
        [Tooltip("The tab shown the first time the Ark opens.")]
        [SerializeField, Min(0)] private int defaultTab;

        int _current = -1;
        bool _screenActive;

        /// <summary>Raised with the new tab's index when the shown tab changes.</summary>
        public event Action<int> Changed;

        public int Current => _current;

        /// <summary>True while tab <paramref name="index"/> is the one shown.</summary>
        public bool IsShown(int index) => index == _current;

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
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = tabs[i];
                bool on = i == index;
                if (t.button) t.button.IsSelected = on;
                // The tab repaints its word on every state change; a locked tab's dim goes back over it.
                if (t.availability) t.availability.Reapply();
                if (t.page) KitVisibility.SetShown(t.page, on);
            }
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
            if (_current >= 0) Tell(_current, false);
        }

        void Tell(int index, bool shown)
        {
            if (tabs[index].pageLogic is IArkTabPage page) page.SetPageShown(shown);
        }
    }
}
