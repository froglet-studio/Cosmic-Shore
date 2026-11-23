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
    /// The Ark's ARTIFACTS tab (Docs/Pilots/INVENTORY.md §2, Docs/Pilots/ARTIFACTS.md §4): the player's hoard of UNIQUE
    /// artifacts down the left (<see cref="PilotLoadoutStore.GetHoard"/>), and the picked one on the right.
    ///
    /// <para><b>2026-11-07 (Garrett): every artifact is unique and accepts ONE element.</b> Each held artifact
    /// (<see cref="ArtifactRecord"/>) was rolled from its own seed on top of a kind (<see cref="ArtifactSO"/>): it has
    /// its own name and serial, it is attuned to exactly one element (<see cref="ArtifactRecord.Element"/>), and feeding
    /// it that element improves exactly one thing (<see cref="ArtifactRecord.Aspect"/>, the kind's track for that
    /// element). It takes any number of crystals with diminishing returns (<see cref="ArtifactCurve"/>), so the attuned
    /// level is a real number creeping toward a ceiling it never reaches; the other three tracks stay at the levels it
    /// was rolled with, forever. So the old "RAISE one whole level per track" design is gone: there is nothing to raise
    /// on three of the four tracks, and no top to reach on the fourth.</para>
    ///
    /// <para><b>How the four track rows are used now.</b> The first row is the attuned track: its level now and after the
    /// next feed, its value now and after, its twist (lit once the level reaches
    /// <see cref="ArtifactLevels.TwistLevel"/>), and the two feed buttons. FEED spends <see cref="feedBatch"/> crystals of
    /// the artifact's own element; OMNI spends the same number of omni crystals instead (the player's pick, never
    /// automatic). The other three rows show the fixed tracks read-only and dimmed, with their buttons hidden: the same
    /// prefab rows, so no new wiring is needed.</para>
    ///
    /// <para><b>The list has four rows</b> but a hoard can hold more. Until the prefab gets a scroll list, the rows are a
    /// window onto the hoard: picking the last visible row scrolls the window down by one, picking the first scrolls it
    /// up, so every artifact can be reached.</para>
    ///
    /// <para>The crystals are the profile's (<see cref="PilotLoadoutStore.Crystals"/>); the crystals fed live on the
    /// artifact itself, so a stolen artifact keeps them. Every word is authored on this component; code fills the
    /// numbers. Words added on 2026-11-07 carry defaults so the page reads before the prefab authors them.</para>
    /// </summary>
    public class ArkArtifactsPage : MonoBehaviour, IArkTabPage
    {
        [System.Serializable]
        public class TrackRow
        {
            public TMP_Text elementText;
            [Tooltip("What the track moves (ArtifactTrack.label; the artifact's Aspect on the attuned row).")]
            public TMP_Text labelText;
            public TMP_Text levelText;
            [Tooltip("The value now, and after the next feed (attuned row) or as rolled (fixed rows).")]
            public TMP_Text valueText;
            [Tooltip("The level-5 twist: dim until it is reached.")]
            public TMP_Text twistText;
            public CanvasGroup twistGroup;
            [Tooltip("FEED on the attuned row; hidden on the fixed rows. (Named raise* for the prefab's wiring.)")]
            public KitButton raiseButton;
            public TMP_Text raiseLabel;
            [Tooltip("Feed in omni crystals instead, on the attuned row; hidden on the fixed rows.")]
            public KitButton omniButton;
            public TMP_Text omniLabel;
        }

        /// <summary>The fixed rows' order after the attuned one: the four elements as the converter lists them.</summary>
        static readonly Element[] TrackOrder = { Element.Mass, Element.Space, Element.Time, Element.Charge };
        /// <summary>The crystal strip's order: the four elements, then Omni.</summary>
        static readonly Element[] CrystalOrder = { Element.Mass, Element.Space, Element.Time, Element.Charge, Element.Omni };

        /// <summary>A fixed track row's text alpha: read-only, so dimmed.</summary>
        const float FixedAlpha = 0.5f;

        [SerializeField] private PilotArtifactCatalogSO catalog;

        [Header("The list")]
        [SerializeField] private SquadRowView[] rows = new SquadRowView[4];
        [SerializeField] private CanvasGroup emptyGroup;

        [Header("The picked artifact")]
        [SerializeField] private CanvasGroup detailGroup;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("What it accepts and improves, what its kind does, and its lore (descriptionTemplate).")]
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("Row 0 is the attuned track; rows 1-3 the fixed tracks, read-only.")]
        [SerializeField] private TrackRow[] tracks = new TrackRow[4];
        [SerializeField] private TMP_Text statusText;

        [Header("Feeding")]
        [Tooltip("Crystals one press of FEED (or OMNI) spends. Each crystal adds a little less than the one before.")]
        [Min(1)]
        [SerializeField] private int feedBatch = 5;

        [Header("Crystals")]
        [Tooltip("Mass, Space, Time, Charge, Omni.")]
        [SerializeField] private TMP_Text[] crystalTexts = new TMP_Text[5];

        [Header("Words")]
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "", "", "", "", "" };
        [Tooltip("A crystal balance; {element} and {count} are filled in.")]
        [SerializeField] private string crystalTemplate = "";
        [Tooltip("A list row's line; {element}, {aspect}, {level} and {serial} are filled in.")]
        [SerializeField] private string rowLineTemplate = "{element} {level} - {aspect}";
        [Tooltip("The picked artifact's name; {name}, {serial} and {kind} are filled in.")]
        [SerializeField] private string nameTemplate = "{name}  #{serial}";
        [Tooltip("The picked artifact's description; {element}, {aspect}, {description} and {lore} are filled in.")]
        [TextArea(2, 6)]
        [SerializeField] private string descriptionTemplate =
            "Accepts only {element} crystals (or Omni). Feeding it raises {aspect}.\n{description}\n{lore}";
        [Tooltip("A fixed track's level; {level} and {max} are filled in.")]
        [SerializeField] private string levelTemplate = "";
        [Tooltip("The attuned track's level; {level}, {next}, {ceiling}, {max} and {fed} are filled in.")]
        [SerializeField] private string attunedLevelTemplate = "{level} -> {next} / {max}  ({fed} fed)";
        [Tooltip("The attuned track's value now and after the next feed; {now} and {next} are filled in.")]
        [SerializeField] private string valueTemplate = "";
        [Tooltip("A fixed track's value; {now} is filled in.")]
        [SerializeField] private string valueTopTemplate = "";
        [Tooltip("The twist before level 5; {twist} and {at} are filled in.")]
        [SerializeField] private string twistLockedTemplate = "";
        [Tooltip("The twist once reached; {twist} is filled in.")]
        [SerializeField] private string twistOpenTemplate = "";
        [Tooltip("FEED's word; {count} and {element} are filled in.")]
        [SerializeField] private string feedTemplate = "Feed {count} {element}";
        [Tooltip("OMNI's word; {count} is filled in. (Authored before 2026-11-07 with {cost}, which is also filled in.)")]
        [SerializeField] private string omniTemplate = "";
        [Tooltip("A fixed track's element suffix; {element} is filled in. Shown in the element column of rows 1-3.")]
        [SerializeField] private string fixedElementTemplate = "{element} (fixed)";
        [Tooltip("A feed was refused for crystals; {element} is the crystal that pays.")]
        [SerializeField] private string notEnoughTemplate = "";
        [Tooltip("A feed was refused because the artifact takes another element; {artifact} and {element} are filled in.")]
        [SerializeField] private string wrongElementTemplate = "{artifact} accepts only {element} crystals.";
        [Tooltip("A feed was refused because the artifact is no longer held.")]
        [SerializeField] private string goneWord = "That artifact is no longer in the hoard.";
        [Tooltip("A feed landed; {artifact}, {aspect}, {element}, {level} and {gain} are filled in.")]
        [SerializeField] private string fedTemplate = "{artifact}: {aspect} is now {level} (+{gain}).";
        [Tooltip("A feed reached the twist; {artifact} and {twist} are filled in.")]
        [SerializeField] private string twistedTemplate = "";

        readonly List<ArtifactRecord> _hoard = new();
        /// <summary>The picked artifact's id: ids survive the hoard changing order, indices do not.</summary>
        string _pickedId = string.Empty;
        /// <summary>The hoard index the first list row shows.</summary>
        int _first;
        bool _shown;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i] && rows[i].Button) rows[i].Button.onClick.AddListener(() => PickRow(index));
            }
            // Only the attuned row (0) feeds; the fixed rows' buttons are hidden.
            if (tracks.Length > 0 && tracks[0] != null)
            {
                if (tracks[0].raiseButton) tracks[0].raiseButton.onClick.AddListener(() => Feed(false));
                if (tracks[0].omniButton) tracks[0].omniButton.onClick.AddListener(() => Feed(true));
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

        void PickRow(int row)
        {
            int index = _first + row;
            if (index < 0 || index >= _hoard.Count) return;
            _pickedId = _hoard[index].Id;
            // Scroll the window by one at either edge so a hoard longer than the rows stays reachable.
            if (row == rows.Length - 1 && _first + rows.Length < _hoard.Count) _first++;
            else if (row == 0 && _first > 0) _first--;
            if (statusText) statusText.text = string.Empty;
            Refresh();
        }

        int PickedIndex()
        {
            for (int i = 0; i < _hoard.Count; i++)
                if (_hoard[i].Id == _pickedId) return i;
            return -1;
        }

        void Feed(bool payWithOmni)
        {
            if (string.IsNullOrEmpty(_pickedId)) return;
            PilotLoadoutStore.TryGetArtifact(_pickedId, out var artifact);
            var outcome = PilotLoadoutStore.TryFeed(_pickedId, feedBatch, payWithOmni, out var result);
            if (!statusText) return;
            string name = artifact != null ? DisplayName(artifact) : string.Empty;
            var element = artifact?.Element ?? Element.None;
            statusText.text = outcome switch
            {
                PilotLoadoutStore.FeedOutcome.Fed when result.CrossedTwist =>
                    TemplateText.Fill(twistedTemplate, "artifact", name, "twist", TwistLine(artifact)),
                PilotLoadoutStore.FeedOutcome.Fed =>
                    TemplateText.Fill(fedTemplate, "artifact", name, "aspect", artifact?.Aspect ?? string.Empty,
                        "element", ElementName(element), "level", Level(result.After), "gain", Gain(result.After - result.Before)),
                PilotLoadoutStore.FeedOutcome.NotEnoughCrystals =>
                    TemplateText.Fill(notEnoughTemplate, "element", ElementName(payWithOmni ? Element.Omni : element)),
                PilotLoadoutStore.FeedOutcome.WrongElement =>
                    TemplateText.Fill(wrongElementTemplate, "artifact", name, "element", ElementName(element)),
                PilotLoadoutStore.FeedOutcome.NoSuchArtifact => goneWord,
                _ => string.Empty,
            };
        }

        void Refresh()
        {
            if (!_shown) return;
            PilotLoadoutStore.GetHoard(_hoard);
            int picked = PickedIndex();
            if (picked < 0)
            {
                picked = 0;
                _pickedId = _hoard.Count > 0 ? _hoard[0].Id : string.Empty;
            }
            // Keep the window inside the hoard and the picked artifact inside the window.
            int visible = Mathf.Max(1, rows.Length);
            _first = Mathf.Clamp(_first, 0, Mathf.Max(0, _hoard.Count - visible));
            if (picked < _first) _first = picked;
            else if (picked >= _first + visible) _first = picked - visible + 1;

            for (int i = 0; i < crystalTexts.Length && i < CrystalOrder.Length; i++)
                if (crystalTexts[i])
                    crystalTexts[i].text = TemplateText.Fill(crystalTemplate, "element", ElementName(CrystalOrder[i]),
                        "count", PilotLoadoutStore.Crystals(CrystalOrder[i]).ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i]) continue;
                int index = _first + i;
                if (index >= _hoard.Count) { rows[i].Draw(false, null, null, false); continue; }
                var a = _hoard[index];
                rows[i].Draw(true, DisplayName(a),
                    TemplateText.Fill(rowLineTemplate, "element", ElementName(a.Element), "aspect", a.Aspect ?? string.Empty,
                        "level", Level(a.AttunedLevel()), "serial", a.Serial()), index == picked);
            }
            KitVisibility.SetShown(emptyGroup, _hoard.Count == 0);
            KitVisibility.SetShown(detailGroup, _hoard.Count > 0);
            if (_hoard.Count > 0) DrawDetail(_hoard[picked]);
        }

        void DrawDetail(ArtifactRecord artifact)
        {
            ArtifactSO kind = null;
            if (catalog) catalog.TryGetArtifact(artifact.Kind(), out kind);
            if (nameText)
                nameText.text = TemplateText.Fill(nameTemplate, "name", DisplayName(artifact), "serial", artifact.Serial(),
                    "kind", kind ? kind.DisplayName : string.Empty);
            if (descriptionText)
                descriptionText.text = TemplateText.Fill(descriptionTemplate, "element", ElementName(artifact.Element),
                    "aspect", artifact.Aspect ?? string.Empty, "description", kind ? kind.Description : string.Empty,
                    "lore", artifact.Lore ?? string.Empty).Trim();

            var levels = artifact.Levels();
            int row = 0;
            if (row < tracks.Length) DrawAttuned(tracks[row++], artifact, kind);
            for (int e = 0; e < TrackOrder.Length && row < tracks.Length; e++)
            {
                if (TrackOrder[e] == artifact.Element) continue;
                DrawFixed(tracks[row++], TrackOrder[e], levels.Of(TrackOrder[e]), kind);
            }
            for (; row < tracks.Length; row++) DrawFixed(tracks[row], Element.None, 0f, null);
        }

        void DrawAttuned(TrackRow row, ArtifactRecord artifact, ArtifactSO kind)
        {
            if (row == null) return;
            var element = artifact.Element;
            var track = kind ? kind.Track(element) : null;
            float level = artifact.AttunedLevel();
            float next = artifact.LevelAfter(feedBatch);
            SetAlpha(row, 1f);
            if (row.elementText) row.elementText.text = ElementName(element);
            if (row.labelText) row.labelText.text = string.IsNullOrEmpty(artifact.Aspect) ? track?.label ?? string.Empty : artifact.Aspect;
            if (row.levelText)
                row.levelText.text = TemplateText.Fill(attunedLevelTemplate, "level", Level(level), "next", Level(next),
                    "ceiling", Level(artifact.Ceiling), "max", Level(ArtifactLevels.MaxLevel),
                    "fed", artifact.Invested.ToString(CultureInfo.InvariantCulture));
            if (row.valueText)
                row.valueText.text = track == null ? string.Empty
                    : TemplateText.Fill(valueTemplate, "now", Number(track.Value(level)), "next", Number(track.Value(next)));
            DrawTwist(row, track, level);

            string count = feedBatch.ToString(CultureInfo.InvariantCulture);
            SetButtonShown(row.raiseButton, true);
            SetButtonShown(row.omniButton, true);
            if (row.raiseLabel) row.raiseLabel.text = TemplateText.Fill(feedTemplate, "count", count, "element", ElementName(element));
            if (row.omniLabel) row.omniLabel.text = TemplateText.Fill(omniTemplate, "count", count, "cost", count);
        }

        void DrawFixed(TrackRow row, Element element, float level, ArtifactSO kind)
        {
            if (row == null) return;
            var track = kind ? kind.Track(element) : null;
            bool used = element != Element.None;
            SetAlpha(row, FixedAlpha);
            if (row.elementText) row.elementText.text = used ? TemplateText.Fill(fixedElementTemplate, "element", ElementName(element)) : string.Empty;
            if (row.labelText) row.labelText.text = track?.label ?? string.Empty;
            if (row.levelText)
                row.levelText.text = used ? TemplateText.Fill(levelTemplate, "level", Level(level), "max", Level(ArtifactLevels.MaxLevel)) : string.Empty;
            if (row.valueText)
                row.valueText.text = track == null ? string.Empty : TemplateText.Fill(valueTopTemplate, "now", Number(track.Value(level)));
            DrawTwist(row, track, level);
            if (row.twistGroup && track != null) row.twistGroup.alpha *= FixedAlpha;
            SetButtonShown(row.raiseButton, false);
            SetButtonShown(row.omniButton, false);
            if (row.raiseLabel) row.raiseLabel.text = string.Empty;
            if (row.omniLabel) row.omniLabel.text = string.Empty;
        }

        void DrawTwist(TrackRow row, ArtifactTrack track, float level)
        {
            bool twisted = level >= ArtifactLevels.TwistLevel;
            if (row.twistText)
                row.twistText.text = track == null || string.IsNullOrEmpty(track.twistLine) ? string.Empty
                    : twisted ? TemplateText.Fill(twistOpenTemplate, "twist", track.twistLine)
                    : TemplateText.Fill(twistLockedTemplate, "twist", track.twistLine, "at", Level(ArtifactLevels.TwistLevel));
            if (row.twistGroup) row.twistGroup.alpha = twisted ? 1f : 0.45f;
        }

        /// <summary>Dims a fixed row's words (rows have no group of their own in the prefab, so the texts carry it).</summary>
        static void SetAlpha(TrackRow row, float alpha)
        {
            if (row.elementText) row.elementText.alpha = alpha;
            if (row.labelText) row.labelText.alpha = alpha;
            if (row.levelText) row.levelText.alpha = alpha;
            if (row.valueText) row.valueText.alpha = alpha;
        }

        static void SetButtonShown(KitButton button, bool shown)
        {
            if (!button) return;
            button.interactable = shown;
            if (button.gameObject.activeSelf != shown) button.gameObject.SetActive(shown);
        }

        string TwistLine(ArtifactRecord artifact)
        {
            if (artifact == null || !catalog || !catalog.TryGetArtifact(artifact.Kind(), out var kind)) return string.Empty;
            return kind.Track(artifact.Element)?.twistLine ?? string.Empty;
        }

        string DisplayName(ArtifactRecord artifact)
        {
            if (!string.IsNullOrEmpty(artifact.Name)) return artifact.Name;
            return catalog && catalog.TryGetArtifact(artifact.Kind(), out var kind) ? kind.DisplayName : artifact.Serial();
        }

        string ElementName(Element element)
        {
            int i = (int)element;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : string.Empty;
        }

        /// <summary>A level on the 0-10 band: two decimals, since a feed late on the curve moves it by hundredths.</summary>
        static string Level(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>What a feed added; never "+0.00" for a real gain too small to show.</summary>
        static string Gain(float v) => v > 0f && v < 0.005f ? "<0.01" : Level(v);

        static string Number(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
