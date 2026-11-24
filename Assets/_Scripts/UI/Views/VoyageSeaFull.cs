using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// What the HyperSea full screen carries over the live map, to Frogma Rev 138 Full (Rev 135 FullScreen as picked;
    /// Docs/MENU_REDESIGN_B.md section 231), in the board's 1920 x 1080 units:
    /// <list type="bullet">
    /// <item><b>On the map</b>: the port the Ark is docked at (a filled diamond and its name); each job's pin standing over
    /// the place it sails to (the giver's face, the kind and the classes wanted, on a stalk to a hollow diamond and the
    /// place's name), the picked one in cyan; the picked job's dashed lane with a ring per cell; and what else is out there:
    /// rival crews (a red warning mark, the crew's name, "Also bound for ..."), wrecks and blooms.</item>
    /// <item><b>Work at</b> down the right: one row per job (the giver's face, kind and faction, the voyage's name, the
    /// classes, where it sails and the sea), Hide folds it away.</item>
    /// <item><b>The foot</b>: the picked job's card (face, kind and giver, name, route and cells, the sea, the classes it
    /// wants, the squad fit, the pay) and Take the job (A).</item>
    /// </list>
    /// The page lends it a projector from the map's points to its own units every frame, so the marks ride the map as it
    /// pans and zooms. The zoom row and Back from full screen stay the page's own.
    /// </summary>
    public sealed class VoyageSeaFull
    {
        /// <summary>Where a flat map point (<see cref="VoyageMapGraphic.RectPointOf"/>) is seen, in this layer's units from
        /// its top left; false when it is off the map.</summary>
        public delegate bool Projector(Vector2 flat, out Vector2 at);

        const float ColumnX = 1480f, ColumnY = 120f, ColumnWidth = 400f, RowHeight = 118f, RowStep = 128f,
            FootX = 40f, FootY = 900f, FootWidth = 1840f, FootHeight = 140f, PinRise = 96f;
        const int MaxEvents = 6, MaxRivals = 3;
        const float CardWidth = 200f, FootTop = 880f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        readonly VoyageQuay _quay;
        readonly VoyageQuaySO _words;
        readonly VoyageClassesSO _classes;

        public RectTransform Root { get; }
        readonly CanvasGroup _group;
        readonly RectTransform _marks, _column, _rows, _foot;
        readonly List<int> _order = new();
        readonly List<RowView> _rowViews = new();
        readonly List<PinView> _pins = new();
        readonly List<EventView> _events = new();
        readonly List<VoyageRing> _cells = new();
        readonly List<(string from, string to, float t, EventView view)> _eventAt = new();
        readonly List<Vector2> _taken = new();
        VoyageChartLine _lane;
        VoyageRing _home;
        TMP_Text _homeName, _workAt, _hideWords;
        bool _hidden;
        IReadOnlyList<VoyageBoard.Offer> _offers = System.Array.Empty<VoyageBoard.Offer>();
        int _open = -1;

        // The foot card.
        Image _footFace;
        TMP_Text _footKicker, _footTitle, _footRoute, _footDanger, _footWants, _footFit, _footPay;
        readonly Image[] _dangerSegs = new Image[4];
        readonly Image[] _fitSegs = new Image[3];
        readonly VoyageGlyph[] _footGlyphs = new VoyageGlyph[3];
        RectTransform _footWantsRow, _footFitRow, _footPayRow;
        Button _take;

        sealed class RowView
        {
            public RectTransform Root;
            public Image Ground, Line, Face;
            public TMP_Text Kicker, Title, Meta;
            public readonly VoyageGlyph[] Glyphs = new VoyageGlyph[3];
            public Button Button;
        }

        sealed class PinView
        {
            public RectTransform Root, Card, Stalk;
            public Image Face, StalkLine;
            public VoyageOutline Edge;
            public TMP_Text Kind, Place;
            public VoyageRing Mark;
            public readonly VoyageGlyph[] Glyphs = new VoyageGlyph[3];
            public Button Button;
            public Vector2 Flat;
            public bool HasFlat;
        }

        sealed class EventView
        {
            public RectTransform Root;
            public VoyageGlyph Icon;
            public VoyageOutline Box;
            public TMP_Text Name, Line;
        }

        public VoyageSeaFull(VoyageBoard board, VoyageKit kit, VoyageSeats rules, VoyageQuay quay, RectTransform frame)
        {
            _board = board;
            _kit = kit;
            _rules = rules;
            _quay = quay;
            _words = VoyageQuaySO.Load();
            _classes = VoyageClassesSO.Load();
            Root = VoyageKit.Node("FullScreen", frame);
            Root.anchorMin = Root.anchorMax = Root.pivot = Vector2.zero;
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _marks = At("Marks", Root, 0f, 0f, 1920f, 1080f);
            var lane = At("Lane", _marks, 0f, 0f, 1920f, 1080f);
            _lane = lane.gameObject.AddComponent<VoyageChartLine>();
            _lane.color = VoyageKit.Fade(_kit.Light, 0.55f);
            _home = Diamond(_marks, "Home", 20f);
            _homeName = Text(_marks, "HomeName", 0f, 0f, 320f, 22f, 15f, _kit.Light);
            _column = At("WorkAt", Root, ColumnX, ColumnY, ColumnWidth, 40f + 4f * RowStep);
            _workAt = Label(_column, "Label", 0f, 0f, ColumnWidth - 120f, 36f, 14f, _kit.Sub);
            var hide = At("Hide", _column, ColumnWidth - 110f, 0f, 110f, 36f);
            _kit.Quiet(hide, null, _words ? _words.Hide : null, ToggleHide, out _hideWords);
            if (_hideWords) _hideWords.fontSize = 14f;
            _rows = At("Rows", _column, 0f, 46f, ColumnWidth, 4f * RowStep);
            _foot = At("Foot", Root, FootX, FootY, FootWidth, FootHeight);
            BuildFoot();
        }

        // ── Building ─────────────────────────────────────────────────────────

        static RectTransform At(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var rt = VoyageKit.Node(name, parent);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        TMP_Text Text(RectTransform parent, string name, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = _kit.Text(At(name, parent, x, y, w, h), size, color, align);
            VoyageKit.Stretch(t.rectTransform);
            return t;
        }

        TMP_Text Label(RectTransform parent, string name, float x, float y, float w, float h, float size, Color color)
        {
            var t = _kit.Label(At(name, parent, x, y, w, h), size, color);
            VoyageKit.Stretch(t.rectTransform);
            return t;
        }

        static VoyageGlyph Glyph(RectTransform parent, float x, float y, float size)
        {
            var g = At("Glyph", parent, x, y, size, size).gameObject.AddComponent<VoyageGlyph>();
            g.raycastTarget = false;
            return g;
        }

        static VoyageRing Diamond(RectTransform parent, string name, float size)
        {
            var mark = At(name, parent, 0f, 0f, size, size).gameObject.AddComponent<VoyageRing>();
            mark.Diamond = true;
            mark.Width = 2f;
            mark.raycastTarget = false;
            return mark;
        }

        Image[] Segments(RectTransform parent, float x, float y, float w, int count, float h)
        {
            var segs = new Image[count];
            float seg = (w - 4f * (count - 1)) / count;
            for (int i = 0; i < count; i++) segs[i] = VoyageKit.Block(At("Segment" + i, parent, x + i * (seg + 4f), y, seg, h), _kit.Light);
            return segs;
        }

        void BuildFoot()
        {
            // The glass card, 1840 x 140, 28 in: face (72 x 86), the words (420), the sea, the classes, the fit, the pay and
            // Take the job (280).
            _kit.Glass(_foot, hit: true);
            var face = At("Face", _foot, 28f, 27f, 72f, 86f);
            VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
            _footFace = VoyageKit.CoverBox(face);
            _footKicker = Label(_foot, "Kicker", 130f, 36f, 440f, 20f, 14f, _kit.Sub);
            _footTitle = Text(_foot, "Title", 130f, 58f, 440f, 30f, 22f, _kit.Light);
            _footRoute = Text(_foot, "Route", 130f, 92f, 440f, 20f, 14f, _kit.Sub);
            var danger = At("Sea", _foot, 580f, 58f, 200f, 24f);
            var segs = Segments(danger, 0f, 8f, 86f, 4, 8f);
            for (int i = 0; i < 4; i++) _dangerSegs[i] = segs[i];
            _footDanger = Text(danger, "Words", 98f, 0f, 110f, 24f, 15f, _kit.Light);
            _footWantsRow = At("Wants", _foot, 800f, 58f, 220f, 24f);
            _footWants = Label(_footWantsRow, "Label", 0f, 0f, 80f, 24f, 14f, _kit.Sub);
            VoyageKit.Set(_footWants, _words ? _words.Wants : null);
            for (int i = 0; i < 3; i++) _footGlyphs[i] = Glyph(_footWantsRow, 86f + i * 30f, 2f, 20f);
            _footFitRow = At("Fit", _foot, 1000f, 58f, 330f, 24f);
            var fit = Segments(_footFitRow, 0f, 8f, 120f, 3, 8f);
            for (int i = 0; i < 3; i++) _fitSegs[i] = fit[i];
            _footFit = Text(_footFitRow, "Words", 134f, 0f, 200f, 24f, 15f, _kit.Light);
            _footPayRow = At("Pay", _foot, 1380f, 42f, 140f, 56f);
            var gem = Glyph(_footPayRow, 140f - 22f - 64f, 17f, 22f);
            gem.Form = VoyageGlyph.Shape.Gem;
            gem.color = _kit.Light;
            _footPay = Text(_footPayRow, "Crystals", 140f - 56f, 0f, 56f, 56f, 24f, _kit.Light);
            (_take, _) = _kit.Cta(At("TakeTheJob", _foot, FootWidth - 28f - 280f, 42f, 280f, 56f),
                _words ? _words.TakeJobKey : null, _words ? _words.TakeJob : null, _board.TakeJob);
        }

        RowView RowFor(int index)
        {
            while (_rowViews.Count <= index)
            {
                var view = new RowView { Root = At("Job" + _rowViews.Count, _rows, 0f, 0f, ColumnWidth, RowHeight) };
                view.Ground = VoyageKit.Block(view.Root, VoyageKit.Fade(_kit.RowPicked, 0.95f), hit: true);
                view.Line = VoyageKit.Block(At("Line", view.Root, 0f, 0f, 2f, RowHeight), _kit.Cyan);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Ground;
                view.Button.transition = Selectable.Transition.None;
                var face = At("Face", view.Root, 16f, (RowHeight - 76f) * 0.5f, 64f, 76f);
                VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
                view.Face = VoyageKit.CoverBox(face);
                const float x = 16f + 64f + 14f, w = ColumnWidth - x - 12f;
                view.Kicker = Label(view.Root, "Kicker", x, 22f, w, 20f, 13f, _kit.Sub);
                view.Title = Text(view.Root, "Title", x, 44f, w, 26f, 18f, _kit.Light);
                for (int i = 0; i < 3; i++) view.Glyphs[i] = Glyph(view.Root, x + i * 24f, 78f, 16f);
                view.Meta = Text(view.Root, "Meta", x + 3f * 24f + 6f, 75f, w - 78f, 22f, 14f, _kit.Sub);
                _rowViews.Add(view);
            }
            return _rowViews[index];
        }

        PinView PinFor(int index)
        {
            while (_pins.Count <= index)
            {
                var view = new PinView { Root = At("Pin" + _pins.Count, _marks, 0f, 0f, 1f, 1f) };
                // The card stands 96 over the place: the face (38 x 46), the kind (caps 13) over the classes (16).
                view.Card = At("Card", view.Root, -30f, -PinRise, CardWidth, 58f);
                var ground = VoyageKit.Block(view.Card, VoyageKit.Fade(_kit.RowPicked, 1.05f), hit: true);
                var edge = VoyageKit.Node("Edge", view.Card);
                VoyageKit.Stretch(edge);
                view.Edge = edge.gameObject.AddComponent<VoyageOutline>();
                view.Edge.raycastTarget = false;
                view.Button = view.Card.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = ground;
                view.Button.transition = Selectable.Transition.None;
                var face = At("Face", view.Card, 6f, 6f, 38f, 46f);
                view.Face = VoyageKit.CoverBox(face);
                view.Kind = Label(view.Card, "Kind", 54f, 8f, 140f, 18f, 13f, _kit.Sub);
                view.Kind.overflowMode = TextOverflowModes.Overflow;
                for (int i = 0; i < 3; i++) view.Glyphs[i] = Glyph(view.Card, 54f + i * 22f, 32f, 16f);
                view.StalkLine = VoyageKit.Block(At("Stalk", view.Root, -1f, -40f, 2f, 30f), _kit.Light);
                view.Mark = Diamond(view.Root, "Place", 18f);
                VoyageKit.At(view.Mark.rectTransform, -9f, -9f, 18f, 18f);
                view.Place = Text(view.Root, "Name", 18f, -11f, 300f, 22f, 15f, _kit.Light);
                _pins.Add(view);
            }
            return _pins[index];
        }

        EventView EventFor(int index)
        {
            while (_events.Count <= index)
            {
                var view = new EventView { Root = At("Event" + _events.Count, _marks, 0f, 0f, 1f, 1f) };
                var box = At("Box", view.Root, 0f, -15f, 30f, 30f);
                VoyageKit.Block(box, VoyageKit.Fade(_kit.RowPicked, 1f));
                var edge = VoyageKit.Node("Edge", box);
                VoyageKit.Stretch(edge);
                view.Box = edge.gameObject.AddComponent<VoyageOutline>();
                view.Box.raycastTarget = false;
                view.Icon = Glyph(box, 6f, 6f, 18f);
                view.Name = Text(view.Root, "Name", 40f, -18f, 360f, 20f, 15f, _kit.Light);
                view.Line = Text(view.Root, "Line", 40f, 2f, 360f, 18f, 13f, _kit.Sub);
                _events.Add(view);
            }
            return _events[index];
        }

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>Stand the layer over <paramref name="full"/> (the full screen in the frame's units) at the full screen's
        /// ease <paramref name="e"/> (0 in the window, 1 full screen).</summary>
        public void Lay(Rect full, float e)
        {
            Root.anchoredPosition = full.position;
            Root.sizeDelta = full.size;
            _group.alpha = e;
            _group.blocksRaycasts = e >= 0.5f;
            Root.gameObject.SetActive(e > 0f);
            // The column and the foot keep to the board's right and foot edges however wide the screen.
            VoyageKit.At(_column, full.width - 1920f + ColumnX, ColumnY, ColumnWidth, 46f + 4f * RowStep);
            VoyageKit.At(_foot, FootX, full.height - 1080f + FootY, full.width - 2f * FootX, FootHeight);
            VoyageKit.At(_marks, 0f, 0f, full.width, full.height);
        }

        void ToggleHide()
        {
            _hidden = !_hidden;
            _rows.gameObject.SetActive(!_hidden);
            VoyageKit.Set(_hideWords, _words ? (_hidden ? _words.Show : _words.Hide) : null);
        }

        /// <summary>Draw the jobs (<paramref name="offers"/>, <paramref name="open"/> picked) and what else is on the sea.</summary>
        public void Draw(IReadOnlyList<VoyageBoard.Offer> offers, int open, int difficulty, VoyageMapGraphic map)
        {
            _offers = offers ?? System.Array.Empty<VoyageBoard.Offer>();
            _open = open;
            VoyageQuay.Order(_offers, _order);
            VoyageKit.Set(_workAt, _words && !string.IsNullOrEmpty(_board.Here)
                ? TemplateText.Fill(_words.WorkAt, "place", _board.Here) : null);
            VoyageKit.Set(_homeName, _board.Here);
            _home.Set(_kit.Light, _kit.Light);
            for (int n = 0; n < _order.Count; n++)
            {
                int index = _order[n];
                var offer = _offers[index];
                bool on = index == open;
                DrawRow(RowFor(n), n, offer, index, on, difficulty);
                DrawPin(PinFor(n), offer, index, on);
            }
            for (int i = _order.Count; i < _rowViews.Count; i++) _rowViews[i].Root.gameObject.SetActive(false);
            for (int i = _order.Count; i < _pins.Count; i++) _pins[i].Root.gameObject.SetActive(false);
            DrawEvents(map);
            DrawFoot(open >= 0 && open < _offers.Count ? _offers[open] : null, difficulty);
        }

        void DrawRow(RowView view, int slot, VoyageBoard.Offer offer, int index, bool on, int difficulty)
        {
            view.Root.gameObject.SetActive(true);
            VoyageKit.At(view.Root, 0f, slot * RowStep, ColumnWidth, RowHeight);
            view.Ground.color = on ? VoyageKit.Fade(_kit.Cyan, 0.06f) : VoyageKit.Fade(_kit.RowPicked, 0.95f);
            view.Line.enabled = on;
            var info = VoyageQuay.TypeOf(offer);
            VoyageKit.ShowCover(view.Face, _quay.GiverFace(info, offer) ?? offer.Picture, new Vector2(64f, 76f), VoyageKit.FaceFocus);
            view.Face.color = new Color(1f, 1f, 1f, on ? 1f : 0.7f);
            string faction = VoyageQuay.GiverFaction(info, offer);
            VoyageKit.Set(view.Kicker, _words
                ? TemplateText.Fill(string.IsNullOrEmpty(faction) ? "{kind}" : _words.GiverKicker, "kind", _quay.KindOf(offer),
                    "giver", _quay.GiverName(info, offer), "faction", faction)
                : null);
            VoyageKit.Set(view.Title, offer.Title);
            view.Title.color = on ? _kit.Light : _kit.Sub;
            Glyphs(view.Glyphs, offer, on ? _kit.Light : _kit.Sub, 24f, 78f, view.Meta, 16f + 64f + 14f);
            VoyageKit.Set(view.Meta, _words
                ? TemplateText.Fill(_words.GiverMeta, "place", offer.Place, "danger", _words.Danger(on ? difficulty : offer.Recommended))
                : null);
            view.Button.onClick.RemoveAllListeners();
            view.Button.onClick.AddListener(() => _board.Pick(index));
        }

        /// <summary>Show the classes <paramref name="offer"/> wants on <paramref name="glyphs"/>; the text after them moves
        /// up to where they end.</summary>
        int Glyphs(VoyageGlyph[] glyphs, VoyageBoard.Offer offer, Color color, float step, float y, TMP_Text after, float x)
        {
            var wants = _classes ? _classes.WantedBy(VoyageClassesSO.JobOf(offer.Key)) : System.Array.Empty<VoyageClass>();
            for (int i = 0; i < glyphs.Length; i++)
            {
                bool shown = i < wants.Count;
                glyphs[i].gameObject.SetActive(shown);
                if (!shown) continue;
                glyphs[i].Form = VoyageGlyph.Of(wants[i]);
                glyphs[i].color = color;
            }
            if (after)
            {
                var rt = (RectTransform)after.transform.parent;
                float left = x + wants.Count * step + (wants.Count > 0 ? 6f : 0f);
                VoyageKit.At(rt, left, y - 3f, ColumnWidth - left - 12f, 22f);
            }
            return wants.Count;
        }

        void DrawPin(PinView view, VoyageBoard.Offer offer, int index, bool on)
        {
            view.HasFlat = offer.Stops.Count > 0 || offer.Course.Count > 0;
            if (view.HasFlat) view.Flat = offer.Stops.Count > 0 ? offer.Stops[^1].At : offer.Course[^1];
            var info = VoyageQuay.TypeOf(offer);
            VoyageKit.ShowCover(view.Face, _quay.GiverFace(info, offer) ?? offer.Picture, new Vector2(38f, 46f), VoyageKit.FaceFocus);
            VoyageKit.Set(view.Kind, _quay.KindOf(offer));
            int n = Glyphs(view.Glyphs, offer, _kit.Light, 22f, 0f, null, 0f);
            float w = Mathf.Max(54f + n * 22f, 54f + Mathf.Ceil(view.Kind.GetPreferredValues(view.Kind.text).x)) + 14f;
            VoyageKit.At(view.Card, -30f, -PinRise, w, 58f);
            view.Edge.Solid(on ? _kit.Cyan : VoyageKit.Fade(_kit.Sub, 0.9f));
            view.Edge.Width = on ? 2f : 1f;
            view.Edge.SetVerticesDirty();
            view.StalkLine.color = on ? _kit.Cyan : VoyageKit.Fade(_kit.Sub, 0.9f);
            view.Mark.Set(_kit.Light, _kit.RowPicked);
            VoyageKit.Set(view.Place, offer.Place);
            view.Button.onClick.RemoveAllListeners();
            view.Button.onClick.AddListener(() => _board.Pick(index));
            if (on) view.Root.SetAsLastSibling();
        }

        /// <summary>What else is on the sea (Rev 135 FullScreen's events next door): every rival crew out and bound somewhere,
        /// every wreck still unclaimed and every place in bloom, at their places.</summary>
        void DrawEvents(VoyageMapGraphic map)
        {
            _eventAt.Clear();
            var state = _board.State;
            var content = VoyageContentSO.Load() is { } so ? so.Content : null;
            int n = 0;
            var threat = ColorTrace.Retrace(ColorTrace.Ui, VoyageHudColors.ThreatAuthored);
            if (state != null && content != null && map)
            {
                // Next door first: rival crews bound for a place a job on the quay goes to, then the rest; wrecks and
                // blooms nearest the home port.
                var bound = new HashSet<string>();
                foreach (var o in _offers) if (o != null && !string.IsNullOrEmpty(o.Place)) bound.Add(o.Place);
                var home = VoyageMapGraphic.PlacePoint(content.Location(map.Here));
                var rivals = new List<RivalState>();
                foreach (var r in state.Rivals ?? new List<RivalState>())
                    if (r != null && r.IsOut && !string.IsNullOrEmpty(r.To) && content.Location(r.At) != null && content.Location(r.To) != null)
                        rivals.Add(r);
                rivals.Sort((a, b) => (bound.Contains(content.Location(b.To).Name) ? 1 : 0).CompareTo(bound.Contains(content.Location(a.To).Name) ? 1 : 0));
                foreach (var r in rivals)
                {
                    if (n >= MaxRivals) break;
                    var crew = content.Rival(r.Id);
                    var view = EventFor(n++);
                    Event(view, VoyageGlyph.Shape.Warn, threat, crew?.Name ?? r.Id,
                        _words ? TemplateText.Fill(_words.RivalBound, "to", content.Location(r.To).Name) : null);
                    _eventAt.Add((r.At, r.To, 0.6f, view));
                }
                var places = new List<(string id, bool wreck)>();
                foreach (var w in state.Wrecks ?? new List<WreckRecord>())
                    if (w != null && content.Location(w.At) != null) places.Add((w.At, true));
                foreach (var p in state.Places ?? new List<LocationState>())
                    if (p != null && p.Bloom > 0 && content.Location(p.Id) != null) places.Add((p.Id, false));
                places.Sort((a, b) => (VoyageMapGraphic.PlacePoint(content.Location(a.id)) - home).sqrMagnitude
                    .CompareTo((VoyageMapGraphic.PlacePoint(content.Location(b.id)) - home).sqrMagnitude));
                foreach (var (id, wreck) in places)
                {
                    if (n >= MaxEvents) break;
                    var view = EventFor(n++);
                    Event(view, wreck ? VoyageGlyph.Shape.Flag : VoyageGlyph.Shape.Gem, _kit.Light,
                        _words ? (wreck ? _words.Wreck : _words.Bloom) : null, content.Location(id).Name);
                    _eventAt.Add((id, id, 0f, view));
                }
            }
            for (int i = n; i < _events.Count; i++) _events[i].Root.gameObject.SetActive(false);
        }

        /// <summary>The map points of the events on show (<see cref="VoyageMapGraphic.MapPoint"/> space), so the page can
        /// frame the full screen on the jobs and what is happening next door together.</summary>
        public void EventPoints(VoyageContent content, List<Vector2> into)
        {
            if (content == null) return;
            foreach (var (a, b, t, _) in _eventAt)
                into.Add(Vector2.Lerp(VoyageMapGraphic.PlacePoint(content.Location(a)), VoyageMapGraphic.PlacePoint(content.Location(b)), t));
        }

        static void Event(EventView view, VoyageGlyph.Shape shape, Color color, string name, string line)
        {
            view.Root.gameObject.SetActive(true);
            view.Icon.Form = shape;
            view.Icon.color = color;
            view.Box.Solid(color);
            VoyageKit.Set(view.Name, name);
            view.Name.color = color;
            VoyageKit.Set(view.Line, line);
        }

        void DrawFoot(VoyageBoard.Offer offer, int difficulty)
        {
            _foot.gameObject.SetActive(offer != null);
            if (offer == null || !_words) return;
            var info = VoyageQuay.TypeOf(offer);
            VoyageKit.ShowCover(_footFace, _quay.GiverFace(info, offer) ?? offer.Picture, new Vector2(72f, 86f), VoyageKit.FaceFocus);
            VoyageKit.Set(_footKicker, TemplateText.Fill(_words.FullKicker, "kind", _quay.KindOf(offer), "giver",
                _quay.GiverName(info, offer)));
            VoyageKit.Set(_footTitle, offer.Title);
            VoyageKit.Set(_footRoute, TemplateText.Fill(_words.FullRoute, "from", string.IsNullOrEmpty(offer.From) ? _board.Here : offer.From,
                "to", offer.Place, "cells", Mathf.Max(1, offer.Cells).ToString(CultureInfo.InvariantCulture)));
            int level = VoyageQuaySO.DangerLevel(difficulty);
            for (int i = 0; i < 4; i++) _dangerSegs[i].color = i < level ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.6f);
            VoyageKit.Set(_footDanger, _words.Danger(difficulty));
            int wants = Glyphs(_footGlyphs, offer, _kit.Light, 30f, 0f, null, 0f);
            _footWantsRow.gameObject.SetActive(wants > 0);
            var squad = VoyageSeatRecord.Get(offer.Key) ?? VoyageSeats.Active;
            _footFitRow.gameObject.SetActive(wants > 0 && squad != null && _classes);
            if (_footFitRow.gameObject.activeSelf)
            {
                var vessels = new List<VesselClassType>(SavedSquad.SlotCount);
                for (int s = 0; s < SavedSquad.SlotCount; s++) vessels.Add(squad.GetSlot(s).Vessel);
                int fit = _classes.Fit(VoyageClassesSO.JobOf(offer.Key), vessels);
                for (int i = 0; i < 3; i++) _fitSegs[i].color = i < fit ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.6f);
                VoyageKit.Set(_footFit, TemplateText.Fill(_words.Fit, "fit", fit.ToString(CultureInfo.InvariantCulture)));
            }
            _footPayRow.gameObject.SetActive(offer.Pays);
            VoyageKit.Set(_footPay, offer.Pays ? VoyageBoard.PayAt(offer, difficulty).ToString(CultureInfo.InvariantCulture) : null);
            _take.interactable = true;
        }

        // ── Following the map ────────────────────────────────────────────────

        /// <summary>Stand every mark where the map shows its place now (<paramref name="project"/>); the home port from
        /// <paramref name="map"/>'s here. The picked job's lane runs from home to its pin with a ring per cell.</summary>
        public void Follow(VoyageMapGraphic map, Projector project)
        {
            if (!Root.gameObject.activeSelf || map == null || project == null) return;
            bool home = !string.IsNullOrEmpty(map.Here) && map.PlaceRectPoint(map.Here, out var homeFlat) &&
                        project(homeFlat, out var homeAt) && Stand(_home.rectTransform, homeAt, 20f);
            Vector2 homeAtShown = home ? Centre(_home.rectTransform) : default;
            _home.gameObject.SetActive(home);
            _homeName.gameObject.SetActive(home);
            if (home) VoyageKit.At(_homeName.rectTransform.parent as RectTransform, homeAtShown.x + 20f, homeAtShown.y - 11f, 320f, 22f);

            Vector2 picked = default;
            bool pickedShown = false;
            _taken.Clear();
            if (home) _taken.Add(homeAtShown);
            for (int n = 0; n < _order.Count && n < _pins.Count; n++)
            {
                var pin = _pins[n];
                // A job's end is a map point; the projector takes the map's flat rect points.
                Vector2 p = default;
                bool shown = pin.HasFlat && project(map.RectPointOf(pin.Flat), out p);
                pin.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                VoyageKit.At(pin.Root, p.x, p.y, 1f, 1f);
                // Two jobs ending at one place stand their cards side by side over the one place mark.
                int beside = 0;
                for (int k = 0; k < n; k++)
                    if (_pins[k].Root.gameObject.activeSelf && (_pins[k].Root.anchoredPosition - pin.Root.anchoredPosition).sqrMagnitude < 16f) beside++;
                VoyageKit.At(pin.Card, -30f + beside * (CardWidth + 10f), -PinRise, CardWidth, 58f);
                pin.Mark.gameObject.SetActive(beside == 0);
                _taken.Add(p);
                _taken.Add(new Vector2(p.x + beside * (CardWidth + 10f), p.y - PinRise + 29f));
                pin.Place.gameObject.SetActive(beside == 0);
                if (_order[n] == _open)
                {
                    picked = p;
                    pickedShown = true;
                }
            }

            bool lane = home && pickedShown;
            _lane.Set(lane ? new[] { homeAtShown, picked } : null, 2f, 8f, 8f, 0f);
            var offer = _open >= 0 && _open < _offers.Count ? _offers[_open] : null;
            int rings = lane && offer != null ? Mathf.Clamp(offer.Cells - 1, 0, 4) : 0;
            while (_cells.Count < rings)
            {
                var ring = At("Cell" + _cells.Count, _marks, 0f, 0f, 108f, 108f).gameObject.AddComponent<VoyageRing>();
                ring.Width = 1f;
                ring.raycastTarget = false;
                ring.transform.SetSiblingIndex(1);
                _cells.Add(ring);
            }
            for (int i = 0; i < _cells.Count; i++)
            {
                bool on = i < rings;
                _cells[i].gameObject.SetActive(on);
                if (!on) continue;
                var at = Vector2.Lerp(homeAtShown, picked, (i + 1f) / (rings + 1f));
                VoyageKit.At(_cells[i].rectTransform, at.x - 54f, at.y - 54f, 108f, 108f);
                _cells[i].Set(VoyageKit.Fade(_kit.Sub, 0.9f), VoyageKit.Fade(_kit.Popup, 0.35f));
            }

            // Two events at one place stack upward instead of printing over each other or over a pin.
            foreach (var (from, to, t, view) in _eventAt)
            {
                Vector2 at = default;
                bool shown = map.PlaceRectPoint(from, out var a) && map.PlaceRectPoint(to, out var b) &&
                             project(Vector2.Lerp(a, b, t), out at);
                view.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                // Only an event on the open sea: never under the givers' column or the foot, never off the top.
                shown = at.x > 40f && at.x < ColumnX - 240f && at.y > 110f && at.y < FootTop - 40f;
                if (shown)
                    for (int guard = 0; guard < MaxEvents && _taken.Exists(t => Mathf.Abs(t.x - at.x) < 220f && Mathf.Abs(t.y - at.y) < 44f); guard++)
                    at.y -= 48f;
                view.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                _taken.Add(at);
                VoyageKit.At(view.Root, at.x, at.y, 1f, 1f);
            }
        }

        static bool Stand(RectTransform rt, Vector2 centre, float size)
        {
            VoyageKit.At(rt, centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);
            return true;
        }

        static Vector2 Centre(RectTransform rt) =>
            new(rt.anchoredPosition.x + rt.sizeDelta.x * 0.5f, -rt.anchoredPosition.y + rt.sizeDelta.y * 0.5f);
    }
}
