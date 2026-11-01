using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's ARTIFACTS tab (Docs/Pilots/ARTIFACTS.md, built on Shombith's 2026-10-26 tab): the vessel's artifact
    /// slot along the top (<see cref="PilotArtifactCatalogSO.SlotsPerVessel"/>), and every artifact below
    /// (<see cref="PilotArtifactCatalogSO.GetArtifactsFor(SO_Vessel, List{ArtifactSO})"/>). An artifact is a payload
    /// transformation: the slotted one reshapes the payloads this vessel grabs. Each row shows whether the player holds it
    /// and their four track levels (<see cref="PilotLoadoutStore.Levels"/>); a press on a held one slots it (replacing
    /// the one there) or takes it out (<see cref="PilotLoadoutStore"/>). The header above the vessel names the slot
    /// (<see cref="IHangarHeader"/>). A match reads the slot through <c>VesselPilotLoadout</c>.
    ///
    /// <para>2026-10-27 (Garrett): the found ranks and the hull / fleet tags went with the numeric artifacts. Every
    /// artifact is still free to hold (Shombith, 2026-10-26).</para>
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
            [Tooltip("What the artifact does to a payload.")]
            public TMP_Text lineText;
            [Tooltip("The player's four track levels (levelsTemplate), or the not-held word.")]
            public TMP_Text valueText;
            [Tooltip("Held or not.")]
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
        [Tooltip("A row's tag while the player holds the artifact.")]
        [SerializeField] private string heldWord = "";
        [Tooltip("A row's value while the player does not hold the artifact.")]
        [SerializeField] private string notHeldWord = "";
        [Tooltip("The four track levels; {mass}, {space}, {time} and {charge} are filled in (0 to 10).")]
        [SerializeField] private string levelsTemplate = "";

        [Header("Header")]
        [Tooltip("The header's kicker; {vessel} and {slots} are filled in. Upper-cased.")]
        [SerializeField] private string headerKickerTemplate = "";
        [SerializeField] private string headerTitle = "";
        [Tooltip("The header's line; {slotted}, {slots} and {held} are filled in.")]
        [SerializeField] private string headerLineTemplate = "";

        readonly List<ArtifactSO> _artifacts = new();
        readonly List<string> _slotted = new();
        readonly List<ArtifactSO> _shown = new();
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
            if (catalog) catalog.GetArtifactsFor(vessel, _artifacts);
            else _artifacts.Clear();
            Refresh();
        }

        public void SetActivePage(bool active) { }

        public bool TryGetHeader(out string kicker, out string title, out string line)
        {
            kicker = title = line = string.Empty;
            if (!catalog || _vesselClass == VesselClassType.Any) return false;
            int held = 0;
            foreach (var a in _artifacts)
                if (PilotLoadoutStore.Holds(a.Id)) held++;
            string n = SlotCount.ToString();
            kicker = TemplateText.Fill(headerKickerTemplate, "vessel", _vesselName, "slots", n).ToUpperInvariant();
            title = headerTitle;
            line = TemplateText.Fill(headerLineTemplate, "slotted", _slotted.Count.ToString(), "slots", n, "held", held.ToString());
            return true;
        }

        void Press(int index)
        {
            if (index < 0 || index >= _artifacts.Count || _vesselClass == VesselClassType.Any) return;
            var a = _artifacts[index];
            if (!PilotLoadoutStore.Holds(a.Id)) return;                   // not held: nothing to slot
            PilotLoadoutStore.ToggleSlot(_vesselClass, a.Id, SlotCount);
        }

        void OnArtifactsChanged(VesselClassType vessel)
        {
            if (vessel == VesselClassType.Any || vessel == _vesselClass) Refresh();
        }

        void Refresh()
        {
            _slotted.Clear();
            if (_vesselClass != VesselClassType.Any) PilotLoadoutStore.GetSlotted(_vesselClass, _slotted);
            // A slotted id the catalog no longer has, or one beyond the slot count, is not shown (and not lost).
            var shown = _shown;
            shown.Clear();
            foreach (var id in _slotted)
            {
                if (!catalog || !catalog.TryGetArtifact(id, out var a)) continue;
                if (_artifacts.Contains(a) && shown.Count < SlotCount) shown.Add(a);
            }

            var selection = UITheme.Resolve(null, UIColorToken.Selection);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                bool used = i < SlotCount;
                KitVisibility.SetShown(s.group, used);
                if (!used) continue;
                var a = i < shown.Count ? shown[i] : null;
                if (s.nameText) s.nameText.text = a ? a.DisplayName : emptySlotWord;
                if (s.valueText) s.valueText.text = a ? LevelsLine(a) : emptySlotLine;
                if (s.fill) s.fill.color = a ? selection : Color.clear;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                if (r == null) continue;
                bool used = i < _artifacts.Count;
                KitVisibility.SetShown(r.group, used);
                if (!used) continue;
                var a = _artifacts[i];
                bool held = PilotLoadoutStore.Holds(a.Id);
                if (r.nameText) r.nameText.text = a.DisplayName;
                if (r.lineText) r.lineText.text = a.Description;
                if (r.valueText) r.valueText.text = held ? LevelsLine(a) : notHeldWord;
                if (r.tagText) r.tagText.text = held ? heldWord : string.Empty;
                if (r.underline) r.underline.color = shown.Contains(a) ? selection : Color.clear;
                if (r.group) r.group.alpha = held ? 1f : 0.55f;
            }
            KitVisibility.SetShown(emptyGroup, _artifacts.Count == 0);
            HeaderChanged?.Invoke();
        }

        string LevelsLine(ArtifactSO a)
        {
            var l = PilotLoadoutStore.Levels(a.Id);
            return TemplateText.Fill(levelsTemplate, "mass", l.Mass.ToString(), "space", l.Space.ToString(),
                "time", l.Time.ToString(), "charge", l.Charge.ToString());
        }
    }
}
