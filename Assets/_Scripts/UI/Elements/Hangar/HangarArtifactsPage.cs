using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's ARTIFACTS tab (Docs/Pilots/INVENTORY.md §2, Docs/Pilots/ARTIFACTS.md, built on Shombith's 2026-10-26
    /// tab): the vessel's artifact slot along the top (<see cref="PilotArtifactCatalogSO.SlotsPerVessel"/>), and below it
    /// every artifact the player holds that this vessel can carry. An artifact is a payload transformation: the slotted
    /// one reshapes the payloads this vessel grabs. A press on a row slots that artifact (replacing the one there) or
    /// takes it out (<see cref="PilotLoadoutStore.ToggleSlot"/>). The header above the vessel names the slot
    /// (<see cref="IHangarHeader"/>). A match reads the slot through <c>VesselPilotLoadout</c>.
    ///
    /// <para><b>2026-11-07 (Garrett): every artifact is unique.</b> The rows are no longer the catalog's kinds but the
    /// player's own artifacts (<see cref="PilotLoadoutStore.GetHoard"/>, <see cref="ArtifactRecord"/>), each rolled from
    /// its own seed on top of a kind, so two rows can share a kind and still differ. A row is shown when its kind's
    /// <see cref="ArtifactSO.Requirement"/> is met by the vessel (the kind is found from the artifact's id,
    /// <see cref="PilotArtifactCatalogSO.TryGetArtifact"/>). Each row reads the artifact's name, the ONE element it
    /// accepts and the one thing that element improves (<see cref="ArtifactRecord.Aspect"/>), and its attuned level, a
    /// real number on the 0-10 band since feeding has diminishing returns (<see cref="ArtifactCurve"/>). Feeding happens on
    /// the Ark's ARTIFACTS tab, not here. Since every row is held, the tag now marks the slotted one.</para>
    ///
    /// <para>The page has a fixed number of rows (eight in the prefab); a hoard with more compatible artifacts shows the
    /// first ones in the order they arrived, plus the slotted one, which is always shown in its slot.</para>
    /// </summary>
    public class HangarArtifactsPage : MonoBehaviour, IHangarTabPage, IHangarHeader
    {
        [System.Serializable]
        public class Slot
        {
            public CanvasGroup group;
            public TMP_Text nameText;
            public TMP_Text valueText;
            [Tooltip("Lit while the slot holds an artifact.")]
            public Image fill;
        }

        [System.Serializable]
        public class Row
        {
            public CanvasGroup group;
            public KitButton button;
            public TMP_Text nameText;
            [Tooltip("What the artifact accepts and improves (lineTemplate).")]
            public TMP_Text lineText;
            [Tooltip("The attuned element and level (attunedTemplate).")]
            public TMP_Text valueText;
            [Tooltip("The slotted word while it is slotted on this vessel.")]
            public TMP_Text tagText;
            [Tooltip("Lit while the artifact is slotted on this vessel.")]
            public Image underline;
        }

        [SerializeField] private PilotArtifactCatalogSO catalog;
        [SerializeField] private Slot[] slots = new Slot[4];
        [SerializeField] private Row[] rows = new Row[8];
        [SerializeField] private CanvasGroup emptyGroup;

        [Header("Words")]
        [Tooltip("An empty slot's name.")]
        [SerializeField] private string emptySlotWord = "";
        [Tooltip("An empty slot's line.")]
        [SerializeField] private string emptySlotLine = "";
        [Tooltip("A row's tag while the artifact is slotted on this vessel.")]
        [SerializeField] private string slottedWord = "SLOTTED";
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "Charge", "Mass", "Space", "Time", "Omni" };
        [Tooltip("A row's line; {element}, {aspect}, {kind} and {serial} are filled in.")]
        [SerializeField] private string lineTemplate = "Feed {element} to raise {aspect}";
        [Tooltip("An artifact's attuned level; {element}, {aspect} and {level} (0 to 10) are filled in.")]
        [SerializeField] private string attunedTemplate = "{element} {level}";

        [Header("Header")]
        [Tooltip("The header's kicker; {vessel} and {slots} are filled in. Upper-cased.")]
        [SerializeField] private string headerKickerTemplate = "";
        [SerializeField] private string headerTitle = "";
        [Tooltip("The header's line; {slotted}, {slots} and {held} are filled in.")]
        [SerializeField] private string headerLineTemplate = "";

        /// <summary>The held artifacts this vessel can carry, in hoard order.</summary>
        readonly List<ArtifactRecord> _artifacts = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<string> _slotted = new();
        readonly List<ArtifactRecord> _shown = new();
        string _vesselName;
        SO_Vessel _vessel;
        VesselClassType _vesselClass = VesselClassType.Any;

        public event System.Action HeaderChanged;

        int SlotCount => catalog ? Mathf.Min(catalog.SlotsPerVessel, slots.Length) : 0;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i]?.button) rows[i].button.onClick.AddListener(() => Press(index));
            }
        }

        void OnEnable()
        {
            PilotLoadoutStore.ArtifactsChanged += OnArtifactsChanged;
            PilotLoadoutStore.UpgradesChanged += Refresh;
        }

        void OnDisable()
        {
            PilotLoadoutStore.ArtifactsChanged -= OnArtifactsChanged;
            PilotLoadoutStore.UpgradesChanged -= Refresh;
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _vessel = vessel;
            _vesselName = vessel ? vessel.CardName : string.Empty;
            _vesselClass = vessel ? vessel.Class : VesselClassType.Any;
            Refresh();
        }

        public void SetActivePage(bool active) { }

        public bool TryGetHeader(out string kicker, out string title, out string line)
        {
            kicker = title = line = string.Empty;
            if (!catalog || _vesselClass == VesselClassType.Any) return false;
            string n = SlotCount.ToString(CultureInfo.InvariantCulture);
            kicker = TemplateText.Fill(headerKickerTemplate, "vessel", _vesselName, "slots", n).ToUpperInvariant();
            title = headerTitle;
            line = TemplateText.Fill(headerLineTemplate, "slotted", _shown.Count.ToString(CultureInfo.InvariantCulture),
                "slots", n, "held", _artifacts.Count.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        void Press(int index)
        {
            if (index < 0 || index >= _artifacts.Count || _vesselClass == VesselClassType.Any) return;
            PilotLoadoutStore.ToggleSlot(_vesselClass, _artifacts[index].Id, SlotCount);
        }

        void OnArtifactsChanged(VesselClassType vessel)
        {
            if (vessel == VesselClassType.Any || vessel == _vesselClass) Refresh();
        }

        /// <summary>The held artifacts whose kind this vessel can carry, into <see cref="_artifacts"/>.</summary>
        void CollectCompatible()
        {
            _artifacts.Clear();
            if (!catalog || !_vessel) return;
            PilotLoadoutStore.GetHoard(_hoard);
            foreach (var a in _hoard)
                if (Fits(a)) _artifacts.Add(a);
        }

        bool Fits(ArtifactRecord a) =>
            a != null && catalog && catalog.TryGetArtifact(a.Kind(), out var kind)
            && kind.Requirement != null && kind.Requirement.IsMetBy(_vesselClass, _vessel ? _vessel.Keywords : null);

        void Refresh()
        {
            CollectCompatible();
            _slotted.Clear();
            if (_vesselClass != VesselClassType.Any) PilotLoadoutStore.GetSlotted(_vesselClass, _slotted);
            // A slotted id no longer held, one this vessel cannot carry, or one beyond the slot count is not shown (and
            // not lost).
            _shown.Clear();
            foreach (var id in _slotted)
                if (PilotLoadoutStore.TryGetArtifact(id, out var a) && Fits(a) && _shown.Count < SlotCount) _shown.Add(a);

            var selection = UITheme.Resolve(null, UIColorToken.Selection);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                bool used = i < SlotCount;
                KitVisibility.SetShown(s.group, used);
                if (!used) continue;
                var a = i < _shown.Count ? _shown[i] : null;
                if (s.nameText) s.nameText.text = a != null ? DisplayName(a) : emptySlotWord;
                if (s.valueText) s.valueText.text = a != null ? AttunedLine(a) : emptySlotLine;
                if (s.fill) s.fill.color = a != null ? selection : Color.clear;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                if (r == null) continue;
                bool used = i < _artifacts.Count;
                KitVisibility.SetShown(r.group, used);
                if (!used) continue;
                var a = _artifacts[i];
                bool slotted = IsShown(a);
                if (r.nameText) r.nameText.text = DisplayName(a);
                if (r.lineText) r.lineText.text = Line(a);
                if (r.valueText) r.valueText.text = AttunedLine(a);
                if (r.tagText) r.tagText.text = slotted ? slottedWord : string.Empty;
                if (r.underline) r.underline.color = slotted ? selection : Color.clear;
                if (r.group) r.group.alpha = 1f;
            }
            KitVisibility.SetShown(emptyGroup, _artifacts.Count == 0);
            HeaderChanged?.Invoke();
        }

        bool IsShown(ArtifactRecord a)
        {
            for (int i = 0; i < _shown.Count; i++)
                if (_shown[i].Id == a.Id) return true;
            return false;
        }

        string DisplayName(ArtifactRecord a)
        {
            if (!string.IsNullOrEmpty(a.Name)) return a.Name;
            return catalog && catalog.TryGetArtifact(a.Kind(), out var kind) ? kind.DisplayName : a.Serial();
        }

        string Line(ArtifactRecord a)
        {
            string kindName = catalog && catalog.TryGetArtifact(a.Kind(), out var kind) ? kind.DisplayName : string.Empty;
            return TemplateText.Fill(lineTemplate, "element", ElementName(a.Element), "aspect", a.Aspect ?? string.Empty,
                "kind", kindName, "serial", a.Serial());
        }

        string AttunedLine(ArtifactRecord a) =>
            TemplateText.Fill(attunedTemplate, "element", ElementName(a.Element), "aspect", a.Aspect ?? string.Empty,
                "level", a.AttunedLevel().ToString("0.00", CultureInfo.InvariantCulture));

        string ElementName(Element element)
        {
            int i = (int)element;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : string.Empty;
        }
    }
}
