using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Ark's PILOTS tab: the crew PDA lying on the hangar's workbench (Rev 99 C, picked by Shombith 2026-11-13: "a PDA
    /// lying somewhere where you see the details and stories of each pilot"; Docs/MENU_REDESIGN_B.md section 191). It
    /// keeps Garrett's crew and profile page (section 92.1) and puts them on the device:
    /// <list type="bullet">
    /// <item><b>Lying down.</b> The backdrop is the bench with the PDA on it (<see cref="ArkRoom.Bench"/>); its glass
    /// shows the close-up of the last pilot read (<see cref="PdaGlassImage"/>), with their callsign and name and PICK UP
    /// beside it.</item>
    /// <item><b>Picked up.</b> The backdrop plays the PDA lifted to the eye (<see cref="ArkBackdropRun.PickUp"/>, then
    /// <see cref="ArkBackdropRun.Held"/>), and the crew appears on its glass once it is up: one cell a pilot, the close-up
    /// of their callsign's scene, their name, their callsign and, under it, their ability: what their sight shows
    /// (<see cref="CallsignSetSO.Callsign.brief"/>; Shombith, 2026-11-13).</item>
    /// <item><b>A pilot's file.</b> A press opens that pilot's file on the glass: the close-up, the callsign and what it
    /// lights, the name, the human and two animals, the faction and home, the story, PREV / NEXT through the crew, and
    /// FULL PROFILE, which opens Garrett's full-screen profile page (<see cref="ScreenSwitcher.OpenPilotProfile"/>).</item>
    /// </list>
    /// <see cref="ElementalConverterView"/> plays the bench's clips from <see cref="HeldChanged"/>.
    ///
    /// <para>The crew is the account's (<see cref="PilotLoadoutStore.GetCrew"/>, in the order they joined): all fifty
    /// painted pilots for now, callsigns shared (Garrett, 2026-11-16: "lets get all 50 into the game. it is ok if we
    /// duplicate callsign everything else will be the different"), and later a crew grown one pilot at a time. So the
    /// glass shows a page of <c>cells</c> at a time and steps through the pages with <c>crewPreviousButton</c> and
    /// <c>crewNextButton</c>, shown only while the crew fills more than one. A cell is a <see cref="SquadPickChip"/>, the
    /// Squads tab's own.</para>
    ///
    /// <para>The picture is the person's own close-up (<see cref="PilotSceneCatalogSO.TryGetFor"/>), else their
    /// callsign's painted pilot's: the same face the profile page shows full screen. With no scene the words show alone.</para>
    /// </summary>
    public class ArkPilotsPage : MonoBehaviour, IArkTabPage, IArkTabBack
    {
        [Tooltip("Each callsign's scene and close-up (Tools/Build/author_pilot_scene_assets.py).")]
        [SerializeField] private PilotSceneCatalogSO scenes;
        [Tooltip("One page of the crew, in crew order: one cell a pilot.")]
        [SerializeField] private SquadPickChip[] cells;
        [Tooltip("One a cell, in the same order: what that pilot's sight shows, under their callsign.")]
        [SerializeField] private TMP_Text[] sightLines;
        [Tooltip("Shown on the glass while the crew is empty.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private TMP_Text countText;
        [Tooltip("Step to the previous and next page of the crew. Shown only while the crew fills more than one page.")]
        [SerializeField] private KitButton crewPreviousButton;
        [SerializeField] private KitButton crewNextButton;
        [SerializeField] private TMP_Text crewPageText;
        [Tooltip("Leave empty to find the one in the scene.")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        [Header("Lying on the bench")]
        [Tooltip("What shows while the PDA lies on the bench: the last pilot read and PICK UP.")]
        [SerializeField] private CanvasGroup restGroup;
        [SerializeField] private KitButton pickUpButton;
        [Tooltip("The PDA's glass in the backdrop: shows the last pilot read.")]
        [SerializeField] private PdaGlassImage glass;
        [SerializeField] private TMP_Text lastCallsignText;
        [SerializeField] private TMP_Text lastNameText;

        [Header("Held up")]
        [Tooltip("The PDA's glass while it is held up: the crew or a pilot's file, and LAY DOWN.")]
        [SerializeField] private CanvasGroup heldGroup;
        [Tooltip("The crew on the glass.")]
        [SerializeField] private CanvasGroup crewGroup;
        [SerializeField] private KitButton putDownButton;
        [Tooltip("Seconds after a press before the glass's words show: the backdrop's pick-up or put-down plays first.")]
        [SerializeField, Min(0f)] private float settleSeconds = 0.4f;
        [Tooltip("Seconds the words take to fade in once the PDA has settled.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.2f;

        [Header("A pilot's file")]
        [SerializeField] private CanvasGroup fileGroup;
        [SerializeField] private Image filePicture;
        [SerializeField] private TMP_Text fileKickerText;
        [SerializeField] private TMP_Text fileNameText;
        [SerializeField] private TMP_Text fileSpeciesText;
        [SerializeField] private TMP_Text fileFactionText;
        [SerializeField] private TMP_Text fileSightCaptionText;
        [SerializeField] private TMP_Text fileSightText;
        [SerializeField] private TMP_Text fileStoryCaptionText;
        [SerializeField] private TMP_Text fileStoryText;
        [SerializeField] private TMP_Text fileCountText;
        [SerializeField] private KitButton backButton;
        [SerializeField] private KitButton previousButton;
        [SerializeField] private KitButton nextButton;
        [Tooltip("Opens the pilot's full-screen profile page (Garrett's, section 92.1).")]
        [SerializeField] private KitButton profileButton;

        [Header("Words (empty shows nothing)")]
        [Tooltip("A cell's first line. {name}, {given}, {family}, {callsign}, {brief}, {sight}, {species} and {faction} are " +
                 "filled in ({brief} is the callsign's sight in a few words, {sight} its whole line).")]
        [SerializeField] private string nameTemplate;
        [Tooltip("A cell's second line. The same tokens.")]
        [SerializeField] private string lineTemplate;
        [Tooltip("A cell's third line, under the callsign: the pilot's ability, what their sight shows. The same tokens.")]
        [SerializeField] private string sightTemplate;
        [Tooltip("Beside the caption: {count} is the crew's size.")]
        [SerializeField] private string countTemplate;
        [Tooltip("Between the crew's page steps: {page} of {pages}. Shown only while the crew fills more than one page.")]
        [SerializeField] private string pageTemplate;

        // Every file template takes every token: {name} {given} {family} (the person), {callsign} {sight} (their
        // callsign's word and what it lights), {species} {blend} (the human and two animals), {faction} {homeworld}
        // {backstory}, {index} {count} (where they stand in the crew). A line whose own fact is empty shows nothing.
        [Tooltip("Lying down, beside the PDA: the last pilot read. Its fact is {callsign}.")]
        [SerializeField] private string lastCallsignTemplate;
        [Tooltip("Lying down, under the callsign. Its fact is {name}.")]
        [SerializeField] private string lastNameTemplate;
        [Tooltip("The file's kicker. Its fact is {callsign}.")]
        [SerializeField] private string fileKickerTemplate;
        [Tooltip("The file's name. Its fact is {name}.")]
        [SerializeField] private string fileNameTemplate;
        [Tooltip("Its fact is {species}: the human and two animals.")]
        [SerializeField] private string fileSpeciesTemplate;
        [Tooltip("Its fact is {faction}; {homeworld} may follow it.")]
        [SerializeField] private string fileFactionTemplate;
        [Tooltip("The caption over the sight. Shown while the callsign has a {sight}.")]
        [SerializeField] private string fileSightCaptionTemplate;
        [Tooltip("Its fact is {sight}: what the callsign lights.")]
        [SerializeField] private string fileSightTemplate;
        [Tooltip("The caption over the story. Shown while the pilot has a {backstory}.")]
        [SerializeField] private string fileStoryCaptionTemplate;
        [Tooltip("Its fact is {backstory}.")]
        [SerializeField] private string fileStoryTemplate;
        [Tooltip("Where the pilot stands in the crew: {index} of {count}.")]
        [SerializeField] private string fileCountTemplate;

        static readonly string[] FileTokens =
        {
            "name", "given", "family", "callsign", "sight", "species", "blend", "faction", "homeworld", "backstory",
            "index", "count",
        };

        /// <summary>Every token a file template of this page may name (the page's generator reads it).</summary>
        public static IReadOnlyList<string> Tokens => FileTokens;

        /// <summary>Raised when the PDA is picked up (true) or laid down (false) by a press; the backdrop plays the move.</summary>
        public event Action<bool> HeldChanged;

        /// <summary>True while the PDA is held up (the crew or a file on its glass).</summary>
        public bool Held => _held;

        // The pilot last read, kept for the session: the PDA lies showing them when the tab is opened again.
        static string s_lastRead;

        readonly List<PilotRecord> _crew = new();
        readonly string[] _pairs = new string[FileTokens.Length * 2];
        bool _shown, _held, _filing;
        int _file;
        int _page;
        CanvasGroup _fading;
        float _fadeAt;

        void Awake()
        {
            for (int i = 0; i < FileTokens.Length; i++) _pairs[i * 2] = FileTokens[i];
            for (int i = 0; i < cells.Length; i++)
            {
                int index = i;
                if (cells[i] && cells[i].Button) cells[i].Button.onClick.AddListener(() => OpenFile(index));
            }
            Listen(pickUpButton, PickUp);
            Listen(putDownButton, PutDown);
            Listen(backButton, BackToCrew);
            Listen(previousButton, () => Step(-1));
            Listen(nextButton, () => Step(1));
            Listen(profileButton, OpenProfile);
            Listen(crewPreviousButton, () => StepPage(-1));
            Listen(crewNextButton, () => StepPage(1));
        }

        int Pages => cells.Length == 0 ? 1 : Mathf.Max(1, (_crew.Count + cells.Length - 1) / cells.Length);

        void StepPage(int by)
        {
            if (!_held || _filing) return;
            PilotLoadoutStore.GetCrew(_crew);
            int pages = Pages;
            _page = ((_page + by) % pages + pages) % pages;
            Refresh();
        }

        static void Listen(KitButton button, UnityEngine.Events.UnityAction action)
        {
            if (button) button.onClick.AddListener(action);
        }

        void OnEnable() => AccountInventoryStore.Changed += Refresh;
        void OnDisable() => AccountInventoryStore.Changed -= Refresh;

        public void SetPageShown(bool shown)
        {
            _shown = shown;
            // Every visit starts with the PDA lying on the bench: the backdrop settles there (ElementalConverterView
            // reads Held when the tab changes), so no move is announced.
            _held = false;
            _filing = false;
            _fading = null;
            if (shown) Refresh();
        }

        void Update()
        {
            if (!_fading) return;
            float t = (Time.unscaledTime - _fadeAt) / fadeSeconds;
            if (t < 0f) return;
            _fading.alpha = Mathf.Clamp01(t);
            if (t < 1f) return;
            KitVisibility.SetShown(_fading, true);
            _fading = null;
        }

        // Shows `group` once the backdrop's move has played: hidden and untouchable until then, then faded in.
        void ShowAfterMove(CanvasGroup group)
        {
            if (!group) return;
            KitVisibility.SetShown(group, false);
            _fading = group;
            _fadeAt = Time.unscaledTime + settleSeconds;
        }

        void PickUp()
        {
            if (!_shown || _held) return;
            _held = true;
            _filing = false;
            Refresh();
            ShowAfterMove(heldGroup);
            HeldChanged?.Invoke(true);
        }

        void PutDown()
        {
            if (!_shown || !_held) return;
            _held = false;
            _filing = false;
            Refresh();
            ShowAfterMove(restGroup);
            HeldChanged?.Invoke(false);
        }

        void Refresh()
        {
            if (!_shown) return;
            PilotLoadoutStore.GetCrew(_crew);
            _page = Mathf.Clamp(_page, 0, Pages - 1);

            DrawCells();
            KitVisibility.SetShown(emptyGroup, _crew.Count == 0);
            if (countText)
                countText.text = _crew.Count == 0
                    ? string.Empty
                    : TemplateText.Fill(countTemplate, "count", _crew.Count.ToString(CultureInfo.InvariantCulture));

            if (_filing && (_file < 0 || _file >= _crew.Count)) _filing = false;
            if (_filing) DrawFile(_crew[_file]);
            else DrawRest();

            // A group already waiting on the backdrop's move keeps waiting; the others follow the state now.
            if (_fading != restGroup) KitVisibility.SetShown(restGroup, !_held);
            if (_fading != heldGroup) KitVisibility.SetShown(heldGroup, _held);
            KitVisibility.SetShown(crewGroup, !_filing);
            KitVisibility.SetShown(fileGroup, _filing);
        }

        void DrawCells()
        {
            int first = _page * cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (!cell) continue;
                var sightLine = sightLines != null && i < sightLines.Length ? sightLines[i] : null;
                if (first + i >= _crew.Count)
                {
                    cell.Draw(false, string.Empty, string.Empty, null, false);
                    if (sightLine) sightLine.text = string.Empty;
                    continue;
                }

                var person = _crew[first + i];
                PilotVision callsign = person.Vision();
                var row = PilotSight.Set ? PilotSight.Set.For(callsign) : null;
                cell.Draw(true, Fill(nameTemplate, person, row), Fill(lineTemplate, person, row), CloseUp(person),
                          person.Id == s_lastRead);
                if (sightLine) sightLine.text = string.IsNullOrEmpty(row?.brief) ? string.Empty : Fill(sightTemplate, person, row);
            }

            int pages = Pages;
            bool several = pages > 1;
            if (crewPreviousButton) crewPreviousButton.gameObject.SetActive(several);
            if (crewNextButton) crewNextButton.gameObject.SetActive(several);
            if (crewPageText)
                crewPageText.text = several
                    ? TemplateText.Fill(pageTemplate, "page", (_page + 1).ToString(CultureInfo.InvariantCulture),
                        "pages", pages.ToString(CultureInfo.InvariantCulture))
                    : string.Empty;
        }

        static string Fill(string template, PilotRecord person, CallsignSetSO.Callsign row) =>
            TemplateText.Fill(template, "name", person.DisplayName(), "given", person.GivenName, "family", person.FamilyName,
                "callsign", row?.word, "brief", row?.brief, "sight", row?.meaning, "species", person.SpeciesCombo(),
                "faction", person.FactionName);

        Sprite CloseUp(PilotRecord person) => scenes && scenes.TryGetFor(person, out var scene) ? scene.closeUp : null;

        int LastReadIndex()
        {
            for (int i = 0; i < _crew.Count; i++)
                if (_crew[i].Id == s_lastRead) return i;
            return _crew.Count > 0 ? 0 : -1;
        }

        // Lying down: the glass shows the last pilot read, and their callsign and name stand beside PICK UP.
        void DrawRest()
        {
            int i = LastReadIndex();
            if (i < 0)
            {
                if (glass) glass.Sprite = null;
                Write(lastCallsignText, lastCallsignTemplate, string.Empty);
                Write(lastNameText, lastNameTemplate, string.Empty);
                return;
            }
            Facts(_crew[i], i, out string word, out _);
            if (glass) glass.Sprite = CloseUp(_crew[i]);
            Write(lastCallsignText, lastCallsignTemplate, word);
            Write(lastNameText, lastNameTemplate, _crew[i].DisplayName());
        }

        void OpenFile(int index)
        {
            // The account's inventory can arrive from the cloud while the tab is up, and that raises no Changed: the
            // press opens who the crew holds now, not who was drawn before it arrived.
            PilotLoadoutStore.GetCrew(_crew);
            index += _page * cells.Length;
            if (!_held || index < 0 || index >= _crew.Count) return;
            _filing = true;
            _file = index;
            s_lastRead = _crew[index].Id;
            Refresh();
        }

        /// <summary>B or Escape (<see cref="ArkTabsView.TryBack"/>): a file goes back to the crew, the crew lays the PDA
        /// down on the bench; lying down, the press is not the page's.</summary>
        public bool TryBack()
        {
            if (!_shown) return false;
            if (_filing) { BackToCrew(); return true; }
            if (_held) { PutDown(); return true; }
            return false;
        }

        void BackToCrew()
        {
            if (!_filing) return;
            _filing = false;
            // Back to the page that holds the pilot last read (PREV / NEXT in the file may have crossed pages).
            if (cells.Length > 0) _page = _file / cells.Length;
            Refresh();
        }

        void Step(int by)
        {
            if (!_filing || _crew.Count < 2) return;
            _file = ((_file + by) % _crew.Count + _crew.Count) % _crew.Count;
            s_lastRead = _crew[_file].Id;
            Refresh();
        }

        void OpenProfile()
        {
            if (!_filing || _file < 0 || _file >= _crew.Count) return;
            if (!screenSwitcher) screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (screenSwitcher) screenSwitcher.OpenPilotProfile(_crew[_file].Id);
        }

        // Fills the token pairs for `person`, the crew's `index`-th; returns their callsign's word and sight.
        void Facts(PilotRecord person, int index, out string word, out string sight)
        {
            var row = PilotSight.Set ? PilotSight.Set.For(person.Vision()) : null;
            word = row?.word ?? string.Empty;
            sight = row?.meaning ?? string.Empty;
            int t = 1;
            _pairs[t] = person.DisplayName(); t += 2;
            _pairs[t] = person.GivenName; t += 2;
            _pairs[t] = person.FamilyName; t += 2;
            _pairs[t] = word; t += 2;
            _pairs[t] = sight; t += 2;
            _pairs[t] = person.SpeciesCombo(); t += 2;
            _pairs[t] = person.SpeciesBlend(); t += 2;
            _pairs[t] = person.FactionName; t += 2;
            _pairs[t] = person.Homeworld; t += 2;
            _pairs[t] = person.Backstory; t += 2;
            _pairs[t] = (index + 1).ToString(CultureInfo.InvariantCulture); t += 2;
            _pairs[t] = _crew.Count.ToString(CultureInfo.InvariantCulture);
        }

        void DrawFile(PilotRecord person)
        {
            Facts(person, _file, out string word, out string sight);
            if (filePicture)
            {
                var face = CloseUp(person);
                filePicture.sprite = face;
                filePicture.enabled = face;
            }
            Write(fileKickerText, fileKickerTemplate, word);
            Write(fileNameText, fileNameTemplate, person.DisplayName());
            Write(fileSpeciesText, fileSpeciesTemplate, person.SpeciesCombo());
            Write(fileFactionText, fileFactionTemplate, person.FactionName);
            Write(fileSightCaptionText, fileSightCaptionTemplate, sight);
            Write(fileSightText, fileSightTemplate, sight);
            Write(fileStoryCaptionText, fileStoryCaptionTemplate, person.Backstory);
            Write(fileStoryText, fileStoryTemplate, person.Backstory);
            Write(fileCountText, fileCountTemplate, _crew.Count.ToString(CultureInfo.InvariantCulture));
            bool several = _crew.Count > 1;
            if (previousButton) previousButton.interactable = several;
            if (nextButton) nextButton.interactable = several;
        }

        void Write(TMP_Text target, string tmpl, string fact)
        {
            if (target) target.text = string.IsNullOrEmpty(fact) ? string.Empty : TemplateText.Fill(tmpl, _pairs);
        }
    }
}
