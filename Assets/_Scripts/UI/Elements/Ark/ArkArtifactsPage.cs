using System;
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
    /// The Ark's ARTIFACTS tab as Rev 140 A draws it (Docs/MENU_REDESIGN_B.md sections 230 and 231; Shombith, 2026-11-24:
    /// "I cannot click in the image itself that should be what I should be able to do", then "Lets build A"). The room's
    /// clip is the screen: the four artifacts floating over their pedestals ARE the buttons (<see cref="ArtifactSpot"/>, one
    /// per kind, laid over its place in the picture), and the dark alcoves are the artifacts still to find. No card behind
    /// any word, no rail. Three levels, one press each, the back button one level up:
    /// <list type="number">
    /// <item><b>The hoard.</b> The room, one hint line. Pointing at an artifact (or the pad's focus) half lights its
    /// pedestal and shows its name.</item>
    /// <item><b>One artifact.</b> Lit where it stands: its pedestal and halo lit, the rest of the room dimmed round it
    /// (the spotlight), and its words hanging in the open air on the side with room, tied to it by a leader line: the kind
    /// and serial, its unique name, what its kind does, its attuned element with the value it improves, which seat
    /// carries it, FEED IT and GIVE TO A SEAT, and the three tracks it was rolled with.</item>
    /// <item><b>Feeding it.</b> The same words, then the attuned track's level now and after the next feed (the twist
    /// marked at 5), the crystals of its element the profile holds, and FEED / OMNI.</item>
    /// </list>
    ///
    /// <para>A pedestal holds a KIND. Pressing it opens the first artifact of that kind in the hoard; pressing it again
    /// while open steps to the next of the same kind. A kind the hoard has none of says so in the hint line.</para>
    ///
    /// <para><b>The feeding rule is unchanged</b> (Garrett, 2026-11-07, Docs/Pilots/INVENTORY.md section 2): every artifact
    /// is unique and accepts ONE element; FEED spends <see cref="feedBatch"/> of that element, OMNI the same number of
    /// omni crystals; the level creeps toward a ceiling on the diminishing curve and the other three tracks are fixed as
    /// rolled. The crystals are the profile's (<see cref="PilotLoadoutStore.Crystals"/>).</para>
    ///
    /// <para>Every word is authored on this component; code fills the numbers (<see cref="TemplateText"/>).</para>
    /// </summary>
    public class ArkArtifactsPage : MonoBehaviour, IArkTabPage, IArkTabBack
    {
        /// <summary>The tab's three levels.</summary>
        public enum Level
        {
            Hoard = 0,
            Artifact = 1,
            Feed = 2,
        }

        /// <summary>One of the three tracks an artifact was rolled with (not its attuned one): fixed, shown at level 2.</summary>
        [Serializable]
        public class FixedRow
        {
            public KitElementFlower flower;
            public TMP_Text labelText;
            public TMP_Text valueText;
        }

        /// <summary>The four tracks in the order the fixed rows take them (the attuned one skipped).</summary>
        static readonly Element[] TrackOrder = { Element.Mass, Element.Space, Element.Time, Element.Charge };

        [SerializeField] private PilotArtifactCatalogSO catalog;

        [Header("Level 1: the hoard")]
        [Tooltip("The places in the room you can touch: one per artifact kind on its pedestal, and the alcoves.")]
        [SerializeField] private ArtifactSpot[] spots = Array.Empty<ArtifactSpot>();
        [Tooltip("The hint line under the room while nothing is picked.")]
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private TMP_Text hintText;
        [Tooltip("No artifacts held: the words that stand in for the hint.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [Tooltip("Where you are, in the tab row: the hoard, the artifact, feeding it.")]
        [SerializeField] private TMP_Text trailText;

        [Header("Level 2: one artifact, lit where it stands")]
        [Tooltip("The room dimmed round the picked artifact: a dark field with a clear hole, laid on the picked spot.")]
        [SerializeField] private CanvasGroup spotlightGroup;
        [SerializeField] private RectTransform spotlight;
        [Tooltip("The picked artifact's words (levels 2 and 3), moved to the side of the room with space.")]
        [SerializeField] private CanvasGroup detailGroup;
        [SerializeField] private RectTransform detailRect;
        [Tooltip("The words' left edge (from the screen's left) for a pedestal left of the column of light.")]
        [SerializeField] private float wordsRightX = 1340f;
        [Tooltip("The words' left edge for a pedestal right of the column.")]
        [SerializeField] private float wordsLeftX = 80f;
        [Tooltip("The leader from the artifact to its words: straight up, then across.")]
        [SerializeField] private CanvasGroup leaderGroup;
        [SerializeField] private RectTransform leaderUp;
        [SerializeField] private RectTransform leaderAcross;
        [SerializeField] private RectTransform leaderDot;
        [Tooltip("The leader's thickness.")]
        [Min(1f)]
        [SerializeField] private float leaderWidth = 2f;
        [Tooltip("How far below the words' top edge the leader meets them (the kicker line).")]
        [SerializeField] private float leaderMeetY = 8f;
        [Tooltip("The gap between the leader's end and the words.")]
        [SerializeField] private float leaderGap = 16f;
        [SerializeField] private TMP_Text kickerText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private KitElementFlower attunedFlower;
        [SerializeField] private TMP_Text attunedText;
        [SerializeField] private TMP_Text heldText;
        [Tooltip("FEED IT and GIVE TO A SEAT (level 2 only).")]
        [SerializeField] private CanvasGroup actionsGroup;
        [SerializeField] private KitButton feedItButton;
        [Tooltip("GIVE TO A SEAT: takes the picked artifact to the Squads tab, where the next seat picked carries it.")]
        [SerializeField] private KitButton giveButton;
        [SerializeField] private TMP_Text giveLabel;
        [Tooltip("The three tracks it was rolled with (level 2 only).")]
        [SerializeField] private CanvasGroup fixedGroup;
        [SerializeField] private FixedRow[] fixedRows = new FixedRow[3];
        [Tooltip("BACK: one level up (levels 2 and 3).")]
        [SerializeField] private CanvasGroup backGroup;
        [SerializeField] private KitButton backButton;

        [Header("Level 3: feeding it")]
        [SerializeField] private CanvasGroup feedGroup;
        [SerializeField] private TMP_Text levelText;
        [Tooltip("The level now, filled horizontally over the 0-10 band.")]
        [SerializeField] private Image levelFill;
        [Tooltip("The level after the next feed, filled under the level now.")]
        [SerializeField] private Image nextFill;
        [Tooltip("The artifact's ceiling on the band: placed by its anchors.")]
        [SerializeField] private RectTransform ceilingMark;
        [SerializeField] private TMP_Text twistText;
        [SerializeField] private CanvasGroup twistGroup;
        [SerializeField] private TMP_Text valueText;
        [Tooltip("The crystals of the artifact's element the profile holds, one icon each, up to the row's length.")]
        [SerializeField] private Image[] crystalIcons = new Image[10];
        [Tooltip("Crystal sprites by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private Sprite[] crystalSprites = new Sprite[6];
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private KitButton feedButton;
        [SerializeField] private TMP_Text feedLabel;
        [SerializeField] private KitButton omniButton;
        [SerializeField] private TMP_Text omniLabel;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("Crystals one press of FEED (or OMNI) spends. Each crystal adds a little less than the one before.")]
        [Min(1)]
        [SerializeField] private int feedBatch = 5;

        /// <summary>Raised by GIVE TO A SEAT with the picked artifact's id and its name (the Ark takes it to the Squads tab).</summary>
        public event Action<string, string> GiveRequested;

        [Header("Words")]
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "", "", "", "", "" };
        [Tooltip("Seat names: Leader, Rogue One, Rogue Two.")]
        [SerializeField] private string[] seatNames = { "", "", "" };
        [Tooltip("The hint line at the hoard.")]
        [SerializeField] private string hintWord = "";
        [Tooltip("An alcove was pressed: it waits for an artifact still to find.")]
        [SerializeField] private string alcoveWord = "";
        [Tooltip("A pedestal whose kind the hoard has none of was pressed; {kind} is filled in.")]
        [SerializeField] private string notFoundTemplate = "";
        [Tooltip("A fixed track's label; {element} and {aspect} are filled in.")]
        [SerializeField] private string fixedTemplate = "";
        [Tooltip("The trail at each level; {kind} is filled in.")]
        [SerializeField] private string trailHoard = "";
        [SerializeField] private string trailArtifact = "";
        [SerializeField] private string trailFeed = "";
        [Tooltip("Over the name; {kind} and {serial} are filled in.")]
        [SerializeField] private string kickerTemplate = "";
        [Tooltip("The attuned line; {element}, {aspect} and {value} are filled in.")]
        [SerializeField] private string attunedTemplate = "";
        [Tooltip("Which seat carries it; {squad} and {seat} are filled in.")]
        [SerializeField] private string heldTemplate = "";
        [Tooltip("No seat carries it.")]
        [SerializeField] private string notHeldWord = "";
        [Tooltip("GIVE's word while no seat carries it.")]
        [SerializeField] private string giveWord = "";
        [Tooltip("GIVE's word while a seat already carries it.")]
        [SerializeField] private string giveAgainWord = "";
        [Tooltip("The level now; {level} is filled in.")]
        [SerializeField] private string levelNowTemplate = "";
        [Tooltip("The level now and after the next feed; {level} and {next} are filled in.")]
        [SerializeField] private string levelNextTemplate = "";
        [Tooltip("The value now and after the next feed; {now}, {next}, {aspect} and {fed} are filled in.")]
        [SerializeField] private string valueTemplate = "";
        [Tooltip("The twist before level 5; {twist} and {at} are filled in.")]
        [SerializeField] private string twistLockedTemplate = "";
        [Tooltip("The twist once reached; {twist} is filled in.")]
        [SerializeField] private string twistOpenTemplate = "";
        [Tooltip("The balances; {element}, {count}, {omni} and {batch} are filled in.")]
        [SerializeField] private string balanceTemplate = "";
        [Tooltip("FEED's word; {count} and {element} are filled in.")]
        [SerializeField] private string feedTemplate = "";
        [Tooltip("OMNI's word; {count} is filled in.")]
        [SerializeField] private string omniTemplate = "";
        [Tooltip("A feed was refused for crystals; {element} is the crystal that pays.")]
        [SerializeField] private string notEnoughTemplate = "";
        [Tooltip("A feed was refused because the artifact takes another element; {artifact} and {element} are filled in.")]
        [SerializeField] private string wrongElementTemplate = "";
        [Tooltip("A feed was refused because the artifact is no longer held.")]
        [SerializeField] private string goneWord = "";
        [Tooltip("A feed landed; {artifact}, {aspect}, {element}, {level} and {gain} are filled in.")]
        [SerializeField] private string fedTemplate = "";
        [Tooltip("A feed reached the twist; {artifact} and {twist} are filled in.")]
        [SerializeField] private string twistedTemplate = "";

        readonly List<ArtifactRecord> _hoard = new();
        /// <summary>The picked artifact's id: ids survive the hoard changing order, indices do not.</summary>
        string _pickedId = string.Empty;
        /// <summary>The hint line in place of <see cref="hintWord"/> until the next level change (an alcove, a kind not held).</summary>
        string _hint = string.Empty;
        Level _level = Level.Hoard;
        bool _shown;
        readonly List<int> _ofKind = new();

        public Level CurrentLevel => _level;

        void Awake()
        {
            for (int i = 0; i < spots.Length; i++)
            {
                int index = i;
                if (spots[i] && spots[i].Button) spots[i].Button.onClick.AddListener(() => Pick(index));
            }
            if (feedItButton) feedItButton.onClick.AddListener(() => GoTo(Level.Feed));
            if (backButton) backButton.onClick.AddListener(Back);
            if (giveButton) giveButton.onClick.AddListener(Give);
            if (feedButton) feedButton.onClick.AddListener(() => Feed(false));
            if (omniButton) omniButton.onClick.AddListener(() => Feed(true));
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
            // Every visit opens on the hoard.
            _level = Level.Hoard;
            _hint = string.Empty;
            if (statusText) statusText.text = string.Empty;
            Refresh();
        }

        void OnArtifactsChanged(VesselClassType _) => Refresh();
        void OnBalance(int _) => Refresh();

        void Pick(int spot)
        {
            var at = spot >= 0 && spot < spots.Length ? spots[spot] : null;
            if (!at) return;
            if (at.IsAlcove)
            {
                ShowHint(alcoveWord);
                return;
            }
            OfKind(at.KindId, _ofKind);
            if (_ofKind.Count == 0)
            {
                ShowHint(TemplateText.Fill(notFoundTemplate, "kind", KindName(at.KindId)));
                return;
            }
            // Pressed again while open: the next of the same kind, if the hoard has more than one.
            int now = _level != Level.Hoard ? _ofKind.IndexOf(PickedIndex()) : -1;
            if (now >= 0 && _ofKind.Count == 1) return;
            int next = now >= 0 ? _ofKind[(now + 1) % _ofKind.Count] : _ofKind[0];
            _pickedId = _hoard[next].Id;
            GoTo(Level.Artifact);
        }

        /// <summary>Back to the hoard, with <paramref name="line"/> in the hint line until the next press.</summary>
        void ShowHint(string line)
        {
            GoTo(Level.Hoard);
            _hint = line ?? string.Empty;
            Refresh();
        }

        /// <summary>The hoard indices of every artifact of kind <paramref name="kindId"/>, in hoard order.</summary>
        void OfKind(string kindId, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < _hoard.Count; i++)
                if (_hoard[i].Kind() == kindId) into.Add(i);
        }

        /// <summary>One level up: feeding back to the artifact, the artifact back to the hoard.</summary>
        public void Back() => GoTo(_level == Level.Feed ? Level.Artifact : Level.Hoard);

        /// <summary>B or Escape (<see cref="ArkTabsView.TryBack"/>): <see cref="Back"/>, unless the hoard is up.</summary>
        public bool TryBack()
        {
            if (!_shown || _level == Level.Hoard) return false;
            Back();
            return true;
        }

        void GoTo(Level level)
        {
            if (level != Level.Hoard && PickedIndex() < 0) level = Level.Hoard;
            _level = level;
            _hint = string.Empty;
            if (statusText) statusText.text = string.Empty;
            Refresh();
        }

        int PickedIndex()
        {
            for (int i = 0; i < _hoard.Count; i++)
                if (_hoard[i].Id == _pickedId) return i;
            return -1;
        }

        void Give()
        {
            if (string.IsNullOrEmpty(_pickedId) || !PilotLoadoutStore.TryGetArtifact(_pickedId, out var artifact)) return;
            GiveRequested?.Invoke(_pickedId, DisplayName(artifact));
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
                        "element", ElementName(element), "level", LevelWord(result.After), "gain", Gain(result.After - result.Before)),
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
                _pickedId = string.Empty;
                _level = Level.Hoard;
            }

            var current = picked >= 0 ? _hoard[picked] : null;
            string currentKindId = current != null && _level != Level.Hoard ? current.Kind() : null;
            ArtifactSpot pickedSpot = null;
            foreach (var spot in spots)
            {
                if (!spot) continue;
                bool on = !spot.IsAlcove && spot.KindId == currentKindId;
                if (on) pickedSpot = spot;
                spot.Draw(spot.IsAlcove ? string.Empty : KindName(spot.KindId), on);
            }

            bool empty = _hoard.Count == 0;
            KitVisibility.SetShown(emptyGroup, empty);
            KitVisibility.SetShown(hintGroup, (!empty || _hint.Length > 0) && _level == Level.Hoard);
            if (hintText) hintText.text = _hint.Length > 0 ? _hint : hintWord;
            if (empty && _hint.Length > 0) KitVisibility.SetShown(emptyGroup, false);
            KitVisibility.SetShown(detailGroup, _level != Level.Hoard);
            KitVisibility.SetShown(actionsGroup, _level == Level.Artifact);
            KitVisibility.SetShown(fixedGroup, _level == Level.Artifact);
            KitVisibility.SetShown(feedGroup, _level == Level.Feed);
            KitVisibility.SetShown(backGroup, _level != Level.Hoard);
            SetButton(feedItButton, _level == Level.Artifact);
            SetButton(giveButton, _level == Level.Artifact);
            SetButton(backButton, _level != Level.Hoard);
            SetButton(feedButton, _level == Level.Feed);
            SetButton(omniButton, _level == Level.Feed);

            var currentKind = current != null ? Kind(current) : null;
            if (trailText)
            {
                string template = _level switch { Level.Feed => trailFeed, Level.Artifact => trailArtifact, _ => trailHoard };
                trailText.text = TemplateText.Fill(template, "kind", currentKind ? currentKind.DisplayName : string.Empty);
            }

            bool lit = current != null && _level != Level.Hoard && pickedSpot;
            KitVisibility.SetShown(spotlightGroup, lit);
            KitVisibility.SetShown(leaderGroup, lit);
            if (!lit) return;
            PlaceWords(pickedSpot);
            DrawDetail(current, currentKind);
            DrawFixed(current, currentKind);
            if (_level == Level.Feed) DrawFeed(current, currentKind);
        }

        void DrawDetail(ArtifactRecord artifact, ArtifactSO kind)
        {
            var track = kind ? kind.Track(artifact.Element) : null;
            float level = artifact.AttunedLevel();
            string kindName = kind ? kind.DisplayName : string.Empty;
            if (kickerText) kickerText.text = TemplateText.Fill(kickerTemplate, "kind", kindName, "serial", artifact.Serial());
            if (nameText) nameText.text = DisplayName(artifact);
            if (descriptionText) descriptionText.text = kind ? kind.Description : string.Empty;
            if (attunedFlower) attunedFlower.Set(artifact.Element, FlowerMarkLevel);
            if (attunedText)
                attunedText.text = TemplateText.Fill(attunedTemplate, "element", ElementName(artifact.Element),
                    "aspect", Aspect(artifact, track), "value", track == null ? string.Empty : Number(track.Value(level)));
            bool carried = TryFindSeat(artifact.Id, out string squad, out int seat);
            if (heldText) heldText.text = carried ? TemplateText.Fill(heldTemplate, "squad", squad, "seat", SeatName(seat)) : notHeldWord;
            if (giveLabel) giveLabel.text = carried ? giveAgainWord : giveWord;
        }

        /// <summary>The flower drawn as the element's mark, all five petals lit (the HUD's level 5), not as a level.</summary>
        const int FlowerMarkLevel = 5;

        /// <summary>Lays the spotlight on <paramref name="spot"/>, moves the words to the side of the room with space and
        /// draws the leader from the top of the artifact up to the words' kicker line and across to them.</summary>
        void PlaceWords(ArtifactSpot spot)
        {
            var spotRect = spot.Rect;
            if (spotlight)
            {
                spotlight.anchorMin = spotRect.anchorMin;
                spotlight.anchorMax = spotRect.anchorMax;
                spotlight.anchoredPosition = spotRect.anchoredPosition;
            }
            if (detailRect)
                detailRect.anchoredPosition = new Vector2(spot.WordsOnLeft ? wordsLeftX : wordsRightX, detailRect.anchoredPosition.y);
            if (!detailRect || !leaderUp || !leaderAcross) return;
            var page = (RectTransform)transform;
            // The spot's pivot is the artifact's centre; its top edge is the artifact's top.
            var r = spotRect.rect;
            Vector2 from = page.InverseTransformPoint(spotRect.TransformPoint(new Vector3(0f, r.yMax, 0f)));
            var d = detailRect.rect;
            float edgeX = spot.WordsOnLeft ? d.xMax + leaderGap : d.xMin - leaderGap;
            Vector2 meet = page.InverseTransformPoint(detailRect.TransformPoint(new Vector3(edgeX, d.yMax - leaderMeetY, 0f)));
            Segment(leaderUp, from, new Vector2(from.x, meet.y));
            Segment(leaderAcross, new Vector2(from.x, meet.y), meet);
            if (leaderDot) leaderDot.localPosition = from;
        }

        /// <summary>An axis-aligned line from <paramref name="a"/> to <paramref name="b"/> in the page's space.</summary>
        void Segment(RectTransform line, Vector2 a, Vector2 b)
        {
            line.localPosition = (a + b) * 0.5f;
            line.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + leaderWidth, Mathf.Abs(b.y - a.y) + leaderWidth);
        }

        void DrawFixed(ArtifactRecord artifact, ArtifactSO kind)
        {
            var levels = artifact.Levels();
            int row = 0;
            foreach (var e in TrackOrder)
            {
                if (e == artifact.Element || row >= fixedRows.Length) continue;
                var at = fixedRows[row++];
                if (at == null) continue;
                var track = kind ? kind.Track(e) : null;
                if (at.flower) at.flower.Set(e, FlowerMarkLevel);
                if (at.labelText)
                    at.labelText.text = TemplateText.Fill(fixedTemplate, "element", ElementName(e), "aspect", track?.label ?? string.Empty);
                if (at.valueText) at.valueText.text = track == null ? string.Empty : Number(track.Value(levels.Of(e)));
            }
        }

        void DrawFeed(ArtifactRecord artifact, ArtifactSO kind)
        {
            var element = artifact.Element;
            var track = kind ? kind.Track(element) : null;
            float level = artifact.AttunedLevel();
            float next = artifact.LevelAfter(feedBatch);
            if (levelText) levelText.text = TemplateText.Fill(levelNextTemplate, "level", LevelWord(level), "next", LevelWord(next));
            if (levelFill) levelFill.fillAmount = level / ArtifactLevels.MaxLevel;
            if (nextFill) nextFill.fillAmount = next / ArtifactLevels.MaxLevel;
            if (ceilingMark)
            {
                float x = Mathf.Clamp01(artifact.Ceiling / ArtifactLevels.MaxLevel);
                ceilingMark.anchorMin = new Vector2(x, ceilingMark.anchorMin.y);
                ceilingMark.anchorMax = new Vector2(x, ceilingMark.anchorMax.y);
            }
            bool twisted = level >= ArtifactLevels.TwistLevel;
            if (twistText)
                twistText.text = track == null || string.IsNullOrEmpty(track.twistLine) ? string.Empty
                    : twisted ? TemplateText.Fill(twistOpenTemplate, "twist", track.twistLine)
                    : TemplateText.Fill(twistLockedTemplate, "twist", track.twistLine, "at", LevelWord(ArtifactLevels.TwistLevel));
            if (twistGroup) twistGroup.alpha = twisted ? 1f : 0.55f;
            if (valueText)
                valueText.text = track == null ? string.Empty
                    : TemplateText.Fill(valueTemplate, "now", Number(track.Value(level)), "next", Number(track.Value(next)),
                        "aspect", Aspect(artifact, track), "fed", artifact.Invested.ToString(CultureInfo.InvariantCulture));

            int held = PilotLoadoutStore.Crystals(element);
            int omni = PilotLoadoutStore.Crystals(Element.Omni);
            var sprite = CrystalSprite(element);
            for (int i = 0; i < crystalIcons.Length; i++)
            {
                if (!crystalIcons[i]) continue;
                crystalIcons[i].sprite = sprite;
                crystalIcons[i].enabled = sprite && i < held;
            }
            string count = feedBatch.ToString(CultureInfo.InvariantCulture);
            if (balanceText)
                balanceText.text = TemplateText.Fill(balanceTemplate, "element", ElementName(element),
                    "count", held.ToString(CultureInfo.InvariantCulture), "omni", omni.ToString(CultureInfo.InvariantCulture),
                    "batch", count);
            if (feedLabel) feedLabel.text = TemplateText.Fill(feedTemplate, "count", count, "element", ElementName(element));
            if (omniLabel) omniLabel.text = TemplateText.Fill(omniTemplate, "count", count);
            if (feedButton) feedButton.interactable = held >= feedBatch;
            if (omniButton) omniButton.interactable = omni >= feedBatch;
        }

        /// <summary>The first squad in the player's book whose seat carries <paramref name="artifactId"/>.</summary>
        static bool TryFindSeat(string artifactId, out string squad, out int seat)
        {
            squad = string.Empty;
            seat = -1;
            var book = VoyageSeats.Book;
            if (book?.Squads == null || string.IsNullOrEmpty(artifactId)) return false;
            foreach (var s in book.Squads)
            {
                if (s == null) continue;
                for (int i = 0; i < SavedSquad.SlotCount; i++)
                {
                    if (s.GetSlot(i).ArtifactId != artifactId) continue;
                    squad = s.Name ?? string.Empty;
                    seat = i;
                    return true;
                }
            }
            return false;
        }

        static void SetButton(KitButton button, bool on)
        {
            if (button) button.interactable = on;
        }

        ArtifactSO Kind(ArtifactRecord artifact) =>
            catalog && catalog.TryGetArtifact(artifact.Kind(), out var kind) ? kind : null;

        string KindName(string kindId) =>
            catalog && catalog.TryGetArtifact(kindId, out var kind) ? kind.DisplayName : string.Empty;

        Sprite CrystalSprite(Element element)
        {
            int i = (int)element;
            return i >= 0 && i < crystalSprites.Length ? crystalSprites[i] : null;
        }

        static string Aspect(ArtifactRecord artifact, ArtifactTrack track) =>
            !string.IsNullOrEmpty(artifact.Aspect) ? artifact.Aspect : track?.label ?? string.Empty;

        string TwistLine(ArtifactRecord artifact)
        {
            if (artifact == null) return string.Empty;
            var kind = Kind(artifact);
            return kind ? kind.Track(artifact.Element)?.twistLine ?? string.Empty : string.Empty;
        }

        string DisplayName(ArtifactRecord artifact)
        {
            if (!string.IsNullOrEmpty(artifact.Name)) return artifact.Name;
            var kind = Kind(artifact);
            return kind ? kind.DisplayName : artifact.Serial();
        }

        string ElementName(Element element)
        {
            int i = (int)element;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : string.Empty;
        }

        string SeatName(int seat) => seat >= 0 && seat < seatNames.Length ? seatNames[seat] : string.Empty;

        /// <summary>A level on the 0-10 band: one decimal on the page (the curve still moves it by hundredths).</summary>
        static string LevelWord(float v) => v.ToString("0.0#", CultureInfo.InvariantCulture);

        /// <summary>What a feed added; never "+0.00" for a real gain too small to show.</summary>
        static string Gain(float v) => v > 0f && v < 0.005f ? "<0.01" : v.ToString("0.00", CultureInfo.InvariantCulture);

        static string Number(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
