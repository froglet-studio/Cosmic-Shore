using System;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Arcade's two dropdowns beside ALL GAMES, ELEMENT and VESSEL (Docs/MENU_REDESIGN_B.md sections 85 and 90;
    /// Shombith 2026-10-27: "Drop the sort controls, the locked/unlocked filter and solo/multiplayer. Two dropdowns:
    /// elements and vessels"). The grid's order is fixed - favourites first, A-Z under them (<see cref="ArcadeGridOrder"/>).
    /// Each button opens its list of choices just under the ALL GAMES line - a row of tabs, the chosen one underlined -
    /// which pushes the grid down rather than floating over it, so a pointer, a finger and the pad all use it the same
    /// way. Picking a choice closes the list, returns the pad to the button and raises <see cref="Changed"/>; the view
    /// re-fills the grid. The choice lasts the session.
    /// </summary>
    public class ArcadeGridOptions : MonoBehaviour
    {
        [Header("Element")]
        [SerializeField] private KitButton elementButton;
        [Tooltip("The element button's word. {choice} is the picked element's name.")]
        [SerializeField] private TMP_Text elementLabel;
        [SerializeField] private string elementCaption = "Element: {choice}";
        [Tooltip("One name per choice: All, then Charge to Time.")]
        [SerializeField] private string[] elementNames = { "All", "Charge", "Mass", "Space", "Time" };
        [Tooltip("The list of element choices (hidden until the button is pressed).")]
        [SerializeField] private CanvasGroup elementRow;
        [Tooltip("One tab per choice: All, then Charge to Time.")]
        [SerializeField] private KitTextTab[] elementTabs = { };

        [Header("Vessel")]
        [SerializeField] private KitButton vesselButton;
        [SerializeField] private TMP_Text vesselLabel;
        [SerializeField] private string vesselCaption = "Vessel: {choice}";
        [Tooltip("One name per choice, in the same order as the vessel tabs (All first).")]
        [SerializeField] private string[] vesselNames = { "All" };
        [Tooltip("The vessel class each tab picks, in its order. Any is All.")]
        [SerializeField] private VesselClassType[] vesselChoices = { VesselClassType.Any };
        [SerializeField] private CanvasGroup vesselRow;
        [SerializeField] private KitTextTab[] vesselTabs = { };

        // The session's choice: the window is opened and closed many times, and the player's filter should stay.
        static int s_element, s_vessel;

        /// <summary>The picked element; <see cref="Data.Element.None"/> is All.</summary>
        public Element Element => s_element <= 0 ? Element.None : Element.Charge + (s_element - 1);

        /// <summary>The picked vessel class; <see cref="VesselClassType.Any"/> is All.</summary>
        public VesselClassType Vessel =>
            s_vessel >= 0 && s_vessel < vesselChoices.Length ? vesselChoices[s_vessel] : VesselClassType.Any;

        /// <summary>A choice changed: the grid should be filled again.</summary>
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
            if (elementButton) elementButton.onClick.AddListener(() => Toggle(elementRow, elementTabs, s_element));
            if (vesselButton) vesselButton.onClick.AddListener(() => Toggle(vesselRow, vesselTabs, s_vessel));
            for (int i = 0; i < elementTabs.Length; i++)
            {
                int v = i;
                if (elementTabs[i]) elementTabs[i].onClick.AddListener(() => Pick(ref s_element, v, elementButton));
            }
            for (int i = 0; i < vesselTabs.Length; i++)
            {
                int v = i;
                if (vesselTabs[i]) vesselTabs[i].onClick.AddListener(() => Pick(ref s_vessel, v, vesselButton));
            }
        }

        void Toggle(CanvasGroup row, KitTextTab[] tabs, int current)
        {
            bool open = !KitVisibility.IsShown(row);
            Close();
            if (!open) return;
            KitVisibility.SetShown(row, true);
            // On the pad the focus moves into the list, onto the current choice.
            if (current >= 0 && current < tabs.Length && tabs[current] && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(tabs[current].gameObject);
        }

        void Pick(ref int field, int value, KitButton back)
        {
            bool changed = field != value;
            field = value;
            Close();
            Draw();
            if (back && EventSystem.current) EventSystem.current.SetSelectedGameObject(back.gameObject);
            if (changed) Changed?.Invoke();
        }

        /// <summary>Both lists shut (the window opening, a choice picked, the other button pressed).</summary>
        public void Close()
        {
            KitVisibility.SetShown(elementRow, false);
            KitVisibility.SetShown(vesselRow, false);
        }

        void Draw()
        {
            DrawOne(elementLabel, elementCaption, elementNames, s_element, elementTabs);
            DrawOne(vesselLabel, vesselCaption, vesselNames, s_vessel, vesselTabs);
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
