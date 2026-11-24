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
    /// The quay, the HyperSea page in the window as Frogma Rev 134 Board B draws it (kept in Rev 138 Board, picked by
    /// Shombith 2026-11-24; Docs/MENU_REDESIGN_B.md section 231). <see cref="VoyageSeaPage"/> lays it out and fades it
    /// as the full screen opens; the chart beside it is the page's own map.
    /// <list type="bullet">
    /// <item><b>The jobs</b> along the top: each a 230 x 420 card, the giver's picture over its foot, the kind, the
    /// voyage's name and the classes it wants; the picked one bright with one cyan line at its foot.</item>
    /// <item><b>The ask</b> under them: the kind and the giver, the giver's words, and "Bring me" with the classes the
    /// voyage wants; See every class (Y) opens Vessels.</item>
    /// <item><b>Your crew says</b> under the chart: each seat's pilot, their line and their mood
    /// (<see cref="VoyageMood"/>).</item>
    /// <item><b>The foot</b>: the squad fit, the pay and Take the job (A), which opens Your Ark.</item>
    /// </list>
    /// The Crystal Run (Milestone 1) has no giver: its card shows its own pilot and story, no classes, and no fit.
    /// </summary>
    public sealed class VoyageQuay
    {
        // Rev 134 Board B, in the page's units (the window page is x 40-1880, y 172-1046 of the 1920 x 1080 board).
        public const float CardY = 18f, CardWidth = 230f, CardHeight = 420f, CardStep = 240f, MaxCards = 5;
        public static readonly Rect Chart = new(1220f, 18f, 620f, 420f);
        const float AskY = 468f, AskWidth = 1180f, CrewX = 1220f, CrewWidth = 620f, FootY = 818f, FootHeight = 56f;
        const float TakeWidth = 300f, SeeX = 860f, SeeY = 618f, SeeWidth = 260f, SeeHeight = 44f;
        const float Inset = 18f, FootShade = 190f, DimPicture = 0.55f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        readonly VoyageQuaySO _words;
        readonly VoyageClassesSO _classes;
        readonly PilotArtifactCatalogSO _catalog;

        public RectTransform Root { get; }
        readonly CanvasGroup _group;
        readonly List<CardView> _cards = new();
        TMP_Text _askLabel, _askQuote, _bringMe, _crewLabel, _fitText, _pay, _cells;
        RectTransform _ask, _bring, _crew, _fit;
        readonly List<(RectTransform root, VoyageGlyph glyph, TMP_Text name)> _bringTags = new();
        readonly List<CrewRow> _crewRows = new();
        readonly Image[] _fitSegments = new Image[3];
        Button _take;

        sealed class CardView
        {
            public RectTransform Root;
            public Image Picture, Foot;
            public VoyageOutline Edge;
            public TMP_Text Kind, Title;
            public readonly List<VoyageGlyph> Glyphs = new();
            public Button Button;
        }

        sealed class CrewRow
        {
            public RectTransform Root;
            public Image Face;
            public TMP_Text Quote;
            public VoyageMoodMark Mood;
        }

        public VoyageQuay(VoyageBoard board, VoyageKit kit, VoyageSeats rules, RectTransform frame)
        {
            _board = board;
            _kit = kit;
            _rules = rules;
            _words = VoyageQuaySO.Load();
            _classes = VoyageClassesSO.Load();
            _catalog = PilotArtifactCatalogSO.Load();
            Root = VoyageKit.Node("Quay", frame);
            Root.anchorMin = Root.anchorMax = Root.pivot = Vector2.zero;
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            BuildAsk();
            BuildCrew();
            BuildFoot();
        }

        // Page units, measured from the page's top-left, inside the quay's own rect (the window page).
        RectTransform At(string name, float x, float y, float w, float h, RectTransform parent = null)
        {
            var rt = VoyageKit.Node(name, parent ? parent : Root);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        /// <summary>Stand the quay at <paramref name="page"/> (the window page in the frame's units) and fade it out as the
        /// full screen opens (<paramref name="full"/> 0-1).</summary>
        public void Lay(Rect page, float full)
        {
            Root.anchoredPosition = page.position;
            Root.sizeDelta = page.size;
            _group.alpha = 1f - full;
            _group.blocksRaycasts = full < 0.5f;
            Root.gameObject.SetActive(full < 1f);
        }

        // ── Building ─────────────────────────────────────────────────────────

        VoyageGlyph Glyph(Transform parent, float size)
        {
            var rt = VoyageKit.Node("Glyph", parent);
            var le = VoyageKit.Le(rt);
            le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = size;
            var g = rt.gameObject.AddComponent<VoyageGlyph>();
            g.raycastTarget = false;
            return g;
        }

        void BuildAsk()
        {
            // The kind and the giver (caps 14, Sub), the giver's words (24, wrapping), then "Bring me" and the classes (16).
            _ask = At("Ask", 0f, AskY, AskWidth, 300f);
            VoyageKit.Column(_ask, 16f, TextAnchor.UpperLeft);
            _askLabel = _kit.Label(_ask, 14, _kit.Sub);
            _askQuote = _kit.Text(_ask, 24, _kit.Light, TextAlignmentOptions.TopLeft);
            _askQuote.textWrappingMode = TextWrappingModes.Normal;
            _askQuote.overflowMode = TextOverflowModes.Ellipsis;
            _askQuote.lineSpacing = 14f;
            VoyageKit.Le(_askQuote).preferredHeight = 110f;
            _bring = VoyageKit.Node("BringMe", _ask);
            VoyageKit.Le(_bring).preferredHeight = 28f;
            var row = VoyageKit.Row(_bring, 28f);
            row.childForceExpandHeight = true;
            _bringMe = _kit.Label(_bring, 14, _kit.Sub);
            _bringMe.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(_bringMe, _words ? _words.BringMe : null);
            for (int i = 0; i < 3; i++)
            {
                var tag = VoyageKit.Node("Class" + i, _bring);
                VoyageKit.Row(tag, 8f).childForceExpandHeight = true;
                var glyph = Glyph(tag, 20f);
                var name = _kit.Text(tag, 16, _kit.Light, TextAlignmentOptions.Left);
                name.overflowMode = TextOverflowModes.Overflow;
                _bringTags.Add((tag, glyph, name));
            }
            var see = At("SeeEveryClass", SeeX, SeeY, SeeWidth, SeeHeight);
            _kit.Quiet(see, _words ? _words.SeeClassesKey : null, _words ? _words.SeeClasses : null, _board.BrowseVessels,
                out var seeWords);
            if (seeWords) seeWords.fontSize = 14f;
        }

        void BuildCrew()
        {
            // "Your crew says" (caps 14, Sub), then a row per seat: the face (36 x 44), the line (15) and the mood mark.
            _crew = At("CrewSays", CrewX, AskY, CrewWidth, 300f);
            VoyageKit.Column(_crew, 12f, TextAnchor.UpperLeft);
            _crewLabel = _kit.Label(_crew, 14, _kit.Sub);
            VoyageKit.Set(_crewLabel, _words ? _words.CrewSays : null);
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            {
                var row = new CrewRow { Root = VoyageKit.Node("Seat" + i, _crew) };
                VoyageKit.Le(row.Root).preferredHeight = 44f;
                var h = VoyageKit.Row(row.Root, 12f);
                h.childForceExpandHeight = false;
                var face = VoyageKit.Node("Face", row.Root);
                var fle = VoyageKit.Le(face);
                fle.preferredWidth = fle.minWidth = 36f;
                fle.preferredHeight = fle.minHeight = 44f;
                VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
                row.Face = VoyageKit.CoverBox(face);
                row.Quote = _kit.Text(row.Root, 15, _kit.Light, TextAlignmentOptions.Left);
                VoyageKit.Le(row.Quote).flexibleWidth = 1f;
                var mark = VoyageKit.Node("Mood", row.Root);
                var mle = VoyageKit.Le(mark);
                mle.preferredWidth = mle.minWidth = mle.preferredHeight = mle.minHeight = 18f;
                row.Mood = mark.gameObject.AddComponent<VoyageMoodMark>();
                row.Mood.raycastTarget = false;
                _crewRows.Add(row);
            }
        }

        void BuildFoot()
        {
            // The fit meter (three 10 high segments in 180) and "Squad fit N of 3" (15) at the left; the pay (16) and Take
            // the job (A) at the right.
            _fit = At("Fit", 0f, FootY, 700f, FootHeight);
            VoyageKit.Row(_fit, 14f);
            var meter = VoyageKit.Node("Meter", _fit);
            var le = VoyageKit.Le(meter);
            le.preferredWidth = le.minWidth = 180f;
            le.preferredHeight = le.minHeight = 10f;
            VoyageKit.Row(meter, 4f, TextAnchor.MiddleLeft, expandWidth: true).childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                var seg = VoyageKit.Node("Segment" + i, meter);
                VoyageKit.Le(seg).flexibleWidth = 1f;
                _fitSegments[i] = VoyageKit.Block(seg, _kit.Light);
            }
            _fitText = _kit.Text(_fit, 15, _kit.Light, TextAlignmentOptions.Left);
            _fitText.overflowMode = TextOverflowModes.Overflow;

            float takeX = Chart.xMax - TakeWidth;
            _pay = _kit.Text(At("Pay", takeX - 24f - 600f, FootY, 600f, FootHeight), 16, Color.Lerp(_kit.Sub, _kit.Light, 0.6f),
                TextAlignmentOptions.Right);
            VoyageKit.Stretch(_pay.rectTransform);
            (_take, _) = _kit.Cta(At("TakeTheJob", takeX, FootY, TakeWidth, FootHeight), _words ? _words.TakeJobKey : null,
                _words ? _words.TakeJob : null, _board.TakeJob);

            // The voyage's length, over the chart's foot (14, Sub).
            _cells = _kit.Text(At("Cells", Chart.x + 20f, Chart.yMax - 38f, Chart.width - 40f, 20f), 14, _kit.Sub,
                TextAlignmentOptions.Left);
            VoyageKit.Stretch(_cells.rectTransform);
        }

        CardView CardFor(int index)
        {
            while (_cards.Count <= index)
            {
                var view = new CardView { Root = At("Job" + _cards.Count, 0f, CardY, CardWidth, CardHeight) };
                var ground = VoyageKit.Block(view.Root, _kit.Popup, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = ground;
                view.Button.transition = Selectable.Transition.None;
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.Stretch(pic);
                view.Picture = VoyageKit.CoverBox(pic);
                // The foot darkens to the page's ground from halfway down its last 190.
                var shade = At("Shade", 0f, CardHeight - FootShade, CardWidth, FootShade, view.Root);
                var g = shade.gameObject.AddComponent<VoyageGradient>();
                g.raycastTarget = false;
                var ink = _kit.RowPicked;
                g.Set(VoyageKit.Fade(ink, 0f), VoyageKit.Fade(ink, 0.95f / Mathf.Max(0.01f, ink.a)), VoyageKit.Fade(ink, 0.95f / Mathf.Max(0.01f, ink.a)), 0.5f);
                var words = At("Words", Inset, CardHeight - 140f, CardWidth - 2f * Inset, 140f - Inset, view.Root);
                VoyageKit.Column(words, 8f, TextAnchor.LowerLeft);
                view.Kind = _kit.Label(words, 14, _kit.Light);
                view.Title = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.BottomLeft);
                view.Title.textWrappingMode = TextWrappingModes.Normal;
                VoyageKit.Le(view.Title).preferredHeight = 48f;
                var glyphs = VoyageKit.Node("Classes", words);
                VoyageKit.Le(glyphs).preferredHeight = 18f;
                var gr = VoyageKit.Row(glyphs, 8f);
                gr.childForceExpandWidth = false;
                for (int i = 0; i < 3; i++) view.Glyphs.Add(Glyph(glyphs, 18f));
                var edge = VoyageKit.Node("Edge", view.Root);
                VoyageKit.Stretch(edge);
                view.Edge = edge.gameObject.AddComponent<VoyageOutline>();
                view.Edge.raycastTarget = false;
                view.Foot = VoyageKit.Block(At("Line", 0f, CardHeight - 2f, CardWidth, 2f, view.Root), _kit.Cyan);
                _cards.Add(view);
            }
            return _cards[index];
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        static VoyageContent Content => VoyageContentSO.Load() is { } so ? so.Content : null;

        /// <summary>The giver of <paramref name="offer"/>'s voyage type (the Voyage Codex), or null (the Crystal Run).</summary>
        public static VoyageTypeInfo TypeOf(VoyageBoard.Offer offer) =>
            offer == null ? null : Content?.VoyageType(VoyageClassesSO.JobOf(offer.Key));

        /// <summary>The faction of the pilot a seat flies with: the player's own pilot record, else the authored crew.</summary>
        public static FactionType FactionOf(string pilotId)
        {
            if (string.IsNullOrEmpty(pilotId)) return FactionType.None;
            if (PilotLoadoutStore.TryGetPilot(pilotId, out var record) && record != null && record.FactionType != FactionType.None)
                return record.FactionType;
            return Content?.Person(pilotId)?.Faction ?? FactionType.None;
        }

        /// <summary>The mood of the pilot in <paramref name="slot"/> (<paramref name="pilot"/> its callsign) on
        /// <paramref name="offer"/>'s voyage.</summary>
        public static PilotMood MoodOf(VoyageBoard.Offer offer, SquadSlot slot, PilotSO pilot)
        {
            var info = TypeOf(offer);
            if (info == null) return PilotMood.None;
            string id = !string.IsNullOrEmpty(slot.PilotId) ? slot.PilotId : pilot ? pilot.Id : null;
            return VoyageMood.Of(info, FactionOf(id));
        }

        /// <summary>Draw <paramref name="offers"/> (the open-now indices <paramref name="shown"/>) with
        /// <paramref name="open"/> picked, at difficulty <paramref name="difficulty"/>.</summary>
        public void Draw(IReadOnlyList<VoyageBoard.Offer> offers, IReadOnlyList<int> shown, int open, int difficulty)
        {
            int n = 0;
            for (int i = 0; i < shown.Count && n < MaxCards; i++, n++)
                DrawCard(CardFor(n), n, offers, shown[i], open);
            for (int i = n; i < _cards.Count; i++) _cards[i].Root.gameObject.SetActive(false);

            var offer = open >= 0 && open < offers.Count ? offers[open] : null;
            _ask.gameObject.SetActive(offer != null);
            _crew.gameObject.SetActive(false);
            _fit.gameObject.SetActive(false);
            _take.interactable = offer != null;
            VoyageKit.Set(_pay, null);
            VoyageKit.Set(_cells, null);
            if (offer == null || !_words) return;

            var info = TypeOf(offer);
            string job = VoyageClassesSO.JobOf(offer.Key);
            string giver = GiverName(info, offer);
            VoyageKit.Set(_askLabel, TemplateText.Fill(_words.AskLabel, "kind", offer.Kind, "giver", giver));
            string say = info != null && !string.IsNullOrEmpty(info.Say) ? info.Say : offer.Story;
            VoyageKit.Set(_askQuote, string.IsNullOrEmpty(say) ? null : TemplateText.Fill(_words.AskQuote, "say", say));
            var wants = _classes ? _classes.WantedBy(job) : System.Array.Empty<VoyageClass>();
            _bring.gameObject.SetActive(wants.Count > 0);
            for (int i = 0; i < _bringTags.Count; i++)
            {
                var (root, glyph, name) = _bringTags[i];
                root.gameObject.SetActive(i < wants.Count);
                if (i >= wants.Count) continue;
                glyph.Form = VoyageGlyph.Of(wants[i]);
                glyph.color = _kit.Light;
                VoyageKit.Set(name, _classes.NameOf(wants[i]));
            }

            var squad = VoyageSeatRecord.Get(offer.Key) ?? VoyageSeats.Active;
            DrawCrew(offer, squad);
            DrawFit(job, squad, wants.Count > 0);
            VoyageKit.Set(_pay, offer.Pays
                ? TemplateText.Fill(_board.Lines?.SeaOfferPay, "pay", VoyageBoard.PayAt(offer, difficulty).ToString(CultureInfo.InvariantCulture),
                    "element", _board.ElementName(offer.Element))
                : null);
            VoyageKit.Set(_cells, offer.Stops.Count > 0
                ? TemplateText.Fill(_words.Cells, "cells", offer.Stops.Count.ToString(CultureInfo.InvariantCulture))
                : null);
        }

        string GiverName(VoyageTypeInfo info, VoyageBoard.Offer offer)
        {
            if (info != null && !string.IsNullOrEmpty(info.Giver))
            {
                var person = Content?.Person(info.Giver);
                if (person != null && !string.IsNullOrEmpty(person.Name)) return person.Name;
                if (_catalog && _catalog.TryGetPilot(info.Giver, out var callsign) && callsign) return callsign.DisplayName;
            }
            return offer.Pilot;
        }

        void DrawCard(CardView view, int slot, IReadOnlyList<VoyageBoard.Offer> offers, int index, int open)
        {
            var offer = offers[index];
            bool on = index == open;
            view.Root.gameObject.SetActive(true);
            VoyageKit.At(view.Root, slot * CardStep, CardY, CardWidth, CardHeight);
            var info = TypeOf(offer);
            Sprite face = null;
            if (info != null && _catalog && _catalog.TryGetPilot(info.Giver, out var giver) && giver) face = giver.Portrait;
            if (!face) face = offer.Portrait;
            bool portrait = face;
            var picture = portrait ? face : offer.Picture;
            VoyageKit.ShowCover(view.Picture, picture, new Vector2(CardWidth, CardHeight),
                portrait ? new Vector2(0.5f, 0.2f) : new Vector2(0.5f, 0.5f));
            view.Picture.color = new Color(1f, 1f, 1f, on ? 1f : DimPicture);
            VoyageKit.Set(view.Kind, offer.Kind);
            view.Kind.color = on ? _kit.Light : _kit.Sub;
            VoyageKit.Set(view.Title, offer.Title);
            var wants = _classes ? _classes.WantedBy(VoyageClassesSO.JobOf(offer.Key)) : System.Array.Empty<VoyageClass>();
            for (int i = 0; i < view.Glyphs.Count; i++)
            {
                bool shown = i < wants.Count;
                view.Glyphs[i].gameObject.SetActive(shown);
                if (!shown) continue;
                view.Glyphs[i].Form = VoyageGlyph.Of(wants[i]);
                view.Glyphs[i].color = on ? _kit.Light : _kit.Sub;
            }
            // The picked job: one cyan line at its foot; the others inside one thin line.
            view.Foot.enabled = on;
            view.Edge.enabled = !on;
            view.Edge.Solid(VoyageKit.Fade(_kit.Outline, 0.6f));
            view.Button.onClick.RemoveAllListeners();
            view.Button.onClick.AddListener(() => _board.Pick(index));
        }

        void DrawCrew(VoyageBoard.Offer offer, SavedSquad squad)
        {
            if (squad == null) return;
            bool any = false;
            var refuse = ColorTrace.Retrace(ColorTrace.Ui, VoyageHudColors.ThreatAuthored);
            for (int seat = 0; seat < _crewRows.Count; seat++)
            {
                var row = _crewRows[seat];
                var slot = squad.GetSlot(seat);
                var vessel = _rules.VesselOf(slot.Vessel);
                var pilot = _rules.PilotOf(slot, vessel);
                var mood = MoodOf(offer, slot, pilot);
                row.Root.gameObject.SetActive(mood != PilotMood.None);
                if (mood == PilotMood.None) continue;
                any = true;
                VoyageKit.ShowCover(row.Face, pilot ? pilot.Portrait : null, new Vector2(36f, 44f), VoyageKit.FaceFocus);
                VoyageKit.Set(row.Quote, TemplateText.Fill(_words.CrewQuote, "say", _words.Say(mood)));
                row.Quote.color = mood == PilotMood.Refuses ? refuse : _kit.Light;
                row.Mood.Set(mood, _kit.Light, VoyageKit.Fade(_kit.Outline, 0.8f), refuse);
            }
            _crew.gameObject.SetActive(any);
        }

        void DrawFit(string job, SavedSquad squad, bool shown)
        {
            _fit.gameObject.SetActive(shown && squad != null && _classes);
            if (!_fit.gameObject.activeSelf) return;
            var vessels = new List<VesselClassType>(SavedSquad.SlotCount);
            for (int s = 0; s < SavedSquad.SlotCount; s++) vessels.Add(squad.GetSlot(s).Vessel);
            int fit = _classes.Fit(job, vessels);
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i].color = i < fit ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.6f);
            VoyageKit.Set(_fitText, TemplateText.Fill(_words.Fit, "fit", fit.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
