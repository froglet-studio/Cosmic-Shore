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
        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        readonly VoyageKit _kit;
        readonly SO_VesselList _vessels;
        readonly PilotArtifactCatalogSO _catalog;
        VoyageLines Lines => _kit.Lines;

        RectTransform _chips;
        TMP_Text _squadLabel;
        readonly List<ChipView> _chipViews = new();
        readonly SeatView[] _seats = new SeatView[3];
        int _openSeat = Unset;

        // Read on each draw (reused, not allocated per press).
        readonly List<PilotRecord> _crew = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<string> _ids = new();
        readonly List<Option> _offered = new();

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
            public HullView Hull;
            public TMP_Text Name, Who, Kit;
            public readonly VoyageKit.Stepper[] Steppers = new VoyageKit.Stepper[4];
        }

        /// <summary>A hull render cropped to the hull (<see cref="VoyageBoardStyleSO.CropOf"/>), fitted in its box.</summary>
        sealed class HullView
        {
            public RawImage Image;
            public AspectRatioFitter Fit;
        }

        /// <summary>One choice a stepper can land on: what it shows and what it writes into the seat.</summary>
        struct Option
        {
            public string Name;
            public bool Picked;
            public VesselClassType Vessel;
            public string Id;
            public Element Element;
        }

        public VoyageSquadColumn(VoyageKit kit, RectTransform host, SO_VesselList vessels)
        {
            _kit = kit;
            _vessels = vessels;
            _catalog = PilotArtifactCatalogSO.Load();
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
            view.Hull = HullBox(hull);

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

        static SquadBook Book => UGSDataService.Instance?.SquadRepo?.Data?.Book;

        static void SaveBook() => UGSDataService.Instance?.SquadRepo?.MarkDirty();

        /// <summary>Draw the squads and the active squad's seats as they are now.</summary>
        public void Draw()
        {
            var active = Book?.GetActive();
            if (_openSeat == Unset) _openSeat = MySeat(active);
            if (_openSeat >= 0 && (active == null || !VoyageOpenCard.CanEdit(active, _openSeat))) _openSeat = -1;
            DrawSquads();
            DrawSeats(active);
        }

        /// <summary>The player's own seat in <paramref name="squad"/> when they may change it; else none.</summary>
        static int MySeat(SavedSquad squad)
        {
            if (squad == null) return -1;
            for (int seat = 0; seat < 3; seat++)
                if (VoyageOpenCard.IsMySeat(squad, seat) && VoyageOpenCard.CanEdit(squad, seat)) return seat;
            return -1;
        }

        void DrawSquads()
        {
            var book = Book;
            int count = book?.Squads?.Count ?? 0;
            VoyageKit.Set(_squadLabel, count > 0 ? Lines?.BoardSquadLabel : Lines?.CardNoSquad);
            while (_chipViews.Count < count) _chipViews.Add(Chip(_chipViews.Count));
            for (int i = 0; i < _chipViews.Count; i++)
            {
                var chip = _chipViews[i];
                bool show = i < count;
                chip.Button.gameObject.SetActive(show);
                if (!show) continue;
                var squad = book.Squads[i];
                bool on = i == book.ActiveIndex;
                VoyageKit.Set(chip.Label, squad == null ? null : squad.IsJoined()
                    ? TemplateText.Fill(Lines?.BoardSquadJoined, "name", squad.Name, "owner", squad.OwnerName)
                    : squad.Name);
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
            var book = Book;
            if (book == null || index == book.ActiveIndex || !book.TrySetActive(index)) return;
            _openSeat = MySeat(book.GetActive());
            SaveBook();
            Changed?.Invoke();
            Draw();
        }

        void DrawSeats(SavedSquad active)
        {
            var s = _kit.Style;
            for (int seat = 0; seat < 3; seat++)
            {
                var view = _seats[seat];
                bool has = active != null;
                view.Root.gameObject.SetActive(has);
                if (!has) continue;
                var slot = active.GetSlot(seat);
                var vessel = Vessel(slot.Vessel);
                bool editable = VoyageOpenCard.CanEdit(active, seat);
                var keywords = vessel ? vessel.Keywords : null;
                var pilot = _catalog ? _catalog.ResolvePilot(slot, keywords) : null;
                VoyageKit.Show(view.Face, pilot ? pilot.Portrait : null);
                SetHull(view.Hull, Hull(vessel));
                VoyageKit.Set(view.Name, vessel ? vessel.CardName : null);
                string pilotName = PilotName(slot, pilot, editable);
                string artifact = ArtifactName(slot, vessel, editable);
                VoyageKit.Set(view.Who, TemplateText.Fill(Lines?.BoardSeatWho, "who", Who(active, seat, slot), "pilot", pilotName));
                VoyageKit.Set(view.Kit, editable
                    ? TemplateText.Fill(Lines?.BoardSeatKit, "element", ElementName(slot.Element), "artifact", artifact)
                    : TemplateText.Fill(Lines?.BoardSeatNotYours, "owner", slot.IsPlayer() ? slot.MemberName : active.OwnerName));
                view.Group.alpha = editable ? 1f : 0.55f;
                view.Button.interactable = editable;
                bool open = seat == _openSeat;
                view.Edge.color = open ? _kit.Jade : _kit.Outline;
                view.Steps.gameObject.SetActive(open);
                if (!open) continue;
                int at = seat;
                view.Steppers[0].Show(Lines?.CardSettingVessel, vessel ? vessel.CardName : null, d => Step(at, 0, d));
                view.Steppers[1].Show(Lines?.CardSettingPilot, pilotName, d => Step(at, 1, d));
                view.Steppers[2].Show(Lines?.CardSettingElement, ElementName(slot.Element), d => Step(at, 2, d));
                view.Steppers[3].Show(Lines?.CardSettingArtifact, artifact, d => Step(at, 3, d));
            }
        }

        void PressSeat(int seat)
        {
            var active = Book?.GetActive();
            if (active == null || !VoyageOpenCard.CanEdit(active, seat)) return;
            _openSeat = _openSeat == seat ? -1 : seat;
            Draw();
        }

        string Who(SavedSquad squad, int seat, SquadSlot slot)
        {
            if (VoyageOpenCard.IsMySeat(squad, seat)) return Lines?.CardSeatYou;
            if (slot.IsOpen()) return Lines?.CardSeatOpen;
            if (slot.IsPlayer()) return slot.MemberName;
            return Lines?.CardSeatAi;
        }

        /// <summary>The person's name on the player's own seats (a person this crew holds), else the callsign.</summary>
        static string PilotName(SquadSlot slot, PilotSO pilot, bool editable)
        {
            if (editable && pilot && PilotLoadoutStore.TryGetPilot(slot.PilotId, out var person) && person.CallsignOrTemplate() == pilot.Id)
                return person.DisplayName();
            return pilot ? pilot.DisplayName : string.Empty;
        }

        string ArtifactName(SquadSlot slot, SO_Vessel vessel, bool editable)
        {
            var kind = _catalog ? _catalog.ResolveArtifact(slot, vessel ? vessel.Keywords : null) : null;
            if (editable && kind && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var held) && held.Kind() == kind.Id) return held.Name;
            return kind ? kind.DisplayName : Lines?.CardNoArtifact;
        }

        // ── Changing a seat ──────────────────────────────────────────────────

        /// <summary>
        /// Step <paramref name="seat"/>'s <paramref name="kind"/> (0 vessel, 1 pilot, 2 element, 3 artifact) one choice
        /// <paramref name="direction"/> along what the seat may take, round the ends, and save.
        /// </summary>
        void Step(int seat, int kind, int direction)
        {
            var active = Book?.GetActive();
            if (active == null || !VoyageOpenCard.CanEdit(active, seat)) return;
            var slot = active.GetSlot(seat);
            Offer(kind, active, seat, slot, Vessel(slot.Vessel));
            int next = StepIndex(_offered.FindIndex(o => o.Picked), direction, _offered.Count);
            if (next < 0 || _offered[next].Picked) return;
            Pick(seat, kind, _offered[next]);
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

        /// <summary>What <paramref name="seat"/> may take for <paramref name="kind"/>, under the rules the Ark's picker
        /// keeps, in the order the steps go.</summary>
        void Offer(int kind, SavedSquad squad, int seat, SquadSlot slot, SO_Vessel vessel)
        {
            _offered.Clear();
            switch (kind)
            {
                case 0:
                    if (_vessels && _vessels.VesselList != null)
                        foreach (var v in _vessels.VesselList)
                            if (v && !v.IsLocked)
                                _offered.Add(new Option { Name = v.CardName, Vessel = v.Class, Picked = v.Class == slot.Vessel });
                    break;
                case 1:
                {
                    // The crew's people whose callsign can fly the hull, in crew order; one who flies another seat swaps.
                    string current = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var seated) ? seated.Id : slot.PilotId;
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetCrew(_crew);
                    foreach (var person in _crew)
                    {
                        if (!_catalog.TryGetPilot(person.Id, out var callsign) || !callsign || callsign.Requirement == null
                            || !callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords)) continue;
                        _offered.Add(new Option { Name = person.DisplayName(), Id = person.Id, Picked = person.Id == current });
                    }
                    break;
                }
                case 2:
                    foreach (var e in Elements)
                        _offered.Add(new Option { Name = ElementName(e), Element = e, Picked = e == slot.Element });
                    break;
                default:
                {
                    // None, then the hoard's artifacts the hull can carry (each one of a kind).
                    string current = PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var carried) ? carried.Id : null;
                    _offered.Add(new Option { Name = Lines?.CardNoArtifact, Id = null, Picked = string.IsNullOrEmpty(current) });
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetHoard(_hoard);
                    foreach (var artifact in _hoard)
                        if (_catalog.TryGetArtifact(artifact.Id, out var held) && held && held.Requirement != null
                            && held.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                            _offered.Add(new Option { Name = artifact.Name, Id = artifact.Id, Picked = artifact.Id == current });
                    break;
                }
            }
        }

        /// <summary>The seat takes <paramref name="option"/> and the squad is saved: the pilot swap and the crew fill run
        /// (the Ark's rules, as <c>VoyageOpenCard.ChangeSetting</c> keeps them).</summary>
        void Pick(int seat, int kind, Option option)
        {
            var book = Book;
            var active = book?.GetActive();
            if (active == null || !VoyageOpenCard.CanEdit(active, seat)) return;
            var squad = SquadMembership.Copy(active);
            var slot = squad.GetSlot(seat);
            string previousPilot = slot.PilotId;
            switch (kind)
            {
                case 0:
                    slot = slot.WithVessel(option.Vessel);
                    // The player's own card starts on the person they keep in that vessel's Hangar seat (a crew id).
                    if (VoyageOpenCard.IsMySeat(squad, seat)) slot.PilotId = PilotLoadoutStore.GetPilot(option.Vessel);
                    break;
                case 1:
                    slot.PilotId = option.Id;
                    break;
                case 2:
                    slot.Element = option.Element;
                    break;
                default:
                    slot.ArtifactId = option.Id;
                    break;
            }
            squad.SetSlot(seat, slot);
            squad.KeepCardsOwn(seat, previousPilot);
            PilotLoadoutStore.GetCrew(_crew);
            _ids.Clear();
            foreach (var p in _crew) if (!string.IsNullOrEmpty(p.Id)) _ids.Add(p.Id);
            squad.FillPilots(_ids);
            int index = book.ActiveIndex;
            if (index < 0 || index >= book.Squads.Count) return;
            book.Squads[index] = squad;
            SaveBook();
            Changed?.Invoke();
            Draw();
        }

        // ── Data ─────────────────────────────────────────────────────────────

        SO_Vessel Vessel(VesselClassType cls) =>
            _vessels && _vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        static Sprite Hull(SO_Vessel v) =>
            !v ? null : v.SquadImage ? v.SquadImage : v.CardSilohoutteActive ? v.CardSilohoutteActive : v.IconActive;

        string ElementName(Element e) => e switch
        {
            Element.Charge => Lines?.ElementCharge,
            Element.Mass => Lines?.ElementMass,
            Element.Space => Lines?.ElementSpace,
            Element.Time => Lines?.ElementTime,
            _ => string.Empty,
        };

        static HullView HullBox(RectTransform host)
        {
            var rt = VoyageKit.Node("Render", host);
            VoyageKit.Stretch(rt);
            var view = new HullView { Image = rt.gameObject.AddComponent<RawImage>() };
            view.Image.raycastTarget = false;
            view.Image.enabled = false;
            view.Fit = rt.gameObject.AddComponent<AspectRatioFitter>();
            view.Fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            return view;
        }

        /// <summary>Show <paramref name="sprite"/> cropped to its hull (the style's measured crop), as large as its box
        /// allows.</summary>
        void SetHull(HullView view, Sprite sprite)
        {
            if (!sprite || !sprite.texture)
            {
                view.Image.enabled = false;
                return;
            }
            var crop = _kit.Style.CropOf(sprite);
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            view.Image.texture = tex;
            view.Image.uvRect = new Rect((tr.x + crop.x * tr.width) / tex.width, (tr.y + crop.y * tr.height) / tex.height,
                crop.width * tr.width / tex.width, crop.height * tr.height / tex.height);
            view.Fit.aspectRatio = crop.width * tr.width / Mathf.Max(1f, crop.height * tr.height);
            view.Image.enabled = true;
        }
    }
}
