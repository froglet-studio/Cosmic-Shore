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
    /// The Voyages page's third step, your Ark, lobby only (Frogma Rev 113 Lobby, picked by Shombith 2026-11-16:
    /// "For this milestone I want lobby approach to be the main task"; Docs/MENU_REDESIGN_B.md section 209.1):
    /// <list type="bullet">
    /// <item><b>Three seats</b>, filled in order: you, then the friends in your lobby, then AI wingmates
    /// (<see cref="VoyageRules.FriendAt"/>, the order the voyage seats them). Your card says it flies this voyage and
    /// carries Vessel, Pilot, Element and Artifact, each opening the seat popup (<see cref="VoyageSeatPicker"/>); a friend's
    /// card says they pick their own; an AI seat offers Invite a friend and Change wingmate.</item>
    /// <item><b>Fill the AI seats from a saved squad</b> (Y): a saved squad's wingmates go into the AI seats only.</item>
    /// <item><b>You sail as</b> beside Set sail: the vessel and element the voyage spawns you in.</item>
    /// </list>
    /// The seats are the voyage's own (<see cref="VoyageSeatRecord"/>, a copy of the active squad made when the page
    /// opens on a voyage): a pick here never changes a saved squad, and Set sail sails exactly these seats. There is no
    /// squad view any more (Rev 108's "Fly a squad instead" is gone). The party carries no ready or choosing state yet, so a
    /// friend's card says they are in your lobby and pick their own seat; it shows the hull they picked.
    /// <para>On a friend's machine (<see cref="VoyageLaunch.IsPartyGuest"/>) the same page is their own seat in the host's
    /// lobby (Shombith, 2026-11-16: "friends should be able to change their configuration as well, not just use their
    /// vessel selected in the menu"): they change Vessel, Pilot, Element and Artifact on their own card, kept in
    /// <see cref="VoyageSeatRecord.PartyKey"/>; each change is written to their player's replicated hull at once
    /// (<see cref="VoyageLaunch.SeatLocal"/>), so the host's page shows it and the voyage spawns them in it, and their
    /// voyage seeds their element from it. The AI seats and Set sail stay the host's.</para>
    /// </summary>
    public sealed class VoyageArkPage
    {
        // Frogma Rev 113 Lobby (on Rev 108's), in its own units: the page is x 40-1880, y 176-934 of the 1920 x 1080 board.
        static readonly Vector2 Design = new(1840f, 758f);
        const float VoyageX = 1140f, SeatY = 124f, SeatHeight = 540f, SeatWidth = 590f, SeatStep = 625f, FootY = 702f;
        const float FillWidth = 520f, SailAsX = 820f, SailAsWidth = 360f, FillRow = 52f;
        const string LastKitKey = "Voyages.LastKit";

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly VoyageSeats _rules;
        readonly HostConnectionDataSO _party;

        public RectTransform Root { get; }
        readonly RectTransform _frame;
        Vector2 _fitFor = new(-1f, -1f);
        TMP_Text _head, _headLine;
        readonly List<SeatView> _seatViews = new();
        Button _sail;
        TMP_Text _sailWords;
        RectTransform _fillButton;
        VoyageSeatPicker _picker;

        // The voyage, top right.
        Image _voyageFace;
        TMP_Text _voyageKind, _voyageTitle, _voyageDifficulty;
        VoyagePips _voyagePips;

        // The foot: the fill button and its list of saved squads, and "You sail as".
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
            public RectTransform Root;
            public VoyageKit.GlassCard Card;
            public TMP_Text Role, Who, Vessel, Pilot, Element, Line, Badge;
            public VoyageRing BadgeDot;
            public RectTransform BadgeRoot;
            public VoyageSeats.HullView Hull;
            public Image Portrait;
            public RectTransform Flower, Settings, AiButtons;
            public VoyageEdgeGlow Glow;
            public readonly Button[] Buttons = new Button[4];
            public Button Invite, Wingmate;
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

            BuildHead();
            BuildVoyage();
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++) _seatViews.Add(BuildSeat(seat));
            BuildFoot();
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

        void BuildHead()
        {
            // "YOUR LOBBY" (caps 14, Light) over how the seats fill (15, Sub).
            var head = At("Head", 0f, 10f, VoyageX - 40f, 60f);
            VoyageKit.Column(head, 12f, TextAnchor.UpperLeft);
            _head = _kit.Label(head, 14, _kit.Light);
            _headLine = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);
        }

        void BuildVoyage()
        {
            // The open voyage: its picture (the pilot, or the Ark, 110 x 60), its name (18), its kind with the difficulty.
            var face = At("VoyagePicture", VoyageX, 6f, 110f, 60f);
            _voyageFace = VoyageKit.CoverBox(face);
            var words = At("Voyage", VoyageX + 128f, 10f, Design.x - VoyageX - 128f, 60f);
            VoyageKit.Column(words, 8f, TextAnchor.UpperLeft);
            _voyageTitle = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
            var meta = VoyageKit.Node("Meta", words);
            VoyageKit.Row(meta, 12f);
            VoyageKit.Le(meta).preferredHeight = 18f;
            _voyageKind = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            _voyageKind.overflowMode = TextOverflowModes.Overflow;
            _voyagePips = _kit.Pips(meta);
            _voyageDifficulty = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_voyageDifficulty).minWidth = VoyageKit.Le(_voyageDifficulty).preferredWidth = 24f;
            meta.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
        }

        SeatView BuildSeat(int seat)
        {
            var view = new SeatView { Root = At("Seat" + seat, seat * SeatStep, SeatY, SeatWidth, SeatHeight) };
            view.Card = _kit.Glass(view.Root, hit: false);
            // A blue glow along the card's edges (Shombith, 2026-11-16), under its content.
            var edgeGlow = VoyageKit.Node("EdgeGlow", view.Root);
            VoyageKit.Le(edgeGlow).ignoreLayout = true;
            VoyageKit.Stretch(edgeGlow);
            edgeGlow.SetSiblingIndex(view.Card.Edge.transform.GetSiblingIndex());
            view.Glow = edgeGlow.gameObject.AddComponent<VoyageEdgeGlow>();
            var c = view.Card.Content;
            float w = SeatWidth;

            // Top left: its place (caps 14, Sub) over who flies it (22); top right: its state, a dot and caps 14.
            view.Role = _kit.Label(At("Role", 28f, 26f, w - 56f - 240f, 20f, c), 14, _kit.Sub);
            VoyageKit.Stretch(view.Role.rectTransform);
            view.Who = _kit.Text(At("Who", 28f, 52f, w - 56f - 240f, 30f, c), 22, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Who.rectTransform);
            view.BadgeRoot = At("State", w - 28f - 240f, 30f, 240f, 20f, c);
            VoyageKit.Row(view.BadgeRoot, 8f, TextAnchor.MiddleRight);
            var dot = VoyageKit.Node("Dot", view.BadgeRoot);
            var dle = VoyageKit.Le(dot);
            dle.preferredWidth = dle.minWidth = dle.preferredHeight = dle.minHeight = 8f;
            view.BadgeDot = dot.gameObject.AddComponent<VoyageRing>();
            view.BadgeDot.raycastTarget = false;
            view.BadgeDot.Width = 4f;
            view.Badge = _kit.Label(view.BadgeRoot, 14, _kit.Light, TextAlignmentOptions.Right);
            view.Badge.overflowMode = TextOverflowModes.Overflow;

            // The hull, large in the middle over a soft light.
            var glow = At("Glow", 0.06f * w, 120f, 0.88f * w, 210f, c);
            VoyageKit.Glow(glow, VoyageKit.Fade(_kit.Outline, 0.55f));
            var hull = At("Hull", 0.08f * w, 100f, 0.84f * w, 220f, c);
            view.Hull = VoyageSeats.HullBox(hull);

            // The pilot (72 x 86), the vessel (22) and pilot (15, Sub) beside it; the element's flower (32) and name at the right.
            var face = At("Pilot", 28f, 350f, 72f, 86f, c);
            VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
            view.Portrait = VoyageKit.CoverBox(face);
            var faceEdge = VoyageKit.Node("Edge", face);
            VoyageKit.Stretch(faceEdge);
            faceEdge.gameObject.AddComponent<VoyageOutline>().Solid(VoyageKit.Fade(_kit.Light, 0.6f));
            var names = At("Names", 118f, 364f, w - 118f - 120f, 60f, c);
            VoyageKit.Column(names, 8f, TextAnchor.UpperLeft);
            view.Vessel = _kit.Text(names, 22, _kit.Light, TextAlignmentOptions.Left);
            view.Pilot = _kit.Text(names, 15, _kit.Sub, TextAlignmentOptions.Left);
            var flowerBox = At("Flower", w - 28f - 32f - 14f, 360f, 32f, 32f, c);
            view.Flower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(view.Flower);
            view.Element = _kit.Text(At("Element", w - 28f - 120f, 398f, 120f, 20f, c), 14, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Stretch(view.Element.rectTransform);

            // The foot (48 tall, 26 from the bottom): your four settings, an AI seat's two buttons, or a line.
            float footY = SeatHeight - 26f - 48f;
            view.Line = _kit.Text(At("Line", 28f, footY, w - 56f, 48f, c), 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Line.rectTransform);
            view.Line.textWrappingMode = TextWrappingModes.Normal;

            view.Settings = At("Settings", 28f, footY + 4f, w - 56f, 40f, c);
            VoyageKit.Row(view.Settings, 8f, TextAnchor.MiddleCenter, expandWidth: true);
            string[] settings = { _lines?.CardSettingVessel, _lines?.CardSettingPilot, _lines?.CardSettingElement, _lines?.CardSettingArtifact };
            for (int kind = 0; kind < 4; kind++)
            {
                int k = kind;
                var b = VoyageKit.Node("Setting" + kind, view.Settings);
                VoyageKit.Le(b).preferredHeight = 40f;
                view.Buttons[kind] = _kit.Quiet(b, null, settings[kind], () => OpenPicker(seat, k), out var label);
                label.fontSize = 14;
            }

            view.AiButtons = At("AiSeat", 28f, footY + 4f, w - 56f, 40f, c);
            VoyageKit.Row(view.AiButtons, 8f, TextAnchor.MiddleCenter, expandWidth: true);
            var invite = VoyageKit.Node("Invite", view.AiButtons);
            VoyageKit.Le(invite).preferredHeight = 40f;
            view.Invite = _kit.Quiet(invite, null, _lines?.ArkInvite, InviteFriend, out var inviteWords);
            inviteWords.fontSize = 14;
            var wingmate = VoyageKit.Node("Wingmate", view.AiButtons);
            VoyageKit.Le(wingmate).preferredHeight = 40f;
            view.Wingmate = _kit.Quiet(wingmate, null, _lines?.ArkChangeWingmate, () => OpenPicker(seat, VoyageSeats.Pilot),
                out var wingmateWords);
            wingmateWords.fontSize = 14;
            return view;
        }

        void BuildFoot()
        {
            var fill = At("FillFromSquad", 0f, FootY, FillWidth, 56f);
            _fillButton = fill;
            _kit.Quiet(fill, _lines?.ArkFillKey, _lines?.ArkFillSquad, ToggleFill);
            // The saved squads to fill from, over the button while it is open.
            _fillList = At("FillFrom", 0f, FootY, FillWidth, 0f);
            _kit.Box(_fillList, VoyageKit.Fade(_kit.Popup, 0.94f), _kit.Outline, hit: true);
            _fillList.gameObject.SetActive(false);

            // "You sail as Manta - Space" and the element's flower (16, Sub; the hull and element in Light), right-aligned.
            var sailAs = At("SailAs", SailAsX, FootY, SailAsWidth, 56f);
            VoyageKit.Row(sailAs, 10f, TextAnchor.MiddleRight);
            _sailAs = _kit.Text(sailAs, 16, _kit.Sub, TextAlignmentOptions.Right);
            _sailAs.overflowMode = TextOverflowModes.Overflow;
            var flowerBox = VoyageKit.Node("Flower", sailAs);
            var fle = VoyageKit.Le(flowerBox);
            fle.preferredWidth = fle.minWidth = fle.preferredHeight = fle.minHeight = 20f;
            _sailAsFlower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_sailAsFlower);

            var back = At("Back", 1200f, FootY, 200f, 56f);
            _kit.Quiet(back, _lines?.PageBackKey, _lines?.PageBack, _board.StepBack);
            var sail = At("SetSail", 1420f, FootY, 420f, 56f);
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

            VoyageKit.Set(_head, _lines?.ArkLobby);
            VoyageKit.Set(_headLine, guest ? _lines?.ArkLobbyLineGuest : _lines?.ArkLobbyLine);
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
            if (_fillList.gameObject.activeSelf) DrawFill();
            // A friend picks their seat; the host fills the AI seats and sets sail.
            _fillButton.gameObject.SetActive(!guest);
            if (guest) _fillList.gameObject.SetActive(false);
            _sail.interactable = !guest && _offer?.SetSail != null;
            VoyageKit.Set(_sailWords, guest ? _lines?.ArkHostSails : _lines?.CardSetSail);
            if (_picker.IsOpen) _picker.Refresh();
        }

        void DrawVoyage()
        {
            var offer = _offer;
            if (offer == null) return;
            var picture = offer.Portrait ? offer.Portrait : offer.Picture;
            VoyageKit.ShowCover(_voyageFace, picture, new Vector2(110f, 60f),
                offer.Portrait ? VoyageKit.FaceFocus : new Vector2(0.5f, 0.5f));
            VoyageKit.Set(_voyageKind, TemplateText.Fill(_lines?.ArkVoyageLine, "kind", offer.Kind));
            VoyageKit.Set(_voyageTitle, offer.Title);
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

        void Badge(SeatView view, string words, Color color)
        {
            view.BadgeRoot.gameObject.SetActive(!string.IsNullOrEmpty(words));
            VoyageKit.Set(view.Badge, words);
            view.Badge.color = color;
            view.BadgeDot.Set(color, color);
        }

        void DrawSeat(SeatView view, SavedSquad squad, int seat, bool you, bool editable)
        {
            view.Root.gameObject.SetActive(true);
            view.Card.Pick(_kit, you);
            Glow(view, you);
            if (squad == null)
            {
                VoyageKit.Set(view.Who, _lines?.CardNoSquad);
                view.Settings.gameObject.SetActive(false);
                view.AiButtons.gameObject.SetActive(false);
                Badge(view, null, _kit.Sub);
                return;
            }
            var slot = squad.GetSlot(seat);
            var vessel = _rules.VesselOf(slot.Vessel);
            var pilot = _rules.PilotOf(slot, vessel);
            bool canEdit = editable && VoyageSeats.CanEdit(squad, seat);
            VoyageKit.Set(view.Role, _rules.Role(seat));
            // A squad member who is not in your lobby has the AI fly for them.
            VoyageKit.Set(view.Who, you ? _lines?.ArkYou : slot.IsPlayer()
                ? TemplateText.Fill(_lines?.ArkAiFor, "name", slot.MemberName)
                : _lines?.ArkWingmate);
            if (you) Badge(view, _lines?.ArkFlies, _kit.Light);
            else Badge(view, _lines?.ArkSeatAiShort, _kit.Sub);
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

            // Your seat: its four settings. An AI seat: Invite a friend and Change wingmate (when you may change it).
            view.Settings.gameObject.SetActive(you && canEdit);
            view.AiButtons.gameObject.SetActive(!you && !Guest);
            view.Wingmate.interactable = canEdit;
            VoyageKit.Set(view.Line, you && !canEdit ? _lines?.CardNoSquad : null);
            int s = seat;
            for (int k = 0; k < 4; k++)
            {
                int kind = k;
                view.Buttons[k].onClick.RemoveAllListeners();
                view.Buttons[k].onClick.AddListener(() => OpenPicker(s, kind));
            }
            view.Wingmate.onClick.RemoveAllListeners();
            view.Wingmate.onClick.AddListener(() => OpenPicker(s, VoyageSeats.Pilot));
        }

        void DrawFriend(SeatView view, int seat, string id, string name)
        {
            // The hull the friend picked on their own page (their replicated hull); pilot and element stay on their machine.
            var vessel = _rules.VesselOf(FriendHull(id, name));
            view.Root.gameObject.SetActive(true);
            view.Card.Pick(_kit, false);
            Glow(view, false);
            VoyageKit.Set(view.Role, _rules.Role(seat));
            VoyageKit.Set(view.Who, name);
            Badge(view, _lines?.ArkInLobby, _kit.Light);
            _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
            VoyageKit.Show(view.Portrait, null);
            VoyageKit.Set(view.Vessel, vessel ? vessel.CardName : null);
            VoyageKit.Set(view.Pilot, null);
            view.Flower.parent.gameObject.SetActive(false);
            VoyageKit.Set(view.Element, null);
            VoyageKit.Set(view.Line, _lines?.ArkFriendLine);
            view.Settings.gameObject.SetActive(false);
            view.AiButtons.gameObject.SetActive(false);
        }

        // The seat's blue edge glow (the worn colour set's cyan, as the picked edge): brighter on your own seat.
        void Glow(SeatView view, bool you)
        {
            view.Glow.color = VoyageKit.Fade(_kit.Cyan, you ? 0.55f : 0.32f);
            view.Glow.Outside = you ? 16f : 12f;
            view.Glow.SetVerticesDirty();
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
            VoyageKit.At(_fillList, 0f, FootY - 12f - h, FillWidth, h);
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
