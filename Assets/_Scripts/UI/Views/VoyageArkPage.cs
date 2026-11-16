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
    /// slides in from the right and leaves the seats in view, so your seat changes behind it as you pick.
    /// <para>On a friend's machine (<see cref="VoyageLaunch.IsPartyGuest"/>) the same page is their own seat in the host's
    /// lobby (section 209.2): they change their own seat, kept in <see cref="VoyageSeatRecord.PartyKey"/> and written to
    /// their player's replicated hull at once (<see cref="VoyageLaunch.SeatLocal"/>), so the host's page shows it and the
    /// voyage spawns them in it. The AI seats and Set sail stay the host's.</para>
    /// </summary>
    public sealed class VoyageArkPage
    {
        // Frogma Rev 115 A, in its own units: the page is x 40-1880, y 176-1046 of the 1920 x 1080 board (the bar's foot).
        static readonly Vector2 Design = new(1840f, 870f);
        const float SeatX = 80f, SeatY = 124f, SeatWidth = 520f, SeatHeight = 620f, SeatStep = 580f;
        const float HullY = 70f, HullHeight = 260f, LineY = 390f, NamesY = 410f, FootY = 520f, FootHeight = 48f;
        const float FloorY = 464f, FloorHeight = 300f, BarY = 814f, BarHeight = 56f;
        const float FillWidth = 420f, SailWidth = 300f, SailAsWidth = 640f, FillRow = 52f;
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

        // The voyage, top left.
        Image _voyageFace;
        TMP_Text _voyageTitle, _voyageKind, _voyageWorlds, _voyageDifficulty;
        VoyagePips _voyagePips;

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
            public TMP_Text Role, State, Vessel, Pilot, Element, Line;
            public VoyageRing StateDot;
            public VoyageSeats.HullView Hull;
            public Image Portrait, Rule;
            public Button ChangeButton, Invite, Wingmate;
        }

        public bool PickerOpen => _picker != null && _picker.IsOpen;

        /// <summary>Removes the seat popup's canvas with the board.</summary>
        public void Dispose() => _picker?.Dispose();

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
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++) _seatViews.Add(BuildSeat(seat));
            BuildBar();
            _picker = new VoyageSeatPicker(_board, _kit, _rules, Root);
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
            Refresh();
        }

        RectTransform At(string name, float x, float y, float w, float h, RectTransform parent = null)
        {
            var rt = VoyageKit.Node(name, parent ? parent : _frame);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        void BuildFloor()
        {
            // The hangar floor: a faint band across the whole board under the seats, lit a little toward the foot, its top
            // edge one muted line.
            var floor = At("Floor", -40f, FloorY, 1920f, FloorHeight);
            var g = floor.gameObject.AddComponent<VoyageGradient>();
            g.raycastTarget = false;
            var light = FloorLight;
            g.Set(VoyageKit.Fade(light, 0f), VoyageKit.Fade(light, 0.03f), VoyageKit.Fade(light, 0.06f), 0.5f);
            VoyageKit.Block(At("Edge", 0f, 0f, 1920f, 1f, floor), VoyageKit.Fade(_kit.Outline, 0.4f));
        }

        // The floor's and the shadows' soft blue light: the worn set's Light, toward Outline.
        Color FloorLight => Color.Lerp(_kit.Light, _kit.Outline, 0.35f);

        void BuildVoyage()
        {
            // The open voyage, top left: its picture (90 x 48), its name (18), then its kind, worlds and difficulty (14, Sub).
            var face = At("VoyagePicture", 0f, 4f, 90f, 48f);
            _voyageFace = VoyageKit.CoverBox(face);
            var words = At("Voyage", 106f, 4f, 900f, 52f);
            VoyageKit.Column(words, 6f, TextAnchor.UpperLeft);
            _voyageTitle = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
            var meta = VoyageKit.Node("Meta", words);
            var row = VoyageKit.Row(meta, 10f);
            row.childForceExpandWidth = false;
            VoyageKit.Le(meta).preferredHeight = 18f;
            _voyageKind = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            _voyageKind.overflowMode = TextOverflowModes.Overflow;
            _voyageWorlds = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            _voyageWorlds.overflowMode = TextOverflowModes.Overflow;
            _voyagePips = _kit.Pips(meta);
            _voyageDifficulty = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_voyageDifficulty).minWidth = VoyageKit.Le(_voyageDifficulty).preferredWidth = 24f;

            // On a friend's machine only: who picks what (15, Sub), at the right.
            _guestLine = _kit.Text(At("GuestLine", 1040f, 10f, 800f, 40f), 15, _kit.Sub, TextAlignmentOptions.TopRight);
            VoyageKit.Stretch(_guestLine.rectTransform);
            _guestLine.textWrappingMode = TextWrappingModes.Normal;
        }

        SeatView BuildSeat(int seat)
        {
            var view = new SeatView { Root = At("Seat" + seat, SeatX + seat * SeatStep, SeatY, SeatWidth, SeatHeight) };
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
            VoyageKit.Glow(At("Shadow", 40f, 300f, w - 80f, 60f, c), VoyageKit.Fade(FloorLight, 0.22f));
            view.Hull = VoyageSeats.HullBox(At("Hull", 20f, HullY, w - 40f, HullHeight, c));
            view.Rule = VoyageKit.Block(At("Line", 0f, LineY, w, 1f, c), _kit.Outline);

            // Under the line: the pilot's face (60 x 72), the vessel (24) over "pilot - artifact" (14, Sub), and the element's
            // flower (34) over its name (13, Sub) at the right.
            var face = At("Pilot", 0f, NamesY, 60f, 72f, c);
            VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
            view.Portrait = VoyageKit.CoverBox(face);
            var names = At("Names", 76f, NamesY + 6f, w - 76f - 96f, 64f, c);
            VoyageKit.Column(names, 6f, TextAnchor.UpperLeft);
            view.Vessel = _kit.Text(names, 24, _kit.Light, TextAlignmentOptions.Left);
            view.Pilot = _kit.Text(names, 14, _kit.Sub, TextAlignmentOptions.Left);
            var flowerBox = At("Flower", w - 34f - 24f, NamesY, 34f, 34f, c);
            view.Flower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(view.Flower);
            view.Element = _kit.Text(At("Element", w - 96f, NamesY + 40f, 96f, 18f, c), 13, _kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(view.Element.rectTransform);

            // The foot: Change your seat (X) on yours; a friend's line; Invite a friend and Change wingmate on an AI seat.
            view.Line = _kit.Text(At("Line", 0f, FootY, w, FootHeight, c), 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Line.rectTransform);
            view.Line.textWrappingMode = TextWrappingModes.Normal;
            view.Change = At("Change", 0f, FootY, w, FootHeight, c);
            view.ChangeButton = _kit.Quiet(view.Change, _lines?.ArkChangeSeatKey, _lines?.ArkChangeSeat,
                () => OpenPicker(seat, VoyageSeats.Vessel));
            view.AiButtons = At("AiSeat", 0f, FootY, w, FootHeight, c);
            VoyageKit.Row(view.AiButtons, 10f, TextAnchor.MiddleCenter, expandWidth: true);
            var invite = VoyageKit.Node("Invite", view.AiButtons);
            VoyageKit.Le(invite).preferredHeight = FootHeight;
            view.Invite = _kit.Quiet(invite, null, _lines?.ArkInvite, InviteFriend, out var inviteWords);
            inviteWords.fontSize = 14;
            var wingmate = VoyageKit.Node("Wingmate", view.AiButtons);
            VoyageKit.Le(wingmate).preferredHeight = FootHeight;
            view.Wingmate = _kit.Quiet(wingmate, null, _lines?.ArkChangeWingmate, () => OpenPicker(seat, VoyageSeats.Pilot),
                out var wingmateWords);
            wingmateWords.fontSize = 14;
            return view;
        }

        void BuildBar()
        {
            var fill = At("FillFromSquad", 0f, BarY, FillWidth, BarHeight);
            _fillButton = fill;
            _kit.Quiet(fill, _lines?.ArkFillKey, _lines?.ArkFillSquad, ToggleFill);
            // The saved squads to fill from, over the button while it is open.
            _fillList = At("FillFrom", 0f, BarY, FillWidth, 0f);
            _kit.Box(_fillList, VoyageKit.Fade(_kit.Popup, 0.94f), _kit.Outline, hit: true);
            _fillList.gameObject.SetActive(false);

            // "You sail as Manta - Space" and the element's flower (16; the hull and element in Light), 24 left of Set sail.
            float sailX = Design.x - SailWidth;
            var sailAs = At("SailAs", sailX - 24f - SailAsWidth, BarY, SailAsWidth, BarHeight);
            VoyageKit.Row(sailAs, 10f, TextAnchor.MiddleRight);
            _sailAs = _kit.Text(sailAs, 16, Color.Lerp(_kit.Sub, _kit.Light, 0.6f), TextAlignmentOptions.Right);
            _sailAs.overflowMode = TextOverflowModes.Overflow;
            var flowerBox = VoyageKit.Node("Flower", sailAs);
            var fle = VoyageKit.Le(flowerBox);
            fle.preferredWidth = fle.minWidth = fle.preferredHeight = fle.minHeight = 20f;
            _sailAsFlower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_sailAsFlower);

            var sail = At("SetSail", sailX, BarY, SailWidth, BarHeight);
            (_sail, _sailWords) = _kit.Cta(sail, _lines?.BoardSailKey, _lines?.CardSetSail, _board.SetSail);
        }

        // ── The seats this voyage sails with ─────────────────────────────────

        /// <summary>The open voyage's own seats (<see cref="VoyageSeatRecord"/>), made from the active squad the first time
        /// the page opens on it; with no voyage key, the squad a voyage would sail with, shown but not changed.</summary>
        SavedSquad Seats()
        {
            var from = VoyageSeats.Active ?? VoyageRun.ActiveSquad(VoyageLaunch.ResolveGameData());
            return VoyageSeatRecord.For(SeatKey, from) ?? from;
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
            int sig = _friends.Count;
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
            _fillButton.gameObject.SetActive(!guest);
            if (guest) _fillList.gameObject.SetActive(false);
            if (_fillList.gameObject.activeSelf) DrawFill();
            _sail.interactable = !guest && _offer?.SetSail != null;
            VoyageKit.Set(_sailWords, guest ? _lines?.ArkHostSails : _lines?.CardSetSail);
            if (_picker.IsOpen) _picker.Refresh();
        }

        void DrawVoyage()
        {
            var offer = _offer;
            if (offer == null) return;
            var picture = offer.Picture ? offer.Picture : offer.Portrait;
            VoyageKit.ShowCover(_voyageFace, picture, new Vector2(90f, 48f),
                offer.Picture ? new Vector2(0.5f, 0.5f) : VoyageKit.FaceFocus);
            VoyageKit.Set(_voyageTitle, offer.Title);
            VoyageKit.Set(_voyageKind, TemplateText.Fill(_lines?.ArkVoyageLine, "kind", offer.Kind));
            VoyageKit.Set(_voyageWorlds, offer.Stops.Count > 0
                ? TemplateText.Fill(_lines?.CrewWorlds, "worlds", offer.Stops.Count.ToString(CultureInfo.InvariantCulture))
                : null);
            _voyagePips.Lit = _difficulty;
            VoyageKit.Set(_voyageDifficulty, _difficulty.ToString(CultureInfo.InvariantCulture));
        }

        // "You sail as <vessel> - <element>": the hull the voyage spawns you in (VoyageLaunch.HullThatSails, the same
        // call Set sail makes) and your seat's element, from the seats Set sail sails.
        void DrawSailAs(SavedSquad squad)
        {
            if (squad == null)
            {
                VoyageKit.Set(_sailAs, null);
                _sailAsFlower.parent.gameObject.SetActive(false);
                return;
            }
            var hull = VoyageLaunch.HullThatSails(VoyageLaunch.ResolveGameData(), squad);
            var vessel = _rules.VesselOf(hull);
            var element = squad.GetSlot(VoyageLaunch.LocalSeat(squad)).Element;
            string light = ColorUtility.ToHtmlStringRGB(_kit.Light);
            string Lit(string word) => string.IsNullOrEmpty(word) ? word : "<color=#" + light + ">" + word + "</color>";
            _sailAs.richText = true;
            VoyageKit.Set(_sailAs, TemplateText.Fill(_lines?.ArkSailAs, "vessel", Lit(vessel ? vessel.CardName : null),
                "element", Lit(_rules.ElementName(element))));
            bool flower = element != Element.None && element != Element.Omni;
            _sailAsFlower.parent.gameObject.SetActive(flower);
            if (flower) VoyageSeatPicker.Petals(_sailAsFlower, element);
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

        void DrawSeat(SeatView view, SavedSquad squad, int seat, bool you, bool editable)
        {
            view.Root.gameObject.SetActive(true);
            var state = you ? SeatState.You : SeatState.Ai;
            VoyageKit.Set(view.Role, _rules.Role(seat));
            State(view, state);
            if (squad == null)
            {
                _rules.SetHull(view.Hull, null);
                VoyageKit.Show(view.Portrait, null);
                VoyageKit.Set(view.Vessel, null);
                VoyageKit.Set(view.Pilot, null);
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
            VoyageKit.Show(view.Portrait, pilot ? pilot.Portrait : null);
            VoyageKit.Set(view.Vessel, vessel ? vessel.CardName : null);
            string artifact = vessel ? _rules.ArtifactName(slot, vessel, canEdit) : null;
            string pilotName = VoyageSeats.PilotName(slot, pilot, canEdit);
            // "Tamako Deephook - No artifact": the pilot, then what the seat carries.
            VoyageKit.Set(view.Pilot, string.IsNullOrEmpty(artifact) || string.IsNullOrEmpty(pilotName)
                ? pilotName + artifact
                : pilotName + _lines?.ArkSquadSeparator + artifact);
            view.Flower.parent.gameObject.SetActive(slot.Element != Element.None);
            VoyageSeatPicker.Petals(view.Flower, slot.Element);
            VoyageKit.Set(view.Element, _rules.ElementName(slot.Element));

            // Your seat changes here when it is yours to change; an AI seat keeps its two buttons.
            Foot(view, state, you && !canEdit ? _lines?.CardNoSquad : null);
            view.ChangeButton.interactable = canEdit;
            view.Wingmate.interactable = canEdit;
        }

        void DrawFriend(SeatView view, int seat, string id, string name)
        {
            view.Root.gameObject.SetActive(true);
            VoyageKit.Set(view.Role, _rules.Role(seat));
            State(view, SeatState.Friend);
            // The hull the friend picked on their own page (their replicated hull); pilot and element stay on their machine.
            var vessel = _rules.VesselOf(FriendHull(id, name));
            _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
            VoyageKit.Show(view.Portrait, null);
            VoyageKit.Set(view.Vessel, vessel ? vessel.CardName : name);
            VoyageKit.Set(view.Pilot, vessel ? name : null);
            view.Flower.parent.gameObject.SetActive(false);
            VoyageKit.Set(view.Element, null);
            Foot(view, SeatState.Friend, TemplateText.Fill(_lines?.ArkFriendLine, "name", name));
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

        public void ClosePicker() => _picker.Close();

        public void Tick(float dt)
        {
            _picker.Tick(dt);
            // A friend joining, leaving or changing their hull redraws the seats.
            if ((_pollIn -= dt) <= 0f && _offer != null && Root.gameObject.activeInHierarchy)
            {
                _pollIn = PollSeconds;
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
