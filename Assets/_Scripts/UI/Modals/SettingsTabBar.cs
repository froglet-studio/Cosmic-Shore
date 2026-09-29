using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Tab navigation for the options panel (GENERAL / DISPLAY / PERFORMANCE / OTHER) or any tabbed
    /// panel. Each tab pairs a button with its content panel plus an active-state underline and label
    /// scale for juice: clicking a tab shows its content and hides the rest, the underline activates
    /// only on the selected tab, and the selected label scales up (others scale down).
    ///
    /// Drag the 4 tabs in and it self-wires - no per-button UnityEvent wiring.
    /// </summary>
    public class SettingsTabBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            [Tooltip("The tab button (GENERAL / DISPLAY / ...).")]
            public Button button;
            [Tooltip("The content panel shown while this tab is active.")]
            public GameObject content;
            [Tooltip("The underline accent shown only while this tab is active.")]
            public GameObject underline;
            [Tooltip("Transform to scale on select (optional - defaults to the button's own transform).")]
            public Transform label;
        }

        [SerializeField] List<Tab> tabs = new();
        [SerializeField] int defaultTab = 0;
        [SerializeField, Tooltip("Scale of the selected tab's label.")] float activeScale = 1.1f;
        [SerializeField, Tooltip("Scale of unselected tab labels.")] float inactiveScale = 0.95f;

        public int Selected { get; private set; } = -1;

        void Awake()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                int idx = i;
                if (tabs[i].button != null)
                    tabs[i].button.onClick.AddListener(() => SelectTab(idx));
            }
            SelectTab(Mathf.Clamp(defaultTab, 0, Mathf.Max(0, tabs.Count - 1)));
        }

        public void SelectTab(int index)
        {
            Selected = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool active = i == index;
                var t = tabs[i];
                if (t.content != null) t.content.SetActive(active);
                if (t.underline != null) t.underline.SetActive(active);

                var scaleTarget = t.label != null ? t.label : (t.button != null ? t.button.transform : null);
                if (scaleTarget != null)
                    scaleTarget.localScale = Vector3.one * (active ? activeScale : inactiveScale);
            }
        }
    }
}
