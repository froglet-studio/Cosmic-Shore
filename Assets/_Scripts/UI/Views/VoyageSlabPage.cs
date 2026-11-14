using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A Voyage window tab drawn as the Sea table (Rev 90, Docs/MENU_REDESIGN_B.md sections 171-172): chips on the left,
    /// a picture lying back in perspective with a front face under its near edge (<see cref="VoyageSlab"/>), a name over
    /// it, and one column on the right. The Voyages tab is <see cref="VoyageBoard"/>; the Saga, Quests and Episodes tabs
    /// are this, each with its own data (<see cref="VoyageSagaSlab"/>, <see cref="VoyageQuestSlab"/>,
    /// <see cref="VoyageEpisodeSlab"/>).
    ///
    /// <para>A page is stood over its tab's page by <see cref="Stand"/> (the first time the Voyages tab is drawn as the
    /// board): what the page drew before is hidden and kept, because its components hold the data (the saga's cards,
    /// the quest list). The tab pages are not the size of the Voyages page, so the table is laid over the Voyages page's
    /// own rect (<see cref="Match"/>): every tab's table stands in the same place.</para>
    /// </summary>
    public abstract class VoyageSlabPage : MonoBehaviour
    {
        protected VoyageKit Kit;
        protected SO_VesselList Vessels;
        protected VoyageWindowTabs Tabs;
        protected VoyageLines Lines => Kit.Lines;

        RectTransform _root, _reference, _column, _faceRect, _topRect;
        float _fitWidth = -1f;
        bool _built;

        protected RectTransform Root => _root;
        /// <summary>The table's top: a still picture lying back.</summary>
        protected VoyageSlabPicture Picture { get; private set; }
        /// <summary>The table's front face, under the picture's near edge.</summary>
        protected RectTransform Face => _faceRect;
        /// <summary>The right column.</summary>
        protected RectTransform Panel { get; private set; }
        protected TMP_Text Kicker, Name, Sub, Extra;

        RectTransform _chipList;
        readonly List<ChipView> _chips = new();
        RectTransform _track;
        TMP_Text _faceWords;
        readonly List<StationView> _stations = new();

        protected sealed class ChipView
        {
            public Button Button;
            public Image Plate, Bar, Edge, Art;
            public CanvasGroup ArtGroup;
            public TMP_Text Title, Meta;
        }

        /// <summary>One chip on the left.</summary>
        protected struct Chip
        {
            public string Title, Meta;
            public Sprite Art;
            public bool Dim;
        }

        sealed class StationView
        {
            public RectTransform Root;
            public TMP_Text Key, Name, Note;
            public Image Ring, Fill, Under;
            public LayoutElement UnderSize;
        }

        /// <summary>One station on the front face's line.</summary>
        protected struct Station
        {
            public string Key, Name, Note;
            /// <summary>The mark's outline and its inside (clear: hollow).</summary>
            public Color Ring, Fill;
            public bool Dim, Picked;
        }

        /// <summary>
        /// Stand the Saga, Quests and Episodes tables over <paramref name="tabs"/>' second, third and fourth pages, each
        /// laid over <paramref name="reference"/> (the rect the Voyages board stands on). Once: a page that already has
        /// its table is left alone.
        /// </summary>
        public static void Stand(VoyageWindowTabs tabs, VoyageKit kit, SO_VesselList vessels, RectTransform reference,
            bool sagaLocked)
        {
            if (!tabs || kit == null || tabs.Count < 4 || !reference) return;
            Add<VoyageSagaSlab>(tabs, 1, kit, vessels, reference, saga => saga.Locked = sagaLocked);
            Add<VoyageQuestSlab>(tabs, 2, kit, vessels, reference, null);
            Add<VoyageEpisodeSlab>(tabs, 3, kit, vessels, reference, null);
        }

        static void Add<T>(VoyageWindowTabs tabs, int index, VoyageKit kit, SO_VesselList vessels, RectTransform reference,
            Action<T> setUp) where T : VoyageSlabPage
        {
            var page = tabs.PageOf(index);
            if (!page || page.TryGetComponent<T>(out _)) return;
            var slab = page.AddComponent<T>();
            slab.Kit = kit;
            slab.Vessels = vessels;
            slab.Tabs = tabs;
            slab._reference = reference;
            setUp?.Invoke(slab);
            // A page that is on show now draws at once; the others draw when their tab is shown (OnEnable).
            if (page.activeInHierarchy) slab.Redraw();
        }

        protected virtual void OnEnable()
        {
            ColorTrace.Changed += Redraw;
            if (Kit != null) Redraw();
        }

        protected virtual void OnDisable() => ColorTrace.Changed -= Redraw;

        void LateUpdate()
        {
            if (!_built) return;
            Match();
            Fit();
        }

        /// <summary>Build the table once, then draw it from the data as it is now.</summary>
        protected void Redraw()
        {
            if (Kit == null) return;
            if (!_built)
            {
                Build();
                _built = true;
            }
            Match();
            Fit();
            Draw();
        }

        /// <summary>Draw the page's own data on the table.</summary>
        protected abstract void Draw();

        /// <summary>Build the page's own parts in <see cref="Panel"/> (the right column), after the shared ones.</summary>
        protected abstract void BuildPanel();

        void Build()
        {
            var s = Kit.Style;
            // What the page drew before stands down; its components stay, they hold the data.
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            _root = VoyageKit.Node("SeaTable", transform);
            VoyageKit.Le(_root).ignoreLayout = true;

            _chipList = VoyageKit.Node("Chips", _root);
            _chipList.anchorMin = new Vector2(0f, 1f);
            _chipList.anchorMax = new Vector2(s.ListRight, 1f);
            _chipList.pivot = new Vector2(0.5f, 1f);
            _chipList.sizeDelta = Vector2.zero;
            _chipList.anchoredPosition = Vector2.zero;
            VoyageKit.Column(_chipList, 10f);
            _chipList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _column = VoyageKit.Node("Table", _root);
            VoyageKit.Anchor(_column, s.CourseLeft, 0f, s.CourseRight, 1f);
            _topRect = VoyageKit.Node("Top", _column);
            Picture = _topRect.gameObject.AddComponent<VoyageSlabPicture>();
            _faceRect = VoyageKit.Node("Face", _column);
            VoyageKit.Block(_faceRect, Kit.Popup);
            var seam = VoyageKit.Node("Seam", _faceRect);
            VoyageKit.Top(seam, 0f, 1f);
            VoyageKit.Block(seam, Kit.Light);

            // The line across the front face, its stations over it; or one line of words.
            var rule = VoyageKit.Node("Line", _faceRect);
            rule.anchorMin = new Vector2(0f, 1f);
            rule.anchorMax = Vector2.one;
            rule.pivot = new Vector2(0.5f, 1f);
            rule.offsetMin = new Vector2(40f, 0f);
            rule.offsetMax = new Vector2(-40f, 0f);
            rule.sizeDelta = new Vector2(rule.sizeDelta.x, 1f);
            rule.anchoredPosition = new Vector2(0f, -72f);
            _rule = VoyageKit.Block(rule, Kit.Outline);
            _track = VoyageKit.Node("Stations", _faceRect);
            VoyageKit.Stretch(_track);
            _track.offsetMin = new Vector2(40f, 0f);
            _track.offsetMax = new Vector2(-40f, -30f);
            VoyageKit.Row(_track, 0f, TextAnchor.UpperCenter, expandWidth: true);
            _faceWords = Kit.Text(_faceRect, 22, Kit.Light, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_faceWords.rectTransform);

            var head = VoyageKit.Node("Name", _root);
            head.anchorMin = new Vector2(s.CourseLeft, 1f);
            head.anchorMax = new Vector2(s.CourseRight, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(0f, 220f);
            head.anchoredPosition = new Vector2(0f, 6f);
            VoyageKit.Column(head, 8f, TextAnchor.UpperCenter);
            Kicker = Kit.Label(head, 13, Kit.Sub, TextAlignmentOptions.Center);
            Name = Kit.Text(head, 48, Kit.Light, TextAlignmentOptions.Center);
            Sub = Kit.Text(head, 18, Kit.Light, TextAlignmentOptions.Center);
            Extra = Kit.Text(head, 22, Kit.Light, TextAlignmentOptions.Center);

            Panel = VoyageKit.Node("Panel", _root);
            VoyageKit.Anchor(Panel, s.PanelLeft, 0f, 1f, 1f);
            BuildPanel();
        }
        Image _rule;

        /// <summary>Lay the table over the Voyages page's rect, so every tab's table stands in the same place.</summary>
        void Match()
        {
            if (!_root || !_reference) return;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = _reference.pivot;
            var size = _reference.rect.size;
            if (_root.sizeDelta != size) _root.sizeDelta = size;
            if (_root.position != _reference.position) _root.position = _reference.position;
        }

        // The table's foot stands this far above the page's foot for a table as wide as the board drew it (1020).
        const float BoardWidth = 1020f, FootGap = 26f;

        /// <summary>The front face at the column's foot and the picture lying over it (Rev 90).</summary>
        void Fit()
        {
            if (!_column) return;
            float w = _column.rect.width;
            if (w < 1f || Mathf.Approximately(w, _fitWidth)) return;
            _fitWidth = w;
            float foot = FootGap * w / BoardWidth, face = w / VoyageSlab.FaceAspect, top = w / VoyageSlab.TopAspect;
            VoyageKit.Foot(_faceRect, foot, face);
            VoyageKit.Foot(_topRect, foot + face, top);
        }

        // ── The shared parts ─────────────────────────────────────────────────

        // A locked page's picture: multiplied down, as the board drew it (saturate .25, brightness .9).
        static readonly Color DimAuthored = new(0.5f, 0.52f, 0.6f, 1f);

        /// <summary>
        /// A locked page (Episodes; the Saga while it is locked): no chips, <paramref name="picture"/> on the table
        /// dimmed (none: the galaxy), the page's name over it with "Coming soon", one line on the front face.
        /// </summary>
        protected void ShowLocked(string kicker, string name, string line, string face, Sprite picture)
        {
            ShowChips(null, -1, null);
            ShowName(kicker, name, line, Lines?.SlabEpisodesSoon);
            Picture.color = ColorTrace.Retrace(ColorTrace.Ui, DimAuthored);
            if (picture)
            {
                Picture.Zoom = 1f;
                Picture.Show(picture);
            }
            else
            {
                Picture.Zoom = 1.9f;
                Picture.Show(VoyageGalaxyArt.Texture(), new Rect(0f, 0f, 1f, 1f), VoyageGalaxyArt.Aspect);
            }
            ShowFaceWords(face);
        }

        /// <summary>A locked page's one way out: a quiet button at the right column's foot, back to the Voyages tab.</summary>
        protected void BuildBack()
        {
            var back = VoyageKit.Node("Back", Panel);
            back.anchorMin = back.anchorMax = new Vector2(1f, 0f);
            back.pivot = new Vector2(1f, 0f);
            back.sizeDelta = new Vector2(340f, 56f);
            back.anchoredPosition = Vector2.zero;
            Kit.Quiet(back, Lines?.SlabEpisodesBackKey, Lines?.SlabEpisodesBack, () => { if (Tabs) Tabs.Show(0); });
        }

        /// <summary>Show the name over the table; an empty line takes no room.</summary>
        protected void ShowName(string kicker, string name, string sub, string extra)
        {
            Line(Kicker, kicker);
            Line(Name, name);
            Line(Sub, sub);
            Line(Extra, extra);
        }

        static void Line(TMP_Text t, string value)
        {
            VoyageKit.Set(t, value);
            t.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        /// <summary>Show <paramref name="chips"/> on the left with chip <paramref name="picked"/> lit; a press calls
        /// <paramref name="pick"/>.</summary>
        protected void ShowChips(IReadOnlyList<Chip> chips, int picked, Action<int> pick)
        {
            int count = chips?.Count ?? 0;
            while (_chips.Count < count) _chips.Add(NewChip(_chips.Count));
            for (int i = 0; i < _chips.Count; i++)
            {
                var view = _chips[i];
                bool show = i < count;
                view.Button.gameObject.SetActive(show);
                if (!show) continue;
                var chip = chips[i];
                bool on = i == picked;
                var plate = Kit.RowPicked;
                view.Plate.color = on ? plate : new Color(plate.r, plate.g, plate.b, plate.a * 0.7f);
                view.Bar.enabled = on;
                view.Edge.enabled = !on;
                VoyageKit.Set(view.Title, chip.Title);
                view.Title.color = on ? Kit.Light : Kit.Sub;
                VoyageKit.Set(view.Meta, chip.Meta);
                VoyageKit.Show(view.Art, chip.Art);
                view.ArtGroup.alpha = chip.Dim ? 0.3f : on ? 1f : 0.6f;
                int index = i;
                view.Button.onClick.RemoveAllListeners();
                view.Button.onClick.AddListener(() => pick?.Invoke(index));
            }
        }

        ChipView NewChip(int index)
        {
            var view = new ChipView();
            var rt = VoyageKit.Node("Chip" + index, _chipList);
            VoyageKit.Le(rt).preferredHeight = Kit.Style.RowHeight;
            view.Button = Kit.Plated(rt, Kit.RowPicked, true, out view.Plate, out view.Edge);

            // A sliver of the chip's own picture at its right, fading in from the words.
            var art = VoyageKit.Node("Art", rt);
            VoyageKit.Anchor(art, 0.56f, 0f, 1f, 1f);
            art.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(48, 0);
            view.ArtGroup = art.gameObject.AddComponent<CanvasGroup>();
            var picture = VoyageKit.Node("Picture", art);
            VoyageKit.Stretch(picture);
            view.Art = VoyageKit.Picture(picture);

            var bar = VoyageKit.Node("Bar", rt);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.sizeDelta = new Vector2(3f, 0f);
            view.Bar = VoyageKit.Block(bar, Kit.Cyan);

            var words = VoyageKit.Node("Words", rt);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(20f, 0f);
            words.offsetMax = new Vector2(-12f, 0f);
            VoyageKit.Column(words, 6f, TextAnchor.MiddleLeft);
            view.Title = Kit.Text(words, 18, Kit.Light, TextAlignmentOptions.Left);
            view.Meta = Kit.Text(words, 14, Kit.Sub, TextAlignmentOptions.Left);
            return view;
        }

        /// <summary>The front face as a line with <paramref name="stations"/> on it, in order.</summary>
        protected void ShowTrack(IReadOnlyList<Station> stations)
        {
            int count = stations?.Count ?? 0;
            VoyageKit.Set(_faceWords, null);
            _rule.enabled = count > 0;
            while (_stations.Count < count) _stations.Add(NewStation(_stations.Count));
            for (int i = 0; i < _stations.Count; i++)
            {
                var view = _stations[i];
                bool show = i < count;
                view.Root.gameObject.SetActive(show);
                if (!show) continue;
                var station = stations[i];
                VoyageKit.Set(view.Key, station.Key);
                VoyageKit.Set(view.Name, station.Name);
                view.Name.color = station.Dim ? Kit.Sub : Kit.Light;
                VoyageKit.Set(view.Note, station.Note);
                view.Note.gameObject.SetActive(!string.IsNullOrEmpty(station.Note));
                view.Ring.color = station.Ring;
                view.Fill.color = station.Fill.a > 0f ? station.Fill : Kit.Popup;
                view.Under.color = station.Picked ? Kit.Cyan : VoyageKit.Clear;
                view.UnderSize.preferredWidth = view.Name.GetPreferredValues(station.Name ?? string.Empty).x;
            }
        }

        /// <summary>The front face as one line of words (no stations).</summary>
        protected void ShowFaceWords(string words)
        {
            ShowTrack(null);
            VoyageKit.Set(_faceWords, words);
        }

        StationView NewStation(int index)
        {
            var view = new StationView { Root = VoyageKit.Node("Station" + index, _track) };
            var v = VoyageKit.Column(view.Root, 8f, TextAnchor.UpperCenter);
            v.childForceExpandWidth = false;
            view.Key = Kit.Label(view.Root, 13, Kit.Sub, TextAlignmentOptions.Center);
            // The mark: a small diamond on the line, an outline round its inside.
            var box = VoyageKit.Node("Mark", view.Root);
            var size = VoyageKit.Le(box);
            size.preferredWidth = size.preferredHeight = 30f;
            var ring = VoyageKit.Node("Ring", box);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.sizeDelta = new Vector2(14f, 14f);
            ring.localRotation = Quaternion.Euler(0f, 0f, 45f);
            view.Ring = VoyageKit.Block(ring, Kit.Light);
            var fill = VoyageKit.Node("Fill", ring);
            VoyageKit.Stretch(fill);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            view.Fill = VoyageKit.Block(fill, Kit.Popup);
            view.Name = Kit.Text(view.Root, 18, Kit.Light, TextAlignmentOptions.Center);
            var under = VoyageKit.Node("Picked", view.Root);
            view.UnderSize = VoyageKit.Le(under);
            view.UnderSize.preferredHeight = 2f;
            view.Under = VoyageKit.Block(under, VoyageKit.Clear);
            view.Note = Kit.Text(view.Root, 14, Kit.Sub, TextAlignmentOptions.Center);
            return view;
        }
    }

    /// <summary>
    /// The whole galaxy as a picture for a table's top (the Quests and Episodes tables): the HyperSea's own density in
    /// its own colours, as the map paints its water. Painted once, the first time a table asks (about 16,000 samples of
    /// the density field), and kept.
    /// </summary>
    public static class VoyageGalaxyArt
    {
        const int Width = 160, Height = 100;
        // The galaxy's disk is 100,000 ly across (the map's own corner picture, VoyageMapGraphic).
        const double Half = 50000;
        static Texture2D s_Texture;

        /// <summary>The picture's shape (width over height).</summary>
        public const float Aspect = Width / (float)Height;

        public static Texture2D Texture()
        {
            if (s_Texture) return s_Texture;
            var px = new Color32[Width * Height];
            double halfY = Half * Height / Width;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                double gx = -Half + 2 * Half * (x + 0.5) / Width;
                double gy = -(-halfY + 2 * halfY * (y + 0.5) / Height);
                var (r, g, b) = HyperSea.SeaColor(HyperSea.Density(gx, gy));
                px[y * Width + x] = new Color32((byte)r, (byte)g, (byte)b, 255);
            }
            s_Texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = "VoyageGalaxy",
            };
            s_Texture.SetPixels32(px);
            s_Texture.Apply(false);
            return s_Texture;
        }
    }
}
