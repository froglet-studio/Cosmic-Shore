using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages page's third step, your Ark, lobby only, as the Hangar line-up (Frogma Rev 115 A, picked by Shombith
    /// 2026-11-16: "Lets go A."; Docs/MENU_REDESIGN_B.md section 211.1; on Rev 113 Lobby, section 209):
    /// <list type="bullet">
    /// <item><b>The voyage</b> top left: its picture, its name, then its kind, worlds and difficulty.</item>
    /// <item><b>Three seats standing as vessels in a row</b>, no boxes, over a faint hangar floor, filled in order: you,
    /// then the friends in your lobby, then AI wingmates (<see cref="VoyageRules.FriendAt"/>). Each: its role and state
    /// above; the hull render on a soft floor shadow; one thin line under it (cyan for your seat); the pilot's face, the
    /// vessel, "pilot - artifact" and the element's flower; and at the foot "Change your seat" (X) on yours, a line saying a
    /// friend picks their own, or Invite a friend and Change wingmate on an AI seat.</item>
    /// <item><b>The bar</b> at the foot: Fill the AI seats from a saved squad (Y) at the left; "You sail as" and Set sail
    /// (A) at the right.</item>
    /// </list>
    /// The seats are the voyage's own (<see cref="VoyageSeatRecord"/>, a copy of the active squad made when the page
    /// opens on a voyage): a pick never changes a saved squad, and Set sail sails exactly these seats. The seat picker
    /// slides in from the right and leaves the seats in view, so your seat changes behind it as you pick. Pressing a seat's
    /// hull opens Vessels (<see cref="VoyageVesselsPage"/>, Rev 136 E / Rev 138), and each vessel carries its voyage class
    /// beside its name (Rev 134 seats), muted when the voyage does not want it.
    /// <para>On a friend's machine (<see cref="VoyageLaunch.IsPartyGuest"/>) the same page is their own seat in the host's
    /// lobby (section 209.2): they change their own seat, kept in <see cref="VoyageSeatRecord.PartyKey"/> and written to
    /// their player's replicated hull at once (<see cref="VoyageLaunch.SeatLocal"/>), so the host's page shows it and the
    /// voyage spawns them in it. The AI seats and Set sail stay the host's.</para>
    /// </summary>
    public sealed class VoyageArkPage
    {
        // Frogma Rev 138 Seats, in its own units: the page is x 40-1880, y 176-1046 of the 1920 x 1080 board (the bar's foot).
        static readonly Vector2 Design = new(1840f, 870f);
        const float SeatX = 80f, SeatY = 82f, SeatWidth = 520f, SeatHeight = 640f, SeatStep = 580f;
        const float HullY = 28f, HullHeight = 200f, NamesY = 364f, LineY = 424f, FaceY = 444f, MoodY = 566f, ReasonY = 597f,
            FootY = 650f, FootHeight = 44f;
        const float FloorY = 324f, FloorHeight = 546f, BarY = 814f, BarHeight = 56f, FitWidth = 180f;
        const float FillWidth = 560f, SailWidth = 300f, SailAsWidth = 640f, FillRow = 52f, PayWidth = 150f;
        const string LastKitKey = "Voyages.LastKit";

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly VoyageSeats _rules;
        readonly HostConnectionDataSO _party;

        public RectTransform Root { get; }
        readonly RectTransform _frame;
        Vector2 _fitFor = new(-1f, -1f);
        TMP_Text _guestLine;
        readonly List<SeatView> _seatViews = new();
        Button _sail;
        TMP_Text _sailWords;
        RectTransform _fillButton;
        VoyageSeatPicker _picker;
        VoyageVesselsPage _vessels;
        VoyagePilotsPage _pilots;
        readonly VoyageClassesSO _classes = VoyageClassesSO.Load();
        readonly VoyageQuaySO _quayWords = VoyageQuaySO.Load();

        // The voyage, top left.
        VoyageJobHeader _header;

        // The bar's squad fit: the meter and its words, bottom left.
        RectTransform _fit;
        readonly Image[] _fitSegments = new Image[3];
        TMP_Text _fitWords;

        // The bar: the fill button and its list of saved squads, and "You sail as".
        RectTransform _fillList, _sailAsFlower;
        TMP_Text _sailAs;
        readonly List<(RectTransform root, TMP_Text words)> _fillRows = new();

        VoyageBoard.Offer _offer;
        int _difficulty;
        readonly List<string> _friends = new();
        readonly List<string> _friendIds = new();
        float _pollIn;
        int _partySig;
        const float PollSeconds = 0.5f;

        sealed class SeatView
        {
            public RectTransform Root, StateRoot, Flower, Change, AiButtons;
            public TMP_Text Role, State, Vessel, Pilot, Element, Line, Class, Mood, Quote, Reason;
            public VoyageMoodMark MoodMark;
            public RectTransform MoodRow;
            public PilotMood Feels;
            public string PilotName;
            public VoyageGlyph ClassGlyph;
            public VoyageRing StateDot;
            public VoyageSeats.HullView Hull;
            public Image Portrait, Rule;
            public Button ChangeButton, Invite, Wingmate, HullButton, PilotButton;
        }

        /// <summary>The seat picker or Vessels is open over the page (Back closes it first).</summary>
        public bool PickerOpen => (_picker != null && _picker.IsOpen) || (_vessels != null && _vessels.IsOpen) ||
                                  (_pilots != null && _pilots.IsOpen);

        /// <summary>Removes the seat popup's and Vessels' canvases with the board.</summary>
        public void Dispose()
        {
            _picker?.Dispose();
            _vessels?.Dispose();
        }

        public VoyageArkPage(VoyageBoard board, VoyageKit kit, VoyageSeats rules, RectTransform pages, HostConnectionDataSO party)
        {
            _board = board;
            _kit = kit;
            _lines = kit.Lines;
            _rules = rules;
            _party = party;
            Root = VoyageKit.Node("YourArk", pages);
            VoyageKit.Stretch(Root);
            _frame = VoyageKit.Node("Frame", Root);
            VoyageKit.Fit(_frame, Design);

            BuildFloor();
            BuildVoyage();
            BuildDomain();
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++) _seatViews.Add(BuildSeat(seat));
            BuildBar();
            _picker = new VoyageSeatPicker(_board, _kit, _rules, Root);
            _vessels = new VoyageVesselsPage(_board, _kit, _rules, pages);
            _pilots = new VoyagePilotsPage(_kit, _rules, pages);
            _rules.Changed += Refresh;
            _rules.VoyageChanged += SeatsChanged;
        }

        /// <summary>A friend's own machine: the host picks the voyage, the AI seats and sets sail.</summary>
        static bool Guest => VoyageLaunch.IsPartyGuest;

        /// <summary>The record the page changes: a friend's own seat in the host's lobby, else the open voyage's.</summary>
        string SeatKey => Guest ? VoyageSeatRecord.PartyKey : _offer?.Key;

        void SeatsChanged()
        {
            // A friend's pick is theirs at once: the hull they spawn in, replicated to the host's page.
            if (Guest) VoyageLaunch.SeatLocal(VoyageLaunch.ResolveGameData(), VoyageSeatRecord.Get(VoyageSeatRecord.PartyKey));
            PublishSeat(force: true);
            Refresh();
        }

        /// <summary>
        /// A friend's machine tells the host the seat it picked (Docs/Voyages/PARTY.md §3): the hull reaches the host through
        /// their replicated player, the pilot, element and artifact only this way. Again every few seconds, so a host
        /// that opens the page later still sees it.
        /// </summary>
        void PublishSeat(bool force)
        {
            // The host publishes too: its seat rides the lobby to every friend (VoyagePartySeats.Snapshot).
            if (!Guest && !VoyageCourseLink.HostWithParty) return;
            var squad = Seats();
            if (squad == null) return;
            var slot = squad.GetSlot(VoyageLaunch.LocalSeat(squad));
            var vessel = _rules.VesselOf(slot.Vessel);
            var pilot = _rules.PilotOf(slot, vessel);
            var me = VoyageLaunch.ResolveGameData()?.LocalPlayer;
            VoyagePartySeats.Publish(slot, me?.Name, VoyageSeats.PilotName(slot, pilot, true), force);
        }

        RectTransform At(string name, float x, float y, float w, float h, RectTransform parent = null)
        {
            var rt = VoyageKit.Node(name, parent ? parent : _frame);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        void BuildFloor()
        {
            // The hangar floor: a faint band across the whole board under the seats, its top
            // edge one muted line.
            var floor = At("Floor", -40f, FloorY, 1920f, FloorHeight);
            VoyageKit.Block(floor, VoyageKit.Fade(FloorLight, 0.015f));
            VoyageKit.Block(At("Edge", 0f, 0f, 1920f, 1f, floor), VoyageKit.Fade(_kit.Outline, 0.4f));
        }

        // The floor's and the shadows' soft blue light: the worn set's Light, toward Outline.
        Color FloorLight => Color.Lerp(_kit.Light, _kit.Outline, 0.35f);

        void BuildVoyage()
        {
            // The open voyage over the seats, its wanted classes at the right (Rev 138 Seats).
            _header = new VoyageJobHeader(_kit, _frame, 0f, 0f, Design.x);

            // On a friend's machine only: who picks what (15, Sub), at the right, under WANTED.
            _guestLine = _kit.Text(At("GuestLine", 1040f, 56f, 800f, 22f), 15, _kit.Sub, TextAlignmentOptions.TopRight);
            VoyageKit.Stretch(_guestLine.rectTransform);
            _guestLine.textWrappingMode = TextWrappingModes.Normal;
        }

        SeatView BuildSeat(int seat)
        {
            var view = new SeatView { Root = At("Seat" + seat, SeatX + seat * SeatStep, SeatY, SeatWidth, SeatHeight) };
            // The seat's rows below stand at page heights (Rev 138 Seats); the seat starts at SeatY.
            const float names = NamesY - SeatY, line = LineY - SeatY, faceY = FaceY - SeatY, moodY = MoodY - SeatY,
                reasonY = ReasonY - SeatY, footY = FootY - SeatY;
            var c = view.Root;
            float w = SeatWidth;

            // Above: its role (caps 14, Sub) at the left; its state, a dot and caps 14 in the state's colour, at the right.
            view.Role = _kit.Label(At("Role", 0f, 0f, w - 260f, 20f, c), 14, _kit.Sub);
            VoyageKit.Stretch(view.Role.rectTransform);
            view.StateRoot = At("State", w - 260f, 0f, 260f, 20f, c);
            VoyageKit.Row(view.StateRoot, 8f, TextAnchor.MiddleRight);
            var dot = VoyageKit.Node("Dot", view.StateRoot);
            var dle = VoyageKit.Le(dot);
            dle.preferredWidth = dle.minWidth = dle.preferredHeight = dle.minHeight = 8f;
            view.StateDot = dot.gameObject.AddComponent<VoyageRing>();
            view.StateDot.raycastTarget = false;
            view.StateDot.Width = 4f;
            view.State = _kit.Label(view.StateRoot, 14, _kit.Light, TextAlignmentOptions.Right);
            view.State.overflowMode = TextOverflowModes.Overflow;

            // The hull (480 x 260) standing on a soft floor shadow, and one thin line under it.
            VoyageKit.Glow(At("Shadow", 40f, FloorY - SeatY - 30f, w - 80f, 60f, c), VoyageKit.Fade(FloorLight, 0.1f));
            var hull = At("Hull", 20f, HullY, w - 40f, HullHeight, c);
            view.Hull = VoyageSeats.HullBox(hull);
            // Pressing the hull opens Vessels on this seat (Frogma Rev 138 Seats, mark 1).
            var hullHit = VoyageKit.Block(hull, VoyageKit.Clear, hit: true);
            view.HullButton = hull.gameObject.AddComponent<Button>();
            view.HullButton.targetGraphic = hullHit;
            view.HullButton.transition = Selectable.Transition.None;
            view.HullButton.onClick.AddListener(() => OpenVessels(seat));
            view.Rule = VoyageKit.Block(At("Line", 0f, line, w, 1f, c), _kit.Outline);

            // Over the line: the vessel (26) with its voyage class beside it, the class's glyph (18) and name (16), Light when
            // this voyage wants the class, muted when it does not; the element's flower (20) and name (16) at the right.
            var vesselRow = At("VesselRow", 0f, names, w - 150f, 34f, c);
            VoyageKit.Row(vesselRow, 14f).childForceExpandHeight = true;
            view.Vessel = _kit.Text(vesselRow, 26, _kit.Light, TextAlignmentOptions.Left);
            view.Vessel.overflowMode = TextOverflowModes.Overflow;
            var glyph = VoyageKit.Node("ClassGlyph", vesselRow);
            var gle = VoyageKit.Le(glyph);
            gle.preferredWidth = gle.minWidth = gle.preferredHeight = gle.minHeight = 18f;
            view.ClassGlyph = glyph.gameObject.AddComponent<VoyageGlyph>();
            view.ClassGlyph.raycastTarget = false;
            view.Class = _kit.Text(vesselRow, 16, _kit.Light, TextAlignmentOptions.Left);
            view.Class.overflowMode = TextOverflowModes.Overflow;
            view.Element = _kit.Text(At("Element", w - 120f, names + 4f, 120f, 26f, c), 16, _kit.Light, TextAlignmentOptions.Right);
            VoyageKit.Stretch(view.Element.rectTransform);
            var flowerBox = At("Flower", w - 150f, names + 7f, 20f, 20f, c);
            view.Flower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(view.Flower);

            // Under the line: the pilot's portrait (84 x 100), their name (19) and what they say of this voyage (17).
            var face = At("Pilot", 0f, faceY, FaceBox.x, FaceBox.y, c);
            VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
            view.Portrait = VoyageKit.CoverBox(face);
            // Pressing the pilot opens Pilots on this seat (Frogma Rev 138 Seats, mark 2).
            var pilotHit = VoyageKit.Block(At("PilotHit", 0f, faceY, w, 100f, c), VoyageKit.Clear, hit: true);
            view.PilotButton = pilotHit.gameObject.AddComponent<Button>();
            view.PilotButton.targetGraphic = pilotHit;
            view.PilotButton.transition = Selectable.Transition.None;
            view.PilotButton.onClick.AddListener(() => OpenPilots(seat));
            view.Pilot = _kit.Text(At("PilotName", 100f, faceY + 2f, w - 100f, 26f, c), 19, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Pilot.rectTransform);
            view.Pilot.overflowMode = TextOverflowModes.Ellipsis;
            view.Quote = _kit.Text(At("Quote", 100f, faceY + 34f, w - 100f, 60f, c), 17, _kit.Light, TextAlignmentOptions.TopLeft);
            VoyageKit.Stretch(view.Quote.rectTransform);
            view.Quote.textWrappingMode = TextWrappingModes.Normal;
            // The pilot's press covers the face, the name and the words: over them, so no text takes it.
            pilotHit.transform.SetAsLastSibling();

            // The pilot's mood for this voyage (Rev 138 Seats): the mark (16) and the mood (16), then why (15, Sub) under it.
            view.MoodRow = At("Mood", 0f, moodY, w, 22f, c);
            VoyageKit.Row(view.MoodRow, 8f).childForceExpandHeight = false;
            var mark = VoyageKit.Node("Mark", view.MoodRow);
            var mle = VoyageKit.Le(mark);
            mle.preferredWidth = mle.minWidth = mle.preferredHeight = mle.minHeight = 16f;
            view.MoodMark = mark.gameObject.AddComponent<VoyageMoodMark>();
            view.MoodMark.raycastTarget = false;
            view.Mood = _kit.Text(view.MoodRow, 16, _kit.Light, TextAlignmentOptions.Left);
            view.Mood.overflowMode = TextOverflowModes.Ellipsis;
            VoyageKit.Le(view.Mood).flexibleWidth = 1f;
            view.Reason = _kit.Text(At("Reason", 0f, reasonY, w, 22f, c), 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Reason.rectTransform);

            // The foot: Change your seat (X) on yours; a friend's line; Invite a friend and Change wingmate on an AI seat.
            view.Line = _kit.Text(At("Line", 0f, footY, w, FootHeight, c), 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Line.rectTransform);
            view.Line.textWrappingMode = TextWrappingModes.Normal;
            view.Change = At("Change", 0f, footY, w, FootHeight, c);
            view.ChangeButton = _kit.Quiet(view.Change, _lines?.ArkChangeSeatKey, _lines?.ArkChangeSeat,
                () => OpenPicker(seat, VoyageSeats.Vessel));
            view.AiButtons = At("AiSeat", 0f, footY, w, FootHeight, c);
            VoyageKit.Row(view.AiButtons, 10f, TextAnchor.MiddleCenter, expandWidth: true);
            var invite = VoyageKit.Node("Invite", view.AiButtons);
            VoyageKit.Le(invite).preferredHeight = FootHeight;
            view.Invite = _kit.Quiet(invite, null, _lines?.ArkInvite, InviteFriend, out var inviteWords);
            inviteWords.fontSize = 14;
            var wingmate = VoyageKit.Node("Wingmate", view.AiButtons);
            VoyageKit.Le(wingmate).preferredHeight = FootHeight;
            view.Wingmate = _kit.Quiet(wingmate, null, _lines?.ArkChangeWingmate, () => OpenPilots(seat),
                out var wingmateWords);
            wingmateWords.fontSize = 14;
            return view;
        }

        void BuildBar()
        {
            var fill = At("FillFromSquad", 0f, BarY, FillWidth, BarHeight);
            _fillButton = fill;
            _kit.Quiet(fill, _lines?.ArkFillKey, _lines?.ArkFillSquad, ToggleFill, out var fillWords);
            // Its words are long: at the button's 18 they ran wider than the button, out past its line and over the Y
            // keycap (Shombith, 2026-11-21: "Fill the seats button is messed"). They shrink to fit, on one line.
            fillWords.textWrappingMode = TextWrappingModes.NoWrap;
            fillWords.enableAutoSizing = true;
            fillWords.fontSizeMax = 16f;
            fillWords.fontSizeMin = 10f;
            fillWords.characterSpacing = 4f;
            fillWords.overflowMode = TextOverflowModes.Ellipsis;
            VoyageKit.Le(fillWords.rectTransform).flexibleWidth = 1f;
            // The saved squads to fill from, over the button while it is open.
            _fillList = At("FillFrom", 0f, BarY, FillWidth, 0f);
            _kit.Box(_fillList, VoyageKit.Fade(_kit.Popup, 0.94f), _kit.Outline, hit: true);
            _fillList.gameObject.SetActive(false);

            // The squad's fit (Rev 138 Seats): three segments (180 wide, 8 tall) and "Squad fit 1 of 3" (16), bottom left.
            _fit = At("Fit", 0f, BarY, 520f, BarHeight);
            float seg = (FitWidth - 8f) / 3f;
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i] = VoyageKit.Block(At("Segment" + i, i * (seg + 4f), 24f, seg, 8f, _fit), _kit.Light);
            _fitWords = _kit.Text(At("Words", FitWidth + 14f, 0f, 320f, BarHeight, _fit), 16, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(_fitWords.rectTransform);

            // "You sail as Manta - Space" and the element's flower (16; the hull and element in Light), 24 left of Set sail.
            float sailX = Design.x - SailWidth;
            var sailAs = At("SailAs", sailX - 24f - SailAsWidth, BarY, SailAsWidth, BarHeight);
            VoyageKit.Row(sailAs, 10f, TextAnchor.MiddleRight);
            _sailAs = _kit.Text(sailAs, 18, Color.Lerp(_kit.Sub, _kit.Light, 0.6f), TextAlignmentOptions.Right);
            _sailAs.overflowMode = TextOverflowModes.Overflow;
            var flowerBox = VoyageKit.Node("Flower", sailAs);
            var fle = VoyageKit.Le(flowerBox);
            fle.preferredWidth = fle.minWidth = fle.preferredHeight = fle.minHeight = 20f;
            _sailAsFlower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_sailAsFlower);

            var sail = At("SetSail", sailX, BarY, SailWidth, BarHeight);
            (_sail, _sailWords) = _kit.Commit(sail, _lines?.BoardSailKey, _lines?.CardSetSail, _board.SetSail);
        }

        // ── The seats this voyage sails with ─────────────────────────────────

        /// <summary>The open voyage's own seats (<see cref="VoyageSeatRecord"/>), made the first time the page opens on it
        /// from the active squad with the player's last line-up in it (the first line-up before they have picked one,
        /// <see cref="VoyageSeats.Seed"/>); with no voyage key, the squad a voyage would sail with, shown but not changed.
        /// A friend's seat in someone else's lobby starts from their squad as before.</summary>
        SavedSquad Seats()
        {
            string key = SeatKey;
            var made = VoyageSeatRecord.Get(key);
            if (made != null) return made;
            var from = VoyageSeats.Active ?? VoyageRun.ActiveSquad(VoyageLaunch.ResolveGameData());
            if (!Guest && !string.IsNullOrEmpty(key))
            {
                var settings = VoyageToyDefinitionSO.Load();
                from = _rules.Seed(from, settings ? settings.FirstLineUp : null) ?? from;
            }
            return VoyageSeatRecord.For(key, from) ?? from;
        }

        // ── The Ark's domain ─────────────────────────────────────────────────

        // Shombith, 2026-11-21: "one single domain for the Ark and the host can select it and the clients can vote, in
        // the In your Ark part of voyages". The host's pick (VoyageLobbyLink.ArkDomain) is what every seat's vessel and the
        // Ark's hull and model fly; a friend's press is a vote the host sees on the chip.
        static readonly Domains[] ArkDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };
        const float DomainChipWidth = 150f, DomainChipHeight = 40f, DomainHeadWidth = 300f;

        sealed class DomainChip
        {
            public Domains Domain;
            public Image Edge;
            public VoyageRing Dot;
            public TMP_Text Words;
        }

        readonly List<DomainChip> _domainChips = new();
        TMP_Text _domainHead;
        int _domainSig;

        void BuildDomain()
        {
            // One row: the head (14, Sub), then Jade, Ruby and Gold, each a quiet plate with the domain's dot.
            // Rev 138 Seats keeps the top right for WANTED, so the Ark's domain stands in the bar, left of Pay and Set sail.
            float rowWidth = DomainHeadWidth + 3 * (DomainChipWidth + 10f);
            var row = At("ArkDomain", Design.x - SailWidth - 24f - PayWidth - rowWidth, BarY + (BarHeight - DomainChipHeight) * 0.5f,
                rowWidth, DomainChipHeight);
            VoyageKit.Row(row, 10f, TextAnchor.MiddleRight);
            _domainHead = _kit.Label(row, 14, _kit.Sub, TextAlignmentOptions.Right);
            _domainHead.overflowMode = TextOverflowModes.Overflow;
            var hle = VoyageKit.Le(_domainHead);
            hle.preferredWidth = hle.minWidth = DomainHeadWidth;
            foreach (var domain in ArkDomains)
            {
                var chip = VoyageKit.Node("Domain" + domain, row);
                var le = VoyageKit.Le(chip);
                le.preferredWidth = le.minWidth = DomainChipWidth;
                le.preferredHeight = le.minHeight = DomainChipHeight;
                var fill = VoyageKit.Sliced(chip, _kit.Style.Sliver10Fill, VoyageKit.Fade(_kit.Popup, 0.78f));
                fill.raycastTarget = true;
                var edge = VoyageKit.Node("Edge", chip);
                VoyageKit.Le(edge).ignoreLayout = true;
                VoyageKit.Stretch(edge);
                var view = new DomainChip { Domain = domain, Edge = VoyageKit.Sliced(edge, _kit.Style.Sliver10Line, _kit.Sub) };
                view.Edge.raycastTarget = false;
                var inner = VoyageKit.Node("Inner", chip);
                VoyageKit.Stretch(inner);
                VoyageKit.Row(inner, 8f, TextAnchor.MiddleCenter);
                var dot = VoyageKit.Node("Dot", inner);
                var dle = VoyageKit.Le(dot);
                dle.preferredWidth = dle.minWidth = dle.preferredHeight = dle.minHeight = 12f;
                view.Dot = dot.gameObject.AddComponent<VoyageRing>();
                view.Dot.raycastTarget = false;
                view.Dot.Width = 2f;
                view.Words = _kit.Text(inner, 15, _kit.Light, TextAlignmentOptions.Left);
                view.Words.overflowMode = TextOverflowModes.Overflow;
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                var picked = domain;
                button.onClick.AddListener(() => PickDomain(picked));
                _domainChips.Add(view);
            }
        }

        void PickDomain(Domains domain)
        {
            if (Guest) VoyageLobbyLink.Vote(domain);
            else VoyageLobbyLink.SetArkDomain(domain);
            _domainSig = DomainSignature();
            DrawDomain();
            // "You sail as" and the seats read the domain too.
            Refresh();
        }

        // The Ark's domain and the votes, so the page redraws when the host picks or a friend votes.
        int DomainSignature()
        {
            int sig = (int)VoyageLobbyLink.ArkDomain * 7 + (int)VoyageLobbyLink.MyVote;
            foreach (var d in ArkDomains) sig = sig * 31 + DomainVotes(d);
            return sig;
        }

        // The votes for a domain: the host counts them; a friend reads the host's count.
        static int DomainVotes(Domains domain) => Guest
            ? VoyageLobbyLink.Latest?.VotesFor(domain) ?? 0
            : VoyageLobbyLink.VotesFor(domain);

        string DomainName(Domains domain) => domain switch
        {
            Domains.Jade => _lines?.ArkDomainJade,
            Domains.Ruby => _lines?.ArkDomainRuby,
            Domains.Gold => _lines?.ArkDomainGold,
            _ => null,
        };

        void DrawDomain()
        {
            bool guest = Guest;
            VoyageKit.Set(_domainHead, guest ? _lines?.ArkDomainHeadGuest : _lines?.ArkDomainHead);
            var ark = VoyageLobbyLink.ArkDomain;
            var gameData = VoyageLaunch.ResolveGameData();
            var theme = gameData ? gameData.ThemeManagerData : null;
            foreach (var chip in _domainChips)
            {
                bool on = chip.Domain == ark;
                bool myVote = guest && chip.Domain == VoyageLobbyLink.MyVote;
                int votes = DomainVotes(chip.Domain);
                string name = DomainName(chip.Domain);
                VoyageKit.Set(chip.Words, votes > 0
                    ? TemplateText.Fill(_lines?.ArkDomainVotes, "domain", name,
                        "votes", votes.ToString(CultureInfo.InvariantCulture))
                    : name);
                // The Ark's domain is the picked one (the selection's cyan line, as your seat's); a friend's own vote in
                // Light; the others idle.
                chip.Edge.color = on ? _kit.Cyan : myVote ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.9f);
                chip.Words.color = on ? _kit.Light : _kit.Sub;
                var color = ToyFactory.DomainAccentColor(theme, chip.Domain);
                chip.Dot.Set(color, on ? color : VoyageKit.Fade(color, 0f));
            }
        }

        // ── Showing ──────────────────────────────────────────────────────────

        public void Show(VoyageBoard.Offer offer, int difficulty)
        {
            _offer = offer;
            _difficulty = difficulty;
            ReadParty();
            _partySig = PartySignature();
            // A friend sails the seat this page shows from the moment they open it, as Set sail fixes the host's.
            if (Guest) VoyageLaunch.SeatLocal(VoyageLaunch.ResolveGameData(), Seats());
            PublishSeat(force: true);
            Refresh();
        }

        void ReadParty()
        {
            _friends.Clear();
            _friendIds.Clear();
            var members = _party ? _party.PartyMembers : null;
            if (members == null) return;
            for (int i = 0; i < members.Count && _friends.Count < SavedSquad.SlotCount - 1; i++)
            {
                var m = members[i];
                if (string.IsNullOrEmpty(m.PlayerId) || m.PlayerId == _party.LocalPlayerId) continue;
                _friends.Add(m.DisplayName);
                _friendIds.Add(m.PlayerId);
            }
        }

        /// <summary>The friend's player in this session (by their UGS id, else their name), or null.</summary>
        static Player FriendPlayer(string id, string name)
        {
            var players = VoyageLaunch.ResolveGameData()?.Players;
            if (players == null) return null;
            Player byName = null;
            foreach (var p in players)
            {
                if (p is not Player player || !player || !player.IsSpawned || player.IsOwner) continue;
                if (!string.IsNullOrEmpty(id) && player.NetUgsPlayerId.Value.ToString() == id) return player;
                if (!byName && !string.IsNullOrEmpty(name) && player.NetName.Value.ToString() == name) byName = player;
            }
            return byName;
        }

        /// <summary>The hull a friend picked (their replicated hull), or none while they have no player here.</summary>
        static VesselClassType FriendHull(string id, string name)
        {
            var player = FriendPlayer(id, name);
            if (!player) return default;
            var hull = player.NetDefaultVesselType.Value;
            return VesselRequirement.IsConcrete(hull) ? hull : default;
        }

        // Who is in the lobby and the hull each friend picked, so the page redraws when a friend changes seat.
        int PartySignature()
        {
            // A friend's reported pilot, element and artifact redraw it too.
            int sig = _friends.Count * 7919 + VoyagePartySeats.Version;
            for (int i = 0; i < _friends.Count; i++)
                sig = sig * 31 + (int)FriendHull(_friendIds[i], _friends[i]) + 1;
            return sig;
        }

        void Refresh()
        {
            if (!Root.gameObject.activeInHierarchy && _offer == null) return;
            var squad = Seats();
            bool editable = squad != null && !string.IsNullOrEmpty(SeatKey);
            bool guest = Guest;

            VoyageKit.Set(_guestLine, guest ? _lines?.ArkLobbyLineGuest : null);
            DrawVoyage();
            DrawDomain();

            // You, then the friends in your lobby in the seats after yours (as the voyage seats them), then an AI wingmate
            // in each seat left.
            int mine = Mathf.Max(0, VoyageSeatRecord.MySeatOf(squad));
            for (int seat = 0; seat < _seatViews.Count; seat++)
            {
                int friend = VoyageRules.FriendAt(seat, mine, _friends.Count);
                if (friend >= 0) DrawFriend(_seatViews[seat], seat, _friendIds[friend], _friends[friend]);
                else DrawSeat(_seatViews[seat], squad, seat, you: seat == mine, editable);
            }
            DrawSailAs(squad);
            // A friend picks their seat; the host fills the AI seats and sets sail.
            // Fill from a saved squad is hidden for now (Shombith, 2026-11-21; VoyageToyDefinitionSO.offerFillFromSquad).
            bool fill = !guest && VoyageToyDefinitionSO.FillFromSquadOffered();
            _fillButton.gameObject.SetActive(fill);
            if (!fill) _fillList.gameObject.SetActive(false);
            if (_fillList.gameObject.activeSelf) DrawFill();
            // A pilot who refuses the voyage holds Set sail until their seat changes (Shombith, 2026-11-24: "refusals yes").
            int refusing = Refusing();
            _sail.interactable = !guest && _offer?.SetSail != null && refusing < 0;
            VoyageKit.Set(_sailWords, guest ? _lines?.ArkHostSails : _lines?.CardSetSail);
            if (refusing >= 0 && _quayWords)
                VoyageKit.Set(_sailAs, TemplateText.Fill(_quayWords.RefusesBlock, "pilot", _seatViews[refusing].PilotName));
            if (_picker.IsOpen) _picker.Refresh();
            if (_vessels.IsOpen) _vessels.Refresh();
            if (_pilots.IsOpen) _pilots.Refresh();
        }

        void DrawVoyage() => _header.Draw(_offer, _difficulty, SeatKey);

        // The squad's fit for the job, bottom left: three segments lit for each wanted class a seat brings.
        void DrawFit(SavedSquad squad)
        {
            string job = VoyageClassesSO.JobOf(SeatKey);
            bool shown = squad != null && _classes && _quayWords && _classes.WantedBy(job).Count > 0;
            _fit.gameObject.SetActive(shown);
            if (!shown) return;
            var vessels = new List<VesselClassType>(SavedSquad.SlotCount);
            for (int s = 0; s < SavedSquad.SlotCount; s++) vessels.Add(squad.GetSlot(s).Vessel);
            int fit = _classes.Fit(job, vessels);
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i].color = i < fit ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.6f);
            VoyageKit.Set(_fitWords, TemplateText.Fill(_quayWords.Fit, "fit", fit.ToString(CultureInfo.InvariantCulture)));
        }

        // "You sail as <vessel> - <element>": the hull the voyage spawns you in (VoyageLaunch.HullThatSails, the same
        // call Set sail makes) and your seat's element, from the seats Set sail sails.
        // "Pay 33" left of Set sail: what the job pays at this difficulty (the quay's number).
        void DrawSailAs(SavedSquad squad)
        {
            _sailAsFlower.parent.gameObject.SetActive(false);
            _sailAs.richText = false;
            bool pays = _offer != null && _offer.Pays && _quayWords;
            VoyageKit.Set(_sailAs, pays
                ? TemplateText.Fill(_quayWords.ArkPay, "pay", VoyageBoard.PayAt(_offer, _difficulty).ToString(CultureInfo.InvariantCulture))
                : null);
            DrawFit(squad);
        }

        /// <summary>A seat's state over its hull (Rev 115 A): you, a friend in your lobby, or the AI.</summary>
        public enum SeatState
        {
            You = 0,
            Friend = 1,
            Ai = 2,
        }

        /// <summary>What a seat on the Ark page is: yours, a friend's (<see cref="VoyageRules.FriendAt"/>), else the AI's.</summary>
        public static SeatState StateOf(int seat, int mySeat, int friends) =>
            seat == mySeat ? SeatState.You : VoyageRules.FriendAt(seat, mySeat, friends) >= 0 ? SeatState.Friend : SeatState.Ai;

        void State(SeatView view, SeatState state)
        {
            // You in cyan, a friend in Lime (they are here and ready to sail with you), the AI in Sub.
            var (words, color) = state switch
            {
                SeatState.You => (_lines?.ArkYou, _kit.Cyan),
                SeatState.Friend => (_lines?.ArkInLobby, _kit.CtaFill),
                _ => (_lines?.ArkWingmate, _kit.Sub),
            };
            view.StateRoot.gameObject.SetActive(!string.IsNullOrEmpty(words));
            VoyageKit.Set(view.State, words);
            view.State.color = color;
            view.StateDot.Set(color, color);
            // One thin line under the hull: cyan under your seat, the muted line under the others.
            view.Rule.color = state == SeatState.You ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.6f);
        }

        void Foot(SeatView view, SeatState state, string line)
        {
            view.Change.gameObject.SetActive(state == SeatState.You && string.IsNullOrEmpty(line));
            view.AiButtons.gameObject.SetActive(state == SeatState.Ai && !Guest);
            VoyageKit.Set(view.Line, line);
        }

        /// <summary>The seat's pilot, whole inside its 84 x 100 face box (Rev 138 Seats) (<see cref="VoyageKit.ShowCover"/>). A bare
        /// <see cref="VoyageKit.Show"/> left the picture at its default 100 x 100, so the box's mask cut the face off
        /// (Shombith, 2026-11-21: "the pilots images are getting cropped").</summary>
        static void ShowFace(SeatView view, Sprite portrait) =>
            VoyageKit.ShowCover(view.Portrait, portrait, FaceBox, VoyageKit.FaceFocus);

        static readonly Vector2 FaceBox = new(84f, 100f);

        void DrawSeat(SeatView view, SavedSquad squad, int seat, bool you, bool editable)
        {
            view.Root.gameObject.SetActive(true);
            var state = you ? SeatState.You : SeatState.Ai;
            VoyageKit.Set(view.Role, _rules.Role(seat));
            State(view, state);
            if (squad == null)
            {
                _rules.SetHull(view.Hull, null);
                ShowFace(view, null);
                VoyageKit.Set(view.Vessel, null);
                DrawClass(view, null);
                DrawMood(view, PilotMood.None, null);
                view.HullButton.interactable = false;
                VoyageKit.Set(view.Pilot, null);
                view.PilotButton.interactable = false;
                VoyageKit.Set(view.Element, null);
                view.Flower.parent.gameObject.SetActive(false);
                Foot(view, state, _lines?.CardNoSquad);
                return;
            }
            var slot = squad.GetSlot(seat);
            var vessel = _rules.VesselOf(slot.Vessel);
            var pilot = _rules.PilotOf(slot, vessel);
            bool canEdit = editable && VoyageSeats.CanEdit(squad, seat);
            _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
            ShowFace(view, pilot ? pilot.Portrait : null);
            VoyageKit.Set(view.Vessel, vessel ? vessel.CardName : null);
            DrawClass(view, vessel);
            DrawMood(view, VoyageQuay.MoodOf(_offer, slot, pilot), VoyageSeats.PilotName(slot, pilot, true));
            // The pilot's name alone under the line (Rev 138 Seats); the artifact is chosen in Pilots.
            VoyageKit.Set(view.Pilot, VoyageSeats.PilotName(slot, pilot, canEdit));
            view.Flower.parent.gameObject.SetActive(slot.Element != Element.None);
            VoyageSeatPicker.Petals(view.Flower, slot.Element);
            VoyageKit.Set(view.Element, _rules.ElementName(slot.Element));
            PlaceFlower(view);

            // Your seat changes here when it is yours to change; an AI seat keeps its two buttons.
            Foot(view, state, you && !canEdit ? _lines?.CardNoSquad : null);
            view.ChangeButton.interactable = canEdit;
            view.HullButton.interactable = canEdit;
            view.Wingmate.interactable = canEdit;
            view.PilotButton.interactable = canEdit;
        }

        void DrawFriend(SeatView view, int seat, string id, string name)
        {
            view.Root.gameObject.SetActive(true);
            VoyageKit.Set(view.Role, _rules.Role(seat));
            State(view, SeatState.Friend);
            // The hull the friend picked on their own page (their replicated hull); their pilot, element and artifact as
            // their machine reported them (VoyagePartySeats), once it has.
            var player = FriendPlayer(id, name);
            var reported = player ? VoyagePartySeats.Of(player.OwnerClientId) : null;
            // The seat they reported names their hull (the host's own seat comes only this way); else their replicated hull.
            var reportedHull = reported != null ? reported.Slot.Vessel : default;
            var vessel = _rules.VesselOf(VesselRequirement.IsConcrete(reportedHull) ? reportedHull : FriendHull(id, name));
            _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
            VoyageKit.Set(view.Vessel, vessel ? vessel.CardName : name);
            DrawClass(view, vessel);
            // A friend's mood is theirs to show on their own page.
            DrawMood(view, PilotMood.None, null);
            // A friend picks their own vessel on their own page.
            view.HullButton.interactable = false;
            view.PilotButton.interactable = false;
            if (reported != null)
            {
                var slot = reported.Slot;
                var pilot = _rules.PilotOf(slot, vessel);
                ShowFace(view, pilot ? pilot.Portrait : null);
                VoyageKit.Set(view.Pilot, !string.IsNullOrEmpty(reported.PilotName) ? reported.PilotName : VoyageSeats.PilotName(slot, pilot, false));
                view.Flower.parent.gameObject.SetActive(slot.Element != Element.None);
                VoyageSeatPicker.Petals(view.Flower, slot.Element);
                VoyageKit.Set(view.Element, _rules.ElementName(slot.Element));
                PlaceFlower(view);
            }
            else
            {
                ShowFace(view, null);
                VoyageKit.Set(view.Pilot, vessel ? name : null);
                view.Flower.parent.gameObject.SetActive(false);
                VoyageKit.Set(view.Element, null);
            }
            Foot(view, SeatState.Friend, TemplateText.Fill(_lines?.ArkFriendLine, "name", name));
        }

        // The element's flower stands 8 left of its word, which is right-aligned to the seat's edge.
        static void PlaceFlower(SeatView view)
        {
            float w = Mathf.Ceil(view.Element.GetPreferredValues(view.Element.text).x);
            var box = (RectTransform)view.Flower.parent;
            box.anchoredPosition = new Vector2(SeatWidth - w - 8f - 20f, box.anchoredPosition.y);
        }

        /// <summary>The seat's voyage class beside its vessel: Light when the open voyage wants it, muted when not;
        /// nothing for a vessel with no class.</summary>
        void DrawClass(SeatView view, SO_Vessel vessel)
        {
            var c = _classes && vessel ? _classes.ClassOf(vessel.Class) : VoyageClass.None;
            bool any = c != VoyageClass.None;
            view.ClassGlyph.gameObject.SetActive(any);
            view.Class.gameObject.SetActive(any);
            if (!any) return;
            var wants = _classes.WantedBy(VoyageClassesSO.JobOf(SeatKey));
            bool lit = wants.Count == 0 || _classes.Wanted(VoyageClassesSO.JobOf(SeatKey), c);
            var col = lit ? _kit.Light : VoyageKit.Fade(_kit.Sub, 0.6f);
            view.ClassGlyph.Form = VoyageGlyph.Of(c);
            view.ClassGlyph.color = col;
            VoyageKit.Set(view.Class, _classes.NameOf(c));
            view.Class.color = col;
        }

        /// <summary>The seat pilot's mood for the open voyage: the mark, the mood and why; nothing for a voyage with no
        /// giver (the Crystal Run).</summary>
        void DrawMood(SeatView view, PilotMood mood, string pilotName)
        {
            view.Feels = mood;
            view.PilotName = pilotName;
            bool any = mood != PilotMood.None && _quayWords;
            view.MoodRow.gameObject.SetActive(any);
            view.Reason.gameObject.SetActive(any);
            view.Quote.gameObject.SetActive(any);
            if (!any) return;
            var refuse = ColorTrace.Retrace(ColorTrace.Ui, VoyageHudColors.ThreatAuthored);
            view.MoodMark.Set(mood, _kit.Light, VoyageKit.Fade(_kit.Outline, 0.8f), refuse);
            var said = mood == PilotMood.Refuses ? refuse : _kit.Light;
            VoyageKit.Set(view.Mood, _quayWords.MoodName(mood));
            view.Mood.color = said;
            VoyageKit.Set(view.Reason, _quayWords.Why(mood));
            VoyageKit.Set(view.Quote, TemplateText.Fill(_quayWords.CrewQuote, "say", _quayWords.Say(mood)));
            view.Quote.color = said;
        }

        /// <summary>The first seat whose pilot refuses the open voyage, or -1 (Set sail waits until it is changed).</summary>
        int Refusing()
        {
            for (int i = 0; i < _seatViews.Count; i++)
                if (_seatViews[i].Root.gameObject.activeSelf && _seatViews[i].Feels == PilotMood.Refuses) return i;
            return -1;
        }

        // ── Invite a friend ──────────────────────────────────────────────────

        // The friends list Menu_Main already has (as the Arcade lobby's add-a-friend slot opens it).
        static void InviteFriend()
        {
            var panel = Object.FindFirstObjectByType<FriendsListPanel>(FindObjectsInactive.Include);
            if (!panel)
            {
                CSDebug.LogWarning("[Voyages] No friends list in this scene to invite from.");
                return;
            }
            if (panel.IsEmbedded) panel.FocusList();
            else panel.Show();
        }

        // ── Fill the AI seats from a saved squad ─────────────────────────────

        void ToggleFill()
        {
            bool open = !_fillList.gameObject.activeSelf;
            var squads = VoyageSeats.Book?.Squads;
            // One saved squad needs no list: it fills at once.
            if (open && squads is { Count: 1 })
            {
                Fill(squads[0]);
                return;
            }
            _fillList.gameObject.SetActive(open);
            if (open)
            {
                _fillList.SetAsLastSibling();
                DrawFill();
            }
        }

        void DrawFill()
        {
            var squads = VoyageSeats.Book?.Squads;
            int count = squads?.Count ?? 0;
            int rows = Mathf.Max(1, count);
            for (int i = 0; i < rows; i++)
            {
                while (_fillRows.Count <= i)
                {
                    int index = _fillRows.Count;
                    var row = At("Squad" + index, 0f, 0f, FillWidth, FillRow, _fillList);
                    var hit = VoyageKit.Block(row, VoyageKit.Clear, hit: true);
                    var b = row.gameObject.AddComponent<Button>();
                    b.targetGraphic = hit;
                    b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() => FillIndex(index));
                    var words = _kit.Text(row, 16, _kit.Light, TextAlignmentOptions.Left);
                    VoyageKit.Stretch(words.rectTransform);
                    words.rectTransform.offsetMin = new Vector2(18f, 0f);
                    _fillRows.Add((row, words));
                }
                var (root, text) = _fillRows[i];
                root.gameObject.SetActive(true);
                VoyageKit.At(root, 0f, i * FillRow, FillWidth, FillRow);
                VoyageKit.Set(text, count == 0 ? _lines?.ArkFillNone : _rules.SquadName(squads[i]));
                text.color = count == 0 ? _kit.Sub : _kit.Light;
            }
            for (int i = rows; i < _fillRows.Count; i++) _fillRows[i].root.gameObject.SetActive(false);
            // The list stands over the button, 12 above it.
            float h = rows * FillRow;
            VoyageKit.At(_fillList, 0f, BarY - 12f - h, FillWidth, h);
        }

        void FillIndex(int index)
        {
            var squads = VoyageSeats.Book?.Squads;
            if (squads == null || index < 0 || index >= squads.Count) return;
            Fill(squads[index]);
        }

        void Fill(SavedSquad preset)
        {
            _fillList.gameObject.SetActive(false);
            Seats();
            if (Guest) return;
            _rules.FillFromPreset(SeatKey, preset, _friends.Count);
        }

        // ── The seat popup ───────────────────────────────────────────────────

        void OpenPicker(int seat, int kind)
        {
            var squad = Seats();
            // A friend changes only their own seat.
            if (Guest && seat != VoyageSeatRecord.MySeatOf(squad)) return;
            _picker.Open(_offer, seat, kind, SeatKey);
        }

        /// <summary>Open Pilots on <paramref name="seat"/> (its pilot pressed, or Change wingmate).</summary>
        void OpenPilots(int seat)
        {
            var squad = Seats();
            // A friend changes only their own seat.
            if (Guest && seat != VoyageSeatRecord.MySeatOf(squad)) return;
            _picker.Close();
            _vessels.Close();
            _pilots.Open(_offer, seat, SeatKey);
        }

        /// <summary>Open Vessels on <paramref name="seat"/> (its hull pressed).</summary>
        void OpenVessels(int seat)
        {
            var squad = Seats();
            // A friend changes only their own seat.
            if (Guest && seat != VoyageSeatRecord.MySeatOf(squad)) return;
            _picker.Close();
            _pilots.Close();
            _vessels.Open(_offer, seat, SeatKey, _difficulty);
        }

        /// <summary>Vessels opened from the quay (See every class): your seat of <paramref name="offer"/>'s voyage, made
        /// now as Your Ark would make it.</summary>
        public void BrowseVessels(VoyageBoard.Offer offer, int difficulty)
        {
            _offer = offer;
            _difficulty = difficulty;
            var squad = Seats();
            if (squad == null || string.IsNullOrEmpty(SeatKey)) return;
            _picker.Close();
            _vessels.Open(offer, Mathf.Max(0, VoyageSeatRecord.MySeatOf(squad)), SeatKey, difficulty);
        }

        /// <summary>The overlays that can stand over another page (Vessels over the quay).</summary>
        public void TickOverlays(float dt) => _vessels.Tick(dt);

        public void ClosePicker()
        {
            _picker.Close();
            _vessels.Close();
            _pilots.Close();
        }

        public void Tick(float dt)
        {
            _picker.Tick(dt);
            _vessels.Tick(dt);
            _pilots.Tick(dt);
            // A friend joining, leaving or changing their hull redraws the seats.
            if ((_pollIn -= dt) <= 0f && _offer != null && Root.gameObject.activeInHierarchy)
            {
                _pollIn = PollSeconds;
                // The party's seat reports travel on the voyage's named messages: listen while the lobby is open, and a
                // friend's machine repeats its own seat now and then.
                VoyageCourseLink.EnsureListening();
                VoyageLobbyLink.EnsureListening();
                PublishSeat(force: false);
                int domainSig = DomainSignature();
                if (domainSig != _domainSig)
                {
                    _domainSig = domainSig;
                    DrawDomain();
                }
                ReadParty();
                int sig = PartySignature();
                if (sig != _partySig)
                {
                    _partySig = sig;
                    Refresh();
                }
            }
            var room = Root.rect.size;
            if (room == _fitFor) return;
            _fitFor = room;
            VoyageKit.Fit(_frame, Design);
        }

        /// <summary>Remember the player's own seat as it sails, for the popup's Last flown kit.</summary>
        public void RememberSeat()
        {
            var squad = VoyageSeatRecord.Get(SeatKey) ?? VoyageSeats.Active;
            // The line-up that sails is the one the next voyage opens on, even when no seat was changed.
            if (!Guest) VoyageSeatRecord.Remember(VoyageSeatRecord.Get(SeatKey));
            int seat = VoyageSeats.MySeat(squad);
            if (seat < 0) return;
            var slot = squad.GetSlot(seat);
            PlayerPrefs.SetString(LastKitKey, VoyageSeatPicker.Kit.Write(new VoyageSeatPicker.Kit
            {
                Vessel = slot.Vessel, PilotId = slot.PilotId, Element = slot.Element, ArtifactId = slot.ArtifactId,
            }));
        }
    }
}
