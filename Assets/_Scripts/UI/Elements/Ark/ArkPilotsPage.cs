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
    /// <para>The crew is the account's (<see cref="PilotLoadoutStore.GetCrew"/>, in the order they joined). It holds
    /// at most one pilot a callsign (Docs/Pilots/INVENTORY.md §3), so the tab has as many cells as there are
    /// callsigns and needs no scrolling. A cell is a <see cref="SquadPickChip"/>, the Squads tab's own.</para>
    ///
    /// <para>The picture is the callsign's, not the person's (<see cref="PilotSceneCatalogSO"/>): the same face the
    /// profile page shows full screen. A callsign with no scene shows the name and callsign alone.</para>
    /// </summary>
    public class ArkPilotsPage : MonoBehaviour, IArkTabPage
    {
        [Tooltip("Each callsign's scene and close-up (Tools/Build/author_pilot_scene_assets.py).")]
        [SerializeField] private PilotSceneCatalogSO scenes;
        [Tooltip("One cell a pilot, in crew order. A crew holds one pilot a callsign, so one cell a callsign.")]
        [SerializeField] private SquadPickChip[] cells;
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

        readonly List<PilotRecord> _crew = new();
        bool _shown;
        bool _warnedOverflow;

        void Awake()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int index = i;
                if (cells[i] && cells[i].Button) cells[i].Button.onClick.AddListener(() => Open(index));
            }
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
            if (_crew.Count > cells.Length && !_warnedOverflow)
            {
                // A pilot the tab cannot show is a fault, not a state: the crew outgrew one pilot a callsign.
                _warnedOverflow = true;
                CSDebug.LogWarning($"[ArkPilotsPage] The crew has {_crew.Count} pilots and the tab {cells.Length} cells; " +
                                   "the last are not shown (Tools/Build/author_menu_ark.py lays out one cell a callsign).", this);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (!cell) continue;
                if (i >= _crew.Count)
                {
                    cell.Draw(false, string.Empty, string.Empty, null, false);
                    continue;
                }

                var person = _crew[i];
                PilotVision callsign = person.Vision();
                var row = PilotSight.Set ? PilotSight.Set.For(callsign) : null;
                Sprite closeUp = scenes && scenes.TryGet(callsign, out var scene) ? scene.closeUp : null;
                cell.Draw(true, Fill(nameTemplate, person, row), Fill(lineTemplate, person, row), closeUp, false);
            }

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
            if (index < 0 || index >= _crew.Count) return;
            if (!screenSwitcher) screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (screenSwitcher) screenSwitcher.OpenPilotProfile(_crew[index].Id);
        }
    }
}
