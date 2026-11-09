using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyage window's right column in the picked look (Shombith, 2026-11-08: Rev 71 C, the Coverflow, with the
    /// Rev 74 C3 "Place" card face, and the other windows of Rev 77; Docs/MENU_REDESIGN_B.md sections 148-151). It
    /// stands over the voyage cards' column and holds four views:
    /// <list type="bullet">
    /// <item>the voyage: the coverflow of voyages (each card a muted close-up of its place, <see cref="VoyagePlaceArt"/>,
    /// and the Ark escort as a card of its own), the picked voyage's numbers (or the escort's difficulty), the squad (its
    /// presets, its three hulls - a press on a hull opens the vessel strip on that seat and one press picks - and who
    /// flies what), Ark crew, Story and Set sail;</item>
    /// <item>Ark crew: the squad's three seats and four settings each (vessel, pilot, element, artifact) and its domain.
    /// A press on a setting opens a strip of every choice; one press picks (it used to cycle one choice per press: up to
    /// 22 presses for a hull). The Ark's own rules decide what may change (<c>SquadsPage</c>: your own seat, and the AI
    /// or open seats of a squad you made), and Save squad keeps it;</item>
    /// <item>the story: one beat at a time over the place;</item>
    /// <item>under way: the place, where the voyage stands, the squad flying it, Set sail dark until it is over.</item>
    /// </list>
    /// <see cref="VoyageJobsView"/> builds it once and fills it; every word is the voyage content's
    /// (<see cref="VoyageLines"/>) or the game's data, never this class's.
    ///
    /// <para><b>Pilots are people, artifacts one of a kind</b> (Garrett 2026-11-07, Docs/Pilots/INVENTORY.md). A seat's
    /// pilot strip lists the people in the player's crew whose callsign can fly the hull, its artifact strip the hoard's
    /// artifacts the hull can carry (and none); each writes the record's own id into the slot, as the Ark's picker
    /// does. A joined squad's other seats carry the owner's ids, so they show the callsign and kind from the catalog.</para>
    /// </summary>
    public class VoyageOpenCard : MonoBehaviour
    {
        /// <summary>One card on the coverflow.</summary>
        public sealed class Card
        {
            public string Name;
            public Texture Art;
        }

        /// <summary>What the column shows (filled by <see cref="VoyageJobsView"/> from the jobs, their places and courses).</summary>
        public sealed class Model
        {
            public string Kicker, Title, CourseValue, SailValue, IntensityValue, PlaceName, CrewVoyage;
            public Texture PlaceArt;
            public readonly List<Card> Flow = new();
            public int Picked;
            /// <summary>The picked card is the Ark escort: its difficulty stands where a voyage's numbers do.</summary>
            public bool Escort;
            public string Difficulty;
            public Action<int> StepDifficulty;
            /// <summary>A voyage (or the escort) is out: the column shows where it stands.</summary>
            public bool Underway;
            public string UnderwayLine, UnderwayCourse;
            public readonly List<VoyageCardRules.Beat> Beats = new();
            public Action SetSail;
        }

        // The style guide's tokens (Docs/UI/froglet-tokens.json, Deep Sea).
        static readonly Color Light = new(0.902f, 0.914f, 1f, 1f);          // text
        static readonly Color Soft = new(0.455f, 0.482f, 0.678f, 1f);       // text-soft
        static readonly Color Line = new(0.263f, 0.298f, 0.537f, 1f);       // line
        static readonly Color Deep = new(0f, 0.004f, 0.039f, 1f);           // ground-deep
        static readonly Color Plate = new(0f, 0.016f, 0.122f, 0.78f);       // ground, glass
        static readonly Color Selection = new(0f, 0.831f, 1f, 1f);          // selection
        static readonly Color Lime = new(0.6f, 1f, 0.502f, 1f);             // cta
        static readonly Color OnLime = new(0f, 0.016f, 0.122f, 1f);         // text on a fill
        static readonly Color Clear = new(0f, 0f, 0f, 0f);
        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        static readonly Domains[] DomainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold };

        // Sized so the voyage view fits the window's right column (about 640 px on a 1080p canvas, seen 2026-11-09).
        const float FlowHeight = 300f, CardWidth = 320f, CardHeight = 270f;

        /// <summary>A card on the coverflow was picked (by a press, or the D-pad's selection landing on it).</summary>
        public event Action<int> Pick;
        /// <summary>The squad changed and was saved: the window redraws (the seated pilots decide the voyages on offer).</summary>
        public event Action SquadChanged;

        VoyageLines _lines;
        SO_VesselList _vessels;
        PilotArtifactCatalogSO _catalog;
        Sprite[] _elementShapes;
        TMP_Text _font;
        Button _buttonFrom;
        RectTransform _root, _panel;
        Model _model;
        int _level = 1, _beat;
        // Ark crew edits a copy of the active squad, as the Ark does (SquadsPage's draft): Save squad writes it back, and
        // leaving the column or picking another squad drops it.
        SavedSquad _draft;
        string _draftId;   // the squad the draft was copied from: matched by id, never by its place in the book
        bool Drafting => _draft != null;

        // Views.
        GameObject _l0, _l1, _l2, _l3;
        RectTransform _flow;
        readonly List<VoyageFlowCard> _cards = new();
        TMP_Text _kicker, _title1, _course, _sail, _intensity, _escortKicker, _difficulty, _crewLine, _crewLine0;
        GameObject _numbers, _escort;
        readonly List<(Button b, TMP_Text t, Image fill)> _presets = new();
        TMP_Text _noSquad;
        readonly (Button b, Image hull, Outline edge)[] _formation = new (Button, Image, Outline)[3];
        readonly Image[] _formation0 = new Image[3];
        Button _sail1, _sail3;
        RawImage _placeArt0, _placeArt3;
        TMP_Text _placeName0, _underwayLine, _underwayCourse, _sailAfter;
        TMP_Text _crewVoyage, _crewName, _crewNote, _saveLabel;
        Button _save;
        readonly (Button b, TMP_Text t, Image fill)[] _domains = new (Button, TMP_Text, Image)[3];
        readonly Seat[] _seats = new Seat[3];
        TMP_Text _storyKicker, _storyTitle, _beatLabel, _beatLine, _beatBy;
        readonly List<Image> _dots = new();

        // The strip of choices, over the top of the column.
        RectTransform _strip;
        TMP_Text _stripCaption;
        RectTransform _stripTiles;
        readonly List<(Button b, Image icon, TMP_Text label, Outline edge)> _tiles = new();
        readonly List<Choice> _choices = new();
        int _stripSeat = -1, _stripSetting = -1;
        bool _stripSaves;

        // Read for the strips (reused, not allocated per press).
        readonly List<PilotRecord> _crew = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<string> _ids = new();

        sealed class Seat
        {
            public TMP_Text Who;
            public Image Hull;
            public Outline Edge;
            public readonly (Button b, TMP_Text label, TMP_Text value, Outline edge)[] Settings = new (Button, TMP_Text, TMP_Text, Outline)[4];
        }

        struct Choice
        {
            public string Value, Label;
            public Sprite Icon;
        }

        // ── Building ─────────────────────────────────────────────────────────

        /// <summary>The column, built once over <paramref name="column"/>, its words from <paramref name="lines"/>, its text
        /// in <paramref name="font"/>'s face, Set sail a copy of <paramref name="button"/>, the element choices drawn with
        /// <paramref name="elementShapes"/> (the Codex's four shapes, Charge to Time; empty draws the names alone).</summary>
        public static VoyageOpenCard Create(RectTransform column, TMP_Text font, Button button, SO_VesselList vessels,
            VoyageLines lines, Sprite[] elementShapes)
        {
            var go = new GameObject("OpenVoyage", typeof(RectTransform));
            go.transform.SetParent(column.parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = column.anchorMin;
            rt.anchorMax = column.anchorMax;
            rt.pivot = column.pivot;
            rt.anchoredPosition = column.anchoredPosition;
            rt.sizeDelta = column.sizeDelta;
            var card = go.AddComponent<VoyageOpenCard>();
            card._root = rt;
            card._font = font;
            card._buttonFrom = button;
            card._vessels = vessels;
            card._lines = lines;
            card._elementShapes = elementShapes;
            card._catalog = PilotArtifactCatalogSO.Load();
            card.Build();
            // Closing the window drops an unsaved crew change, as closing the Ark's edit does.
            card._window = column.GetComponentInParent<ModalWindowManager>(true);
            if (card._window) card._window.OnModalClosed += card.WindowClosed;
            go.SetActive(false);
            return card;
        }

        ModalWindowManager _window;

        void OnDestroy()
        {
            if (_window) _window.OnModalClosed -= WindowClosed;
        }

        void WindowClosed()
        {
            DropDraft();
            CloseStrip();
            if (_model != null) Draw();
        }

        void Build()
        {
            _panel = Node("Panel", _root);
            Stretch(_panel);
            var content = Node("Content", _panel);
            Stretch(content);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(0, 0, 0, 0);
            v.spacing = 0;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _l0 = Level(content, "Underway");
            _l1 = Level(content, "Voyage");
            _l2 = Level(content, "ArkCrew");
            _l3 = Level(content, "Story");
            BuildUnderway(_l0.transform);
            BuildVoyage(_l1.transform);
            BuildCrew(_l2.transform);
            BuildStory(_l3.transform);
            BuildStrip();
        }

        void BuildVoyage(Transform l)
        {
            _flow = Node("Flow", l);
            Height(_flow, FlowHeight);
            _flow.gameObject.AddComponent<RectMask2D>();

            Spacer(l, 8f);
            _kicker = Text(l, 14, Soft, TextAlignmentOptions.Left);
            Height(_kicker, 22);
            _title1 = Text(l, 28, Light, TextAlignmentOptions.Left);
            Height(_title1, 38);
            var nums = Row(l, 40, 34);
            _numbers = nums.gameObject;
            _course = Text(nums, 28, Light, TextAlignmentOptions.Left, 180);
            _sail = Text(nums, 28, Light, TextAlignmentOptions.Left, 160);
            _intensity = Text(nums, 28, Light, TextAlignmentOptions.Left, 240);
            Spacer(nums);
            var esc = Row(l, 44, 14);
            _escort = esc.gameObject;
            _escortKicker = Text(esc, 14, Soft, TextAlignmentOptions.Left);
            Flex(_escortKicker);
            MakeButton(esc, _lines.CardPrevious, 22, Plate, () => _model?.StepDifficulty?.Invoke(-1), 52);
            _difficulty = Text(esc, 22, Light, TextAlignmentOptions.Center, 200);
            MakeButton(esc, _lines.CardNext, 22, Plate, () => _model?.StepDifficulty?.Invoke(+1), 52);
            Spacer(l, 6f);

            var presets = Row(l, 40, 10);
            for (int i = 0; i < SquadBook.MaxSquads; i++)
            {
                int index = i;
                _presets.Add(MakeButton(presets, string.Empty, 14, Plate, () => PickSquad(index), 170));
            }
            _noSquad = Text(presets, 15, Soft, TextAlignmentOptions.Left);
            _noSquad.text = _lines.CardNoSquad ?? string.Empty;
            Flex(_noSquad);
            Spacer(l, 10f);
            var form = Row(l, 76, 10);
            for (int i = 0; i < 3; i++)
            {
                int seat = i;
                var b = MakeButton(form, string.Empty, 14, Clear, () => OpenStrip(seat, 0, true), 0);
                Flex(b.b);
                var hull = Node("Hull", b.b.transform);
                Stretch(hull);
                hull.offsetMin = new Vector2(6, 6);
                hull.offsetMax = new Vector2(-6, -6);
                var img = hull.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                var edge = b.b.gameObject.AddComponent<Outline>();
                edge.effectDistance = new Vector2(2, -2);
                edge.effectColor = Clear;
                _formation[i] = (b.b, img, edge);
            }
            _crewLine = Text(l, 15, Light, TextAlignmentOptions.Left);
            Height(_crewLine, 24);
            Spacer(l, true);
            // Ark crew, Story and the one call to action share the foot.
            var buttons = Row(l, 56, 10);
            Flex(MakeButton(buttons, _lines.CardCrew, 14, Plate, () => ShowLevel(2), 0).b);
            Flex(MakeButton(buttons, _lines.CardStory, 14, Plate, () => ShowLevel(3), 0).b);
            _sail1 = BigButton(buttons, _lines.CardSetSail, () => _model?.SetSail?.Invoke());
            Le(_sail1).flexibleWidth = 2f;
        }

        void BuildUnderway(Transform l)
        {
            PlacePanel(l, 360, out _placeArt0, out var words);
            _placeName0 = Text(words, 48, Light, TextAlignmentOptions.BottomLeft);
            Height(_placeName0, 56);
            Spacer(l, 18f);
            _underwayLine = Text(l, 22, Light, TextAlignmentOptions.Left);
            Height(_underwayLine, 30);
            _underwayCourse = Text(l, 15, Soft, TextAlignmentOptions.Left);
            Height(_underwayCourse, 24);
            Spacer(l, 18f);
            var form = Row(l, 76, 10);
            for (int i = 0; i < 3; i++)
            {
                var h = Node("Hull", form);
                Flex(h);
                _formation0[i] = h.gameObject.AddComponent<Image>();
                _formation0[i].preserveAspect = true;
                _formation0[i].raycastTarget = false;
            }
            _crewLine0 = Text(l, 15, Light, TextAlignmentOptions.Left);
            Height(_crewLine0, 24);
            Spacer(l, true);
            var dark = MakeButton(l, _lines.CardSailAfter, 18, Plate, () => { }, 0);
            dark.b.interactable = false;
            dark.t.color = Soft;
            Height(dark.b, 56);
            _sailAfter = dark.t;
        }

        void BuildCrew(Transform l)
        {
            _crewVoyage = Text(l, 14, Soft, TextAlignmentOptions.Left);
            Height(_crewVoyage, 22);
            var head = Row(l, 48, 12);
            _crewName = Text(head, 28, Light, TextAlignmentOptions.Left, 260);
            string[] names = { _lines.CardDomainJade, _lines.CardDomainRuby, _lines.CardDomainGold };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _domains[i] = MakeButton(head, names[i], 14, Plate, () => PickDomain(DomainOrder[index]), 110);
            }
            Spacer(head);
            _crewNote = Text(l, 15, Soft, TextAlignmentOptions.Left);
            Height(_crewNote, 24);
            Spacer(l, 8f);
            string[] settings = { _lines.CardSettingVessel, _lines.CardSettingPilot, _lines.CardSettingElement, _lines.CardSettingArtifact };
            for (int s = 0; s < 3; s++)
            {
                var row = Node("Seat" + s, l);
                Height(row, 154);
                Fill(row, Plate);
                var seat = _seats[s] = new Seat();
                seat.Edge = row.gameObject.AddComponent<Outline>();
                seat.Edge.effectColor = Line;
                seat.Edge.effectDistance = new Vector2(1, -1);
                seat.Who = Text(row, 14, Soft, TextAlignmentOptions.TopLeft);
                var wrt = seat.Who.rectTransform;
                wrt.anchorMin = new Vector2(0f, 1f);
                wrt.anchorMax = new Vector2(0.34f, 1f);
                wrt.pivot = new Vector2(0f, 1f);
                wrt.offsetMin = new Vector2(16f, -34f);
                wrt.offsetMax = new Vector2(0f, -12f);
                var hull = Node("Hull", row);
                hull.anchorMin = new Vector2(0f, 0f);
                hull.anchorMax = new Vector2(0.34f, 1f);
                hull.offsetMin = new Vector2(12f, 12f);
                hull.offsetMax = new Vector2(-8f, -40f);
                seat.Hull = hull.gameObject.AddComponent<Image>();
                seat.Hull.preserveAspect = true;
                seat.Hull.raycastTarget = false;
                var grid = Node("Settings", row);
                grid.anchorMin = new Vector2(0.34f, 0f);
                grid.anchorMax = new Vector2(1f, 1f);
                grid.offsetMin = new Vector2(8f, 14f);
                grid.offsetMax = new Vector2(-14f, -14f);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(220f, 60f);
                g.spacing = new Vector2(10f, 6f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = 2;
                for (int k = 0; k < 4; k++)
                {
                    int seatIndex = s, setting = k;
                    var b = MakeButton(grid, string.Empty, 18, Plate, () => OpenStrip(seatIndex, setting, false), 0);
                    b.t.alignment = TextAlignmentOptions.BottomLeft;
                    b.t.margin = new Vector4(14, 0, 10, 10);
                    var label = Text(b.b.transform, 14, Soft, TextAlignmentOptions.TopLeft);
                    label.text = settings[k] ?? string.Empty;
                    label.margin = new Vector4(14, 10, 10, 0);
                    Stretch(label.rectTransform);
                    var edge = b.b.gameObject.AddComponent<Outline>();
                    edge.effectDistance = new Vector2(2, -2);
                    edge.effectColor = Line;
                    seat.Settings[k] = (b.b, label, b.t, edge);
                }
                if (s < 2) Spacer(l, 6f);
            }
            Spacer(l, true);
            var buttons = Row(l, 56, 10);
            var save = MakeButton(buttons, _lines.CardSaveSquad, 18, Lime, SaveSquad, 0);
            save.t.color = OnLime;
            Le(save.b).flexibleWidth = 2f;
            _save = save.b;
            _saveLabel = save.t;
            Flex(MakeButton(buttons, _lines.CardVoyage, 14, Plate, () => ShowLevel(1), 0).b);
        }

        void BuildStory(Transform l)
        {
            _storyKicker = Text(l, 14, Soft, TextAlignmentOptions.Left);
            Height(_storyKicker, 22);
            _storyTitle = Text(l, 36, Light, TextAlignmentOptions.Left);
            Height(_storyTitle, 46);
            Spacer(l, 12f);
            PlacePanel(l, 440, out _placeArt3, out var words);
            _beatLabel = Text(words, 14, Soft, TextAlignmentOptions.Left);
            Height(_beatLabel, 20);
            _beatLine = Text(words, 28, Light, TextAlignmentOptions.BottomLeft);
            _beatLine.textWrappingMode = TextWrappingModes.Normal;
            Height(_beatLine, 120);
            _beatBy = Text(words, 15, Light, TextAlignmentOptions.Left);
            Height(_beatBy, 22);
            Spacer(l, 12f);
            var nav = Row(l, 44, 12);
            MakeButton(nav, _lines.CardPrevious, 22, Plate, () => StepBeat(-1), 56);
            Spacer(nav);
            for (int i = 0; i < 6; i++)
            {
                var d = Node("Dot" + i, nav);
                Width(d, 12);
                Height(d, 4);
                _dots.Add(Fill(d, Line));
            }
            Spacer(nav);
            MakeButton(nav, _lines.CardNext, 22, Plate, () => StepBeat(+1), 56);
            Spacer(l, true);
            var buttons = Row(l, 56, 10);
            _sail3 = BigButton(buttons, _lines.CardSetSail, () => _model?.SetSail?.Invoke());
            Le(_sail3).flexibleWidth = 2f;
            Flex(MakeButton(buttons, _lines.CardVoyage, 14, Plate, () => ShowLevel(1), 0).b);
        }

        /// <summary>A panel filled with a place's close-up; words go in its foot (returned).</summary>
        RectTransform PlacePanel(Transform parent, float height, out RawImage art, out RectTransform words)
        {
            var panel = Node("Place", parent);
            Height(panel, height);
            Fill(panel, Deep);
            panel.gameObject.AddComponent<RectMask2D>();
            var a = Node("Art", panel);
            Stretch(a);
            art = a.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            var edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = Line;
            edge.effectDistance = new Vector2(1, -1);
            words = Node("Words", panel);
            words.anchorMin = new Vector2(0f, 0f);
            words.anchorMax = new Vector2(1f, 0f);
            words.pivot = new Vector2(0.5f, 0f);
            words.offsetMin = new Vector2(30f, 26f);
            words.offsetMax = new Vector2(-30f, 26f);
            var v = words.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.LowerLeft;
            var fit = words.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        void BuildStrip()
        {
            _strip = Node("Strip", _panel);
            _strip.anchorMin = new Vector2(0f, 1f);
            _strip.anchorMax = new Vector2(1f, 1f);
            _strip.pivot = new Vector2(0.5f, 1f);
            _strip.sizeDelta = new Vector2(0f, 236f);
            _strip.anchoredPosition = Vector2.zero;
            var bg = Fill(_strip, new Color(Deep.r, Deep.g, Deep.b, 0.96f));
            bg.raycastTarget = true;
            var edge = _strip.gameObject.AddComponent<Outline>();
            edge.effectColor = Selection;
            edge.effectDistance = new Vector2(1, -1);
            var head = Row(_strip, 30, 10);
            var hrt = head;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.offsetMin = new Vector2(20f, -46f);
            hrt.offsetMax = new Vector2(-14f, -14f);
            _stripCaption = Text(head, 14, Light, TextAlignmentOptions.Left);
            Flex(_stripCaption);
            MakeButton(head, _lines.CardBack, 14, Plate, CloseStrip, 110);
            _stripTiles = Node("Tiles", _strip);
            _stripTiles.anchorMin = Vector2.zero;
            _stripTiles.anchorMax = Vector2.one;
            _stripTiles.offsetMin = new Vector2(16f, 16f);
            _stripTiles.offsetMax = new Vector2(-16f, -58f);
            var h = _stripTiles.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            _strip.gameObject.SetActive(false);
        }

        // ── Showing ──────────────────────────────────────────────────────────

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Show the column on <paramref name="model"/>, on the voyage view unless <paramref name="keepLevel"/>
        /// and the same picked card.</summary>
        public void Show(Model model, bool keepLevel = false)
        {
            bool same = _model != null && model != null && _model.Picked == model.Picked && _model.Title == model.Title;
            _model = model;
            if (!keepLevel || !same)
            {
                _level = 1;
                _beat = 0;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Draw();
        }

        /// <summary>Put the column away (no voyage to show), dropping an unsaved change.</summary>
        public void Hide()
        {
            DropDraft();
            CloseStrip();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void ShowLevel(int level)
        {
            _level = Mathf.Clamp(level, 1, 3);
            CloseStrip();
            Draw();
        }

        void StepBeat(int step)
        {
            if (_model == null) return;
            _beat = VoyageCardRules.Wrap(_beat, step, _model.Beats.Count);
            Draw();
        }

        void Draw()
        {
            if (_model == null) return;
            bool under = _model.Underway;
            _l0.SetActive(under);
            _l1.SetActive(!under && _level == 1);
            _l2.SetActive(!under && _level == 2);
            _l3.SetActive(!under && _level == 3);
            bool sail = _model.SetSail != null;
            if (_sail1) _sail1.interactable = sail;
            if (_sail3) _sail3.interactable = sail;

            DrawFlow();
            Set(_kicker, _model.Kicker);
            Set(_title1, _model.Escort ? string.Empty : _model.Title);
            _numbers.SetActive(!_model.Escort);
            _escort.SetActive(_model.Escort);
            Set(_course, _model.CourseValue);
            Set(_sail, _model.SailValue);
            Set(_intensity, _model.IntensityValue);
            Set(_escortKicker, _model.Kicker);
            Set(_difficulty, _model.Difficulty);

            Place(_placeArt0, _model.PlaceArt);
            Set(_placeName0, _model.PlaceName);
            Set(_underwayLine, _model.UnderwayLine);
            Set(_underwayCourse, _model.UnderwayCourse);
            Set(_sailAfter, _lines.CardSailAfter);

            Set(_crewVoyage, _model.CrewVoyage);
            Set(_storyKicker, _model.Kicker);
            Set(_storyTitle, _model.Title);
            Place(_placeArt3, _model.PlaceArt);
            DrawSquad();

            int beats = _model.Beats.Count;
            if (beats > 0)
            {
                _beat = Mathf.Clamp(_beat, 0, beats - 1);
                var b = _model.Beats[_beat];
                Set(_beatLabel, b.Label);
                Set(_beatLine, b.Line);
                Set(_beatBy, b.By);
            }
            else
            {
                // No beats (the Ark escort): nothing of the last voyage's story stays on screen.
                Set(_beatLabel, string.Empty);
                Set(_beatLine, string.Empty);
                Set(_beatBy, string.Empty);
            }
            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].gameObject.SetActive(i < beats);
                _dots[i].color = i == _beat ? Light : Line;
                var le = _dots[i].GetComponent<LayoutElement>();
                if (le) le.preferredWidth = i == _beat ? 30f : 12f;
            }
        }

        void DrawFlow()
        {
            var flow = _model.Flow;
            while (_cards.Count < flow.Count) _cards.Add(MakeCard(_cards.Count));
            float step = Mathf.Max(200f, _flow.rect.width * 0.33f);
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                bool shown = i < flow.Count;
                c.gameObject.SetActive(shown);
                if (!shown) continue;
                c.Index = i;
                c.Art.texture = flow[i].Art;
                c.Art.uvRect = Cover(flow[i].Art, CardWidth / CardHeight);
                c.Name.text = flow[i].Name ?? string.Empty;
                int offset = i - _model.Picked;
                c.Pose(offset, step);
                var outline = c.Edge.GetComponent<Outline>();
                if (outline) outline.effectColor = offset == 0 ? Selection : Line;
            }
            // The picked card stands in front: the further out, the further back.
            var order = new List<VoyageFlowCard>();
            for (int i = 0; i < flow.Count; i++) order.Add(_cards[i]);
            order.Sort((a, b) => Mathf.Abs(b.Index - _model.Picked).CompareTo(Mathf.Abs(a.Index - _model.Picked)));
            foreach (var c in order) c.transform.SetAsLastSibling();
        }

        VoyageFlowCard MakeCard(int index)
        {
            var rt = Node("Card" + index, _flow);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = Deep;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var edge = rt.gameObject.AddComponent<Outline>();
            edge.effectDistance = new Vector2(2, -2);
            edge.effectColor = Line;
            var a = Node("Art", rt);
            Stretch(a);
            var art = a.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            var name = Text(rt, 28, Light, TextAlignmentOptions.BottomLeft);
            Stretch(name.rectTransform);
            name.margin = new Vector4(24, 0, 20, 22);
            name.textWrappingMode = TextWrappingModes.Normal;
            var card = rt.gameObject.AddComponent<VoyageFlowCard>();
            card.Init(b, art, name, bg);
            card.Index = index;
            card.Picked = i => Pick?.Invoke(i);
            return card;
        }

        /// <summary>The uv rectangle that fills an <paramref name="aspect"/>-shaped frame with <paramref name="tex"/>,
        /// centred, cropping what spills (CSS object-fit: cover).</summary>
        public static Rect Cover(Texture tex, float aspect)
        {
            if (!tex || tex.height == 0 || aspect <= 0f) return new Rect(0f, 0f, 1f, 1f);
            float t = (float)tex.width / tex.height;
            if (t > aspect)
            {
                float w = aspect / t;
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            float h = t / aspect;
            return new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }

        static void Place(RawImage img, Texture tex)
        {
            if (!img) return;
            img.texture = tex;
            img.enabled = tex;
            var r = img.rectTransform.rect;
            img.uvRect = Cover(tex, r.height > 1f ? r.width / r.height : 1.5f);
        }

        static void Set(TMP_Text t, string value)
        {
            if (t) t.text = value ?? string.Empty;
        }

        // ── The squad (the Ark's squads) ─────────────────────────────────────

        static SquadBook Book => UGSDataService.Instance?.SquadRepo?.Data?.Book;

        // Never `GetComponent<T>() ?? Add...`: in the Editor a missing component comes back as a fake-null object, which
        // `??` keeps (Unity overloads ==, not ??), so the add never runs and the next setter throws.
        static Outline EdgeOf(Button b) => b.TryGetComponent(out Outline e) ? e : b.gameObject.AddComponent<Outline>();

        /// <summary>Who the published squad record names (the Ark's SquadsPage publishes the same record); empty while
        /// unknown, and then nothing is published from here.</summary>
        public Func<string> PlayerName;

        /// <summary>Keep the book; with <paramref name="publish"/>, also write the public squad record, as the Ark does, so
        /// the friends in the squad (or its owner, for a joined one) see the change.</summary>
        void SaveBook(bool publish = false)
        {
            UGSDataService.Instance?.SquadRepo?.MarkDirty();
            if (!publish || !SquadDirectory.IsReady) return;
            string name = PlayerName?.Invoke();
            if (string.IsNullOrEmpty(name)) return;
            SquadDirectory.PublishAsync(SquadMembership.BuildPublicRecord(Book, name)).Forget();
        }

        void DrawSquad()
        {
            var book = Book;
            var active = Editing(book);
            int count = book?.Squads?.Count ?? 0;
            for (int i = 0; i < _presets.Count; i++)
            {
                bool show = i < count;
                _presets[i].b.gameObject.SetActive(show);
                if (!show) continue;
                var s = book.Squads[i];
                bool on = i == book.ActiveIndex;
                _presets[i].t.text = s?.Name ?? string.Empty;
                _presets[i].t.color = on ? Light : Soft;
                var edge = EdgeOf(_presets[i].b);
                edge.effectDistance = new Vector2(2, -2);
                edge.effectColor = on ? Selection : Line;
            }
            if (_noSquad) _noSquad.gameObject.SetActive(count == 0);
            Set(_crewName, active?.Name);
            Set(_crewNote, active != null && active.IsJoined()
                ? TemplateText.Fill(_lines.CardCrewJoined, "owner", active.OwnerName)
                : _lines.CardCrewNote);
            if (_saveLabel) _saveLabel.text = (Drafting ? _lines.CardSaveSquad : _lines.CardSaved) ?? string.Empty;
            if (_save) _save.interactable = Drafting;

            var palette = ThemeManager.LiveOr(null);
            for (int i = 0; i < 3; i++)
            {
                var d = DomainOrder[i];
                bool on = active != null && active.Domain == d;
                _domains[i].b.interactable = active != null && !active.IsJoined();
                _domains[i].t.color = on ? Light : Soft;
                _domains[i].fill.color = palette ? Color.Lerp(Plate, palette.GetDomainSignalColor(d), on ? 0.35f : 0.12f) : Plate;
                var edge = EdgeOf(_domains[i].b);
                edge.effectDistance = new Vector2(2, -2);
                edge.effectColor = on ? Selection : Line;
            }

            string[] roles = { _lines.CardSeatLeader, _lines.CardSeatRogueOne, _lines.CardSeatRogueTwo };
            var line = new System.Text.StringBuilder();
            for (int seat = 0; seat < 3; seat++)
            {
                var slot = active != null ? active.GetSlot(seat) : default;
                var vessel = active != null ? Vessel(slot.Vessel) : null;
                var sprite = Hull(vessel);
                _formation[seat].hull.sprite = sprite;
                _formation[seat].hull.enabled = sprite;
                _formation[seat].b.interactable = active != null && CanEdit(active, seat);
                _formation[seat].edge.effectColor = _stripSaves && _stripSeat == seat ? Selection : Clear;
                _formation0[seat].sprite = sprite;
                _formation0[seat].enabled = sprite;
                if (active != null && vessel)
                {
                    if (line.Length > 0) line.Append(_lines.CardCrewSeparator);
                    line.Append(TemplateText.Fill(_lines.CardCrewSeat, "who", Who(active, seat, slot), "vessel", vessel.CardName));
                }

                var view = _seats[seat];
                Set(view.Who, active == null ? string.Empty
                    : TemplateText.Fill(_lines.CardSeatWho, "role", roles[seat], "who", Who(active, seat, slot)));
                view.Hull.sprite = sprite;
                view.Hull.enabled = sprite;
                view.Edge.effectColor = !_stripSaves && _stripSeat == seat ? Selection : Line;
                bool editable = active != null && CanEdit(active, seat);
                var keywords = vessel ? vessel.Keywords : null;
                var pilot = active != null && _catalog ? _catalog.ResolvePilot(slot, keywords) : null;
                var artifact = active != null && _catalog ? _catalog.ResolveArtifact(slot, keywords) : null;
                // The catalog gives the callsign and kind for anyone's ids; only the player's own name a person and an
                // artifact this inventory holds (SquadsPage.IdsAreMine).
                PilotRecord person = null;
                ArtifactRecord held = null;
                if (editable && pilot && PilotLoadoutStore.TryGetPilot(slot.PilotId, out var p) && p.CallsignOrTemplate() == pilot.Id) person = p;
                if (editable && artifact && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var a) && a.Kind() == artifact.Id) held = a;
                string word = pilot ? Callsigns.WordOf(pilot.Vision) : string.Empty;
                Set(view.Settings[1].label, string.IsNullOrEmpty(word) ? _lines.CardSettingPilot
                    : TemplateText.Fill(_lines.CardSettingWord, "setting", _lines.CardSettingPilot, "word", word));
                string[] values =
                {
                    vessel ? vessel.CardName : string.Empty,
                    person != null ? person.DisplayName() : pilot ? pilot.DisplayName : string.Empty,
                    ElementName(slot.Element),
                    held != null ? held.Name : artifact ? artifact.DisplayName : _lines.CardNoArtifact,
                };
                for (int k = 0; k < 4; k++)
                {
                    Set(view.Settings[k].value, active == null ? string.Empty : values[k]);
                    view.Settings[k].b.interactable = editable;
                    view.Settings[k].edge.effectColor = !_stripSaves && _stripSeat == seat && _stripSetting == k ? Selection : Line;
                }
            }
            Set(_crewLine, line.ToString());
            Set(_crewLine0, line.ToString());
        }

        void PickSquad(int index)
        {
            var book = Book;
            if (book == null || index == book.ActiveIndex) return;
            if (!book.TrySetActive(index)) return;
            DropDraft();
            CloseStrip();
            SaveBook();
            // The pilots seated in the squad decide which voyages are offered: the window redraws.
            SquadChanged?.Invoke();
            Draw();
        }

        void PickDomain(Domains domain)
        {
            var book = Book;
            var active = Editing(book);
            if (active == null || active.IsJoined() || active.Domain == domain) return;
            Draft(book).Domain = domain;
            Draw();
        }

        // The Ark's rules (SquadsPage.IsMySeat / CanEdit): your own card, and the AI or open cards of a squad you made.
        static bool IsMySeat(SavedSquad squad, int seat) => squad.IsJoined() ? seat == squad.MySeat : seat == 0;

        static bool CanEdit(SavedSquad squad, int seat) =>
            squad != null && (IsMySeat(squad, seat) || (!squad.IsJoined() && seat > 0 && !squad.GetSlot(seat).IsPlayer()));

        string Who(SavedSquad squad, int seat, SquadSlot slot)
        {
            if (IsMySeat(squad, seat)) return _lines.CardSeatYou;
            if (slot.IsOpen()) return _lines.CardSeatOpen;
            if (slot.IsPlayer()) return slot.MemberName;
            return _lines.CardSeatAi;
        }

        /// <summary>The squad being edited: the draft, made from the active squad on the first change.</summary>
        SavedSquad Draft(SquadBook book)
        {
            if (_draft == null)
            {
                var active = book.GetActive();
                _draft = SquadMembership.Copy(active);
                _draftId = active?.Id;
            }
            return _draft;
        }

        // ── The strip of choices (one press picks) ───────────────────────────

        /// <summary>Open the strip of every choice for <paramref name="seat"/>'s <paramref name="setting"/> (0 vessel,
        /// 1 pilot, 2 element, 3 artifact). From the voyage view the pick is saved at once (<paramref name="saves"/>); in
        /// Ark crew it waits for Save squad. Pressing the same setting again shuts it.</summary>
        void OpenStrip(int seat, int setting, bool saves)
        {
            if (_stripSeat == seat && _stripSetting == setting && _stripSaves == saves && _strip.gameObject.activeSelf)
            {
                CloseStrip();
                return;
            }
            var book = Book;
            var squad = Editing(book);
            if (squad == null || !CanEdit(squad, seat)) return;
            Choices(squad, seat, setting);
            if (_choices.Count == 0) return;
            _stripSeat = seat;
            _stripSetting = setting;
            _stripSaves = saves;
            string[] roles = { _lines.CardSeatLeader, _lines.CardSeatRogueOne, _lines.CardSeatRogueTwo };
            string[] settings = { _lines.CardSettingVessel, _lines.CardSettingPilot, _lines.CardSettingElement, _lines.CardSettingArtifact };
            Set(_stripCaption, TemplateText.Fill(_lines.CardPickSetting, "seat", roles[seat], "setting", settings[setting]));
            string current = Current(squad.GetSlot(seat), setting);
            while (_tiles.Count < _choices.Count) _tiles.Add(MakeTile(_tiles.Count));
            for (int i = 0; i < _tiles.Count; i++)
            {
                bool shown = i < _choices.Count;
                _tiles[i].b.gameObject.SetActive(shown);
                if (!shown) continue;
                var c = _choices[i];
                _tiles[i].icon.sprite = c.Icon;
                _tiles[i].icon.enabled = c.Icon;
                _tiles[i].label.text = c.Label ?? string.Empty;
                _tiles[i].edge.effectColor = c.Value == current ? Selection : Clear;
            }
            _strip.gameObject.SetActive(true);
            _strip.SetAsLastSibling();
            Draw();
        }

        void CloseStrip()
        {
            _stripSeat = _stripSetting = -1;
            if (_strip) _strip.gameObject.SetActive(false);
        }

        (Button b, Image icon, TMP_Text label, Outline edge) MakeTile(int index)
        {
            var b = MakeButton(_stripTiles, string.Empty, 15, Clear, () => PickChoice(index), 0);
            var icon = Node("Icon", b.b.transform);
            icon.anchorMin = new Vector2(0f, 0.28f);
            icon.anchorMax = new Vector2(1f, 1f);
            icon.offsetMin = new Vector2(8f, 0f);
            icon.offsetMax = new Vector2(-8f, -8f);
            var img = icon.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            b.t.alignment = TextAlignmentOptions.Bottom;
            b.t.margin = new Vector4(4, 0, 4, 8);
            b.t.textWrappingMode = TextWrappingModes.Normal;
            var edge = b.b.gameObject.AddComponent<Outline>();
            edge.effectDistance = new Vector2(2, -2);
            edge.effectColor = Clear;
            return (b.b, img, b.t, edge);
        }

        void PickChoice(int index)
        {
            if (index < 0 || index >= _choices.Count || _stripSeat < 0) return;
            int seat = _stripSeat, setting = _stripSetting;
            bool saves = _stripSaves;
            CloseStrip();
            SetSetting(seat, setting, _choices[index].Value);
            if (saves) SaveSquad();
            else Draw();
        }

        /// <summary>The value a choice carries for <paramref name="slot"/>'s <paramref name="setting"/> as it stands.</summary>
        static string Current(SquadSlot slot, int setting) => setting switch
        {
            0 => slot.Vessel.ToString(),
            1 => PilotLoadoutStore.TryGetPilot(slot.PilotId, out var p) ? p.Id : slot.PilotId,
            2 => slot.Element.ToString(),
            _ => PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var a) ? a.Id : string.Empty,
        };

        /// <summary>Every choice the seat's setting offers, as the Ark's picker offers them.</summary>
        void Choices(SavedSquad squad, int seat, int setting)
        {
            _choices.Clear();
            var slot = squad.GetSlot(seat);
            var vessel = Vessel(slot.Vessel);
            switch (setting)
            {
                case 0:
                    if (_vessels && _vessels.VesselList != null)
                        foreach (var v in _vessels.VesselList)
                            if (v && !v.IsLocked)
                                _choices.Add(new Choice { Value = v.Class.ToString(), Label = v.CardName, Icon = Hull(v) });
                    break;
                case 1:
                    // The crew's people whose callsign can fly the hull, in crew order.
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetCrew(_crew);
                    foreach (var person in _crew)
                        if (_catalog.TryGetPilot(person.Id, out var callsign) && callsign && callsign.Requirement != null
                            && callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                            _choices.Add(new Choice { Value = person.Id, Label = person.DisplayName(), Icon = callsign.Portrait ? callsign.Portrait : callsign.Icon });
                    break;
                case 2:
                    for (int i = 0; i < Elements.Length; i++)
                        _choices.Add(new Choice
                        {
                            Value = Elements[i].ToString(), Label = ElementName(Elements[i]),
                            Icon = _elementShapes != null && i < _elementShapes.Length ? _elementShapes[i] : null,
                        });
                    break;
                default:
                    // The hoard's artifacts the hull can carry: each is one of a kind, so every seat this column edits (the
                    // player's own, or an AI or open seat of a squad they made) carries only what is held. Empty is none.
                    _choices.Add(new Choice { Value = string.Empty, Label = _lines.CardNoArtifact });
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetHoard(_hoard);
                    foreach (var artifact in _hoard)
                        if (_catalog.TryGetArtifact(artifact.Id, out var kind) && kind && kind.Requirement != null
                            && kind.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                            _choices.Add(new Choice { Value = artifact.Id, Label = artifact.Name, Icon = kind.Icon });
                    break;
            }
        }

        /// <summary>Set <paramref name="seat"/>'s <paramref name="setting"/> to <paramref name="value"/> in the draft, then
        /// keep each card on its own vessel and every seat piloted, as the Ark does.</summary>
        void SetSetting(int seat, int setting, string value)
        {
            var book = Book;
            var active = book?.GetActive();
            if (active == null || !CanEdit(active, seat)) return;
            var squad = Draft(book);
            var slot = squad.GetSlot(seat);
            string previousPilot = slot.PilotId;
            bool mine = IsMySeat(squad, seat);
            switch (setting)
            {
                case 0:
                    if (!Enum.TryParse(value, out VesselClassType cls) || cls == slot.Vessel) return;
                    slot = slot.WithVessel(cls);
                    // The player's own card starts on the person they keep in that vessel's Hangar seat (a crew id).
                    if (mine) slot.PilotId = PilotLoadoutStore.GetPilot(cls);
                    break;
                case 1:
                    slot.PilotId = value;
                    break;
                case 2:
                    if (!Enum.TryParse(value, out Element element)) return;
                    slot.Element = element;
                    break;
                default:
                    slot.ArtifactId = string.IsNullOrEmpty(value) ? null : value;
                    break;
            }
            squad.SetSlot(seat, slot);
            // Each vessel has its own pilot and artifact: one moved onto this vessel leaves the others (the pilot swaps).
            squad.KeepCardsOwn(seat, previousPilot);
            // Every vessel needs a pilot: an empty or doubled seat takes the next free person of the crew.
            // A legacy callsign id on one of the player's cards becomes the person it resolves to first, so the
            // "another seat flies them" test compares people (SquadsPage.FillPilots).
            for (int s = 0; s < SavedSquad.SlotCount; s++)
            {
                if (!CanEdit(squad, s)) continue;
                var own = squad.GetSlot(s);
                if (own.UsesDefaultPilot() || !PilotLoadoutStore.TryGetPilot(own.PilotId, out var person) || person.Id == own.PilotId) continue;
                own.PilotId = person.Id;
                squad.SetSlot(s, own);
            }
            PilotLoadoutStore.GetCrew(_crew);
            _ids.Clear();
            foreach (var p in _crew) if (!string.IsNullOrEmpty(p.Id)) _ids.Add(p.Id);
            squad.FillPilots(_ids);
        }

        /// <summary>The squad shown: the unsaved copy while one is being edited, else the active squad.</summary>
        SavedSquad Editing(SquadBook book) =>
            _draft != null && DraftIsOf(book?.GetActive()) ? _draft : book?.GetActive();

        bool DraftIsOf(SavedSquad squad) => squad != null && !string.IsNullOrEmpty(_draftId) && squad.Id == _draftId;

        void DropDraft()
        {
            _draft = null;
            _draftId = null;
        }

        void SaveSquad()
        {
            var book = Book;
            if (_draft == null || book?.Squads == null) return;
            int at = book.Squads.FindIndex(DraftIsOf);
            if (at >= 0) book.Squads[at] = _draft;
            DropDraft();
            SaveBook(publish: at >= 0);
            SquadChanged?.Invoke();
            Draw();
        }

        SO_Vessel Vessel(VesselClassType cls) =>
            _vessels && _vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        static Sprite Hull(SO_Vessel v) =>
            !v ? null : v.CardSilohoutteActive ? v.CardSilohoutteActive : v.SquadImage ? v.SquadImage : v.IconActive;

        string ElementName(Element e) => e switch
        {
            Element.Charge => _lines.ElementCharge,
            Element.Mass => _lines.ElementMass,
            Element.Space => _lines.ElementSpace,
            Element.Time => _lines.ElementTime,
            _ => string.Empty,
        };

        // ── Small builders ───────────────────────────────────────────────────

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image Fill(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static LayoutElement Le(Component c) =>
            c.TryGetComponent(out LayoutElement le) ? le : c.gameObject.AddComponent<LayoutElement>();

        static void Height(Component c, float h)
        {
            var le = Le(c);
            le.minHeight = le.preferredHeight = h;
        }

        static void Width(Component c, float w)
        {
            var le = Le(c);
            le.minWidth = le.preferredWidth = w;
        }

        static void Flex(Component c) => Le(c).flexibleWidth = 1f;

        static void Spacer(Transform parent, bool vertical = false)
        {
            var s = Node("Space", parent);
            var le = s.gameObject.AddComponent<LayoutElement>();
            if (vertical) le.flexibleHeight = 1f;
            else le.flexibleWidth = 1f;
        }

        static void Spacer(Transform parent, float height)
        {
            var s = Node("Gap", parent);
            Height(s, height);
        }

        static RectTransform Row(Transform parent, float height, float spacing)
        {
            var r = Node("Row", parent);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            // A button has no height of its own (its Image's preferred height is 0), so a row that controls its
            // children's height without stretching them drew every button 0 px high: the presets, the hulls, Ark crew
            // and Story never showed (2026-11-09, seen in the editor).
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            Height(r, height);
            return r;
        }

        static GameObject Level(Transform parent, string name)
        {
            var c = Node(name, parent);
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 0;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            Le(c).flexibleHeight = 1f;
            return c.gameObject;
        }

        TMP_Text Text(Transform parent, float size, Color color, TextAlignmentOptions align, float width = -1f)
        {
            var rt = Node("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font)
            {
                t.font = _font.font;
                t.fontSharedMaterial = _font.fontSharedMaterial;
            }
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            if (width > 0f) Width(t, width);
            return t;
        }

        (Button b, TMP_Text t, Image fill) MakeButton(Transform parent, string label, float size, Color fill, Action onClick, float width)
        {
            var rt = Node("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = Text(rt, size, Light, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.text = label ?? string.Empty;
            if (width > 0f) Width(rt, width);
            return (b, t, img);
        }

        /// <summary>The column's one call to action (Set sail): a Lime button the width of its place. Not a copy of the
        /// old card's keycap, which kept its own fixed size and drew small in the middle (2026-11-09).</summary>
        Button BigButton(Transform parent, string label, Action onClick)
        {
            var made = MakeButton(parent, label, 20, Lime, onClick, 0);
            made.t.color = OnLime;
            made.b.name = "SetSail";
            var b = made.b;
            var le = Le(b);
            le.flexibleWidth = 1f;
            le.minHeight = le.preferredHeight = 56f;
            return b;
        }
    }
}
