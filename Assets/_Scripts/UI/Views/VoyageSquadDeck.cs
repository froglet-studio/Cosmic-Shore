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
    /// The Voyage window's squad screen, as Shombith picked it on Frogma (Rev 98 2D, Docs/MENU_REDESIGN_B.md sections
    /// 181-184): the squad's three seats large across the screen, and under them every choice the picked seat may take.
    /// <list type="bullet">
    /// <item><b>The stage</b>: each seat is its pilot and its hull, its place in the squad, the vessel's name, and one
    /// line of who flies it, the pilot, the element and the artifact. A press on a seat the player may change picks it;
    /// the picked seat is lit and underlined, the others dimmed. The player's own seat is picked first.</item>
    /// <item><b>The shelf</b>: Vessel, Pilot, Element and Artifact, every choice on screen as an open picture with its
    /// name under it. One press takes it and saves; the taken one is lit and underlined. No choice has a box.</item>
    /// </list>
    /// The rules are <see cref="VoyageSeats"/>'s (the Ark's). The spacing is the board's rule: lines of one group 10 or
    /// more apart, groups 28 or more apart, no text wrapping inside a tile.
    /// </summary>
    public sealed class VoyageSquadDeck
    {
        // The board's page is 1840 x 796; the stage and the shelf are placed as fractions of it.
        const float PageW = 1840f, PageH = 796f;
        const float StageTop = 68f, StageFoot = 398f, ShelfTop = 420f, ShelfFoot = 720f;
        const float ShelfInset = 110f, GroupGap = 28f, LabelRoom = 38f, TileGap = 16f;
        // The shelf's four groups, left to right, as widths of the page (they share what the insets and gaps leave).
        static readonly float[] GroupWidth = { 520f, 560f, 230f, 226f };
        const float VesselTileH = 104f, PilotTileH = 216f, ElementTileH = 104f, ArtifactTileH = 36f;

        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        VoyageLines Lines => _kit.Lines;

        readonly RectTransform _root;
        readonly SeatView[] _seats = new SeatView[SavedSquad.SlotCount];
        readonly GroupView[] _groups = new GroupView[4];
        readonly List<VoyageSeats.Option> _offered = new();
        int _picked = Unset;
        const int Unset = -2;

        /// <summary>The seat whose choices are on the shelf; -1: none.</summary>
        public int PickedSeat => _picked < 0 ? -1 : _picked;

        sealed class SeatView
        {
            public Button Button;
            public CanvasGroup Group;
            public VoyageSoftFill Glow;
            public Image Face;
            public VoyageSeats.HullView Hull;
            public TMP_Text Role, Name;
            public Image Bar;
            public readonly TMP_Text[] Parts = new TMP_Text[4];
        }

        sealed class GroupView
        {
            public RectTransform Area;
            public GridLayoutGroup Grid;
            public TMP_Text Label;
            public readonly List<TileView> Tiles = new();
        }

        sealed class TileView
        {
            public RectTransform Root, Flower;
            public Button Button;
            public CanvasGroup Group;
            public VoyageSoftFill Glow;
            public Image Face;
            public VoyageSeats.HullView Hull;
            public TMP_Text Name, Note;
            public Image Bar;
            public Element FlowerOf;
        }

        public VoyageSquadDeck(VoyageKit kit, VoyageSeats rules, RectTransform host)
        {
            _kit = kit;
            _rules = rules;
            _root = host;
            Build();
        }

        // ── Building ─────────────────────────────────────────────────────────

        void Build()
        {
            var stage = VoyageKit.Node("Stage", _root);
            VoyageKit.Anchor(stage, 0f, 1f - StageFoot / PageH, 1f, 1f - StageTop / PageH);
            for (int i = 0; i < _seats.Length; i++)
            {
                var seat = VoyageKit.Node("Seat" + i, stage);
                VoyageKit.Anchor(seat, i / 3f + 0.004f, 0f, (i + 1) / 3f - 0.004f, 1f);
                _seats[i] = Seat(seat, i);
            }

            var shelf = VoyageKit.Node("Shelf", _root);
            VoyageKit.Anchor(shelf, 0f, 1f - ShelfFoot / PageH, 1f, 1f - ShelfTop / PageH);
            // The band behind the choices: solid across the middle, fading out at both ends.
            VoyageKit.Band(shelf, _kit.RowPicked, 0.14f);
            float x = ShelfInset;
            for (int g = 0; g < _groups.Length; g++)
            {
                _groups[g] = Group(shelf, g, x / PageW, (x + GroupWidth[g]) / PageW);
                x += GroupWidth[g] + GroupGap;
            }
        }

        SeatView Seat(RectTransform rt, int seat)
        {
            var view = new SeatView();
            var hit = VoyageKit.Block(rt, VoyageKit.Clear, hit: true);
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            view.Button.onClick.AddListener(() => PressSeat(seat));
            view.Group = rt.gameObject.AddComponent<CanvasGroup>();

            var glow = VoyageKit.Node("Glow", rt);
            VoyageKit.Anchor(glow, 0.06f, 0.36f, 0.94f, 0.70f);
            view.Glow = VoyageKit.Glow(glow, VoyageKit.Fade(_kit.Light, 0.2f));

            var hull = VoyageKit.Node("Hull", rt);
            VoyageKit.Anchor(hull, 0.20f, 0.48f, 0.93f, 0.96f);
            view.Hull = VoyageSeats.HullBox(hull);

            var face = VoyageKit.Node("Pilot", rt);
            VoyageKit.Anchor(face, 0.04f, 0.45f, 0.27f, 1f);
            view.Face = VoyageKit.Picture(face);

            // The words, from the foot up: the place in the squad, the vessel over its line, then who and what.
            var words = VoyageKit.Node("Words", rt);
            VoyageKit.Anchor(words, 0f, 0f, 1f, 0.42f);
            VoyageKit.Column(words, 12f, TextAnchor.LowerCenter);
            view.Role = _kit.Label(words, 14, _kit.Light, TextAlignmentOptions.Center);
            (view.Name, view.Bar) = _kit.Underlined(words, 28);
            var parts = VoyageKit.Node("Parts", words);
            VoyageKit.Row(parts, 28f, TextAnchor.MiddleCenter);
            for (int i = 0; i < view.Parts.Length; i++) view.Parts[i] = _kit.Text(parts, 15, _kit.Light, TextAlignmentOptions.Center);
            return view;
        }

        GroupView Group(RectTransform shelf, int kind, float x0, float x1)
        {
            var view = new GroupView();
            var rt = VoyageKit.Node("Group" + kind, shelf);
            VoyageKit.Anchor(rt, x0, 0f, x1, 1f);
            rt.offsetMin = new Vector2(0f, 8f);
            rt.offsetMax = new Vector2(0f, -20f);
            view.Label = _kit.Label(rt, 14, _kit.Light);
            VoyageKit.Top(view.Label.rectTransform, 0f, 20f);
            view.Area = VoyageKit.Node("Choices", rt);
            VoyageKit.Stretch(view.Area);
            view.Area.offsetMax = new Vector2(0f, -LabelRoom);
            view.Grid = view.Area.gameObject.AddComponent<GridLayoutGroup>();
            view.Grid.spacing = new Vector2(TileGap, TileGap);
            view.Grid.childAlignment = TextAnchor.UpperLeft;
            view.Grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            return view;
        }

        TileView Tile(GroupView group, int kind)
        {
            var view = new TileView();
            view.Root = VoyageKit.Node("Choice" + group.Tiles.Count, group.Area);
            var hit = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();

            // The words stand at the tile's foot; the picture fills what is above them.
            float wordsRoom = kind == VoyageSeats.Artifact ? 0f : kind == VoyageSeats.Pilot ? 66f : 40f;
            if (kind != VoyageSeats.Artifact)
            {
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.Stretch(pic);
                pic.offsetMin = new Vector2(0f, wordsRoom);
                var glow = VoyageKit.Node("Glow", pic);
                VoyageKit.Stretch(glow);
                view.Glow = VoyageKit.Glow(glow, VoyageKit.Fade(_kit.Light, 0.22f));
                switch (kind)
                {
                    case VoyageSeats.Vessel:
                        view.Hull = VoyageSeats.HullBox(pic);
                        break;
                    case VoyageSeats.Pilot:
                    {
                        var face = VoyageKit.Node("Face", pic);
                        VoyageKit.Stretch(face);
                        view.Face = VoyageKit.Picture(face);
                        break;
                    }
                    default:
                    {
                        // The flower stands in a square in the middle of the picture's room.
                        var box = VoyageKit.Node("Mark", pic);
                        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
                        box.sizeDelta = new Vector2(48f, 48f);
                        view.Flower = box;
                        break;
                    }
                }
            }
            var words = VoyageKit.Node("Words", view.Root);
            VoyageKit.Stretch(words);
            VoyageKit.Column(words, 8f, kind == VoyageSeats.Artifact ? TextAnchor.MiddleCenter : TextAnchor.LowerCenter);
            (view.Name, view.Bar) = _kit.Underlined(words, 15);
            if (kind == VoyageSeats.Pilot) view.Note = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Center);
            group.Tiles.Add(view);
            return view;
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        /// <summary>Draw the squad that sails: its seats, and the picked seat's choices.</summary>
        public void Draw()
        {
            var active = VoyageSeats.Active;
            if (_picked == Unset) _picked = VoyageSeats.MySeat(active);
            if (_picked >= 0 && !VoyageSeats.CanEdit(active, _picked)) _picked = FirstEditable(active);
            DrawSeats(active);
            DrawShelf(active);
        }

        /// <summary>Another squad sails: its own seat is picked again.</summary>
        public void Reset() => _picked = Unset;

        static int FirstEditable(SavedSquad squad)
        {
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                if (VoyageSeats.CanEdit(squad, seat)) return seat;
            return -1;
        }

        void DrawSeats(SavedSquad active)
        {
            for (int seat = 0; seat < _seats.Length; seat++)
            {
                var view = _seats[seat];
                bool has = active != null;
                view.Button.gameObject.SetActive(has);
                if (!has) continue;
                var slot = active.GetSlot(seat);
                var vessel = _rules.VesselOf(slot.Vessel);
                bool editable = VoyageSeats.CanEdit(active, seat), on = seat == _picked;
                var pilot = _rules.PilotOf(slot, vessel);
                VoyageKit.Show(view.Face, pilot ? pilot.Portrait : null);
                _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
                VoyageKit.Set(view.Role, _rules.Role(seat));
                VoyageKit.Set(view.Name, vessel ? vessel.CardName : null);
                VoyageKit.Set(view.Parts[0], _rules.Who(active, seat, slot));
                VoyageKit.Set(view.Parts[1], VoyageSeats.PilotName(slot, pilot, editable));
                VoyageKit.Set(view.Parts[2], _rules.ElementName(slot.Element));
                VoyageKit.Set(view.Parts[3], _rules.ArtifactName(slot, vessel, editable));
                view.Bar.color = on ? _kit.Cyan : VoyageKit.Clear;
                view.Glow.color = VoyageKit.Fade(_kit.Light, on ? 0.2f : 0.07f);
                view.Group.alpha = on ? 1f : editable ? 0.62f : 0.4f;
                view.Button.interactable = editable;
            }
        }

        void DrawShelf(SavedSquad active)
        {
            float scale = Scale();
            for (int kind = 0; kind < _groups.Length; kind++)
            {
                var group = _groups[kind];
                VoyageKit.Set(group.Label, kind switch
                {
                    VoyageSeats.Vessel => Lines?.CardSettingVessel,
                    VoyageSeats.Pilot => Lines?.CardSettingPilot,
                    VoyageSeats.ElementKind => Lines?.CardSettingElement,
                    _ => Lines?.CardSettingArtifact,
                });
                if (_picked >= 0) _rules.Offer(kind, active, _picked, _offered);
                else _offered.Clear();
                Cells(kind, _offered.Count, GroupWidth[kind] * scale, out int columns, out Vector2 cell);
                group.Grid.constraintCount = columns;
                group.Grid.cellSize = cell;
                for (int i = 0; i < _offered.Count; i++)
                {
                    var tile = i < group.Tiles.Count ? group.Tiles[i] : Tile(group, kind);
                    tile.Root.gameObject.SetActive(true);
                    Show(tile, kind, _offered[i], cell.x);
                }
                for (int i = _offered.Count; i < group.Tiles.Count; i++) group.Tiles[i].Root.gameObject.SetActive(false);
            }
        }

        // The page's width over the board's: the tiles are cut for the width the shelf really has.
        float Scale()
        {
            float w = _root.rect.width;
            return w > 1f ? w / PageW : 1f;
        }

        /// <summary>
        /// How <paramref name="count"/> choices of <paramref name="kind"/> are cut into a group <paramref name="width"/>
        /// wide: vessels in two rows, pilots in one, elements two by two, artifacts five to a column. A tile is never
        /// wider than the board drew it, and narrows when there are more choices than the board showed.
        /// </summary>
        public static void Cells(int kind, int count, float width, out int columns, out Vector2 cell)
        {
            float most, height;
            switch (kind)
            {
                case VoyageSeats.Vessel:
                    columns = Mathf.Max(3, Mathf.CeilToInt(count / 2f));
                    most = 170f;
                    height = VesselTileH;
                    break;
                case VoyageSeats.Pilot:
                    columns = Mathf.Max(4, count);
                    most = 140f;
                    height = PilotTileH;
                    break;
                case VoyageSeats.ElementKind:
                    columns = 2;
                    most = 116f;
                    height = ElementTileH;
                    break;
                default:
                    columns = Mathf.Max(1, Mathf.CeilToInt(count / 5f));
                    most = width;
                    height = ArtifactTileH;
                    break;
            }
            float w = (width - TileGap * (columns - 1)) / columns;
            cell = new Vector2(Mathf.Max(1f, Mathf.Min(most, w)), height);
        }

        void Show(TileView tile, int kind, VoyageSeats.Option option, float width)
        {
            bool on = option.Picked;
            VoyageKit.Set(tile.Name, option.Name);
            tile.Bar.color = on ? _kit.Cyan : VoyageKit.Clear;
            tile.Group.alpha = on ? 1f : 0.6f;
            if (tile.Glow) tile.Glow.enabled = on;
            if (tile.Hull != null) _rules.SetHull(tile.Hull, option.Picture);
            if (tile.Face) VoyageKit.Show(tile.Face, option.Picture);
            if (tile.Flower && (tile.Flower.childCount == 0 || tile.FlowerOf != option.Element))
            {
                for (int i = tile.Flower.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(tile.Flower.GetChild(i).gameObject);
                VoyageKit.Stretch(VoyageKit.Flower(tile.Flower, option.Element, _kit.Light));
                tile.FlowerOf = option.Element;
            }
            if (tile.Note)
            {
                // A note that would not fit on one line is left out, never wrapped (the tile narrows with a large crew).
                bool fits = !string.IsNullOrEmpty(option.Note) && tile.Note.GetPreferredValues(option.Note).x <= width + TileGap - 4f;
                VoyageKit.Set(tile.Note, fits ? option.Note : null);
                tile.Note.gameObject.SetActive(fits);
            }
            tile.Button.onClick.RemoveAllListeners();
            var taken = option;
            tile.Button.onClick.AddListener(() => Take(kind, taken));
        }

        // ── Pressing ─────────────────────────────────────────────────────────

        void PressSeat(int seat)
        {
            if (seat == _picked || !VoyageSeats.CanEdit(VoyageSeats.Active, seat)) return;
            _picked = seat;
            Draw();
        }

        // One press takes a choice and saves; the rules' Changed redraws the window (and so this screen).
        void Take(int kind, VoyageSeats.Option option)
        {
            if (option.Picked || _picked < 0) return;
            _rules.Pick(_picked, kind, option);
        }
    }
}
