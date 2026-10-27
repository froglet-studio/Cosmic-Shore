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
    /// The Hangar's PILOTS tab (Docs/Pilots/CALLSIGNS.md, built on Shombith's 2026-10-26 tab). The rows are the vessel's
    /// pilots from <see cref="PilotArtifactCatalogSO.GetPilotsFor(SO_Vessel, List{PilotSO})"/>, each a pair of eyes: its
    /// line names the callsign it carries and what holding LB lights (<see cref="CallsignSetSO"/>). Pressing a pilot
    /// seats it on the vessel (<see cref="PilotLoadoutStore"/>); pressing the seated pilot again empties the seat, and an
    /// empty seat has no sight. Moving the pad onto a row shows that pilot in the header above the vessel
    /// (<see cref="IHangarHeader"/>). A match reads the seat through <c>VesselPilotLoadout</c>.
    ///
    /// <para>2026-10-27 (Garrett): the ability each pilot used to rewrite, and the default "base kit" pilot, are gone.</para>
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
        [Tooltip("A row's line; {callsign} and {sight} are filled in from the callsign set.")]
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
        [Tooltip("The header's line; {sight}, {description} and {ai} are filled in.")]
        [SerializeField] private string headerLineTemplate = "";

        readonly List<PilotSO> _pilots = new();
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
            _vesselName = vessel ? vessel.CardName : string.Empty;
            _vesselClass = vessel ? vessel.Class : VesselClassType.Any;
            if (catalog) catalog.GetPilotsFor(vessel, _pilots);
            else _pilots.Clear();

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                bool used = i < _pilots.Count;
                KitVisibility.SetShown(card.group, used);
                if (!used) continue;
                var p = _pilots[i];
                if (card.portrait) { card.portrait.sprite = p.Portrait; card.portrait.enabled = p.Portrait; }
                if (card.nameText) card.nameText.text = p.DisplayName;
                if (card.lineText) card.lineText.text = TemplateText.Fill(lineTemplate, "callsign", CallsignOf(p), "sight", SightOf(p));
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
            var p = _pilots[_selected];
            kicker = TemplateText.Fill(headerKickerTemplate, "vessel", _vesselName, "callsign", CallsignOf(p)).ToUpperInvariant();
            title = p.DisplayName.ToUpperInvariant();
            line = TemplateText.Fill(headerLineTemplate, "sight", SightOf(p), "description", p.Description, "ai", p.AIBehaviorDescription);
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

        /// <summary>A press: the pilot takes the seat, or leaves it if it already sits there.</summary>
        void Seat(int index)
        {
            if (index < 0 || index >= _pilots.Count || _vesselClass == VesselClassType.Any) return;
            Select(index);
            var p = _pilots[index];
            bool seated = PilotLoadoutStore.GetPilot(_vesselClass) == p.Id;
            PilotLoadoutStore.SetPilot(_vesselClass, seated ? string.Empty : p.Id);
        }

        static string CallsignOf(PilotSO p) => PilotSight.Set.For(p.Vision)?.word ?? string.Empty;
        static string SightOf(PilotSO p) => PilotSight.Set.For(p.Vision)?.meaning ?? string.Empty;

        void OnPilotChanged(VesselClassType vessel, string pilotId)
        {
            if (vessel == _vesselClass) RefreshStates();
        }

        void RefreshStates()
        {
            string seated = _vesselClass == VesselClassType.Any ? string.Empty : PilotLoadoutStore.GetPilot(_vesselClass);
            // A saved id the catalog no longer offers this hull is an empty seat, as the match will read it (ResolvePilot).
            bool seatOffered = false;
            foreach (var p in _pilots)
                if (p.Id == seated) seatOffered = true;
            if (!seatOffered) seated = string.Empty;
            var selection = UITheme.Resolve(null, UIColorToken.Selection);
            for (int i = 0; i < cards.Length && i < _pilots.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                var p = _pilots[i];
                bool inSeat = seated.Length > 0 && p.Id == seated;
                if (card.underline) card.underline.color = inSeat ? selection : Color.clear;
                if (card.stateText)
                    card.stateText.text = inSeat ? seatedWord
                        : !p.Portrait ? noArtWord : p.PortraitIsStandIn ? standInWord : string.Empty;
            }
        }
    }
}
