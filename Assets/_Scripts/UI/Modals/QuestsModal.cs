using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The QUESTS WINDOW (Shombith, 2026-11-13: "Quest should be a seperate screen with its own interactive UI not inside
    /// this itself"; picked Frogma Rev 107 D on 2026-11-13, Docs/MENU_REDESIGN_B.md sections 187, 188 and 200). Its own window
    /// over Home, opened from Home's quest card (<see cref="ScreenSwitcher.OnClickQuests"/>); the Voyage window no
    /// longer has a Quests tab (<see cref="VoyageWindowTabs.Drop{T}"/>).
    ///
    /// <para><b>The screen.</b> At the top, "Quests", how many are claimed and a meter, and Back. Across the screen, the
    /// route: each quest's picture over its station on one course, its name and state under it; the stretch to an open
    /// quest is drawn solid, the rest dotted (<see cref="QuestRouteGraphic"/>). Under the route, side by side: the picked
    /// quest and its goals as a ladder ending in Claim; its reward, a colour set, as one prism per domain (the body in
    /// the set's face colour, the edge in its rim colour); and what the claim also opens over the one button, Claim or
    /// Play quest. No episode is named (Shombith: "dont name it Episode 1, its just quest").</para>
    ///
    /// <para><b>The data.</b> The quests and their states are the progression service's
    /// (<see cref="GameModeProgressionService.QuestList"/>, <see cref="QuestFrontier"/>); the goals are each quest's
    /// authored intensity goals (<see cref="QuestsRules.Goals"/>); the reward is the quest's
    /// <see cref="SO_UnlockData.RewardColorSet"/>, named as the colour set catalog names it. Claim calls
    /// <see cref="GameModeProgressionService.ClaimQuestAndUnlockNext"/>, the quest track's own claim; Play quest opens
    /// the Arcade, as the track did. The window changes no quest rule.</para>
    ///
    /// <para><b>Built in code</b> with the Voyage window's kit (<see cref="VoyageKit"/>: its style, faces and soft
    /// buttons; every colour through the worn colour set), by <see cref="ScreenSwitcher"/> at start
    /// (EnsureQuestsModal). Every word is a <see cref="VoyageLines"/> field (Docs/Voyages/voyage_slice.json).</para>
    /// </summary>
    public class QuestsModal : ModalWindowManager
    {
        // The board's measures (Rev 107 D, at 1920 x 1080; the menu's canvas is 1080 tall at every width).
        const float Inset = 64f, HeaderTop = 104f, HeaderHeight = 48f;
        const float RouteTop = 220f, RouteHeight = 380f, RouteMiddleFromTop = 220f;
        const float LowerTop = 640f, LowerBottom = 40f;
        const float ArtW = 200f, ArtH = 112f, ArtPickedW = 280f, ArtPickedH = 158f, ArtGap = 30f;

        VoyageKit _kit;
        bool _built;
        int _picked = -1;
        GameModeProgressionService _heard;
        ScreenSwitcher _switcher;

        RectTransform _routeRoot;
        QuestRouteGraphic _line;
        TMP_Text _title, _progress;
        readonly List<Image> _meter = new();
        readonly List<Station> _stations = new();

        TMP_Text _kicker, _questName, _questState;
        RectTransform _ladder;
        readonly List<Step> _steps = new();
        Image _ladderLine;

        TMP_Text _rewardKicker, _rewardName, _rewardKind, _rewardLine, _rewardState;
        GameObject _rewardGroup;
        readonly List<Prism> _prisms = new();

        GameObject _opens;
        Image _opensArt;
        TMP_Text _opensKicker, _opensName;
        TMP_Text _playNote;
        Button _cta;
        TMP_Text _ctaWords;
        GameObject _wait;
        TMP_Text _waitWords;
        TMP_Text _backWords;

        sealed class Station
        {
            public RectTransform Root, ArtBox;
            public Image Art, Ring, Fill;
            public TMP_Text Name, Line;
            public Image Bar;
            public Button Button;
        }

        sealed class Step
        {
            public GameObject Root;
            public Image Ring, Fill;
            public TMP_Text Text, Tier, Note;
        }

        sealed class Prism
        {
            public Image Rim, Face;
            public TMP_Text Label;
        }

        VoyageLines Lines => _kit?.Lines;
        SO_UnlockList Quests => GameModeProgressionService.Instance ? GameModeProgressionService.Instance.QuestList : null;

        /// <summary>The kit the window is built with (the Voyage window's style, words and face). Called by
        /// <see cref="ScreenSwitcher"/> before the window's first frame.</summary>
        public void Setup(VoyageKit kit) => _kit = kit;

        protected override void Start()
        {
            base.Start();
            Build();
            Listen(true);
            Redraw();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Listen(false);
        }

        void OnEnable()
        {
            if (_built) Listen(true);
        }

        bool _listening;

        void Listen(bool on)
        {
            if (on == _listening) return;
            _listening = on;
            if (on)
            {
                OnModalOpened += Opened;
                ColorTrace.Changed += Redraw;
                _heard = GameModeProgressionService.Instance;
                if (_heard) _heard.OnProgressionChanged += Changed;
            }
            else
            {
                OnModalOpened -= Opened;
                ColorTrace.Changed -= Redraw;
                if (_heard) _heard.OnProgressionChanged -= Changed;
                _heard = null;
            }
        }

        void Changed(GameModeProgressionData _) => Redraw();

        // Every visit opens on the quest to play or claim next.
        void Opened()
        {
            if (!_heard)
            {
                _heard = GameModeProgressionService.Instance;
                if (_heard) _heard.OnProgressionChanged += Changed;
            }
            _picked = -1;
            Redraw();
        }

        // ── Building ───────────────────────────────────────────────────────

        void Build()
        {
            if (_built || _kit == null) return;
            _built = true;
            var root = (RectTransform)transform;

            // The ground: the window's black, and the HyperSea faint behind everything (the quests are its own).
            var ground = VoyageKit.Node("Ground", root);
            VoyageKit.Stretch(ground);
            VoyageKit.Block(ground, _kit.Popup, hit: true);
            var sea = VoyageKit.Node("Sea", root);
            VoyageKit.Stretch(sea);
            var seaImage = sea.gameObject.AddComponent<RawImage>();
            seaImage.texture = VoyageGalaxyArt.Texture();
            seaImage.raycastTarget = false;
            seaImage.color = VoyageKit.Fade(_kit.Light, 0.14f);
            var fit = sea.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = VoyageGalaxyArt.Aspect;

            BuildHeader(root);
            BuildRoute(root);
            BuildQuest(root);
            BuildReward(root);
            BuildClaim(root);
        }

        // An area of the lower band, from x0 to x1 of the screen's width (the board's columns).
        static RectTransform Lower(RectTransform root, string name, float x0, float x1)
        {
            var rt = VoyageKit.Node(name, root);
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = new Vector2(0f, LowerBottom);
            rt.offsetMax = new Vector2(0f, -LowerTop);
            return rt;
        }

        void BuildHeader(RectTransform root)
        {
            var header = VoyageKit.Node("Header", root);
            VoyageKit.Top(header, HeaderTop, HeaderHeight);
            header.offsetMin = new Vector2(Inset, header.offsetMin.y);
            header.offsetMax = new Vector2(-Inset, header.offsetMax.y);

            var left = VoyageKit.Node("Title", header);
            VoyageKit.Stretch(left);
            VoyageKit.Row(left, 28f);
            _title = _kit.Text(left, 22, _kit.Light, TextAlignmentOptions.Left);
            _progress = _kit.Text(left, 16, _kit.Light, TextAlignmentOptions.Left);
            var meter = VoyageKit.Node("Meter", left);
            var mle = VoyageKit.Le(meter);
            mle.preferredWidth = 260f;
            mle.preferredHeight = 3f;
            VoyageKit.Row(meter, 6f, TextAnchor.MiddleLeft, expandWidth: true);

            var back = VoyageKit.Node("Back", header);
            back.anchorMin = new Vector2(1f, 0.5f);
            back.anchorMax = new Vector2(1f, 0.5f);
            back.pivot = new Vector2(1f, 0.5f);
            back.sizeDelta = new Vector2(180f, 44f);
            back.anchoredPosition = Vector2.zero;
            _kit.Quiet(back, Lines?.QuestsBackKey, Lines?.QuestsBack, ModalWindowOut, out _backWords);
        }

        void BuildRoute(RectTransform root)
        {
            _routeRoot = VoyageKit.Node("Route", root);
            VoyageKit.Top(_routeRoot, RouteTop, RouteHeight);
            var line = VoyageKit.Node("Course", _routeRoot);
            VoyageKit.Stretch(line);
            _line = line.gameObject.AddComponent<QuestRouteGraphic>();
            _line.raycastTarget = false;
        }

        Station MakeStation(int index)
        {
            var s = new Station();
            s.Root = VoyageKit.Node("Quest" + (index + 1).ToString("00", CultureInfo.InvariantCulture), _routeRoot);
            s.Root.anchorMin = s.Root.anchorMax = new Vector2(0.5f, 1f);
            s.Root.pivot = new Vector2(0.5f, 0.5f);
            s.Root.sizeDelta = Vector2.zero;

            // The picture, its edges soft, over the station.
            s.ArtBox = VoyageKit.Node("Picture", s.Root);
            s.ArtBox.anchorMin = s.ArtBox.anchorMax = new Vector2(0.5f, 0.5f);
            s.ArtBox.pivot = new Vector2(0.5f, 0f);
            var mask = s.ArtBox.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(48, 32);
            var art = VoyageKit.Node("Art", s.ArtBox);
            VoyageKit.Stretch(art);
            s.Art = art.gameObject.AddComponent<Image>();
            s.Art.raycastTarget = false;
            s.Art.preserveAspect = false;

            // The mark: a diamond, its ring and its fill.
            var mark = VoyageKit.Node("Mark", s.Root);
            mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
            mark.sizeDelta = new Vector2(22f, 22f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            s.Ring = VoyageKit.Block(mark, _kit.Light);
            var fill = VoyageKit.Node("Fill", mark);
            VoyageKit.Stretch(fill);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            s.Fill = VoyageKit.Block(fill, _kit.Popup);

            // The name, underlined when picked, and the state under it.
            var words = VoyageKit.Node("Words", s.Root);
            words.anchorMin = words.anchorMax = new Vector2(0.5f, 0.5f);
            words.pivot = new Vector2(0.5f, 1f);
            words.sizeDelta = new Vector2(280f, 76f);
            words.anchoredPosition = new Vector2(0f, -22f);
            VoyageKit.Column(words, 8f, TextAnchor.UpperCenter);
            var (name, bar) = _kit.Underlined(words, 20);
            s.Name = name;
            s.Bar = bar;
            s.Line = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Center);

            // One press anywhere on the mark and words picks the quest.
            var hit = VoyageKit.Node("Hit", s.Root);
            hit.anchorMin = hit.anchorMax = new Vector2(0.5f, 0.5f);
            hit.pivot = new Vector2(0.5f, 1f);
            hit.sizeDelta = new Vector2(280f, 120f);
            hit.anchoredPosition = new Vector2(0f, 24f);
            var hitImage = VoyageKit.Block(hit, VoyageKit.Clear, hit: true);
            s.Button = hit.gameObject.AddComponent<Button>();
            s.Button.targetGraphic = hitImage;
            s.Button.transition = Selectable.Transition.None;
            int at = index;
            s.Button.onClick.AddListener(() => Pick(at));
            return s;
        }

        void BuildQuest(RectTransform root)
        {
            var col = Lower(root, "Quest", Inset / 1920f, 624f / 1920f);
            var head = VoyageKit.Node("Head", col);
            VoyageKit.Top(head, 0f, 110f);
            VoyageKit.Column(head, 8f);
            _kicker = _kit.Label(head, 14, _kit.Sub);
            _questName = _kit.Text(head, 40, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Le(_questName).preferredHeight = 48f;
            _questState = _kit.Text(head, 16, _kit.Sub, TextAlignmentOptions.Left);

            _ladder = VoyageKit.Node("Goals", col);
            _ladder.anchorMin = Vector2.zero;
            _ladder.anchorMax = Vector2.one;
            _ladder.offsetMin = Vector2.zero;
            _ladder.offsetMax = new Vector2(0f, -136f);
            var line = VoyageKit.Node("Line", _ladder);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.anchoredPosition = new Vector2(9f, -14f);
            _ladderLine = VoyageKit.Block(line, _kit.Outline);
        }

        Step MakeStep(int index)
        {
            var s = new Step();
            var row = VoyageKit.Node("Step" + (index + 1), _ladder);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, 60f);
            row.anchoredPosition = new Vector2(0f, -index * 72f);
            s.Root = row.gameObject;

            var mark = VoyageKit.Node("Mark", row);
            mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f);
            mark.sizeDelta = new Vector2(16f, 16f);
            mark.anchoredPosition = new Vector2(9f, -14f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // A ground ring first, so the ladder's line stops short of the mark.
            var halo = VoyageKit.Node("Halo", mark);
            halo.anchorMin = Vector2.zero;
            halo.anchorMax = Vector2.one;
            halo.offsetMin = new Vector2(-5f, -5f);
            halo.offsetMax = new Vector2(5f, 5f);
            VoyageKit.Block(halo, _kit.Popup);
            var ring = VoyageKit.Node("Ring", mark);
            VoyageKit.Stretch(ring);
            s.Ring = VoyageKit.Block(ring, _kit.Light);
            var fill = VoyageKit.Node("Fill", mark);
            VoyageKit.Stretch(fill);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            s.Fill = VoyageKit.Block(fill, _kit.Popup);

            var words = VoyageKit.Node("Words", row);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(40f, 0f);
            s.Text = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.TopLeft);
            VoyageKit.Top(s.Text.rectTransform, 2f, 26f);
            s.Tier = _kit.Label(words, 13, _kit.Sub, TextAlignmentOptions.TopLeft);
            VoyageKit.Top(s.Tier.rectTransform, 34f, 20f);
            s.Note = _kit.Label(words, 13, _kit.Light, TextAlignmentOptions.TopRight);
            VoyageKit.Top(s.Note.rectTransform, 34f, 20f);
            return s;
        }

        void BuildReward(RectTransform root)
        {
            var col = Lower(root, "Reward", 700f / 1920f, 1340f / 1920f);
            _rewardGroup = col.gameObject;
            var stack = VoyageKit.Node("Words", col);
            VoyageKit.Stretch(stack);
            VoyageKit.Column(stack, 6f);
            _rewardKicker = _kit.Label(stack, 14, _kit.Sub);
            _rewardName = _kit.Text(stack, 30, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Le(_rewardName).preferredHeight = 38f;
            _rewardKind = _kit.Label(stack, 14, _kit.Sub);

            var gap = VoyageKit.Node("Gap", stack);
            VoyageKit.Le(gap).preferredHeight = 16f;
            var row = VoyageKit.Node("Prisms", stack);
            VoyageKit.Le(row).preferredHeight = 88f;
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96f, 88f);
            grid.spacing = new Vector2(12f, 0f);
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            grid.childAlignment = TextAnchor.UpperLeft;
            for (int i = 0; i < 4; i++) _prisms.Add(MakePrism(row, i));

            var gap2 = VoyageKit.Node("Gap", stack);
            VoyageKit.Le(gap2).preferredHeight = 12f;
            _rewardLine = _kit.Text(stack, 16, _kit.Light, TextAlignmentOptions.TopLeft);
            _rewardLine.textWrappingMode = TextWrappingModes.Normal;
            _rewardState = _kit.Text(stack, 15, _kit.Sub, TextAlignmentOptions.Left);
        }

        Prism MakePrism(RectTransform parent, int index)
        {
            var p = new Prism();
            var cell = VoyageKit.Node("Prism" + (index + 1), parent);
            var mark = VoyageKit.Node("Mark", cell);
            mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 1f);
            mark.sizeDelta = new Vector2(40f, 40f);
            mark.anchoredPosition = new Vector2(0f, -30f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            p.Rim = VoyageKit.Block(mark, _kit.Light);
            var face = VoyageKit.Node("Face", mark);
            VoyageKit.Stretch(face);
            face.offsetMin = new Vector2(3f, 3f);
            face.offsetMax = new Vector2(-3f, -3f);
            p.Face = VoyageKit.Block(face, _kit.Popup);
            p.Label = _kit.Label(cell, 12, _kit.Sub, TextAlignmentOptions.Center);
            var lrt = p.Label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(0f, 18f);
            lrt.anchoredPosition = Vector2.zero;
            return p;
        }

        void BuildClaim(RectTransform root)
        {
            var col = Lower(root, "Claim", 1420f / 1920f, 1f);
            col.offsetMax = new Vector2(-Inset, col.offsetMax.y);

            // What the claim also opens: the next quest's picture and name.
            var opens = VoyageKit.Node("AlsoOpens", col);
            VoyageKit.Top(opens, 0f, 54f);
            _opens = opens.gameObject;
            var pic = VoyageKit.Node("Picture", opens);
            pic.anchorMin = new Vector2(0f, 0f);
            pic.anchorMax = new Vector2(0f, 1f);
            pic.pivot = new Vector2(0f, 0.5f);
            pic.sizeDelta = new Vector2(96f, 0f);
            pic.anchoredPosition = Vector2.zero;
            var picMask = pic.gameObject.AddComponent<RectMask2D>();
            picMask.softness = new Vector2Int(24, 0);
            var art = VoyageKit.Node("Art", pic);
            VoyageKit.Stretch(art);
            _opensArt = art.gameObject.AddComponent<Image>();
            _opensArt.raycastTarget = false;
            var words = VoyageKit.Node("Words", opens);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(112f, 0f);
            VoyageKit.Column(words, 4f, TextAnchor.MiddleLeft);
            _opensKicker = _kit.Label(words, 13, _kit.Sub);
            _opensName = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);

            // The one button at the foot, the line saying where Play quest goes over it.
            var note = VoyageKit.Node("Note", col);
            VoyageKit.Foot(note, 68f, 24f);
            _playNote = _kit.Text(note, 15, _kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_playNote.rectTransform);
            var cta = VoyageKit.Node("Button", col);
            VoyageKit.Foot(cta, 0f, 56f);
            (_cta, _ctaWords) = _kit.Cta(cta, Lines?.BoardSailKey, null, Press);
            var wait = VoyageKit.Node("State", col);
            VoyageKit.Foot(wait, 0f, 56f);
            VoyageKit.Hairline(wait, true, VoyageKit.Fade(_kit.Outline, 0.9f));
            VoyageKit.Hairline(wait, false, VoyageKit.Fade(_kit.Outline, 0.9f));
            _waitWords = _kit.Label(wait, 18, _kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_waitWords.rectTransform);
            _wait = wait.gameObject;
        }

        // ── Drawing ────────────────────────────────────────────────────────

        QuestItemState StateOf(int index) => QuestFrontier.StateOf(Quests, index, GameModeProgressionService.Instance);

        int Unlocked(SO_UnlockData quest)
        {
            var service = GameModeProgressionService.Instance;
            return service && quest && quest.FeatureKind == FeatureKind.GameMode ? service.GetMaxUnlockedIntensity(quest.GameMode) : 0;
        }

        string StateLine(int index, QuestItemState state, SO_UnlockData quest)
        {
            switch (state)
            {
                case QuestItemState.Claimed: return Lines?.SlabQuestClaimed;
                case QuestItemState.ReadyToClaim: return Lines?.QuestsReady;
                case QuestItemState.Unlocked:
                    int total = QuestsRules.Goals(quest).Count;
                    int done = QuestsRules.GoalsDone(state, total, Unlocked(quest));
                    return TemplateText.Fill(Lines?.QuestsOpen, "done", done.ToString(CultureInfo.InvariantCulture),
                        "total", total.ToString(CultureInfo.InvariantCulture));
                default:
                    var list = Quests;
                    return TemplateText.Fill(Lines?.SlabQuestLocked, "name",
                        index > 0 && list.Quests[index - 1] ? list.Quests[index - 1].DisplayName : null);
            }
        }

        public void Redraw()
        {
            if (!_built || _kit == null) return;
            var list = Quests;
            int count = list && list.Quests != null ? list.Quests.Count : 0;

            VoyageKit.Set(_title, Lines?.QuestsTitle);
            int claimed = 0;
            for (int i = 0; i < count; i++)
                if (StateOf(i) == QuestItemState.Claimed) claimed++;
            VoyageKit.Set(_progress, count > 0
                ? TemplateText.Fill(Lines?.SlabQuestsProgress, "claimed", claimed.ToString(CultureInfo.InvariantCulture),
                    "total", count.ToString(CultureInfo.InvariantCulture))
                : null);
            DrawMeter(count);

            if (count == 0)
            {
                foreach (var s in _stations) s.Root.gameObject.SetActive(false);
                _line.Set(System.Array.Empty<Vector2>(), System.Array.Empty<bool>(), _kit.Light, _kit.Outline);
                ShowQuest(-1);
                return;
            }
            if (_picked < 0 || _picked >= count)
                _picked = Mathf.Clamp(QuestFrontier.FrontierIndex(list, GameModeProgressionService.Instance), 0, count - 1);

            DrawRoute(list, count);
            ShowQuest(_picked);
        }

        void DrawMeter(int count)
        {
            var meter = _title ? _title.transform.parent.Find("Meter") as RectTransform : null;
            if (!meter) return;
            while (_meter.Count < count)
            {
                var seg = VoyageKit.Node("Segment", meter);
                VoyageKit.Le(seg).preferredHeight = 3f;
                _meter.Add(VoyageKit.Block(seg, _kit.Outline));
            }
            for (int i = 0; i < _meter.Count; i++)
            {
                bool shown = i < count;
                _meter[i].gameObject.SetActive(shown);
                if (!shown) continue;
                var state = StateOf(i);
                _meter[i].color = state switch
                {
                    QuestItemState.Claimed => _kit.Light,
                    QuestItemState.ReadyToClaim => _kit.CtaFill,
                    QuestItemState.Unlocked => _kit.Sub,
                    _ => _kit.Outline,
                };
            }
        }

        void DrawRoute(SO_UnlockList list, int count)
        {
            float width = _routeRoot.rect.width;
            if (width <= 1f) width = 1920f;
            while (_stations.Count < count) _stations.Add(MakeStation(_stations.Count));

            var points = new Vector2[count];
            var lit = new bool[Mathf.Max(0, count - 1)];
            for (int i = 0; i < _stations.Count; i++)
            {
                var s = _stations[i];
                bool shown = i < count;
                s.Root.gameObject.SetActive(shown);
                if (!shown) continue;

                var quest = list.Quests[i];
                var state = StateOf(i);
                bool picked = i == _picked, locked = state == QuestItemState.Locked;
                Vector2 at = QuestsRules.Station(i, count, width);
                // The station's place under the route's top edge; the graphic's space is centred on the route.
                s.Root.anchoredPosition = new Vector2(at.x, -RouteMiddleFromTop + at.y);
                points[i] = new Vector2(at.x, RouteHeight * 0.5f - RouteMiddleFromTop + at.y);
                if (i > 0) lit[i - 1] = !locked;

                s.ArtBox.sizeDelta = picked ? new Vector2(ArtPickedW, ArtPickedH) : new Vector2(ArtW, ArtH);
                s.ArtBox.anchoredPosition = new Vector2(0f, ArtGap);
                VoyageKit.Show(s.Art, quest ? quest.Icon : null);
                s.Art.color = VoyageKit.Fade(_kit.Light, locked ? 0.28f : picked ? 1f : 0.6f);

                ((RectTransform)s.Ring.transform).sizeDelta = picked ? new Vector2(22f, 22f) : new Vector2(16f, 16f);
                s.Ring.color = state switch
                {
                    QuestItemState.ReadyToClaim => _kit.CtaFill,
                    QuestItemState.Locked => _kit.Outline,
                    _ => _kit.Light,
                };
                s.Fill.color = state == QuestItemState.Claimed ? _kit.Light : _kit.Popup;

                VoyageKit.Set(s.Name, quest ? quest.DisplayName : null);
                s.Name.color = locked ? _kit.Sub : _kit.Light;
                s.Bar.color = picked ? _kit.Cyan : VoyageKit.Clear;
                VoyageKit.Set(s.Line, StateLine(i, state, quest));
                s.Line.color = state == QuestItemState.ReadyToClaim ? _kit.CtaFill : _kit.Sub;
            }
            _line.Set(points, lit, VoyageKit.Fade(_kit.Light, 0.75f), _kit.Outline);
        }

        void ShowQuest(int index)
        {
            var list = Quests;
            var quest = index >= 0 && list ? list.Quests[index] : null;
            var state = quest ? StateOf(index) : QuestItemState.Locked;

            VoyageKit.Set(_kicker, quest ? TemplateText.Fill(Lines?.SlabQuestKicker,
                "n", (index + 1).ToString("00", CultureInfo.InvariantCulture),
                "count", list.Quests.Count.ToString("00", CultureInfo.InvariantCulture)) : null);
            VoyageKit.Set(_questName, quest ? quest.DisplayName : null);
            VoyageKit.Set(_questState, quest ? StateLine(index, state, quest) : null);
            _questState.color = state == QuestItemState.ReadyToClaim ? _kit.CtaFill : _kit.Sub;

            DrawSteps(quest, state);
            DrawReward(quest, state);

            // What the claim also opens: the next quest, or for the last the feature it is (the Vessel Hangar).
            var next = quest && index + 1 < list.Quests.Count ? list.Quests[index + 1] : quest;
            _opens.SetActive(next);
            if (next)
            {
                VoyageKit.Set(_opensKicker, state == QuestItemState.Claimed ? Lines?.QuestsOpened : Lines?.QuestsAlsoOpens);
                VoyageKit.Set(_opensName, next.DisplayName);
                VoyageKit.Show(_opensArt, next.Icon);
            }

            bool claim = quest && state == QuestItemState.ReadyToClaim, play = quest && state == QuestItemState.Unlocked;
            _cta.gameObject.SetActive(claim || play);
            VoyageKit.Set(_ctaWords, claim ? Lines?.SlabQuestClaim : Lines?.SlabQuestPlay);
            VoyageKit.Set(_playNote, play ? TemplateText.Fill(Lines?.QuestsPlayNote, "name", quest.DisplayName) : null);
            _wait.SetActive(quest && !claim && !play);
            VoyageKit.Set(_waitWords, quest && !claim && !play ? StateLine(index, state, quest) : null);
        }

        void DrawSteps(SO_UnlockData quest, QuestItemState state)
        {
            var goals = QuestsRules.Goals(quest);
            bool game = quest && quest.FeatureKind == FeatureKind.GameMode;
            int done = QuestsRules.GoalsDone(state, goals.Count, Unlocked(quest));
            int rows = quest ? goals.Count + 1 : 0;
            while (_steps.Count < rows) _steps.Add(MakeStep(_steps.Count));
            for (int i = 0; i < _steps.Count; i++)
            {
                var s = _steps[i];
                bool shown = i < rows;
                s.Root.SetActive(shown);
                if (!shown) continue;
                if (i < goals.Count)
                {
                    bool ok = i < done, next = state == QuestItemState.Unlocked && i == done;
                    VoyageKit.Set(s.Text, goals[i]);
                    VoyageKit.Set(s.Tier, !game ? TemplateText.Fill(Lines?.QuestsTierFeature, "name", quest.DisplayName)
                        : i == 0 ? Lines?.QuestsTier3 : Lines?.QuestsTier4);
                    VoyageKit.Set(s.Note, ok ? Lines?.QuestsGoalDone : next ? Lines?.QuestsGoalNext : null);
                    s.Text.color = ok || next ? _kit.Light : _kit.Sub;
                    s.Ring.color = ok || next ? _kit.Light : _kit.Outline;
                    s.Fill.color = ok ? _kit.Light : _kit.Popup;
                }
                else
                {
                    // The last step: Claim, and what it unlocks.
                    bool ready = state == QuestItemState.ReadyToClaim, claimed = state == QuestItemState.Claimed;
                    string reward = RewardName(quest);
                    VoyageKit.Set(s.Text, Lines?.QuestsClaimStep);
                    VoyageKit.Set(s.Tier, string.IsNullOrEmpty(reward) ? null : TemplateText.Fill(Lines?.QuestsClaimUnlocks, "reward", reward));
                    VoyageKit.Set(s.Note, claimed ? Lines?.SlabQuestClaimed : ready ? Lines?.QuestsClaimReady : null);
                    s.Text.color = ready || claimed ? _kit.Light : _kit.Sub;
                    s.Ring.color = ready ? _kit.CtaFill : claimed ? _kit.Light : _kit.Outline;
                    s.Fill.color = claimed ? _kit.Light : _kit.Popup;
                }
            }
            // The ladder's line runs from the first mark to the last.
            var line = (RectTransform)_ladderLine.transform;
            line.sizeDelta = new Vector2(1f, Mathf.Max(0f, (rows - 1) * 72f));
            _ladderLine.enabled = rows > 1;
            _ladderLine.transform.SetAsFirstSibling();
        }

        static ColorSetCatalogSO.Entry CatalogEntry(SO_ColorSet set)
        {
            var catalog = ThemeManager.Data ? ThemeManager.Data.Catalog : null;
            return catalog && set ? catalog.Get(catalog.IndexOf(set)) : null;
        }

        static string RewardName(SO_UnlockData quest)
        {
            var set = quest ? quest.RewardColorSet : null;
            if (!set) return null;
            var entry = CatalogEntry(set);
            return entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : set.name;
        }

        void DrawReward(SO_UnlockData quest, QuestItemState state)
        {
            var set = quest ? quest.RewardColorSet : null;
            _rewardGroup.SetActive(set);
            if (!set) return;
            bool claimed = state == QuestItemState.Claimed, ready = state == QuestItemState.ReadyToClaim;
            var entry = CatalogEntry(set);
            VoyageKit.Set(_rewardKicker, claimed ? Lines?.QuestsRewardKickerYours : Lines?.QuestsRewardKicker);
            VoyageKit.Set(_rewardName, RewardName(quest));
            VoyageKit.Set(_rewardKind, Lines?.QuestsRewardKind);
            VoyageKit.Set(_rewardLine, entry?.line);
            VoyageKit.Set(_rewardState, claimed ? Lines?.QuestsRewardYours : ready ? Lines?.QuestsRewardReady : Lines?.QuestsRewardLater);
            _rewardState.color = ready ? _kit.CtaFill : claimed ? _kit.Light : _kit.Sub;

            // The four domains, as the Profile orders them: Jade, Ruby, Gold, Blue.
            DomainColorSet[] domains = { set.JadeColors, set.RubyColors, set.GoldColors, set.BlueColors };
            string[] names = { Lines?.QuestsDomainJade, Lines?.QuestsDomainRuby, Lines?.QuestsDomainGold, Lines?.QuestsDomainBlue };
            float shown = claimed || ready ? 1f : 0.55f;
            for (int i = 0; i < _prisms.Count; i++)
            {
                var p = _prisms[i];
                p.Rim.color = VoyageKit.Fade(QuestsRules.Rim(domains[i]), shown);
                p.Face.color = VoyageKit.Fade(QuestsRules.Face(domains[i]), shown);
                VoyageKit.Set(p.Label, names[i]);
            }
        }

        // ── Pressing ───────────────────────────────────────────────────────

        void Pick(int index)
        {
            if (index == _picked) return;
            _picked = index;
            Redraw();
        }

        /// <summary>The one button: claim a quest that is done (the next opens, and the course draws on to it), or go
        /// and play the picked one in the Arcade.</summary>
        void Press()
        {
            var list = Quests;
            if (!list || _picked < 0 || _picked >= list.Quests.Count) return;
            var quest = list.Quests[_picked];
            switch (StateOf(_picked))
            {
                case QuestItemState.ReadyToClaim:
                    GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(quest.GameMode);
                    Redraw();
                    break;
                case QuestItemState.Unlocked:
                    if (!_switcher) _switcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
                    if (_switcher) _switcher.OnClickArcadeNav();
                    break;
            }
        }
    }
}
