using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's PILOTS tab (Docs/Pilots/CALLSIGNS.md, built on Shombith's 2026-10-26 tab). The rows are the people in
    /// the player's crew who can fly this vessel (<see cref="PilotLoadoutStore.GetCrew"/>, Docs/Pilots/INVENTORY.md §3):
    /// each is a unique person - a name, a human and two animals, a faction, a backstory - layered on ONE callsign, and
    /// the callsign is what the vessel cares about. A person is offered when their callsign's <see cref="PilotSO"/>
    /// (<see cref="PilotArtifactCatalogSO.TryGetPilot"/> strips the person's seed from the id) can fly the hull, the rule
    /// <see cref="PilotArtifactCatalogSO.GetPilotsFor(SO_Vessel, List{PilotSO})"/> applies to callsigns. The row shows the
    /// person's name and the callsign's portrait; its line names the callsign and what holding LB lights
    /// (<see cref="CallsignSetSO"/>). Pressing a person seats THAT PERSON on the vessel (<see cref="PilotLoadoutStore.SetPilot"/>
    /// writes their <see cref="PilotRecord.Id"/>). Moving the pad onto a row shows the person in the header above the
    /// vessel (<see cref="IHangarHeader"/>). A match reads the seat through <c>VesselPilotLoadout</c>.
    ///
    /// <para>2026-10-27 (Garrett): the ability each pilot used to rewrite, and the default "base kit" pilot, are gone.</para>
    /// <para>2026-11-07 (Garrett): pilots are people, not the eighteen callsign assets; a crew holds at most one person
    /// per callsign, so the rows are never more than the callsigns the hull can fly. A seat saved before then holds a bare
    /// callsign id, which <see cref="PilotLoadoutStore.TryGetPilot"/> resolves to the crew's person on that callsign, so
    /// the comparison that lights the seated row goes through the resolved person, never the raw id.</para>
    /// </summary>
    public class HangarPilotsPage : MonoBehaviour, IHangarTabPage, IHangarHeader
    {
        [System.Serializable]
        public class Card
        {
            public CanvasGroup group;
            public KitButton button;
            public Image portrait;
            public TMP_Text nameText;
            [Tooltip("The pilot's callsign and what it lights (lineTemplate).")]
            public TMP_Text lineText;
            [Tooltip("In the seat, stand-in art, or art to come.")]
            public TMP_Text stateText;
            [Tooltip("Lit while this pilot is in the seat.")]
            public Image underline;
        }

        [SerializeField] private PilotArtifactCatalogSO catalog;
        [SerializeField] private CanvasGroup emptyGroup;

        [Header("Cards")]
        [SerializeField] private Card[] cards = new Card[4];

        [Header("Words")]
        [Tooltip("A row's line; {callsign} and {sight} are filled in from the callsign set, {species} and {faction} from the person.")]
        [SerializeField] private string lineTemplate = "";
        [Tooltip("A row's state while the pilot is in the seat.")]
        [SerializeField] private string seatedWord = "";
        [Tooltip("A row's state while its portrait is a spare standing in.")]
        [SerializeField] private string standInWord = "";
        [Tooltip("A row's state while the pilot has no portrait yet.")]
        [SerializeField] private string noArtWord = "";

        [Header("Header")]
        [Tooltip("The header's kicker while PILOTS is shown; {vessel} and {callsign} are filled in. Upper-cased.")]
        [SerializeField] private string headerKickerTemplate = "";
        [Tooltip("The header's line; {sight}, {description} and {ai} (the callsign's), {species}, {faction}, {homeworld} and {backstory} (the person's) are filled in.")]
        [SerializeField] private string headerLineTemplate = "";

        // The crew read fresh on each SetVessel, and the offered people with their callsigns, index for index.
        readonly List<PilotRecord> _crew = new();
        readonly List<PilotRecord> _pilots = new();
        readonly List<PilotSO> _callsigns = new();
        SO_Vessel _vessel;
        HangarVesselStage _stage;
        string _vesselName;
        VesselClassType _vesselClass = VesselClassType.Any;
        int _selected;
        bool _active;
        GameObject _lastSelected;

        public event System.Action HeaderChanged;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i]?.button) cards[i].button.onClick.AddListener(() => Seat(index));
            }
        }

        void OnEnable() => PilotLoadoutStore.PilotChanged += OnPilotChanged;
        void OnDisable() => PilotLoadoutStore.PilotChanged -= OnPilotChanged;

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _stage = stage;
            _vesselName = vessel ? vessel.CardName : string.Empty;
            _vesselClass = vessel ? vessel.Class : VesselClassType.Any;
            OfferedPilots(vessel);

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                bool used = i < _pilots.Count;
                KitVisibility.SetShown(card.group, used);
                if (!used) continue;
                var person = _pilots[i];
                var p = _callsigns[i];
                if (card.portrait) { card.portrait.sprite = p.Portrait; card.portrait.enabled = p.Portrait; }
                if (card.nameText) card.nameText.text = person.DisplayName();
                if (card.lineText)
                    card.lineText.text = TemplateText.Fill(lineTemplate, "callsign", CallsignOf(p), "sight", SightOf(p),
                        "species", person.SpeciesCombo(), "faction", person.FactionName);
            }
            KitVisibility.SetShown(emptyGroup, _pilots.Count == 0);
            RefreshStates();
            Select(0);
        }

        public void SetActivePage(bool active) => _active = active;

        public bool TryGetHeader(out string kicker, out string title, out string line)
        {
            kicker = title = line = string.Empty;
            if (_pilots.Count == 0) return false;
            var person = _pilots[_selected];
            var p = _callsigns[_selected];
            kicker = TemplateText.Fill(headerKickerTemplate, "vessel", _vesselName, "callsign", CallsignOf(p)).ToUpperInvariant();
            title = person.DisplayName().ToUpperInvariant();
            line = TemplateText.Fill(headerLineTemplate, "sight", SightOf(p), "description", p.Description, "ai", p.AIBehaviorDescription,
                "species", person.SpeciesCombo(), "faction", person.FactionName, "homeworld", person.Homeworld,
                "backstory", person.Backstory);
            return true;
        }

        void Update()
        {
            // The pad moving onto a pilot shows it in the header: one reference compare a frame while the tab is shown.
            if (!_active || !EventSystem.current) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            for (int i = 0; i < cards.Length && i < _pilots.Count; i++)
                if (cards[i]?.button && cards[i].button.gameObject == selected && i != _selected) Select(i);
        }

        void Select(int index)
        {
            if (_pilots.Count == 0) return;
            _selected = Mathf.Clamp(index, 0, _pilots.Count - 1);
            HeaderChanged?.Invoke();
        }

        /// <summary>A press: the person takes the seat (their unique id is saved). The seat is never emptied: every vessel
        /// needs a pilot.</summary>
        void Seat(int index)
        {
            if (index < 0 || index >= _pilots.Count || _vesselClass == VesselClassType.Any) return;
            Select(index);
            PilotLoadoutStore.SetPilot(_vesselClass, _pilots[index].Id);
        }

        /// <summary>
        /// The crew's people this hull can fly, in the order they joined the crew, with their callsign assets: a person
        /// whose callsign the catalog no longer has, or whose callsign cannot fly the hull, is left out. Reuses the page's
        /// lists (no allocation per vessel change).
        /// </summary>
        void OfferedPilots(SO_Vessel vessel)
        {
            _pilots.Clear();
            _callsigns.Clear();
            if (!catalog || !vessel) return;
            PilotLoadoutStore.GetCrew(_crew);
            foreach (var person in _crew)
            {
                if (!catalog.TryGetPilot(person.Id, out var callsign) || !callsign) continue;
                if (callsign.Requirement == null || !callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords)) continue;
                _pilots.Add(person);
                _callsigns.Add(callsign);
            }
        }

        static string CallsignOf(PilotSO p) => PilotSight.Set.For(p.Vision)?.word ?? string.Empty;
        static string SightOf(PilotSO p) => PilotSight.Set.For(p.Vision)?.meaning ?? string.Empty;

        void OnPilotChanged(VesselClassType vessel, string pilotId)
        {
            // Any: the crew itself changed (a recruit), so the rows are read again.
            if (vessel == VesselClassType.Any && _vessel) SetVessel(_vessel, _stage);
            else if (vessel == _vesselClass) RefreshStates();
        }

        void RefreshStates()
        {
            string seated = _vesselClass == VesselClassType.Any ? string.Empty : PilotLoadoutStore.GetPilot(_vesselClass);
            // The seat may hold a legacy callsign id (saved before pilots were people): compare the person it resolves to.
            seated = PilotLoadoutStore.TryGetPilot(seated, out var seatedPerson) ? seatedPerson.Id : string.Empty;
            // An unset seat, or a person who cannot fly this hull, flies the hull's first offered pilot, as the match
            // reads it (VesselPilotLoadout).
            bool seatOffered = false;
            foreach (var p in _pilots)
                if (p.Id == seated) seatOffered = true;
            if (!seatOffered) seated = _pilots.Count > 0 ? _pilots[0].Id : string.Empty;
            var selection = UITheme.Resolve(null, UIColorToken.Selection);
            for (int i = 0; i < cards.Length && i < _pilots.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                var p = _callsigns[i];
                bool inSeat = seated.Length > 0 && _pilots[i].Id == seated;
                if (card.underline) card.underline.color = inSeat ? selection : Color.clear;
                if (card.stateText)
                    card.stateText.text = inSeat ? seatedWord
                        : !p.Portrait ? noArtWord : p.PortraitIsStandIn ? standInWord : string.Empty;
            }
        }
    }
}
