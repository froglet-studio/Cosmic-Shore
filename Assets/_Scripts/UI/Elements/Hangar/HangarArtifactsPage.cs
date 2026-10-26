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
    /// The Hangar's ARTIFACTS tab (Docs/Pilots/PILOTS_AND_ARTIFACTS.md, built on Docs/PILOTS_AND_ARTIFACTS.md §10): the vessel's
    /// artifact slots along the top (<see cref="PilotArtifactCatalogSO.SlotsPerVessel"/>), and every artifact it can carry
    /// below (<see cref="PilotArtifactCatalogSO.GetArtifactsFor(SO_Vessel, List{ArtifactSO})"/>) - its own two first, then the
    /// fleet's five. An artifact moves one number of the vessel's
    /// kit in three ranks. One the player has found shows its rank and value, and a press slots it or takes it out
    /// (<see cref="PilotLoadoutStore"/>); one not found yet reads "Undiscovered" and does nothing. The header above the
    /// vessel names the slots (<see cref="IHangarHeader"/>). Replaces the single "Undiscovered" slot the tab drew before.
    ///
    /// <para>Finding an artifact is not built yet (the HyperSea drops are an open decision), so on a fresh profile every row
    /// is undiscovered: FrogletTools > Pilots > Grant Every Artifact gives them all at a random rank for testing. No match
    /// reads a slot yet.</para>
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
            [Tooltip("What the artifact changes.")]
            public TMP_Text lineText;
            [Tooltip("The found rank's value, or the undiscovered word.")]
            public TMP_Text valueText;
            [Tooltip("Hull or fleet, and the rank.")]
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
        [SerializeField] private string emptySlotWord = "Empty";
        [Tooltip("An empty slot's line.")]
        [SerializeField] private string emptySlotLine = "Press a found artifact";
        [Tooltip("A row's value while the artifact is not found yet.")]
        [SerializeField] private string undiscoveredWord = "Undiscovered";
        [Tooltip("A hull artifact's tag; {rank} is filled in (empty when not found).")]
        [SerializeField] private string hullTagTemplate = "HULL {rank}";
        [Tooltip("A fleet artifact's tag; {rank} is filled in.")]
        [SerializeField] private string fleetTagTemplate = "FLEET {rank}";
        [SerializeField] private string[] rankWords = { "", "RANK I", "RANK II", "RANK III" };

        [Header("Header")]
        [Tooltip("The header's kicker; {vessel} and {slots} are filled in. Upper-cased.")]
        [SerializeField] private string headerKickerTemplate = "{vessel} · {slots} SLOTS";
        [SerializeField] private string headerTitle = "ARTIFACTS";
        [Tooltip("The header's line; {slotted}, {slots} and {found} are filled in.")]
        [SerializeField] private string headerLineTemplate = "{slotted} of {slots} slotted. {found} found. Each one moves one number of the kit.";

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

        void OnEnable() => PilotLoadoutStore.ArtifactsChanged += OnArtifactsChanged;
        void OnDisable() => PilotLoadoutStore.ArtifactsChanged -= OnArtifactsChanged;

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
            int found = 0;
            foreach (var a in _artifacts)
                if (PilotLoadoutStore.FoundRank(a.Id) > 0) found++;
            string n = SlotCount.ToString();
            kicker = TemplateText.Fill(headerKickerTemplate, "vessel", _vesselName, "slots", n).ToUpperInvariant();
            title = headerTitle;
            line = TemplateText.Fill(headerLineTemplate, "slotted", _slotted.Count.ToString(), "slots", n, "found", found.ToString());
            return true;
        }

        void Press(int index)
        {
            if (index < 0 || index >= _artifacts.Count || _vesselClass == VesselClassType.Any) return;
            var a = _artifacts[index];
            if (PilotLoadoutStore.FoundRank(a.Id) == 0) return;           // not found yet: nothing to slot
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
                int rank = a ? PilotLoadoutStore.FoundRank(a.Id) : 0;
                if (s.nameText) s.nameText.text = a ? a.DisplayName : emptySlotWord;
                if (s.valueText) s.valueText.text = a ? a.RankLabel(rank) : emptySlotLine;
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
                int rank = PilotLoadoutStore.FoundRank(a.Id);
                string rankWord = rank >= 0 && rank < rankWords.Length ? rankWords[rank] : string.Empty;
                if (r.nameText) r.nameText.text = a.DisplayName;
                if (r.lineText) r.lineText.text = a.Description;
                if (r.valueText) r.valueText.text = rank > 0 ? a.RankLabel(rank) : undiscoveredWord;
                if (r.tagText) r.tagText.text = TemplateText.Fill(a.IsFleet ? fleetTagTemplate : hullTagTemplate, "rank", rankWord).Trim();
                if (r.underline) r.underline.color = shown.Contains(a) ? selection : Color.clear;
                if (r.group) r.group.alpha = rank > 0 ? 1f : 0.55f;
            }
            KitVisibility.SetShown(emptyGroup, _artifacts.Count == 0);
            HeaderChanged?.Invoke();
        }
    }
}
