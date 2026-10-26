using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Arcade's SORT and FILTER (Docs/MENU_REDESIGN_B.md section 85; Shombith, 2026-10-26: "In arcade games beside the
    /// all games tasks I want to have a sort and filter button"). Each button opens its row of options just under the ALL
    /// GAMES line - a row of tabs, the chosen one underlined - which pushes the grid down rather than floating over it,
    /// so a pointer, a finger and the pad all use it the same way. Picking an option closes the row, returns the pad to
    /// the button and raises <see cref="Changed"/>; the view re-fills the grid. The choice lasts the session.
    /// </summary>
    public class ArcadeGridOptions : MonoBehaviour
    {
        [Header("Sort")]
        [SerializeField] private KitButton sortButton;
        [Tooltip("The sort button's word. {choice} is the picked sort's name.")]
        [SerializeField] private TMP_Text sortLabel;
        [SerializeField] private string sortCaption = "Sort: {choice}";
        [Tooltip("One name per ArcadeGridSort value, in its order. Empty: the button shows its caption without one.")]
        [SerializeField] private string[] sortNames = { "Favourites", "A-Z", "Unlocked" };
        [Tooltip("The row of sort options (hidden until the button is pressed).")]
        [SerializeField] private CanvasGroup sortRow;
        [Tooltip("One tab per ArcadeGridSort value, in its order.")]
        [SerializeField] private KitTextTab[] sortTabs = { };

        [Header("Filter")]
        [SerializeField] private KitButton filterButton;
        [SerializeField] private TMP_Text filterLabel;
        [SerializeField] private string filterCaption = "Filter: {choice}";
        [Tooltip("One name per ArcadeGridFilter value, in its order.")]
        [SerializeField] private string[] filterNames = { "All", "Favourites", "Unlocked", "Solo", "Multiplayer", "Charge", "Mass", "Space", "Time" };
        [SerializeField] private CanvasGroup filterRow;
        [Tooltip("One tab per ArcadeGridFilter value, in its order.")]
        [SerializeField] private KitTextTab[] filterTabs = { };

        // The session's choice: the window is opened and closed many times, and the player's order should stay.
        static ArcadeGridSort s_sort = ArcadeGridSort.FavouritesFirst;
        static ArcadeGridFilter s_filter = ArcadeGridFilter.All;

        public ArcadeGridSort Sort => s_sort;
        public ArcadeGridFilter Filter => s_filter;

        /// <summary>The sort or the filter changed: the grid should be filled again.</summary>
        public event Action Changed;

        bool _wired;

        void Awake() => Wire();

        void OnEnable()
        {
            Close();
            Draw();
        }

        void Wire()
        {
            if (_wired) return;
            _wired = true;
            if (sortButton) sortButton.onClick.AddListener(() => Toggle(sortRow, sortTabs, (int)s_sort));
            if (filterButton) filterButton.onClick.AddListener(() => Toggle(filterRow, filterTabs, (int)s_filter));
            for (int i = 0; i < sortTabs.Length; i++)
            {
                int v = i;
                if (sortTabs[i]) sortTabs[i].onClick.AddListener(() => Pick(ref s_sort, (ArcadeGridSort)v, sortButton));
            }
            for (int i = 0; i < filterTabs.Length; i++)
            {
                int v = i;
                if (filterTabs[i]) filterTabs[i].onClick.AddListener(() => Pick(ref s_filter, (ArcadeGridFilter)v, filterButton));
            }
        }

        void Toggle(CanvasGroup row, KitTextTab[] tabs, int current)
        {
            bool open = !KitVisibility.IsShown(row);
            Close();
            if (!open) return;
            KitVisibility.SetShown(row, true);
            // On the pad the focus moves into the row, onto the current choice.
            if (current >= 0 && current < tabs.Length && tabs[current] && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(tabs[current].gameObject);
        }

        void Pick<T>(ref T field, T value, KitButton back) where T : struct, Enum
        {
            bool changed = !field.Equals(value);
            field = value;
            Close();
            Draw();
            if (back && EventSystem.current) EventSystem.current.SetSelectedGameObject(back.gameObject);
            if (changed) Changed?.Invoke();
        }

        /// <summary>Both rows shut (the window opening, an option picked, the other button pressed).</summary>
        public void Close()
        {
            KitVisibility.SetShown(sortRow, false);
            KitVisibility.SetShown(filterRow, false);
        }

        void Draw()
        {
            DrawOne(sortLabel, sortCaption, sortNames, (int)s_sort, sortTabs);
            DrawOne(filterLabel, filterCaption, filterNames, (int)s_filter, filterTabs);
        }

        static void DrawOne(TMP_Text label, string caption, string[] names, int index, KitTextTab[] tabs)
        {
            string choice = names != null && index >= 0 && index < names.Length ? names[index] : string.Empty;
            if (label) label.text = TemplateText.Fill(caption, "choice", choice);
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i]) tabs[i].IsSelected = i == index;
        }
    }
}
