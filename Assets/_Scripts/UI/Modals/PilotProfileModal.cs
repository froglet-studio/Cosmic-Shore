using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A pilot's PROFILE PAGE (Garrett, 2026-11-11: "the main use will be a full screen profile for each pilot
    /// accessible through the ark app shell menu"; Docs/Pilots/PORTRAITS.md §8, Docs/MENU_REDESIGN_B.md §92.1): the
    /// pilot's scene across the whole screen, their name, callsign, animals, faction, home and story in a panel on the
    /// side they do not stand on, and a step to the next and previous pilot of the crew.
    ///
    /// <para><b>Who is shown.</b> A person of the player's crew (<see cref="PilotLoadoutStore.GetCrew"/>): the words
    /// are that person's own record. The picture is their CALLSIGN's scene (<see cref="PilotSceneCatalogSO"/>). A
    /// crew's starter pilots ARE the painted pilots (<see cref="PaintedCrewSO"/>, Garrett 2026-11-12: the stories must
    /// match the art), so for them the words and the picture are one person. A person who is not the one pictured
    /// (a recruit, until each person has art of their own) gets a caption saying who is (<c>picturedTemplate</c>). A
    /// callsign with no scene shows the words on the plain ground.</para>
    ///
    /// <para><b>Opened with a pilot.</b> <see cref="ScreenSwitcher.OpenPilotProfile"/> hands the page a pilot id and
    /// opens it; the Ark's Pilots tab calls that (<see cref="ArkPilotsPage"/>). A window type alone does not say
    /// which pilot, so the switcher never reopens this page after a match (it is left out of the return-to-window
    /// list), and a page opened with no pilot shows the crew's first.</para>
    ///
    /// <para><b>One scene in memory.</b> The scene loads when a pilot is drawn and is let go once the closed window
    /// has faded (<see cref="PilotSceneImage"/>); the menu never holds a scene it is not showing.</para>
    ///
    /// <para>Built by <c>Tools/Build/author_menu_pilot_profile.py</c> into
    /// <c>Resources/Pilots/PilotProfileView</c>; every word is a template on this component.</para>
    /// </summary>
    public class PilotProfileModal : ModalWindowManager
    {
        [Header("Scene")]
        [Tooltip("Which scene each callsign's painted pilot has (Tools/Build/author_pilot_scene_assets.py).")]
        [SerializeField] private PilotSceneCatalogSO scenes;
        [SerializeField] private PilotSceneImage sceneImage;
        [Tooltip("The words. Authored on the left; the page moves it to the side the pilot does not stand on.")]
        [SerializeField] private RectTransform panel;

        [Header("Texts")]
        [SerializeField] private TMP_Text kickerText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text speciesText;
        [SerializeField] private TMP_Text factionText;
        [SerializeField] private TMP_Text homeText;
        [SerializeField] private TMP_Text sightCaptionText;
        [SerializeField] private TMP_Text sightText;
        [SerializeField] private TMP_Text storyCaptionText;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private TMP_Text picturedText;
        [SerializeField] private TMP_Text countText;

        [Header("Stepping through the crew")]
        [SerializeField] private KitButton nextButton;
        [SerializeField] private KitButton previousButton;

        // Every template takes every token: {name} {given} {family} (the person), {callsign} {sight} (their callsign's
        // word and what it lights), {species} {blend} (the human and two animals), {faction} {homeworld} {backstory},
        // {painted} (the pilot in the picture), {index} {count} (where they stand in the crew). A line whose own fact
        // is empty shows nothing, whatever its template says around the token.
        [Header("Words (empty shows nothing)")]
        [Tooltip("Above the name. Its fact is {callsign}.")]
        [SerializeField] private string kickerTemplate;
        [Tooltip("The name. Its fact is {name}.")]
        [SerializeField] private string nameTemplate;
        [Tooltip("Its fact is {species}: the human and two animals.")]
        [SerializeField] private string speciesTemplate;
        [Tooltip("Its fact is {faction}.")]
        [SerializeField] private string factionTemplate;
        [Tooltip("Its fact is {homeworld}.")]
        [SerializeField] private string homeTemplate;
        [Tooltip("The caption over the sight. Shown while the callsign has a {sight}.")]
        [SerializeField] private string sightCaptionTemplate;
        [Tooltip("Its fact is {sight}: what the callsign lights.")]
        [SerializeField] private string sightTemplate;
        [Tooltip("The caption over the story. Shown while the pilot has a {backstory}.")]
        [SerializeField] private string storyCaptionTemplate;
        [Tooltip("Its fact is {backstory}.")]
        [SerializeField] private string storyTemplate;
        [Tooltip("Who the picture shows. Its fact is {painted}: the callsign's painted pilot.")]
        [SerializeField] private string picturedTemplate;
        [Tooltip("Where the pilot stands in the crew: {index} of {count}.")]
        [SerializeField] private string countTemplate;

        static readonly string[] TokenNames =
        {
            "name", "given", "family", "callsign", "sight", "species", "blend", "faction", "homeworld", "backstory",
            "painted", "index", "count",
        };

        /// <summary>Every token a template of this page may name (the page's generator and its tests read it).</summary>
        public static IReadOnlyList<string> Tokens => TokenNames;

        readonly List<PilotRecord> _crew = new();
        readonly string[] _pairs = new string[TokenNames.Length * 2];
        CanvasGroup _group;
        string _pilotId;
        int _index;
        float _panelInset;

        /// <summary>The pilot on the page, or the one it opens on. Empty: the crew's first.</summary>
        public string PilotId => _pilotId ?? string.Empty;

        void Awake()
        {
            TryGetComponent(out _group);
            for (int i = 0; i < TokenNames.Length; i++) _pairs[i * 2] = TokenNames[i];
            if (panel) _panelInset = Mathf.Abs(panel.anchoredPosition.x);
            OnModalOpened += Refresh;
            if (nextButton) nextButton.onClick.AddListener(Next);
            if (previousButton) previousButton.onClick.AddListener(Previous);
        }

        void OnEnable() => AccountInventoryStore.Changed += OnInventoryChanged;

        protected override void OnDisable()
        {
            AccountInventoryStore.Changed -= OnInventoryChanged;
            base.OnDisable();
        }

        void OnDestroy()
        {
            OnModalOpened -= Refresh;
            if (nextButton) nextButton.onClick.RemoveListener(Next);
            if (previousButton) previousButton.onClick.RemoveListener(Previous);
        }

        protected override void Update()
        {
            base.Update();
            // The scene stays through the closing fade and goes once nothing of the window shows.
            if (sceneImage && sceneImage.IsShowing && !IsOpen && (!_group || _group.alpha <= 0.01f))
                sceneImage.Release();
        }

        /// <summary>Which pilot the page shows: now when it is open, else the next time it opens.</summary>
        public void SetPilot(string pilotId)
        {
            _pilotId = pilotId;
            if (IsOpen) Refresh();
        }

        void OnInventoryChanged()
        {
            if (IsOpen) Refresh();
        }

        void Next() => Step(1);
        void Previous() => Step(-1);

        void Step(int by)
        {
            if (!IsOpen || _crew.Count < 2) return;
            _index = ((_index + by) % _crew.Count + _crew.Count) % _crew.Count;
            _pilotId = _crew[_index].Id;
            Draw(_crew[_index]);
        }

        void Refresh()
        {
            PilotLoadoutStore.GetCrew(_crew);
            if (_crew.Count == 0)
            {
                // Nobody to show (the starter crew is granted with the account's inventory): the page has nothing to say.
                if (sceneImage) sceneImage.Release();
                if (IsOpen) ModalWindowOut();
                return;
            }

            _index = Mathf.Max(0, IndexOf(_pilotId));
            _pilotId = _crew[_index].Id;
            Draw(_crew[_index]);
        }

        // A seat or a squad saved before pilots were people holds a bare callsign id: the store resolves either kind
        // to the crew's person, and the person's own id is what the crew is searched for.
        int IndexOf(string pilotId)
        {
            if (!PilotLoadoutStore.TryGetPilot(pilotId, out var person)) return -1;
            for (int i = 0; i < _crew.Count; i++)
                if (_crew[i].Id == person.Id) return i;
            return -1;
        }

        void Draw(PilotRecord person)
        {
            PilotVision callsign = person.Vision();
            var row = PilotSight.Set ? PilotSight.Set.For(callsign) : null;
            PilotSceneCatalogSO.Entry scene = null;
            bool pictured = scenes && scenes.TryGet(callsign, out scene);

            string name = person.DisplayName();
            string word = row?.word ?? string.Empty;
            string sight = row?.meaning ?? string.Empty;
            string species = person.SpeciesCombo();
            // The caption names who is pictured only when that is someone else: a starter IS the painted pilot.
            string painted = pictured && scene.painted != name ? scene.painted : string.Empty;

            int t = 1;
            _pairs[t] = name; t += 2;
            _pairs[t] = person.GivenName; t += 2;
            _pairs[t] = person.FamilyName; t += 2;
            _pairs[t] = word; t += 2;
            _pairs[t] = sight; t += 2;
            _pairs[t] = species; t += 2;
            _pairs[t] = person.SpeciesBlend(); t += 2;
            _pairs[t] = person.FactionName; t += 2;
            _pairs[t] = person.Homeworld; t += 2;
            _pairs[t] = person.Backstory; t += 2;
            _pairs[t] = painted; t += 2;
            _pairs[t] = (_index + 1).ToString(CultureInfo.InvariantCulture); t += 2;
            string count = _crew.Count.ToString(CultureInfo.InvariantCulture);
            _pairs[t] = count;

            Write(kickerText, kickerTemplate, word);
            Write(nameText, nameTemplate, name);
            Write(speciesText, speciesTemplate, species);
            Write(factionText, factionTemplate, person.FactionName);
            Write(homeText, homeTemplate, person.Homeworld);
            Write(sightCaptionText, sightCaptionTemplate, sight);
            Write(sightText, sightTemplate, sight);
            Write(storyCaptionText, storyCaptionTemplate, person.Backstory);
            Write(storyText, storyTemplate, person.Backstory);
            Write(picturedText, picturedTemplate, painted);
            Write(countText, countTemplate, count);

            bool several = _crew.Count > 1;
            if (nextButton) nextButton.interactable = several;
            if (previousButton) previousButton.interactable = several;

            // The words go where the pilot is not: the panel first, so the picture is cut against where the words are.
            PlacePanel(pictured && scene.side == PilotSceneSide.Left);
            if (!sceneImage) return;
            if (pictured) sceneImage.Show(scene.resource, scene.face);
            else sceneImage.Release();
        }

        void Write(TMP_Text target, string template, string fact)
        {
            if (target) target.text = string.IsNullOrEmpty(fact) ? string.Empty : TemplateText.Fill(template, _pairs);
        }

        // The panel is authored against the left edge. Its children are laid out by its rectangle, not its pivot, so
        // moving the pivot with the anchor carries the whole panel to the other edge unchanged.
        void PlacePanel(bool onRight)
        {
            if (!panel) return;
            float x = onRight ? 1f : 0f;
            if (Mathf.Approximately(panel.pivot.x, x)) return;
            panel.anchorMin = new Vector2(x, panel.anchorMin.y);
            panel.anchorMax = new Vector2(x, panel.anchorMax.y);
            panel.pivot = new Vector2(x, panel.pivot.y);
            panel.anchoredPosition = new Vector2(onRight ? -_panelInset : _panelInset, panel.anchoredPosition.y);
        }
    }
}
