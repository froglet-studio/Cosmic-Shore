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
    /// The Ark's ARTIFACTS tab as Rev 105 B draws it (Docs/MENU_REDESIGN_B.md sections 193 and 201; Shombith, 2026-11-13:
    /// "B is good", after "the UI cards on top of each screen, I do not like them at all"): no card behind any word, the
    /// room's backdrop whole, and the tab in THREE LEVELS, one press each, the back button one level up:
    /// <list type="number">
    /// <item><b>The hoard.</b> The rail of medallions along the floor: every held artifact's mark and kind, then empty
    /// slots for the ones still to find. Nothing else on screen but a one-line hint.</item>
    /// <item><b>One artifact.</b> The lens opens (<see cref="ArtifactLens"/>): its payload plays over prisms, its four
    /// tracks round the rim. On the left, no box: the kind and serial, its unique name, what its kind does, its attuned
    /// element with the value it improves, and which seat carries it; FEED IT and GIVE TO A SEAT.</item>
    /// <item><b>Feeding it.</b> The same words, then the attuned track's level now and after the next feed (the twist
    /// marked at 5), the crystals of its element the profile holds, and FEED / OMNI.</item>
    /// </list>
    ///
    /// <para><b>The feeding rule is unchanged</b> (Garrett, 2026-11-07, Docs/Pilots/INVENTORY.md section 2): every artifact
    /// is unique and accepts ONE element; FEED spends <see cref="feedBatch"/> of that element, OMNI the same number of
    /// omni crystals; the level creeps toward a ceiling on the diminishing curve and the other three tracks are fixed as
    /// rolled. The crystals are the profile's (<see cref="PilotLoadoutStore.Crystals"/>).</para>
    ///
    /// <para>Every word is authored on this component; code fills the numbers (<see cref="TemplateText"/>).</para>
    /// </summary>
    public class ArkArtifactsPage : MonoBehaviour, IArkTabPage
    {
        /// <summary>The tab's three levels.</summary>
        public enum Level
        {
            Hoard = 0,
            Artifact = 1,
            Feed = 2,
        }

        /// <summary>How a kind of artifact looks on the rail and plays in the lens.</summary>
        [Serializable]
        public class KindLook
        {
            [Tooltip("The kind's id (ArtifactSO.Id), e.g. artifact.hydra.")]
            public string kindId;
            [Tooltip("Its mark on the rail.")]
            public Sprite glyph;
            [Tooltip("The shape its payload plays in the lens.")]
            public ArtifactLens.Payload payload;
            [Tooltip("The track whose value is the payload's count (heads, cracks, shocks, re-raises).")]
            public Element countTrack = Element.Charge;
            [Tooltip("Added to that value: 1 for a count of re-raises, where the first shell is not a re-raise.")]
            public int countOffset;
        }

        /// <summary>The tracks round the lens's rim, in the lens's arc order.</summary>
        static readonly Element[] TrackOrder = { Element.Mass, Element.Space, Element.Time, Element.Charge };

        [SerializeField] private PilotArtifactCatalogSO catalog;
        [SerializeField] private KindLook[] kinds = Array.Empty<KindLook>();

        [Header("Level 1: the hoard")]
        [Tooltip("The rail's medallions, left to right. Past the hoard they show as empty slots.")]
        [SerializeField] private ArtifactMedallion[] medallions = new ArtifactMedallion[7];
        [Tooltip("The hint over the rail while nothing is picked.")]
        [SerializeField] private CanvasGroup hintGroup;
        [Tooltip("No artifacts held: the words that stand in for the rail's medallions.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [Tooltip("Where you are, in the tab row: the hoard, the artifact, feeding it.")]
        [SerializeField] private TMP_Text trailText;

        [Header("Level 2: one artifact")]
        [SerializeField] private ArtifactLens lens;
        [Tooltip("The picked artifact's words on the left (levels 2 and 3).")]
        [SerializeField] private CanvasGroup detailGroup;
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
        /// <summary>The hoard index the first medallion shows.</summary>
        int _first;
        Level _level = Level.Hoard;
        bool _shown;
        bool _replayLens;

        public Level CurrentLevel => _level;

        void Awake()
        {
            for (int i = 0; i < medallions.Length; i++)
            {
                int index = i;
                if (medallions[i] && medallions[i].Button) medallions[i].Button.onClick.AddListener(() => Pick(index));
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
            if (!shown)
            {
                if (lens) lens.Hide();
                return;
            }
            // Every visit opens on the hoard.
            _level = Level.Hoard;
            if (statusText) statusText.text = string.Empty;
            Refresh();
        }

        void OnArtifactsChanged(VesselClassType _) => Refresh();
        void OnBalance(int _) => Refresh();

        void Pick(int medallion)
        {
            int index = _first + medallion;
            if (index < 0 || index >= _hoard.Count) return;
            string id = _hoard[index].Id;
            if (id == _pickedId && _level != Level.Hoard) return;
            _pickedId = id;
            // Scroll the window by one at either edge so a hoard longer than the rail stays reachable.
            if (medallion == medallions.Length - 1 && _first + medallions.Length < _hoard.Count) _first++;
            else if (medallion == 0 && _first > 0) _first--;
            _replayLens = true;
            GoTo(Level.Artifact);
        }

        /// <summary>One level up: feeding back to the artifact, the artifact back to the hoard.</summary>
        public void Back() => GoTo(_level == Level.Feed ? Level.Artifact : Level.Hoard);

        void GoTo(Level level)
        {
            if (level != Level.Hoard && PickedIndex() < 0) level = Level.Hoard;
            if (level == Level.Hoard) _replayLens = true;
            _level = level;
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

            int visible = Mathf.Max(1, medallions.Length);
            _first = Mathf.Clamp(_first, 0, Mathf.Max(0, _hoard.Count - visible));
            if (picked >= 0 && picked < _first) _first = picked;
            else if (picked >= _first + visible) _first = picked - visible + 1;

            bool anyPicked = _level != Level.Hoard;
            for (int i = 0; i < medallions.Length; i++)
            {
                if (!medallions[i]) continue;
                int index = _first + i;
                if (index >= _hoard.Count) { medallions[i].Draw(false, null, null, false, anyPicked); continue; }
                var a = _hoard[index];
                var kind = Kind(a);
                medallions[i].Draw(true, Look(a)?.glyph, kind ? kind.DisplayName : DisplayName(a), anyPicked && index == picked, anyPicked);
            }

            bool empty = _hoard.Count == 0;
            KitVisibility.SetShown(emptyGroup, empty);
            KitVisibility.SetShown(hintGroup, !empty && _level == Level.Hoard);
            KitVisibility.SetShown(detailGroup, _level != Level.Hoard);
            KitVisibility.SetShown(actionsGroup, _level == Level.Artifact);
            KitVisibility.SetShown(feedGroup, _level == Level.Feed);
            KitVisibility.SetShown(backGroup, _level != Level.Hoard);
            SetButton(feedItButton, _level == Level.Artifact);
            SetButton(giveButton, _level == Level.Artifact);
            SetButton(backButton, _level != Level.Hoard);
            SetButton(feedButton, _level == Level.Feed);
            SetButton(omniButton, _level == Level.Feed);

            var current = picked >= 0 ? _hoard[picked] : null;
            var currentKind = current != null ? Kind(current) : null;
            if (trailText)
            {
                string template = _level switch { Level.Feed => trailFeed, Level.Artifact => trailArtifact, _ => trailHoard };
                trailText.text = TemplateText.Fill(template, "kind", currentKind ? currentKind.DisplayName : string.Empty);
            }

            if (current == null)
            {
                if (lens) lens.Hide();
                return;
            }
            DrawDetail(current, currentKind);
            DrawLens(current, currentKind);
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

        void DrawLens(ArtifactRecord artifact, ArtifactSO kind)
        {
            if (!lens) return;
            if (_level == Level.Hoard) { lens.Hide(); return; }
            var levels = artifact.Levels();
            for (int i = 0; i < TrackOrder.Length; i++)
            {
                var e = TrackOrder[i];
                var track = kind ? kind.Track(e) : null;
                float lv = levels.Of(e);
                lens.SetArc(i, e, lv, track == null ? string.Empty : Number(track.Value(lv)), track?.label ?? string.Empty,
                    e == artifact.Element);
            }
            var look = Look(artifact);
            int count = 1;
            if (look != null && kind && kind.Track(look.countTrack) is { } countTrack)
                count = Mathf.RoundToInt(countTrack.Value(levels.Of(look.countTrack))) + look.countOffset;
            lens.Show(look?.payload ?? ArtifactLens.Payload.Fan, count, Seed(artifact.Id), _replayLens);
            _replayLens = false;
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

        KindLook Look(ArtifactRecord artifact)
        {
            string id = artifact.Kind();
            foreach (var k in kinds)
                if (k != null && k.kindId == id) return k;
            return null;
        }

        Sprite CrystalSprite(Element element)
        {
            int i = (int)element;
            return i >= 0 && i < crystalSprites.Length ? crystalSprites[i] : null;
        }

        static string Aspect(ArtifactRecord artifact, ArtifactTrack track) =>
            !string.IsNullOrEmpty(artifact.Aspect) ? artifact.Aspect : track?.label ?? string.Empty;

        /// <summary>A stable seed from an artifact's id (FNV-1a), so its field of prisms is the same on every visit.</summary>
        static int Seed(string id)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in id ?? string.Empty) h = (h ^ ch) * 16777619;
                return (int)h;
            }
        }

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

        /// <summary>A level on the 0-10 band: one decimal on the lens's page (the curve still moves it by hundredths).</summary>
        static string LevelWord(float v) => v.ToString("0.0#", CultureInfo.InvariantCulture);

        /// <summary>What a feed added; never "+0.00" for a real gain too small to show.</summary>
        static string Gain(float v) => v > 0f && v < 0.005f ? "<0.01" : v.ToString("0.00", CultureInfo.InvariantCulture);

        static string Number(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
