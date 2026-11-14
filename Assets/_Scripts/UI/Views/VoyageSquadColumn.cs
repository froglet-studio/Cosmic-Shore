using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Sea table's right column (Rev 88 and 89 A, Docs/MENU_REDESIGN_B.md sections 169-172): which squad sails, its
    /// three seats, and the screen's one button at the foot (<see cref="Foot"/>, filled by whoever stands the column).
    /// <list type="bullet">
    /// <item><b>The squads</b>: their names as words, the active one light over a line.</item>
    /// <item><b>A seat</b>: the pilot, the vessel's name, who flies it and the pilot, the element and artifact, the hull.
    /// A seat the player may not change is dimmed and says whose it is.</item>
    /// <item><b>Changing a seat</b>: a press on a seat the player may change opens Vessel, Pilot, Element and Artifact
    /// under it as four steppers. There is no editor screen, so nothing covers the table. A step picks and saves at
    /// once. The player's own seat stands open first.</item>
    /// </list>
    /// The squads are the Ark's (<see cref="SquadBook"/>) under the Ark's rules (<c>SquadsPage</c>: your own seat, and the AI
    /// or open seats of a squad you made; a pilot flies one seat, so stepping onto one who flies another seat swaps them,
    /// <see cref="SavedSquad.KeepCardsOwn"/>).
    /// </summary>
    public sealed class VoyageSquadColumn
    {
        /// <summary>The squad changed (another squad picked, a seat changed).</summary>
        public event Action Changed;

        /// <summary>Where the screen's one button stands, at the column's foot.</summary>
        public RectTransform Foot { get; private set; }

        /// <summary>The seat whose steppers are open; -1: none.</summary>
        public int OpenSeat => _openSeat;

        const float FootHeight = 56f, ChipsHeight = 40f, SeatHeight = 128f;
        const int Unset = -2;

        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        VoyageLines Lines => _kit.Lines;

        RectTransform _chips;
        TMP_Text _squadLabel;
        readonly List<ChipView> _chipViews = new();
        readonly SeatView[] _seats = new SeatView[3];
        int _openSeat = Unset;

        // Read on each step (reused, not allocated per press).
        readonly List<VoyageSeats.Option> _offered = new();

        sealed class ChipView
        {
            public Button Button;
            public Image Bar;
            public TMP_Text Label;
        }

        sealed class SeatView
        {
            public RectTransform Root, Steps;
            public Button Button;
            public CanvasGroup Group;
            public Image Edge, Face;
            public VoyageSeats.HullView Hull;
            public TMP_Text Name, Who, Kit;
            public readonly VoyageKit.Stepper[] Steppers = new VoyageKit.Stepper[4];
        }

        public VoyageSquadColumn(VoyageKit kit, RectTransform host, SO_VesselList vessels)
        {
            _kit = kit;
            _rules = new VoyageSeats(kit.Style, kit.Lines, vessels);
            _rules.Changed += () => Changed?.Invoke();
            Build(host);
        }

        void Build(RectTransform host)
        {
            _chips = VoyageKit.Node("Squads", host);
            VoyageKit.Top(_chips, 0f, ChipsHeight);
            VoyageKit.Row(_chips, 26f, TextAnchor.LowerLeft);
            _squadLabel = _kit.Label(_chips, 13, _kit.Sub);

            var list = VoyageKit.Node("Seats", host);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = Vector2.one;
            list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = Vector2.zero;
            list.anchoredPosition = new Vector2(0f, -(ChipsHeight + 16f));
            VoyageKit.Column(list, 10f);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (int i = 0; i < 3; i++) _seats[i] = Seat(list, i);

            Foot = VoyageKit.Node("Foot", host);
            VoyageKit.Foot(Foot, 0f, FootHeight);
        }

        SeatView Seat(Transform parent, int seat)
        {
            var s = _kit.Style;
            var view = new SeatView();
            view.Root = VoyageKit.Node("Seat" + seat, parent);
            VoyageKit.Sliced(view.Root, s.Sliver10Fill, _kit.RowPicked);
            VoyageKit.Column(view.Root, 0f);
            view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();

            // The seat itself: a press opens or closes its steppers.
            var head = VoyageKit.Node("Card", view.Root);
            VoyageKit.Le(head).preferredHeight = SeatHeight;
            var hit = VoyageKit.Block(head, VoyageKit.Clear, hit: true);
            view.Button = head.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            int index = seat;
            view.Button.onClick.AddListener(() => PressSeat(index));

            const float pad = 14f, faceW = 84f, hullW = 150f, gap = 12f;
            var face = VoyageKit.Node("Pilot", head);
            face.anchorMin = Vector2.zero;
            face.anchorMax = new Vector2(0f, 1f);
            face.pivot = new Vector2(0f, 0.5f);
            face.sizeDelta = new Vector2(faceW, -8f);
            face.anchoredPosition = new Vector2(pad, 0f);
            view.Face = VoyageKit.Picture(face);

            var hull = VoyageKit.Node("Hull", head);
            hull.anchorMin = new Vector2(1f, 0.5f);
            hull.anchorMax = new Vector2(1f, 0.5f);
            hull.pivot = new Vector2(1f, 0.5f);
            hull.sizeDelta = new Vector2(hullW, 84f);
            hull.anchoredPosition = new Vector2(-pad, 0f);
            view.Hull = VoyageSeats.HullBox(hull);

            var words = VoyageKit.Node("Words", head);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(pad + faceW + gap, 0f);
            words.offsetMax = new Vector2(-(pad + hullW + gap), 0f);
            VoyageKit.Column(words, 5f, TextAnchor.MiddleLeft);
            view.Name = _kit.Text(words, 20, _kit.Light, TextAlignmentOptions.Left);
            view.Who = _kit.Text(words, 14, _kit.Light, TextAlignmentOptions.Left);
            view.Kit = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);

            // The seat's four choices, two to a row, under it while it is open.
            view.Steps = VoyageKit.Node("Steppers", view.Root);
            var sv = VoyageKit.Column(view.Steps, 10f);
            sv.padding = new RectOffset(14, 14, 2, 14);
            for (int row = 0; row < 2; row++)
            {
                var line = VoyageKit.Node("Row" + row, view.Steps);
                VoyageKit.Row(line, 14f, TextAnchor.UpperLeft, expandWidth: true);
                for (int k = 0; k < 2; k++) view.Steppers[row * 2 + k] = _kit.NewStepper(line);
            }

            var edge = VoyageKit.Node("Edge", view.Root);
            VoyageKit.Le(edge).ignoreLayout = true;
            VoyageKit.Stretch(edge);
            view.Edge = VoyageKit.Sliced(edge, s.Sliver10Line, _kit.Outline);
            return view;
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        /// <summary>Draw the squads and the active squad's seats as they are now.</summary>
        public void Draw()
        {
            var active = VoyageSeats.Active;
            if (_openSeat == Unset) _openSeat = VoyageSeats.MySeat(active);
            if (_openSeat >= 0 && !VoyageSeats.CanEdit(active, _openSeat)) _openSeat = -1;
            DrawSquads();
            DrawSeats(active);
        }

        void DrawSquads()
        {
            var book = VoyageSeats.Book;
            int count = book?.Squads?.Count ?? 0;
            VoyageKit.Set(_squadLabel, count > 0 ? Lines?.BoardSquadLabel : Lines?.CardNoSquad);
            while (_chipViews.Count < count) _chipViews.Add(Chip(_chipViews.Count));
            for (int i = 0; i < _chipViews.Count; i++)
            {
                var chip = _chipViews[i];
                bool show = i < count;
                chip.Button.gameObject.SetActive(show);
                if (!show) continue;
                bool on = i == book.ActiveIndex;
                VoyageKit.Set(chip.Label, _rules.SquadName(book.Squads[i]));
                chip.Label.color = on ? _kit.Light : _kit.Sub;
                chip.Bar.enabled = on;
            }
        }

        ChipView Chip(int index)
        {
            var view = new ChipView();
            var rt = VoyageKit.Node("Squad" + index, _chips);
            var hit = VoyageKit.Block(rt, VoyageKit.Clear, hit: true);
            var v = VoyageKit.Column(rt, 4f, TextAnchor.LowerLeft);
            v.childForceExpandWidth = true;
            view.Label = _kit.Text(rt, 18, _kit.Sub, TextAlignmentOptions.Left);
            var bar = VoyageKit.Node("Bar", rt);
            VoyageKit.Le(bar).preferredHeight = 2f;
            view.Bar = VoyageKit.Block(bar, _kit.Cyan);
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            view.Button.onClick.AddListener(() => PickSquad(index));
            return view;
        }

        void PickSquad(int index)
        {
            if (!_rules.PickSquad(index)) return;
            _openSeat = VoyageSeats.MySeat(VoyageSeats.Active);
            Draw();
        }

        void DrawSeats(SavedSquad active)
        {
            for (int seat = 0; seat < 3; seat++)
            {
                var view = _seats[seat];
                bool has = active != null;
                view.Root.gameObject.SetActive(has);
                if (!has) continue;
                var slot = active.GetSlot(seat);
                var vessel = _rules.VesselOf(slot.Vessel);
                bool editable = VoyageSeats.CanEdit(active, seat);
                var pilot = _rules.PilotOf(slot, vessel);
                VoyageKit.Show(view.Face, pilot ? pilot.Portrait : null);
                _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
                VoyageKit.Set(view.Name, vessel ? vessel.CardName : null);
                string pilotName = VoyageSeats.PilotName(slot, pilot, editable);
                string artifact = _rules.ArtifactName(slot, vessel, editable);
                VoyageKit.Set(view.Who, TemplateText.Fill(Lines?.BoardSeatWho, "who", _rules.Who(active, seat, slot), "pilot", pilotName));
                VoyageKit.Set(view.Kit, editable
                    ? TemplateText.Fill(Lines?.BoardSeatKit, "element", _rules.ElementName(slot.Element), "artifact", artifact)
                    : TemplateText.Fill(Lines?.BoardSeatNotYours, "owner", slot.IsPlayer() ? slot.MemberName : active.OwnerName));
                view.Group.alpha = editable ? 1f : 0.55f;
                view.Button.interactable = editable;
                bool open = seat == _openSeat;
                view.Edge.color = open ? _kit.Jade : _kit.Outline;
                view.Steps.gameObject.SetActive(open);
                if (!open) continue;
                int at = seat;
                view.Steppers[0].Show(Lines?.CardSettingVessel, vessel ? vessel.CardName : null, d => Step(at, VoyageSeats.Vessel, d));
                view.Steppers[1].Show(Lines?.CardSettingPilot, pilotName, d => Step(at, VoyageSeats.Pilot, d));
                view.Steppers[2].Show(Lines?.CardSettingElement, _rules.ElementName(slot.Element), d => Step(at, VoyageSeats.ElementKind, d));
                view.Steppers[3].Show(Lines?.CardSettingArtifact, artifact, d => Step(at, VoyageSeats.Artifact, d));
            }
        }

        void PressSeat(int seat)
        {
            if (!VoyageSeats.CanEdit(VoyageSeats.Active, seat)) return;
            _openSeat = _openSeat == seat ? -1 : seat;
            Draw();
        }

        // ── Changing a seat ──────────────────────────────────────────────────

        /// <summary>
        /// Step <paramref name="seat"/>'s <paramref name="kind"/> (<see cref="VoyageSeats.Vessel"/> and the rest) one
        /// choice <paramref name="direction"/> along what the seat may take, round the ends, and save.
        /// </summary>
        void Step(int seat, int kind, int direction)
        {
            var active = VoyageSeats.Active;
            if (!VoyageSeats.CanEdit(active, seat)) return;
            _rules.Offer(kind, active, seat, _offered);
            int next = StepIndex(_offered.FindIndex(o => o.Picked), direction, _offered.Count);
            if (next < 0 || _offered[next].Picked) return;
            if (_rules.Pick(seat, kind, _offered[next])) Draw();
        }

        /// <summary>The choice one step <paramref name="direction"/> from <paramref name="from"/> among
        /// <paramref name="count"/>, round the ends; from nothing picked (-1) a step lands on the first or the last.
        /// -1 when there is nothing to step to.</summary>
        public static int StepIndex(int from, int direction, int count)
        {
            if (count <= 0) return -1;
            if (from < 0) return direction >= 0 ? 0 : count - 1;
            return ((from + Math.Sign(direction)) % count + count) % count;
        }
    }
}
