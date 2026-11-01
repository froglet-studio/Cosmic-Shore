using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Ark's ARTIFACTS tab (Docs/Pilots/ARTIFACTS.md §4, Garrett 2026-10-27: "upgrade is done by spending elemental
    /// crystals on it"): the player's artifacts down the left, and for the picked one its four element tracks, each 0 to
    /// 10 with a twist at 5. RAISE pays the next level in that track's own crystals; OMNI pays it in omni crystals
    /// instead (the player's pick, never automatic). The crystals are the profile's
    /// (<see cref="PilotLoadoutStore.Crystals"/>); the track levels are a device stand-in until the profile holds them
    /// (the backend, Shombith). Every word is authored on this component; code fills the numbers.
    /// </summary>
    public class ArkArtifactsPage : MonoBehaviour, IArkTabPage
    {
        [System.Serializable]
        public class TrackRow
        {
            public TMP_Text elementText;
            [Tooltip("What the track moves (ArtifactTrack.label).")]
            public TMP_Text labelText;
            public TMP_Text levelText;
            [Tooltip("The value now, and at the next level.")]
            public TMP_Text valueText;
            [Tooltip("The level-5 twist: dim until it is reached.")]
            public TMP_Text twistText;
            public CanvasGroup twistGroup;
            public KitButton raiseButton;
            public TMP_Text raiseLabel;
            public KitButton omniButton;
            public TMP_Text omniLabel;
        }

        /// <summary>The track rows' order: the four elements as the converter lists them.</summary>
        static readonly Element[] TrackOrder = { Element.Mass, Element.Space, Element.Time, Element.Charge };
        /// <summary>The crystal strip's order: the four elements, then Omni.</summary>
        static readonly Element[] CrystalOrder = { Element.Mass, Element.Space, Element.Time, Element.Charge, Element.Omni };

        [SerializeField] private PilotArtifactCatalogSO catalog;

        [Header("The list")]
        [SerializeField] private SquadRowView[] rows = new SquadRowView[4];
        [SerializeField] private CanvasGroup emptyGroup;

        [Header("The picked artifact")]
        [SerializeField] private CanvasGroup detailGroup;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TrackRow[] tracks = new TrackRow[4];
        [SerializeField] private TMP_Text statusText;

        [Header("Crystals")]
        [Tooltip("Mass, Space, Time, Charge, Omni.")]
        [SerializeField] private TMP_Text[] crystalTexts = new TMP_Text[5];

        [Header("Words")]
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "", "", "", "", "" };
        [Tooltip("A crystal balance; {element} and {count} are filled in.")]
        [SerializeField] private string crystalTemplate = "";
        [Tooltip("A list row's line; {mass}, {space}, {time} and {charge} are the levels.")]
        [SerializeField] private string levelsTemplate = "";
        [Tooltip("A track's level; {level} and {max} are filled in.")]
        [SerializeField] private string levelTemplate = "";
        [Tooltip("A track's value below the top; {now} and {next} are filled in.")]
        [SerializeField] private string valueTemplate = "";
        [Tooltip("A track's value at the top; {now} is filled in.")]
        [SerializeField] private string valueTopTemplate = "";
        [Tooltip("The twist before level 5; {twist} and {at} are filled in.")]
        [SerializeField] private string twistLockedTemplate = "";
        [Tooltip("The twist once reached; {twist} is filled in.")]
        [SerializeField] private string twistOpenTemplate = "";
        [Tooltip("RAISE's word; {cost} and {element} are filled in.")]
        [SerializeField] private string raiseTemplate = "";
        [Tooltip("OMNI's word; {cost} is filled in.")]
        [SerializeField] private string omniTemplate = "";
        [Tooltip("RAISE's word at the top.")]
        [SerializeField] private string topWord = "";
        [Tooltip("A raise was refused for crystals; {element} is the crystal that pays.")]
        [SerializeField] private string notEnoughTemplate = "";
        [Tooltip("A raise landed; {artifact}, {element} and {level} are filled in.")]
        [SerializeField] private string raisedTemplate = "";
        [Tooltip("A raise reached the twist; {artifact} and {twist} are filled in.")]
        [SerializeField] private string twistedTemplate = "";

        readonly List<ArtifactSO> _artifacts = new();
        int _picked;
        bool _shown;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i] && rows[i].Button) rows[i].Button.onClick.AddListener(() => Pick(index));
            }
            for (int i = 0; i < tracks.Length && i < TrackOrder.Length; i++)
            {
                var element = TrackOrder[i];
                if (tracks[i]?.raiseButton) tracks[i].raiseButton.onClick.AddListener(() => Raise(element, false));
                if (tracks[i]?.omniButton) tracks[i].omniButton.onClick.AddListener(() => Raise(element, true));
            }
        }

        void OnEnable()
        {
            PilotLoadoutStore.UpgradesChanged += Refresh;
            PilotLoadoutStore.ArtifactsChanged += OnArtifactsChanged;
            PlayerDataService.OnCrystalBalanceChanged += OnBalance;
        }

        void OnDisable()
        {
            PilotLoadoutStore.UpgradesChanged -= Refresh;
            PilotLoadoutStore.ArtifactsChanged -= OnArtifactsChanged;
            PlayerDataService.OnCrystalBalanceChanged -= OnBalance;
        }

        public void SetPageShown(bool shown)
        {
            _shown = shown;
            if (!shown) return;
            if (statusText) statusText.text = string.Empty;
            Refresh();
        }

        void OnArtifactsChanged(VesselClassType _) => Refresh();
        void OnBalance(int _) => Refresh();

        void Pick(int index)
        {
            if (index < 0 || index >= _artifacts.Count) return;
            _picked = index;
            if (statusText) statusText.text = string.Empty;
            Refresh();
        }

        void Raise(Element element, bool payWithOmni)
        {
            if (_picked < 0 || _picked >= _artifacts.Count) return;
            var artifact = _artifacts[_picked];
            var result = PilotLoadoutStore.TryRaise(artifact, element, payWithOmni);
            if (!statusText) return;
            int level = PilotLoadoutStore.TrackLevel(artifact.Id, element);
            statusText.text = result switch
            {
                PilotLoadoutStore.RaiseResult.Raised when level == ArtifactTrack.TwistLevel =>
                    TemplateText.Fill(twistedTemplate, "artifact", artifact.DisplayName, "twist", artifact.Track(element)?.twistLine ?? string.Empty),
                PilotLoadoutStore.RaiseResult.Raised =>
                    TemplateText.Fill(raisedTemplate, "artifact", artifact.DisplayName, "element", ElementName(element), "level", level.ToString()),
                PilotLoadoutStore.RaiseResult.NotEnoughCrystals =>
                    TemplateText.Fill(notEnoughTemplate, "element", ElementName(payWithOmni ? Element.Omni : element)),
                _ => string.Empty,
            };
        }

        void Refresh()
        {
            if (!_shown) return;
            _artifacts.Clear();
            if (catalog)
                foreach (var a in catalog.Artifacts)
                    if (a && PilotLoadoutStore.Holds(a.Id)) _artifacts.Add(a);
            _picked = Mathf.Clamp(_picked, 0, Mathf.Max(0, _artifacts.Count - 1));

            for (int i = 0; i < crystalTexts.Length && i < CrystalOrder.Length; i++)
                if (crystalTexts[i])
                    crystalTexts[i].text = TemplateText.Fill(crystalTemplate, "element", ElementName(CrystalOrder[i]),
                        "count", PilotLoadoutStore.Crystals(CrystalOrder[i]).ToString());

            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i]) continue;
                bool used = i < _artifacts.Count;
                if (!used) { rows[i].Draw(false, null, null, false); continue; }
                var l = PilotLoadoutStore.Levels(_artifacts[i].Id);
                rows[i].Draw(true, _artifacts[i].DisplayName,
                    TemplateText.Fill(levelsTemplate, "mass", l.Mass.ToString(), "space", l.Space.ToString(),
                        "time", l.Time.ToString(), "charge", l.Charge.ToString()), i == _picked);
            }
            KitVisibility.SetShown(emptyGroup, _artifacts.Count == 0);
            KitVisibility.SetShown(detailGroup, _artifacts.Count > 0);
            if (_artifacts.Count > 0) DrawDetail(_artifacts[_picked]);
        }

        void DrawDetail(ArtifactSO artifact)
        {
            if (nameText) nameText.text = artifact.DisplayName;
            if (descriptionText) descriptionText.text = artifact.Description;
            var levels = PilotLoadoutStore.Levels(artifact.Id);
            for (int i = 0; i < tracks.Length && i < TrackOrder.Length; i++)
            {
                var row = tracks[i];
                if (row == null) continue;
                var element = TrackOrder[i];
                var track = artifact.Track(element);
                int level = levels.Of(element);
                bool top = track == null || level >= ArtifactTrack.MaxLevel;
                int cost = track?.CostFrom(level) ?? 0;
                if (row.elementText) row.elementText.text = ElementName(element);
                if (row.labelText) row.labelText.text = track?.label ?? string.Empty;
                if (row.levelText)
                    row.levelText.text = TemplateText.Fill(levelTemplate, "level", level.ToString(), "max", ArtifactTrack.MaxLevel.ToString());
                if (row.valueText)
                    row.valueText.text = track == null ? string.Empty
                        : top ? TemplateText.Fill(valueTopTemplate, "now", Number(track.Value(level)))
                        : TemplateText.Fill(valueTemplate, "now", Number(track.Value(level)), "next", Number(track.Value(level + 1)));
                bool twisted = level >= ArtifactTrack.TwistLevel;
                if (row.twistText)
                    row.twistText.text = track == null ? string.Empty
                        : twisted ? TemplateText.Fill(twistOpenTemplate, "twist", track.twistLine)
                        : TemplateText.Fill(twistLockedTemplate, "twist", track.twistLine, "at", ArtifactTrack.TwistLevel.ToString());
                if (row.twistGroup) row.twistGroup.alpha = twisted ? 1f : 0.45f;
                if (row.raiseLabel)
                    row.raiseLabel.text = top ? topWord : TemplateText.Fill(raiseTemplate, "cost", cost.ToString(), "element", ElementName(element));
                if (row.raiseButton) row.raiseButton.interactable = !top;
                if (row.omniLabel) row.omniLabel.text = top ? string.Empty : TemplateText.Fill(omniTemplate, "cost", cost.ToString());
                if (row.omniButton) row.omniButton.interactable = !top;
            }
        }

        string ElementName(Element element)
        {
            int i = (int)element;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : string.Empty;
        }

        static string Number(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
