using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The settings window's four tabs (GENERAL / DISPLAY / PERFORMANCE / OTHER): one tab active,
    /// its page shown, the others hidden.
    ///
    /// <para>Two looks share this class. The legacy one toggles each page and underline with
    /// SetActive and scales the labels. The style-B one (Docs/MENU_REDESIGN_B.md section 18) gives
    /// each tab a <see cref="KitTile"/> as its button - the tile's SELECTED state is the active tab -
    /// and a <see cref="CanvasGroup"/> as its page, and writes the tab's name into the window title.
    /// A tab uses the style-B path for whichever of those it has wired.</para>
    /// </summary>
    public class SettingsTabBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            [Tooltip("The tab button (GENERAL / DISPLAY / ...). A KitTile draws the active tab as its selected state.")]
            public Button button;
            [Tooltip("Legacy: the content panel shown (SetActive) while this tab is active.")]
            public GameObject content;
            [Tooltip("Legacy: the underline accent shown only while this tab is active.")]
            public GameObject underline;
            [Tooltip("Transform to scale on select (optional - defaults to the button's own transform).")]
            public Transform label;
            [Tooltip("Style-B: the page, shown and hidden through its CanvasGroup.")]
            public CanvasGroup page;
            [Tooltip("Style-B: the name written into the window title while this tab is active.")]
            public string title;
        }

        [SerializeField] List<Tab> tabs = new();
        [SerializeField] int defaultTab = 0;
        [SerializeField, Tooltip("Scale of the selected tab's label.")] float activeScale = 1.1f;
        [SerializeField, Tooltip("Scale of unselected tab labels.")] float inactiveScale = 0.95f;
        [SerializeField, Tooltip("Scale the labels on select. Off for the style-B hex tabs, whose selected state is the tile's own.")]
        bool scaleLabels = true;
        [SerializeField, Tooltip("Style-B: the window title, set to the active tab's title. Optional.")]
        TMP_Text titleText;

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
                if (t.page != null) KitVisibility.SetShown(t.page, active);
                if (t.button is KitTile tile) tile.IsSelected = active;
                if (active && titleText != null && !string.IsNullOrEmpty(t.title)) titleText.text = t.title;

                if (!scaleLabels) continue;
                var scaleTarget = t.label != null ? t.label : (t.button != null ? t.button.transform : null);
                if (scaleTarget != null)
                    scaleTarget.localScale = Vector3.one * (active ? activeScale : inactiveScale);
            }
        }
    }
}
