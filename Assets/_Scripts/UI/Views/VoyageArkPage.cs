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
    /// The Voyages page's third step, your Ark (Frogma Rev 100 P3A "Lobby", drawn by Rev 108 as glass cards on sea lines;
    /// Docs/MENU_REDESIGN_B.md sections 201-202):
    /// <list type="bullet">
    /// <item><b>Your lobby first</b>: your own seat beside the people in your party, each a glass card with its hull, pilot,
    /// element and artifact. Your card carries Vessel, Pilot, Element and Artifact; each opens the seat popup
    /// (<see cref="VoyageSeatPicker"/>).</item>
    /// <item><b>Fly a squad instead</b>: the Ark's squads as presets over three seat cards, your own and your AI wingmates',
    /// each changed through the same popup. With nobody in your lobby the page opens here.</item>
    /// <item>The open voyage at the top right (its kind, name, difficulty and pay), Back and Set sail at the foot.</item>
    /// </list>
    /// The party is read from the lobby's <see cref="HostConnectionDataSO"/>; it carries no ready or choosing state yet, so a
    /// friend's card says they are in your lobby and pick their own vessel.
    /// </summary>
    public sealed class VoyageArkPage
    {
        // Frogma Rev 108 Lobby and Squad (on Rev 100 P3A), in their own units: the page is x 40-1880, y 176-934 of the
        // 1920 x 1080 board.
        static readonly Vector2 Design = new(1840f, 758f);
        const float PresetX = 600f, PresetWidth = 250f, PresetHeight = 104f, PresetGap = 16f;
        const float VoyageX = 1140f, SeatY = 124f, SeatHeight = 540f, SeatWidth = 592f, SeatStep = 625f, FootY = 702f;
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
        RectTransform _presets;
        readonly List<PresetView> _presetViews = new();
        readonly List<SeatView> _seatViews = new();
        RectTransform _flySquadBox;
        TMP_Text _flySquadText;
        Button _sail;
        VoyageSeatPicker _picker;

        // The voyage, top right.
        Image _voyageFace;
        TMP_Text _voyageKind, _voyageTitle, _voyageDifficulty;
        VoyagePips _voyagePips;

        VoyageBoard.Offer _offer;
        int _difficulty;
        bool _squadView, _editable;
        readonly List<string> _friends = new();

        sealed class PresetView
        {
            public RectTransform Root;
            public VoyageKit.GlassCard Card;
            public Button Button;
            public TMP_Text Name, Who;
            public VoyageSeats.HullView Hull;
        }

        sealed class SeatView
        {
            public RectTransform Root;
            public VoyageKit.GlassCard Card;
            public TMP_Text Role, Who, Vessel, Pilot, Element, Artifact, Line;
            public VoyageSeats.HullView Hull;
            public Image Portrait;
            public RectTransform Flower, Settings;
            public readonly Button[] Buttons = new Button[4];
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
            _presets = At("Squads", PresetX, 0f, VoyageX - PresetX - 24f, PresetHeight);
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++) _seatViews.Add(BuildSeat(seat));
            BuildFoot();
            _picker = new VoyageSeatPicker(_board, _kit, _rules, Root);
            _rules.Changed += Refresh;
        }

        RectTransform At(string name, float x, float y, float w, float h, RectTransform parent = null)
        {
            var rt = VoyageKit.Node(name, parent ? parent : _frame);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        void BuildHead()
        {
            // "YOUR LOBBY" (caps 14, Light) over who is in it (16, Sub).
            var head = At("Head", 0f, 10f, PresetX - 30f, 60f);
            VoyageKit.Column(head, 8f, TextAnchor.UpperLeft);
            _head = _kit.Label(head, 14, _kit.Light);
            _headLine = _kit.Text(head, 16, _kit.Sub, TextAlignmentOptions.Left);
        }

        void BuildVoyage()
        {
            // The open voyage: its pilot's face, its name (20), its kind with the difficulty pips and number.
            var face = At("VoyagePilot", VoyageX, 6f, 60f, 60f);
            _voyageFace = VoyageKit.CoverBox(face);
            var words = At("Voyage", VoyageX + 78f, 10f, Design.x - VoyageX - 78f, 60f);
            VoyageKit.Column(words, 8f, TextAnchor.UpperLeft);
            _voyageTitle = _kit.Text(words, 20, _kit.Light, TextAlignmentOptions.Left);
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
            var c = view.Card.Content;
            float w = SeatWidth;

            // Its name (24) at the top left; its role or state (14, Sub) at the top right.
            view.Who = _kit.Text(At("Who", 28f, 22f, w - 56f - 200f, 32f, c), 24, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(view.Who.rectTransform);
            view.Role = _kit.Text(At("Role", w - 28f - 220f, 28f, 220f, 22f, c), 14, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Stretch(view.Role.rectTransform);

            // The hull, large in the middle over a soft light.
            var glow = At("Glow", 60f, 120f, w - 120f, 200f, c);
            VoyageKit.Glow(glow, VoyageKit.Fade(_kit.Outline, 0.55f));
            var hull = At("Hull", 40f, 76f, w - 80f, 260f, c);
            view.Hull = VoyageSeats.HullBox(hull);

            // The pilot (72 x 84), the vessel (24) and pilot's name (16, Sub) beside it; the element's flower at the right.
            var face = At("Pilot", 28f, 350f, 72f, 84f, c);
            VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
            view.Portrait = VoyageKit.CoverBox(face);
            var faceEdge = VoyageKit.Node("Edge", face);
            VoyageKit.Stretch(faceEdge);
            faceEdge.gameObject.AddComponent<VoyageOutline>().Solid(VoyageKit.Fade(_kit.Light, 0.6f));
            var names = At("Names", 118f, 362f, w - 118f - 120f, 60f, c);
            VoyageKit.Column(names, 6f, TextAnchor.UpperLeft);
            view.Vessel = _kit.Text(names, 24, _kit.Light, TextAlignmentOptions.Left);
            view.Pilot = _kit.Text(names, 16, _kit.Sub, TextAlignmentOptions.Left);
            var flowerBox = At("Flower", w - 28f - 32f - 14f, 366f, 32f, 32f, c);
            view.Flower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(view.Flower);
            view.Element = _kit.Text(At("Element", w - 28f - 120f, 402f, 120f, 20f, c), 14, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Stretch(view.Element.rectTransform);

            // Under them: the artifact (another's seat) or a line; your own seat has its four settings there instead.
            var lines = At("Lines", 28f, 468f, w - 56f, 48f, c);
            VoyageKit.Column(lines, 4f, TextAnchor.UpperLeft);
            view.Artifact = _kit.Text(lines, 16, _kit.Sub, TextAlignmentOptions.Left);
            view.Line = _kit.Text(lines, 16, _kit.Sub, TextAlignmentOptions.Left);

            view.Settings = At("Settings", 28f, 470f, w - 56f, 40f, c);
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
            return view;
        }

        void BuildFoot()
        {
            _flySquadBox = At("FlySquad", 0f, FootY, 360f, 56f);
            _kit.Quiet(_flySquadBox, _lines?.ArkFlySquadKey, _lines?.ArkFlySquad, () =>
            {
                _squadView = !_squadView;
                Refresh();
            }, out _flySquadText);
            var back = At("Back", 1200f, FootY, 200f, 56f);
            _kit.Quiet(back, _lines?.PageBackKey, _lines?.PageBack, _board.StepBack);
            var sail = At("SetSail", 1420f, FootY, 420f, 56f);
            (_sail, _) = _kit.Cta(sail, _lines?.BoardSailKey, _lines?.CardSetSail, _board.SetSail);
        }

        // ── Showing ──────────────────────────────────────────────────────────

        public void Show(VoyageBoard.Offer offer, int difficulty)
        {
            _offer = offer;
            _difficulty = difficulty;
            ReadParty();
            Refresh();
        }

        void ReadParty()
        {
            _friends.Clear();
            var members = _party ? _party.PartyMembers : null;
            if (members == null) return;
            for (int i = 0; i < members.Count && _friends.Count < SavedSquad.SlotCount - 1; i++)
            {
                var m = members[i];
                if (string.IsNullOrEmpty(m.PlayerId) || m.PlayerId == _party.LocalPlayerId) continue;
                _friends.Add(m.DisplayName);
            }
        }

        bool Lobby => _friends.Count > 0 && !_squadView;

        void Refresh()
        {
            if (!Root.gameObject.activeInHierarchy && _offer == null) return;
            // The squad that sails: the Ark's, or with no squad yet (offline, before the cloud loads, a new player) the one a voyage
            // would sail with, shown but not changed here.
            var book = VoyageSeats.Active;
            var squad = book ?? VoyageRun.ActiveSquad(VoyageLaunch.ResolveGameData());
            _editable = book != null;

            VoyageKit.Set(_head, Lobby ? _lines?.ArkLobby : _lines?.ArkSquadHead);
            VoyageKit.Set(_headLine, Lobby
                ? TemplateText.Fill(_lines?.ArkLobbyWith, "names", string.Join(", ", _friends))
                : _friends.Count > 0 ? _lines?.ArkSquadLineLobby : _lines?.ArkSquadLine);
            _flySquadBox.gameObject.SetActive(_friends.Count > 0);
            VoyageKit.Set(_flySquadText, _squadView ? _lines?.ArkBackToLobby : _lines?.ArkFlySquad);

            DrawPresets(!Lobby);
            DrawVoyage();

            int mine = VoyageSeats.MySeat(squad);
            if (Lobby)
            {
                // Your seat, then a card per person in your lobby.
                DrawSeat(_seatViews[0], squad, mine >= 0 ? mine : 0, you: true);
                for (int i = 1; i < _seatViews.Count; i++)
                {
                    bool friend = i - 1 < _friends.Count;
                    _seatViews[i].Root.gameObject.SetActive(friend);
                    if (friend) DrawFriend(_seatViews[i], _friends[i - 1]);
                }
            }
            else
            {
                for (int seat = 0; seat < _seatViews.Count; seat++)
                {
                    _seatViews[seat].Root.gameObject.SetActive(squad != null);
                    if (squad != null) DrawSeat(_seatViews[seat], squad, seat, you: seat == mine);
                }
            }
            _sail.interactable = _offer?.SetSail != null;
            if (_picker.IsOpen) _picker.Refresh();
        }

        void DrawPresets(bool show)
        {
            var book = VoyageSeats.Book;
            int count = show && book?.Squads != null ? book.Squads.Count : 0;
            _presets.gameObject.SetActive(count > 0);
            while (_presetViews.Count < count)
            {
                int index = _presetViews.Count;
                // A squad: a glass card 250 x 104, its name (20), who flies it (14, Sub), its leader's hull at the foot.
                var view = new PresetView { Root = At("Squad" + index, index * (PresetWidth + PresetGap), 0f, PresetWidth, PresetHeight, _presets) };
                view.Card = _kit.Glass(view.Root, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Card.Fill;
                view.Button.transition = Selectable.Transition.None;
                view.Button.onClick.AddListener(() => _rules.PickSquad(index));
                var words = At("Words", 18f, 14f, PresetWidth - 36f, 50f, view.Card.Content);
                VoyageKit.Column(words, 6f, TextAnchor.UpperLeft);
                view.Name = _kit.Text(words, 20, _kit.Light, TextAlignmentOptions.Left);
                view.Who = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);
                var hull = At("Hull", PresetWidth - 18f - 72f, PresetHeight - 10f - 32f, 72f, 32f, view.Card.Content);
                view.Hull = VoyageSeats.HullBox(hull);
                _presetViews.Add(view);
            }
            for (int i = 0; i < _presetViews.Count; i++)
            {
                var view = _presetViews[i];
                bool on = i < count;
                view.Root.gameObject.SetActive(on);
                if (!on) continue;
                var squad = book.Squads[i];
                bool active = i == book.ActiveIndex;
                VoyageKit.Set(view.Name, _rules.SquadName(squad));
                VoyageKit.Set(view.Who, Who(squad));
                view.Card.Pick(_kit, active);
                var lead = squad != null ? _rules.VesselOf(squad.GetSlot(0).Vessel) : null;
                _rules.SetHull(view.Hull, VoyageSeats.HullOf(lead));
            }
        }

        // Who flies a squad, seat by seat ("You · AI · AI"): the content's words for each seat.
        string Who(SavedSquad squad)
        {
            if (squad == null) return null;
            var parts = new List<string>(SavedSquad.SlotCount);
            int mine = VoyageSeats.MySeat(squad);
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                var slot = squad.GetSlot(seat);
                parts.Add(seat == mine ? _lines?.ArkYou : slot.IsPlayer() ? slot.MemberName
                    : slot.IsOpen() ? _lines?.CardSeatOpen : _lines?.ArkSeatAiShort);
            }
            return string.Join(_lines?.ArkSquadSeparator ?? string.Empty, parts);
        }

        void DrawVoyage()
        {
            var offer = _offer;
            if (offer == null) return;
            VoyageKit.ShowCover(_voyageFace, offer.Portrait, new Vector2(60f, 60f), VoyageKit.FaceFocus);
            VoyageKit.Set(_voyageKind, TemplateText.Fill(_lines?.ArkVoyageLine, "kind", offer.Kind));
            VoyageKit.Set(_voyageTitle, offer.Title);
            _voyagePips.Lit = _difficulty;
            VoyageKit.Set(_voyageDifficulty, _difficulty.ToString(CultureInfo.InvariantCulture));
        }

        void DrawSeat(SeatView view, SavedSquad squad, int seat, bool you)
        {
            view.Root.gameObject.SetActive(true);
            view.Card.Pick(_kit, you);
            if (squad == null)
            {
                VoyageKit.Set(view.Who, _lines?.CardNoSquad);
                view.Settings.gameObject.SetActive(false);
                return;
            }
            var slot = squad.GetSlot(seat);
            var vessel = _rules.VesselOf(slot.Vessel);
            var pilot = _rules.PilotOf(slot, vessel);
            bool editable = VoyageSeats.CanEdit(squad, seat);
            VoyageKit.Set(view.Role, _rules.Role(seat));
            VoyageKit.Set(view.Who, you ? _lines?.ArkYou : slot.IsPlayer() ? slot.MemberName
                : slot.IsOpen() ? _lines?.CardSeatOpen : _lines?.ArkWingmate);
            _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
            VoyageKit.Show(view.Portrait, pilot ? pilot.Portrait : null);
            VoyageKit.Set(view.Vessel, vessel ? vessel.CardName : null);
            VoyageKit.Set(view.Pilot, VoyageSeats.PilotName(slot, pilot, editable));
            view.Flower.parent.gameObject.SetActive(slot.Element != Element.None);
            VoyageSeatPicker.Petals(view.Flower, slot.Element);
            VoyageKit.Set(view.Element, _rules.ElementName(slot.Element));
            VoyageKit.Set(view.Artifact, vessel
                ? TemplateText.Fill(_lines?.ArkArtifact, "artifact", _rules.ArtifactName(slot, vessel, editable))
                : null);
            // No squad yet (a new player, or before the cloud loads): the player's card says where to make one.
            VoyageKit.Set(view.Line, !_editable && you ? _lines?.CardNoSquad : null);
            view.Settings.gameObject.SetActive(editable && _editable);
            // Your own seat shows its four settings where another seat shows its artifact.
            view.Artifact.transform.parent.gameObject.SetActive(!view.Settings.gameObject.activeSelf || !string.IsNullOrEmpty(view.Line.text));
            int s = seat;
            for (int k = 0; k < 4; k++)
            {
                int kind = k;
                view.Buttons[k].onClick.RemoveAllListeners();
                view.Buttons[k].onClick.AddListener(() => OpenPicker(s, kind));
            }
        }

        void DrawFriend(SeatView view, string name)
        {
            view.Card.Pick(_kit, false);
            VoyageKit.Set(view.Role, _lines?.ArkInLobby);
            VoyageKit.Set(view.Who, name);
            view.Hull.Image.enabled = false;
            VoyageKit.Show(view.Portrait, null);
            VoyageKit.Set(view.Vessel, null);
            VoyageKit.Set(view.Pilot, null);
            view.Flower.parent.gameObject.SetActive(false);
            VoyageKit.Set(view.Element, null);
            VoyageKit.Set(view.Artifact, null);
            VoyageKit.Set(view.Line, _lines?.ArkFriendLine);
            view.Settings.gameObject.SetActive(false);
            view.Artifact.transform.parent.gameObject.SetActive(true);
        }

        // ── The seat popup ───────────────────────────────────────────────────

        void OpenPicker(int seat, int kind) => _picker.Open(_offer, seat, kind, LastKit());

        public void ClosePicker() => _picker.Close();

        public void Tick(float dt)
        {
            _picker.Tick(dt);
            var room = Root.rect.size;
            if (room == _fitFor) return;
            _fitFor = room;
            VoyageKit.Fit(_frame, Design);
        }

        /// <summary>Remember the player's own seat as it sails, for the popup's Last flown kit.</summary>
        public void RememberSeat()
        {
            var squad = VoyageSeats.Active;
            int seat = VoyageSeats.MySeat(squad);
            if (seat < 0) return;
            var slot = squad.GetSlot(seat);
            PlayerPrefs.SetString(LastKitKey, VoyageSeatPicker.Kit.Write(new VoyageSeatPicker.Kit
            {
                Vessel = slot.Vessel, PilotId = slot.PilotId, Element = slot.Element, ArtifactId = slot.ArtifactId,
            }));
        }

        static VoyageSeatPicker.Kit? LastKit() => VoyageSeatPicker.Kit.Read(PlayerPrefs.GetString(LastKitKey, null));
    }
}
