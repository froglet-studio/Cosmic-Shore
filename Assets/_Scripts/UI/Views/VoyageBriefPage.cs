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
    /// The Voyages page's second step, the voyage (Frogma Rev 100 P2A "Briefing", Docs/MENU_REDESIGN_B.md section 201):
    /// <list type="bullet">
    /// <item><b>The pilot</b> who brings it, on the left: portrait, house, name and their story of the job.</item>
    /// <item><b>The route</b> in the middle, drawing itself over the HyperSea's own water (<see cref="VoyageRoutePoster"/>),
    /// each stop named as the line reaches it.</item>
    /// <item><b>The difficulty</b> 1-9 on the right with the pilot's recommendation marked (Rev 100: "For Voyages, they dont
    /// use intensity rather they use the difficulty level 1-9"), and the <b>rewards</b> arriving one by one under it; the
    /// crystals follow the picked difficulty (<see cref="VoyageDifficulty"/>).</item>
    /// <item>Back and <b>Ready your Ark</b> at the foot.</item>
    /// </list>
    /// </summary>
    public sealed class VoyageBriefPage
    {
        const float DrawSeconds = 2.4f, RewardDelay = 0.6f, RewardStep = 0.35f, RewardFade = 0.4f;
        const float FootHeight = 56f, Gap = 24f, DifficultyButton = 44f, HeadHeight = 150f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;

        public RectTransform Root { get; }

        // The pilot.
        Image _portrait;
        TMP_Text _faction, _pilot, _story;
        // The voyage and its route.
        TMP_Text _kicker, _title, _stats, _stopCount, _stopName, _stopLine;
        VoyageRoutePoster _poster;
        // The difficulty and the rewards.
        TMP_Text _recommended;
        readonly List<DifficultyView> _difficulty = new();
        readonly List<RewardView> _rewards = new();
        RewardView _crystals, _standing, _role;
        Button _ready;

        VoyageBoard.Offer _offer;
        int _picked;
        float _t;
        int _stopShown = -2;
        readonly List<float> _stopShares = new();

        sealed class DifficultyView
        {
            public RectTransform Root;
            public Image Edge, Mark;
            public TMP_Text Text;
        }

        sealed class RewardView
        {
            public RectTransform Root;
            public CanvasGroup Group;
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

            var ground = VoyageKit.Node("SeaLines", Root);
            VoyageKit.Stretch(ground);
            var lines = ground.gameObject.AddComponent<VoyageSeaLines>();
            lines.color = VoyageKit.Fade(_kit.Outline, 0.45f);
            lines.Lines = 9;
            lines.Speed = 0.008f;

            var body = VoyageKit.Node("Body", Root);
            VoyageKit.Stretch(body);
            body.offsetMin = new Vector2(0f, FootHeight + Gap);
            body.offsetMax = new Vector2(0f, -Gap);
            BuildPilot(body);
            BuildRoute(body);
            BuildDifficulty(body);
            BuildFoot();
        }

        void BuildPilot(RectTransform body)
        {
            var rt = VoyageKit.Node("Pilot", body);
            VoyageKit.Anchor(rt, 0f, 0f, 0.25f, 1f);
            var glass = _kit.Glass(rt, hit: false);
            var face = VoyageKit.Node("Portrait", glass.Content);
            VoyageKit.Anchor(face, 0f, 0.42f, 1f, 1f);
            face.offsetMin = new Vector2(20f, 0f);
            face.offsetMax = new Vector2(-20f, -20f);
            _portrait = VoyageKit.Picture(face);
            var words = VoyageKit.Node("Words", glass.Content);
            VoyageKit.Anchor(words, 0f, 0f, 1f, 0.42f);
            words.offsetMin = new Vector2(24f, 20f);
            words.offsetMax = new Vector2(-24f, -12f);
            VoyageKit.Column(words, 8f, TextAnchor.UpperLeft);
            _faction = _kit.Label(words, 13, _kit.Sub);
            _pilot = _kit.Text(words, 26, _kit.Light, TextAlignmentOptions.Left);
            _story = _kit.Text(words, 16, _kit.Sub, TextAlignmentOptions.TopLeft);
            _story.textWrappingMode = TextWrappingModes.Normal;
            _story.overflowMode = TextOverflowModes.Ellipsis;
            VoyageKit.Le(_story).flexibleHeight = 1f;
        }

        void BuildRoute(RectTransform body)
        {
            var rt = VoyageKit.Node("Route", body);
            VoyageKit.Anchor(rt, 0.27f, 0f, 0.69f, 1f);

            var head = VoyageKit.Node("Head", rt);
            VoyageKit.Top(head, 0f, HeadHeight);
            VoyageKit.Column(head, 6f, TextAnchor.UpperLeft);
            _kicker = _kit.Label(head, 14, _kit.Light);
            _title = _kit.Text(head, 30, _kit.Light, TextAlignmentOptions.TopLeft);
            // A long name takes a second line rather than losing its end.
            _title.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_title).preferredHeight = 80f;
            _stats = _kit.Text(head, 16, _kit.Sub, TextAlignmentOptions.Left);

            var box = VoyageKit.Node("Course", rt);
            VoyageKit.Stretch(box);
            box.offsetMin = new Vector2(0f, 112f);
            box.offsetMax = new Vector2(0f, -(HeadHeight + 8f));
            var glass = _kit.Glass(box, hit: false);
            var poster = VoyageKit.Node("Poster", glass.Content);
            VoyageKit.Stretch(poster);
            poster.offsetMin = new Vector2(6f, 6f);
            poster.offsetMax = new Vector2(-6f, -6f);
            poster.gameObject.AddComponent<RectMask2D>();
            var drawn = VoyageKit.Node("Drawn", poster);
            VoyageKit.Stretch(drawn);
            _poster = drawn.gameObject.AddComponent<VoyageRoutePoster>();

            var stop = VoyageKit.Node("Stop", rt);
            VoyageKit.Foot(stop, 0f, 100f);
            VoyageKit.Column(stop, 6f, TextAnchor.UpperLeft);
            _stopCount = _kit.Label(stop, 13, _kit.Cyan);
            _stopName = _kit.Text(stop, 20, _kit.Light, TextAlignmentOptions.Left);
            _stopLine = _kit.Text(stop, 15, _kit.Sub, TextAlignmentOptions.TopLeft);
            _stopLine.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_stopLine).preferredHeight = 40f;
        }

        void BuildDifficulty(RectTransform body)
        {
            var rt = VoyageKit.Node("Picks", body);
            VoyageKit.Anchor(rt, 0.71f, 0f, 1f, 1f);
            VoyageKit.Column(rt, 12f, TextAnchor.UpperLeft);

            VoyageKit.Set(_kit.Label(rt, 14, _kit.Light), _lines?.BriefDifficulty);
            _recommended = _kit.Text(rt, 15, _kit.Sub, TextAlignmentOptions.Left);
            var row = VoyageKit.Node("Levels", rt);
            VoyageKit.Row(row, 6f);
            VoyageKit.Le(row).preferredHeight = DifficultyButton + 8f;
            for (int d = VoyageDifficulty.Min; d <= VoyageDifficulty.Max; d++)
            {
                int level = d;
                var view = new DifficultyView { Root = VoyageKit.Node("Level" + d, row) };
                var le = VoyageKit.Le(view.Root);
                le.preferredWidth = le.minWidth = DifficultyButton;
                le.preferredHeight = le.minHeight = DifficultyButton;
                var (_, text) = _kit.Square(view.Root, d.ToString(CultureInfo.InvariantCulture), () => _board.SetDifficulty(level));
                view.Text = text;
                view.Text.fontSize = 18;
                var edge = view.Root.Find("Edge");
                view.Edge = edge ? edge.GetComponent<Image>() : null;
                // The pilot's recommendation: a short line under its number.
                var mark = VoyageKit.Node("Recommended", view.Root);
                mark.anchorMin = new Vector2(0.25f, 0f);
                mark.anchorMax = new Vector2(0.75f, 0f);
                mark.pivot = new Vector2(0.5f, 1f);
                mark.sizeDelta = new Vector2(0f, 2f);
                mark.anchoredPosition = new Vector2(0f, -5f);
                view.Mark = VoyageKit.Block(mark, _kit.Jade);
                _difficulty.Add(view);
            }

            var gap = VoyageKit.Node("Gap", rt);
            VoyageKit.Le(gap).preferredHeight = 18f;
            VoyageKit.Set(_kit.Label(rt, 14, _kit.Light), _lines?.BriefRewards);
            _crystals = Reward(rt, flower: true);
            _standing = Reward(rt, flower: false);
            _role = Reward(rt, flower: false);
        }

        RewardView Reward(RectTransform parent, bool flower)
        {
            var view = new RewardView { Root = VoyageKit.Node("Reward" + _rewards.Count, parent) };
            VoyageKit.Le(view.Root).preferredHeight = 76f;
            _kit.Glass(view.Root, hit: false);
            view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();
            var words = VoyageKit.Node("Words", view.Root);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(flower ? 64f : 20f, 10f);
            words.offsetMax = new Vector2(-16f, -10f);
            VoyageKit.Column(words, 4f, TextAnchor.MiddleLeft);
            view.Main = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
            view.By = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);
            if (flower)
            {
                var box = VoyageKit.Node("Element", view.Root);
                box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
                box.pivot = new Vector2(0f, 0.5f);
                box.sizeDelta = new Vector2(28f, 28f);
                box.anchoredPosition = new Vector2(20f, 0f);
                view.Flower = VoyageKit.Flower(box, Element.Charge, _kit.Light);
                VoyageKit.Stretch(view.Flower);
            }
            _rewards.Add(view);
            return view;
        }

        void BuildFoot()
        {
            var foot = VoyageKit.Node("Foot", Root);
            VoyageKit.Foot(foot, 0f, FootHeight);
            var back = VoyageKit.Node("Back", foot);
            back.anchorMin = back.anchorMax = back.pivot = new Vector2(0f, 0.5f);
            back.sizeDelta = new Vector2(220f, 48f);
            _kit.Quiet(back, _lines?.PageBackKey, _lines?.PageBack, _board.StepBack);
            var ready = VoyageKit.Node("ReadyYourArk", foot);
            ready.anchorMin = ready.anchorMax = ready.pivot = new Vector2(1f, 0.5f);
            ready.sizeDelta = new Vector2(380f, 56f);
            (_ready, _) = _kit.Cta(ready, _lines?.BoardSailKey, _lines?.BriefReady,
                () => _board.ShowPage(VoyageBoard.Page.Ark));
        }

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>Show <paramref name="offer"/> at <paramref name="difficulty"/>; <paramref name="replay"/> draws the
        /// route and brings the rewards in again (the page was just opened).</summary>
        public void Show(VoyageBoard.Offer offer, int difficulty, bool replay)
        {
            bool fresh = replay || offer != _offer;
            _offer = offer;
            _picked = difficulty;
            if (offer == null) return;

            VoyageKit.Show(_portrait, offer.Portrait);
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
                _poster.Show(_board.Map, offer.Course, VoyageKit.RouteAuthored, VoyageKit.ArkAuthored, VoyageKit.SeaAuthored);
                var stops = new List<Vector2>(offer.Stops.Count);
                foreach (var s in offer.Stops) stops.Add(s.At);
                _poster.SetStops(stops);
                _stopShares.Clear();
                foreach (var s in offer.Stops) _stopShares.Add(_poster.ShareAt(s.At));
                _t = 0f;
                _stopShown = -2;
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
                if (view.Edge) view.Edge.color = on ? _kit.Cyan : _kit.Outline;
                view.Text.color = on ? _kit.Light : _kit.Sub;
                view.Mark.enabled = level == offer.Recommended;
            }

            _crystals.Root.gameObject.SetActive(offer.Pays);
            if (offer.Pays)
            {
                VoyageKit.Set(_crystals.Main, TemplateText.Fill(_lines?.BriefCrystals,
                    "pay", VoyageBoard.PayAt(offer, difficulty).ToString(CultureInfo.InvariantCulture),
                    "element", _board.ElementName(offer.Element)));
                VoyageKit.Set(_crystals.By, TemplateText.Fill(_lines?.BriefCrystalsBy,
                    "difficulty", difficulty.ToString(CultureInfo.InvariantCulture)));
                Petals(_crystals.Flower, offer.Element);
            }
            _standing.Root.gameObject.SetActive(!string.IsNullOrEmpty(offer.Faction));
            VoyageKit.Set(_standing.Main, TemplateText.Fill(_lines?.BriefStanding, "faction", offer.Faction));
            VoyageKit.Set(_standing.By, _lines?.BriefStandingBy);
            _role.Root.gameObject.SetActive(!string.IsNullOrEmpty(offer.Role));
            VoyageKit.Set(_role.Main, TemplateText.Fill(_lines?.BriefRole, "role", offer.Role));
            VoyageKit.Set(_role.By, null);

            _ready.interactable = offer.SetSail != null;
        }

        static void Petals(RectTransform flower, Element element)
        {
            if (!flower) return;
            var sprite = Resources.Load<Sprite>("ElementPetals/" + element.ToString().ToLowerInvariant() + "_petal");
            int i = 0;
            foreach (var img in flower.GetComponentsInChildren<Image>(true))
            {
                ElementalBarsView.ConfigurePetal(img, sprite, i++);
                img.enabled = sprite;
            }
        }

        public void Tick(float dt)
        {
            if (_offer == null) return;
            float end = DrawSeconds + RewardDelay + RewardStep * _rewards.Count + RewardFade;
            if (_t >= end) return;
            _t += dt;
            Animate();
        }

        // The route draws itself, the stop it reaches is named, and the rewards come in one by one after it.
        void Animate()
        {
            float drawn = Mathf.Clamp01(_t / DrawSeconds);
            _poster.Drawn = 1f - (1f - drawn) * (1f - drawn);
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
                float a = Mathf.Clamp01((_t - DrawSeconds * 0.5f - RewardDelay - RewardStep * i) / RewardFade);
                _rewards[i].Group.alpha = a;
            }
        }
    }
}
