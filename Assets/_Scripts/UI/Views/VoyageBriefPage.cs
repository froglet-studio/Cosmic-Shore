using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using TMPro;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages page's second step, the voyage, built to Frogma Rev 100 P2A "Briefing" (where Rev 111's Open voyage
    /// leads; Docs/MENU_REDESIGN_B.md sections 201 and 205), at the board's own numbers:
    /// <list type="bullet">
    /// <item><b>The pilot</b> who brings it, 520 x 748 on the left: their picture filling it, darkening from a quarter of
    /// the way down, their house, name and story at its foot.</item>
    /// <item><b>The voyage</b> in the middle: its kind and pilot, its name (36), its numbers; the route drawing itself in
    /// light over the HyperSea's own water (<see cref="VoyageRoutePoster"/>, 780 x 476), each stop ringed and named; the
    /// stop the line has reached under it.</item>
    /// <item><b>The difficulty</b> 1-9 on the right (the picked level filled, the pilot's recommendation underlined in
    /// cyan), the <b>rewards</b> arriving one by one under it, then Back and <b>Ready your Ark</b>.</item>
    /// </list>
    /// </summary>
    public sealed class VoyageBriefPage
    {
        // Frogma P2A's timings: the route draws over 2.4s after .3s (cubic-bezier(.4,0,.2,1)); each reward rises (.7s,
        // cubic-bezier(.2,.7,.2,1), 16 up) one after another.
        const float DrawDelay = 0.3f, DrawSeconds = 2.4f, RewardDelay = 0.6f, RewardStep = 0.35f, RewardFade = 0.7f, RewardLift = 16f;
        // Frogma Rev 100 P2A, in its own units: the page is x 40-1880, y 176-924 of the 1920 x 1080 board.
        static readonly Vector2 Design = new(1840f, 748f);
        const float MidX = 560f, MidWidth = 780f, RightX = 1380f, RightWidth = 460f;
        const float FigureY = 140f, FigureHeight = 476f, StopY = 632f, StopHeight = 116f;
        const float RewardsY = 140f, RewardRow = 64f, RewardGap = 16f, ButtonsY = 692f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;

        public RectTransform Root { get; }
        readonly RectTransform _frame;
        Vector2 _fitFor = new(-1f, -1f);

        // The pilot.
        Image _portrait;
        TMP_Text _faction, _pilot, _story;
        // The voyage and its route.
        TMP_Text _kicker, _title, _stats, _stopCount, _stopName, _stopLine;
        VoyageRoutePoster _poster;
        RectTransform _stopNames;
        readonly List<TMP_Text> _stopLabels = new();
        // The difficulty and the rewards.
        TMP_Text _recommended;
        readonly List<DifficultyView> _difficulty = new();
        readonly List<RewardView> _rewards = new();
        RewardView _crystals, _standing, _role;
        Button _ready;

        VoyageBoard.Offer _offer;
        string _offerKey;
        float _t;
        int _stopShown = -2;
        readonly List<float> _stopShares = new();
        // When each stop's name came in, for its pop (scale .3 to 1, .45s cubic-bezier(.3,1.4,.5,1)).
        readonly List<float> _stopReachedAt = new();
        const float PopSeconds = 0.45f;

        sealed class DifficultyView
        {
            public Image Ground, Mark;
            public VoyageOutline Edge;
            public TMP_Text Text;
        }

        sealed class RewardView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Vector2 Base;
            public RectTransform Flower;
            public TMP_Text Main, By;
        }

        public VoyageBriefPage(VoyageBoard board, VoyageKit kit, RectTransform pages)
        {
            _board = board;
            _kit = kit;
            _lines = kit.Lines;
            Root = VoyageKit.Node("TheVoyage", pages);
            VoyageKit.Stretch(Root);
            _frame = VoyageKit.Node("Frame", Root);
            VoyageKit.Fit(_frame, Design);
            BuildPilot();
            BuildVoyage();
            BuildDifficulty();
            BuildRewards();
            BuildButtons();
        }

        RectTransform At(string name, float x, float y, float w, float h, RectTransform parent = null)
        {
            var rt = VoyageKit.Node(name, parent ? parent : _frame);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        void BuildPilot()
        {
            var rt = At("Pilot", 0f, 0f, 520f, Design.y);
            VoyageKit.Block(rt, _kit.Popup);
            _portrait = VoyageKit.CoverBox(rt);
            // Clear to a quarter of the way down, near-black by 62%, black at the foot.
            var shade = VoyageKit.Node("Shade", rt);
            VoyageKit.Stretch(shade);
            var g = shade.gameObject.AddComponent<VoyageGradient>();
            g.raycastTarget = false;
            var dark = _kit.Popup;
            g.Set(VoyageKit.Fade(dark, 0f), VoyageKit.Fade(dark, 0.92f), dark, 0.49f);
            // The shade covers the lower three quarters: clear at its top (25% down), .92 at 62% of the whole, black at
            // the foot.
            shade.anchorMax = new Vector2(1f, 0.75f);

            var words = VoyageKit.Node("Words", rt);
            words.anchorMin = Vector2.zero;
            words.anchorMax = new Vector2(1f, 0f);
            words.pivot = new Vector2(0.5f, 0f);
            words.sizeDelta = new Vector2(-64f, 360f);
            words.anchoredPosition = new Vector2(0f, 32f);
            var col = VoyageKit.Column(words, 12f, TextAnchor.LowerLeft);
            col.childForceExpandHeight = false;
            _faction = _kit.Label(words, 14, _kit.Light);
            _pilot = _kit.Text(words, 28, _kit.Light, TextAlignmentOptions.Left);
            var gap = VoyageKit.Node("Gap", words);
            VoyageKit.Le(gap).preferredHeight = 2f;
            _story = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.BottomLeft);
            _story.textWrappingMode = TextWrappingModes.Normal;
            _story.overflowMode = TextOverflowModes.Ellipsis;
            _story.lineSpacing = 30f;
            VoyageKit.Le(_story).preferredHeight = 200f;
        }

        void BuildVoyage()
        {
            var head = At("Head", MidX, 0f, MidWidth, 130f);
            VoyageKit.Column(head, 12f, TextAnchor.UpperLeft);
            _kicker = _kit.Label(head, 14, _kit.Sub);
            _title = _kit.Text(head, 36, _kit.Light, TextAlignmentOptions.TopLeft);
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 26f;
            _title.fontSizeMax = 36f;
            VoyageKit.Le(_title).preferredHeight = 46f;
            _stats = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);

            // The figure: the voyage's water, dimmed toward its rim, a 1 unit outline.
            var figure = At("Course", MidX, FigureY, MidWidth, FigureHeight);
            VoyageKit.Block(figure, _kit.Popup);
            figure.gameObject.AddComponent<RectMask2D>();
            var drawn = VoyageKit.Node("Drawn", figure);
            VoyageKit.Stretch(drawn);
            _poster = drawn.gameObject.AddComponent<VoyageRoutePoster>();
            _poster.StopRadius = 12f;
            _poster.StopFill = VoyageKit.Fade(_kit.Style.Popup, 0.75f);
            _poster.StopAhead = _kit.Style.Sub;
            _stopNames = VoyageKit.Node("StopNames", figure);
            VoyageKit.Stretch(_stopNames);
            var edge = VoyageKit.Node("Edge", figure);
            VoyageKit.Stretch(edge);
            edge.gameObject.AddComponent<VoyageOutline>().Solid(_kit.Outline);

            var stop = At("Stop", MidX, StopY, MidWidth, StopHeight);
            VoyageKit.Column(stop, 8f, TextAnchor.MiddleLeft);
            _stopCount = _kit.Label(stop, 14, _kit.Sub);
            _stopName = _kit.Text(stop, 20, _kit.Light, TextAlignmentOptions.Left);
            _stopLine = _kit.Text(stop, 15, _kit.Sub, TextAlignmentOptions.TopLeft);
            _stopLine.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_stopLine).preferredHeight = 22f;
        }

        void BuildDifficulty()
        {
            var head = At("DifficultyHead", RightX, 0f, RightWidth, 20f);
            var label = _kit.Label(head, 14, _kit.Sub);
            VoyageKit.Stretch(label.rectTransform);
            VoyageKit.Set(label, _lines?.BriefDifficulty);
            _recommended = _kit.Text(head, 14, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Stretch(_recommended.rectTransform);

            // Nine levels across 460, 6 apart, 44 tall.
            const int Count = VoyageDifficulty.Max - VoyageDifficulty.Min + 1;
            const float Gap = 6f;
            float w = (RightWidth - Gap * (Count - 1)) / Count;
            for (int d = VoyageDifficulty.Min; d <= VoyageDifficulty.Max; d++)
            {
                int level = d, i = d - VoyageDifficulty.Min;
                var rt = At("Level" + d, RightX + i * (w + Gap), 32f, w, 44f);
                var view = new DifficultyView();
                var (ground, line) = _kit.Box(rt, VoyageKit.Fade(_kit.Popup, 0.7f), VoyageKit.Fade(_kit.Outline, 0.8f), hit: true);
                view.Ground = ground;
                view.Edge = line;
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = ground;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => _board.SetDifficulty(level));
                view.Text = _kit.Text(rt, 18, _kit.Sub, TextAlignmentOptions.Center);
                VoyageKit.Stretch(view.Text.rectTransform);
                view.Text.text = d.ToString(CultureInfo.InvariantCulture);
                // The pilot's recommendation (when another level is picked): a 2 unit cyan line along its foot.
                var mark = VoyageKit.Node("Recommended", rt);
                VoyageKit.Foot(mark, 0f, 2f);
                view.Mark = VoyageKit.Block(mark, _kit.Cyan);
                _difficulty.Add(view);
            }
        }

        void BuildRewards()
        {
            var label = _kit.Label(At("RewardsHead", RightX, RewardsY, RightWidth, 20f), 14, _kit.Sub);
            VoyageKit.Stretch(label.rectTransform);
            VoyageKit.Set(label, _lines?.BriefRewards);
            _crystals = Reward(flower: true);
            _standing = Reward(flower: false);
            _role = Reward(flower: false);
        }

        RewardView Reward(bool flower)
        {
            int i = _rewards.Count;
            var view = new RewardView { Root = At("Reward" + i, RightX, RewardsY + 20f + RewardGap + i * (RewardRow + RewardGap), RightWidth, RewardRow) };
            view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();
            view.Base = view.Root.anchoredPosition;
            // Each row stands over a 1 unit line (rgba(67,76,137,.5)).
            var foot = VoyageKit.Node("Foot", view.Root);
            VoyageKit.Foot(foot, 0f, 1f);
            VoyageKit.Block(foot, VoyageKit.Fade(_kit.Outline, 0.5f));
            var words = VoyageKit.Node("Words", view.Root);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(flower ? 44f + 18f : 0f, 14f);
            VoyageKit.Column(words, 6f, TextAnchor.MiddleLeft);
            view.Main = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
            view.By = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);
            if (flower)
            {
                var box = VoyageKit.Node("Element", view.Root);
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 0.5f);
                box.sizeDelta = new Vector2(44f, 44f);
                box.anchoredPosition = new Vector2(0f, 7f);
                view.Flower = VoyageKit.Flower(box, Element.Charge, _kit.Light);
                VoyageKit.Stretch(view.Flower);
            }
            _rewards.Add(view);
            return view;
        }

        void BuildButtons()
        {
            var back = At("Back", RightX, ButtonsY, 160f, 56f);
            _kit.Quiet(back, _lines?.PageBackKey, _lines?.PageBack, _board.StepBack);
            var ready = At("ReadyYourArk", RightX + 160f + 20f, ButtonsY, 280f, 56f);
            (_ready, _) = _kit.Cta(ready, _lines?.BoardSailKey, _lines?.BriefReady,
                () => _board.ShowPage(VoyageBoard.Page.Ark));
        }

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>Show <paramref name="offer"/> at <paramref name="difficulty"/>; <paramref name="replay"/> draws the
        /// route and brings the rewards in again (the page was just opened). The window rebuilds its offers on every
        /// refresh, so a voyage is known by its name and pilot, not by the object: a refresh never restarts the drawing.</summary>
        public void Show(VoyageBoard.Offer offer, int difficulty, bool replay)
        {
            string key = offer == null ? null : offer.Title + "|" + offer.Pilot;
            bool fresh = replay || key != _offerKey;
            _offerKey = key;
            _offer = offer;
            if (offer == null) return;

            VoyageKit.ShowCover(_portrait, offer.Portrait, new Vector2(520f, Design.y), new Vector2(0.5f, 0.2f));
            VoyageKit.Set(_faction, offer.Faction);
            VoyageKit.Set(_pilot, offer.Pilot);
            VoyageKit.Set(_story, offer.Story);

            VoyageKit.Set(_kicker, string.IsNullOrEmpty(offer.Pilot) ? offer.Kind
                : TemplateText.Fill(_lines?.BriefKicker, "kind", offer.Kind, "pilot", offer.Pilot));
            VoyageKit.Set(_title, offer.Title);
            VoyageKit.Set(_stats, string.IsNullOrEmpty(offer.Ly) ? null
                : TemplateText.Fill(_lines?.BriefStats, "ly", offer.Ly, "minutes", offer.Minutes,
                    "stops", offer.Stops.Count.ToString(CultureInfo.InvariantCulture)));

            if (fresh)
            {
                // The route in light (the board's #E6E9FF line over a faint wide one), the Ark's start in amber; the water
                // tinted down to the style's Sub, as the board's vignette (rgba(0,1,10,.2) to .7) dims its map.
                _poster.Show(_board.Map, offer.Course, _kit.Style.Light, VoyageKit.ArkAuthored, _kit.Style.Sub);
                var stops = new List<Vector2>(offer.Stops.Count);
                foreach (var s in offer.Stops) stops.Add(s.At);
                _poster.SetStops(stops);
                _stopShares.Clear();
                foreach (var s in offer.Stops) _stopShares.Add(_poster.ShareAt(s.At));
                StopLabels(offer);
                _t = 0f;
                _stopShown = -2;
                _stopReachedAt.Clear();
                foreach (var _ in offer.Stops) _stopReachedAt.Add(-1f);
                Animate();
            }

            VoyageKit.Set(_recommended, string.IsNullOrEmpty(offer.Pilot) ? null
                : TemplateText.Fill(_lines?.BriefRecommended, "pilot", offer.Pilot,
                    "difficulty", offer.Recommended.ToString(CultureInfo.InvariantCulture)));
            for (int i = 0; i < _difficulty.Count; i++)
            {
                var view = _difficulty[i];
                int level = i + VoyageDifficulty.Min;
                bool on = level == difficulty;
                view.Ground.color = on ? _kit.Light : VoyageKit.Fade(_kit.Popup, 0.7f);
                view.Edge.enabled = !on && level != offer.Recommended;
                view.Text.color = on ? _kit.Popup : level <= difficulty ? _kit.Light : _kit.Sub;
                view.Mark.enabled = !on && level == offer.Recommended;
            }

            _crystals.Root.gameObject.SetActive(offer.Pays);
            if (offer.Pays)
            {
                VoyageKit.Set(_crystals.Main, TemplateText.Fill(_lines?.BriefCrystals,
                    "pay", VoyageBoard.PayAt(offer, difficulty).ToString(CultureInfo.InvariantCulture),
                    "element", _board.ElementName(offer.Element)));
                VoyageKit.Set(_crystals.By, TemplateText.Fill(_lines?.BriefCrystalsBy,
                    "difficulty", difficulty.ToString(CultureInfo.InvariantCulture)));
                VoyageSeaPage.Petals(_crystals.Flower, offer.Element);
            }
            _standing.Root.gameObject.SetActive(!string.IsNullOrEmpty(offer.Faction));
            VoyageKit.Set(_standing.Main, TemplateText.Fill(_lines?.BriefStanding, "faction", offer.Faction));
            VoyageKit.Set(_standing.By, _lines?.BriefStandingBy);
            _role.Root.gameObject.SetActive(!string.IsNullOrEmpty(offer.Role));
            VoyageKit.Set(_role.Main, TemplateText.Fill(_lines?.BriefRole, "role", offer.Role));
            VoyageKit.Set(_role.By, null);
            StackRewards();

            _ready.interactable = offer.SetSail != null;
        }

        // The rewards shown stand one under another from the top, 16 apart.
        void StackRewards()
        {
            float y = RewardsY + 20f + RewardGap;
            foreach (var r in _rewards)
            {
                if (!r.Root.gameObject.activeSelf) continue;
                VoyageKit.At(r.Root, RightX, y, RightWidth, RewardRow);
                y += RewardRow + RewardGap;
            }
        }

        // A name beside each stop's ring (caps 14), lit for the stop the line has reached, Sub for the rest.
        void StopLabels(VoyageBoard.Offer offer)
        {
            while (_stopLabels.Count < offer.Stops.Count)
            {
                var t = _kit.Label(_stopNames, 14, _kit.Sub);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 20f);
                t.characterSpacing = 10f;
                t.overflowMode = TextOverflowModes.Overflow;
                _stopLabels.Add(t);
            }
            for (int i = 0; i < _stopLabels.Count; i++)
            {
                bool on = i < offer.Stops.Count;
                _stopLabels[i].gameObject.SetActive(on);
                if (on) VoyageKit.Set(_stopLabels[i], offer.Stops[i].Name);
            }
        }

        void PlaceStopLabels()
        {
            if (_offer == null) return;
            var r = _stopNames.rect;
            for (int i = 0; i < _offer.Stops.Count && i < _stopLabels.Count; i++)
            {
                var t = _stopLabels[i];
                bool reached = i < _stopShares.Count && _stopShares[i] <= _poster.Drawn + 1e-3f;
                if (!_poster.TryLocal(_offer.Stops[i].At, out var p) || !reached)
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                t.gameObject.SetActive(true);
                if (i < _stopReachedAt.Count)
                {
                    if (_stopReachedAt[i] < 0f) _stopReachedAt[i] = _t;
                    float pop = 0.3f + 0.7f * VoyageKit.Pop((_t - _stopReachedAt[i]) / PopSeconds);
                    t.rectTransform.localScale = new Vector3(pop, pop, 1f);
                }
                t.color = i == _stopShown ? _kit.Light : _kit.Sub;
                // Beside the ring, on the side with room (the right, unless it would run off the figure).
                bool left = p.x > r.xMax - 220f;
                var rt = t.rectTransform;
                rt.pivot = new Vector2(left ? 1f : 0f, 0.5f);
                t.alignment = left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                rt.anchoredPosition = p + new Vector2(left ? -22f : 22f, 0f);
            }
        }

        public void Tick(float dt)
        {
            var room = Root.rect.size;
            if (room != _fitFor)
            {
                _fitFor = room;
                VoyageKit.Fit(_frame, Design);
            }
            if (_offer == null) return;
            float end = PopSeconds + DrawDelay + DrawSeconds + RewardDelay + RewardStep * _rewards.Count + RewardFade;
            if (_t < end)
            {
                _t += dt;
                Animate();
            }
            PlaceStopLabels();
        }

        // The route draws itself, the stop it reaches is named, and the rewards come in one by one after it.
        void Animate()
        {
            _poster.Drawn = VoyageKit.Draw((_t - DrawDelay) / DrawSeconds);
            int reached = -1;
            for (int i = 0; i < _stopShares.Count; i++)
                if (_stopShares[i] <= _poster.Drawn + 1e-3f) reached = i;
            if (reached != _stopShown)
            {
                _stopShown = reached;
                var stops = _offer.Stops;
                bool any = reached >= 0 && reached < stops.Count;
                VoyageKit.Set(_stopCount, any ? TemplateText.Fill(_lines?.BriefStop,
                    "n", (reached + 1).ToString(CultureInfo.InvariantCulture),
                    "count", stops.Count.ToString(CultureInfo.InvariantCulture)) : null);
                VoyageKit.Set(_stopName, any ? stops[reached].Name : null);
                VoyageKit.Set(_stopLine, any ? stops[reached].Line : null);
            }
            for (int i = 0; i < _rewards.Count; i++)
            {
                var view = _rewards[i];
                float a = VoyageKit.Rise((_t - DrawDelay - DrawSeconds * 0.5f - RewardDelay - RewardStep * i) / RewardFade);
                view.Group.alpha = a;
                view.Root.anchoredPosition = view.Base + new Vector2(0f, -RewardLift * (1f - a));
            }
        }
    }
}
