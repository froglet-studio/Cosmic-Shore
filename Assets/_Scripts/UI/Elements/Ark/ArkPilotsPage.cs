using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Ark's PILOTS tab (Garrett, 2026-11-12: "use the full size art for each pilot's profile page in the ark";
    /// Docs/MENU_REDESIGN_B.md §92.1): the player's crew, one cell a pilot, each with the close-up of their callsign's
    /// scene, their name and their callsign. Pressing a pilot opens their profile page
    /// (<see cref="ScreenSwitcher.OpenPilotProfile"/>, <see cref="PilotProfileModal"/>).
    ///
    /// <para>The crew is the account's (<see cref="PilotLoadoutStore.GetCrew"/>, in the order they joined): all fifty
    /// painted pilots for now (Garrett, 2026-11-16), and later a crew grown one pilot at a time, so the tab shows a page
    /// of <c>cells</c> at a time and steps through the pages with PREVIOUS and NEXT (the Squads picker's way). A cell is
    /// a <see cref="SquadPickChip"/>, the Squads tab's own.</para>
    ///
    /// <para>The picture is the person's own close-up when they are a painted pilot, else their callsign's own painted
    /// pilot's (<see cref="PilotSceneCatalogSO.TryGetFor"/>): the same face the profile page shows full screen.</para>
    /// </summary>
    public class ArkPilotsPage : MonoBehaviour, IArkTabPage
    {
        [Tooltip("Each callsign's scene and close-up (Tools/Build/author_pilot_scene_assets.py).")]
        [SerializeField] private PilotSceneCatalogSO scenes;
        [Tooltip("One page of the crew, in crew order: one cell a pilot.")]
        [SerializeField] private SquadPickChip[] cells;
        [Tooltip("Step to the previous and next page of the crew. Shown only while the crew fills more than one page.")]
        [SerializeField] private KitButton previousButton;
        [SerializeField] private KitButton nextButton;
        [SerializeField] private TMP_Text pageText;
        [Tooltip("Shown while the crew is empty.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private TMP_Text countText;
        [Tooltip("Leave empty to find the one in the scene.")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        [Header("Words (empty shows nothing)")]
        [Tooltip("A cell's first line. {name}, {given}, {family}, {callsign}, {species} and {faction} are filled in.")]
        [SerializeField] private string nameTemplate;
        [Tooltip("A cell's second line. The same tokens.")]
        [SerializeField] private string lineTemplate;
        [Tooltip("Beside the caption: {count} is the crew's size.")]
        [SerializeField] private string countTemplate;
        [Tooltip("Between PREVIOUS and NEXT: {page} of {pages}. Shown only while the crew fills more than one page.")]
        [SerializeField] private string pageTemplate;

        readonly List<PilotRecord> _crew = new();
        bool _shown;
        int _page;

        void Awake()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int index = i;
                if (cells[i] && cells[i].Button) cells[i].Button.onClick.AddListener(() => Open(index));
            }
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(1));
        }

        int Pages => cells.Length == 0 ? 1 : Mathf.Max(1, (_crew.Count + cells.Length - 1) / cells.Length);

        void Step(int by)
        {
            PilotLoadoutStore.GetCrew(_crew);
            int pages = Pages;
            _page = ((_page + by) % pages + pages) % pages;
            Refresh();
        }

        void OnEnable() => AccountInventoryStore.Changed += Refresh;
        void OnDisable() => AccountInventoryStore.Changed -= Refresh;

        public void SetPageShown(bool shown)
        {
            _shown = shown;
            if (shown) Refresh();
        }

        void Refresh()
        {
            if (!_shown) return;
            PilotLoadoutStore.GetCrew(_crew);
            int pages = Pages;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            int first = _page * cells.Length;

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (!cell) continue;
                if (first + i >= _crew.Count)
                {
                    cell.Draw(false, string.Empty, string.Empty, null, false);
                    continue;
                }

                var person = _crew[first + i];
                PilotVision callsign = person.Vision();
                var row = PilotSight.Set ? PilotSight.Set.For(callsign) : null;
                Sprite closeUp = scenes && scenes.TryGetFor(person, out var scene) ? scene.closeUp : null;
                cell.Draw(true, Fill(nameTemplate, person, row), Fill(lineTemplate, person, row), closeUp, false);
            }

            bool several = pages > 1;
            if (previousButton) previousButton.gameObject.SetActive(several);
            if (nextButton) nextButton.gameObject.SetActive(several);
            if (pageText)
                pageText.text = several
                    ? TemplateText.Fill(pageTemplate, "page", (_page + 1).ToString(CultureInfo.InvariantCulture),
                        "pages", pages.ToString(CultureInfo.InvariantCulture))
                    : string.Empty;

            KitVisibility.SetShown(emptyGroup, _crew.Count == 0);
            if (countText)
                countText.text = _crew.Count == 0
                    ? string.Empty
                    : TemplateText.Fill(countTemplate, "count", _crew.Count.ToString(CultureInfo.InvariantCulture));
        }

        static string Fill(string template, PilotRecord person, CallsignSetSO.Callsign row) =>
            TemplateText.Fill(template, "name", person.DisplayName(), "given", person.GivenName, "family", person.FamilyName,
                "callsign", row?.word, "species", person.SpeciesCombo(), "faction", person.FactionName);

        void Open(int index)
        {
            // The account's inventory can arrive from the cloud while the tab is up, and that raises no Changed: the
            // press opens (and the cells show) who the crew holds now, not who was drawn before it arrived.
            Refresh();
            index += _page * cells.Length;
            if (index < 0 || index >= _crew.Count) return;
            if (!screenSwitcher) screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (screenSwitcher) screenSwitcher.OpenPilotProfile(_crew[index].Id);
        }
    }
}
