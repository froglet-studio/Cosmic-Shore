using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages tab as the Sea table, as Shombith picked it on Frogma (Rev 89 A, Docs/MENU_REDESIGN_B.md sections
    /// 170-172; it replaces Rev 81 B5 and the Rev 83 S2 seat editor of section 165).
    /// <list type="bullet">
    /// <item><b>The voyages</b> (left): a small chip per voyage on offer, with its number and place, its numbers, and a
    /// sliver of its own water (<see cref="VoyageRoutePoster"/>). A press opens it: the chip lights and the table shows
    /// its course.</item>
    /// <item><b>The table</b> (middle): the window's own galaxy map lying back in perspective
    /// (<see cref="VoyageMapGraphic.Tilt"/>, <see cref="VoyageSlab"/>) with its edge-on view standing under its near
    /// edge as the front face: one slab cut out of the HyperSea. <see cref="VoyageJobsView"/> stands them in
    /// <see cref="CourseColumn"/> and <see cref="FitSlab"/> shapes them.</item>
    /// <item><b>The name</b>, over the table's far edge: who brought the voyage, where it goes, what it is, its numbers,
    /// and the Ark escort's difficulty stepper.</item>
    /// <item><b>The squad</b> (right): <see cref="VoyageSquadColumn"/>, with Set sail at its foot. A seat changes where
    /// it stands; there is no editor screen.</item>
    /// </list>
    /// Every word is the voyage content's (<see cref="VoyageLines"/>) or the game's data; every colour, sprite and
    /// proportion is <see cref="VoyageBoardStyleSO"/>'s.
    /// </summary>
    public class VoyageBoard : MonoBehaviour
    {
        /// <summary>One voyage on the list.</summary>
        public sealed class Row
        {
            public string Title, By, Meta;
            public readonly List<Vector2> Course = new();
        }

        /// <summary>The open voyage: its name over the table, and Set sail.</summary>
        public sealed class Voyage
        {
            /// <summary>The small line over the name (which voyage it is and who brought it).</summary>
            public string Kicker;
            /// <summary>Where it goes: the name over the table. Empty: <see cref="Title"/> is the name and
            /// <see cref="Line"/> stands under it.</summary>
            public string Place;
            public string Title, Line, Distance, Sail, Intensity;
            /// <summary>Set sail's label; empty: the content's own.</summary>
            public string SailLabel;
            public Action SetSail;
            /// <summary>A stepper under the numbers (the Ark escort's difficulty); null hides it.</summary>
            public string LowerLabel, RaiseLabel;
            public Action Lower, Raise;
        }

        /// <summary>The squad changed (another squad picked, a seat changed): the pilots bring the voyages, so the window
        /// redraws.</summary>
        public event Action SquadChanged;

        /// <summary>Where the window's galaxy map and edge view stand.</summary>
        public RectTransform CourseColumn => _courseColumn;

        /// <summary>The squad on the right (its seats and steppers).</summary>
        public VoyageSquadColumn Squad => _squad;

        // The table's foot stands this far above the page's foot, and the name this far below its top, for a table as wide
        // as the board drew it (1020); both scale with the table's width.
        internal const float BoardWidth = 1020f, FootGap = 26f;

        static readonly Color RouteAuthored = new(0.784f, 1f, 0.353f, 1f);
        static readonly Color ArkAuthored = new(1f, 0.78f, 0.25f, 1f);
        static readonly Color SeaAuthored = new(0.75f, 0.75f, 0.8f, 1f);

        // Every colour is painted as the worn colour set has it (ColorTrace, Docs/VesselModels/COLOR_TRACE.md).
        static Color Route => ColorTrace.Retrace(ColorTrace.Ui, RouteAuthored);
        static Color Ark => ColorTrace.Retrace(ColorTrace.Ui, ArkAuthored);
        static Color Sea => ColorTrace.Retrace(ColorTrace.Ui, SeaAuthored);

        VoyageKit _kit;
        VoyageBoardStyleSO _style;
        VoyageLines _lines;
        VoyageSquadColumn _squad;

        RectTransform _root, _listContent, _courseColumn, _mapHost, _edgeHost;
        TMP_Text _message, _kicker, _name, _sub, _stats, _setSailText, _lowerText, _raiseText;
        Button _setSail, _lower, _raise;
        RectTransform _stepper;
        readonly List<RowView> _rows = new();
        Voyage _voyage;
        Action<int> _pick;
        float _fitWidth = -1f;

        sealed class RowView
        {
            public Button Button;
            public Image Plate, Bar, Edge;
            public VoyageRoutePoster Poster;
            public CanvasGroup Water;
            public TMP_Text Title, Meta;
        }

        // ── Building ─────────────────────────────────────────────────────────

        /// <summary>The board, built once over <paramref name="host"/> (the Voyages page), its text in
        /// <paramref name="font"/>'s face.</summary>
        public static VoyageBoard Create(RectTransform host, TMP_Text font, SO_VesselList vessels, VoyageLines lines)
        {
            var rt = VoyageKit.Node("VoyageBoard", host);
            rt.SetAsLastSibling();
            VoyageKit.Stretch(rt);
            var board = rt.gameObject.AddComponent<VoyageBoard>();
            board._root = rt;
            board._lines = lines;
            board._style = VoyageBoardStyleSO.Load();
            board._kit = new VoyageKit(board._style, lines, font);
            board.Build(vessels);
            return board;
        }

        void Build(SO_VesselList vessels)
        {
            var s = _style;
            // The voyages: chips in a scroll, so more voyages than fit can be reached.
            var list = VoyageKit.Node("Voyages", _root);
            VoyageKit.Anchor(list, 0f, 0f, s.ListRight, 1f);
            list.gameObject.AddComponent<RectMask2D>();
            _listContent = VoyageKit.Node("Chips", list);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = Vector2.one;
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.sizeDelta = Vector2.zero;
            VoyageKit.Column(_listContent, 10f);
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.content = _listContent;
            scroll.viewport = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            _message = _kit.Text(list, 18, _kit.Sub, TextAlignmentOptions.TopLeft);
            VoyageKit.Anchor(_message.rectTransform, 0f, 0f, 1f, 1f);
            _message.textWrappingMode = TextWrappingModes.Normal;

            // The table's column: VoyageJobsView stands the map and the edge view in it, FitSlab shapes them.
            _courseColumn = VoyageKit.Node("Table", _root);
            VoyageKit.Anchor(_courseColumn, s.CourseLeft, 0f, s.CourseRight, 1f);

            BuildName();

            var panel = VoyageKit.Node("Squad", _root);
            VoyageKit.Anchor(panel, s.PanelLeft, 0f, 1f, 1f);
            _squad = new VoyageSquadColumn(_kit, panel, vessels);
            _squad.Changed += () => SquadChanged?.Invoke();
            (_setSail, _setSailText) = _kit.Cta(_squad.Foot, _lines?.BoardSailKey, _lines?.CardSetSail, () => _voyage?.SetSail?.Invoke());
        }

        /// <summary>The open voyage's name, over the table's far edge (where the lying map has faded out).</summary>
        void BuildName()
        {
            var head = VoyageKit.Node("Name", _root);
            head.anchorMin = new Vector2(_style.CourseLeft, 1f);
            head.anchorMax = new Vector2(_style.CourseRight, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(0f, 220f);
            head.anchoredPosition = new Vector2(0f, 6f);
            VoyageKit.Column(head, 8f, TextAnchor.UpperCenter);
            _kicker = _kit.Label(head, 13, _kit.Sub, TextAlignmentOptions.Center);
            _name = _kit.Text(head, 48, _kit.Light, TextAlignmentOptions.Center);
            _sub = _kit.Text(head, 18, _kit.Light, TextAlignmentOptions.Center);
            _stats = _kit.Text(head, 22, _kit.Light, TextAlignmentOptions.Center);

            // The Ark escort's difficulty, under the numbers.
            _stepper = VoyageKit.Node("Stepper", head);
            VoyageKit.Row(_stepper, 10f, TextAnchor.MiddleCenter);
            (_lower, _lowerText) = SmallButton(_stepper, () => _voyage?.Lower?.Invoke());
            (_raise, _raiseText) = SmallButton(_stepper, () => _voyage?.Raise?.Invoke());
        }

        (Button b, TMP_Text t) SmallButton(Transform parent, Action onClick)
        {
            var rt = VoyageKit.Node("Step", parent);
            var b = _kit.Plated(rt, _kit.Popup, true, out _, out _);
            var h = VoyageKit.Row(rt, 0f, TextAnchor.MiddleCenter);
            h.padding = new RectOffset(16, 16, 8, 8);
            var t = _kit.Text(rt, 15, _kit.Light, TextAlignmentOptions.Center);
            b.onClick.AddListener(() => onClick());
            return (b, t);
        }

        RowView RowFor(int index)
        {
            while (_rows.Count <= index)
            {
                var s = _style;
                var view = new RowView();
                int i = _rows.Count;
                var rt = VoyageKit.Node("Voyage" + i, _listContent);
                VoyageKit.Le(rt).preferredHeight = s.RowHeight;
                view.Button = _kit.Plated(rt, _kit.RowPicked, true, out view.Plate, out view.Edge);
                view.Button.onClick.AddListener(() => _pick?.Invoke(i));

                // A sliver of the voyage's own water at the chip's right, fading in from the words.
                var water = VoyageKit.Node("Water", rt);
                VoyageKit.Anchor(water, 0.56f, 0f, 1f, 1f);
                water.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(48, 0);
                view.Water = water.gameObject.AddComponent<CanvasGroup>();
                var poster = VoyageKit.Node("Course", water);
                VoyageKit.Stretch(poster);
                view.Poster = poster.gameObject.AddComponent<VoyageRoutePoster>();
                view.Poster.LineScale = 0.45f;

                var bar = VoyageKit.Node("Bar", rt);
                bar.anchorMin = Vector2.zero;
                bar.anchorMax = new Vector2(0f, 1f);
                bar.pivot = new Vector2(0f, 0.5f);
                bar.sizeDelta = new Vector2(3f, 0f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);

                var words = VoyageKit.Node("Words", rt);
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(20f, 0f);
                words.offsetMax = new Vector2(-12f, 0f);
                VoyageKit.Column(words, 6f, TextAnchor.MiddleLeft);
                view.Title = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
                view.Meta = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);
                _rows.Add(view);
            }
            return _rows[index];
        }

        // ── The table ────────────────────────────────────────────────────────

        /// <summary>
        /// Shape the table from the hosts <see cref="VoyageJobsView"/> stood in <see cref="CourseColumn"/>: the edge view
        /// is the front face at the column's foot, the map lies over it, both as wide as the column and in the board's
        /// shapes (<see cref="VoyageSlab.FaceAspect"/>, <see cref="VoyageSlab.TopAspect"/>). Refitted when the column's
        /// width changes.
        /// </summary>
        public void FitSlab(RectTransform mapHost, RectTransform edgeHost)
        {
            _mapHost = mapHost;
            _edgeHost = edgeHost;
            _fitWidth = -1f;
            Fit();
        }

        void Fit()
        {
            if (!_courseColumn) return;
            float w = _courseColumn.rect.width;
            if (w < 1f || Mathf.Approximately(w, _fitWidth)) return;
            _fitWidth = w;
            float foot = FootGap * w / BoardWidth, face = w / VoyageSlab.FaceAspect, top = w / VoyageSlab.TopAspect;
            if (_edgeHost) VoyageKit.Foot(_edgeHost, foot, face);
            if (_mapHost) VoyageKit.Foot(_mapHost, foot + face, top);
        }

        void LateUpdate() => Fit();

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Show <paramref name="rows"/> with row <paramref name="open"/> open (-1: none), the open voyage's name over the
        /// table and the squad. <paramref name="message"/> stands in the list when there are no rows (no work offered).
        /// </summary>
        public void Show(IReadOnlyList<Row> rows, int open, Action<int> pick, Voyage voyage, VoyageMapGraphic map, string message)
        {
            _pick = pick;
            _voyage = voyage;
            int count = rows?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                var view = RowFor(i);
                view.Button.gameObject.SetActive(true);
                bool on = i == open;
                var plate = _kit.RowPicked;
                view.Plate.color = on ? plate : new Color(plate.r, plate.g, plate.b, plate.a * 0.7f);
                view.Bar.enabled = on;
                view.Edge.enabled = !on;
                view.Water.alpha = on ? 1f : 0.5f;
                VoyageKit.Set(view.Title, row.Title);
                view.Title.color = on ? _kit.Light : _kit.Sub;
                VoyageKit.Set(view.Meta, row.Meta);
                view.Poster.Show(map, row.Course, Route, Ark, Sea);
            }
            for (int i = count; i < _rows.Count; i++) _rows[i].Button.gameObject.SetActive(false);
            VoyageKit.Set(_message, count == 0 ? message : null);

            bool placed = !string.IsNullOrEmpty(voyage?.Place);
            VoyageKit.Set(_kicker, voyage?.Kicker);
            VoyageKit.Set(_name, placed ? voyage.Place : voyage?.Title);
            VoyageKit.Set(_sub, placed ? voyage.Title : voyage?.Line);
            VoyageKit.Set(_stats, Stats(voyage));
            Blank(_kicker);
            Blank(_sub);
            Blank(_stats);
            bool step = voyage?.Lower != null && voyage.Raise != null;
            _stepper.gameObject.SetActive(step);
            if (step)
            {
                VoyageKit.Set(_lowerText, voyage.LowerLabel);
                VoyageKit.Set(_raiseText, voyage.RaiseLabel);
            }
            if (_setSail)
            {
                _setSail.interactable = voyage?.SetSail != null;
                VoyageKit.Set(_setSailText, !string.IsNullOrEmpty(voyage?.SailLabel) ? voyage.SailLabel : _lines?.CardSetSail);
            }
            _squad.Draw();
        }

        /// <summary>The open voyage's numbers on one line (the content's <see cref="VoyageLines.BoardStats"/>); nothing
        /// for a voyage with none (the Ark escort).</summary>
        string Stats(Voyage voyage) =>
            voyage == null || string.IsNullOrEmpty(voyage.Distance) ? null
                : TemplateText.Fill(_lines?.BoardStats, "distance", voyage.Distance, "sail", voyage.Sail, "intensity", voyage.Intensity);

        // An empty line takes no room in the name's column.
        static void Blank(TMP_Text t) => t.gameObject.SetActive(!string.IsNullOrEmpty(t.text));
    }
}
