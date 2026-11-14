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
        const float FootHeight = 56f, Gap = 24f, HeadHeight = 96f, PresetHeight = 48f;
        const string LastKitKey = "Voyages.LastKit";

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly VoyageSeats _rules;
        readonly HostConnectionDataSO _party;

        public RectTransform Root { get; }
        TMP_Text _head, _headLine;
        RectTransform _presets, _seats;
        readonly List<Button> _presetButtons = new();
        readonly List<TMP_Text> _presetNames = new();
        readonly List<Image> _presetBars = new();
        readonly List<SeatView> _seatViews = new();
        RectTransform _flySquadBox;
        TMP_Text _flySquadText;
        Button _sail;
        VoyageSeatPicker _picker;

        // The voyage, top right.
        TMP_Text _voyageKind, _voyageTitle, _voyageDifficulty, _voyagePay;
        VoyagePips _voyagePips;
        RectTransform _voyageFlower;

        VoyageBoard.Offer _offer;
        int _difficulty;
        bool _squadView, _editable;
        readonly List<string> _friends = new();

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

            var ground = VoyageKit.Node("SeaLines", Root);
            VoyageKit.Stretch(ground);
            var lines = ground.gameObject.AddComponent<VoyageSeaLines>();
            lines.color = VoyageKit.Fade(_kit.Outline, 0.45f);
            lines.Lines = 9;
            lines.Speed = 0.008f;

            BuildHead();
            BuildVoyage();
            _presets = VoyageKit.Node("Squads", Root);
            VoyageKit.Top(_presets, HeadHeight + Gap, PresetHeight);
            _presets.offsetMax = new Vector2(-480f, _presets.offsetMax.y);
            VoyageKit.Row(_presets, 12f);
            _seats = VoyageKit.Node("Seats", Root);
            VoyageKit.Stretch(_seats);
            _seats.offsetMin = new Vector2(0f, FootHeight + Gap);
            _seats.offsetMax = new Vector2(0f, -(HeadHeight + Gap * 2f + PresetHeight));
            var row = VoyageKit.Row(_seats, Gap, TextAnchor.UpperLeft, expandWidth: true);
            row.childForceExpandHeight = true;
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++) _seatViews.Add(BuildSeat(seat));
            BuildFoot();
            _picker = new VoyageSeatPicker(_board, _kit, _rules, Root);
            _rules.Changed += Refresh;
        }

        void BuildHead()
        {
            var head = VoyageKit.Node("Head", Root);
            VoyageKit.Top(head, 0f, HeadHeight);
            head.offsetMax = new Vector2(-480f, head.offsetMax.y);
            VoyageKit.Column(head, 8f, TextAnchor.MiddleLeft);
            _head = _kit.Label(head, 16, _kit.Light);
            _headLine = _kit.Text(head, 18, _kit.Sub, TextAlignmentOptions.Left);
        }

        void BuildVoyage()
        {
            var rt = VoyageKit.Node("Voyage", Root);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
            rt.sizeDelta = new Vector2(440f, HeadHeight + 30f);
            rt.anchoredPosition = Vector2.zero;
            var glass = _kit.Glass(rt, hit: false);
            var words = VoyageKit.Node("Words", glass.Content);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(20f, 12f);
            words.offsetMax = new Vector2(-20f, -12f);
            VoyageKit.Column(words, 6f, TextAnchor.MiddleLeft);
            _voyageKind = _kit.Label(words, 13, _kit.Sub);
            _voyageTitle = _kit.Text(words, 20, _kit.Light, TextAlignmentOptions.Left);
            var meta = VoyageKit.Node("Meta", words);
            VoyageKit.Row(meta, 12f);
            VoyageKit.Le(meta).preferredHeight = 20f;
            _voyagePips = _kit.Pips(meta);
            _voyageDifficulty = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_voyageDifficulty).minWidth = VoyageKit.Le(_voyageDifficulty).preferredWidth = 24f;
            var box = VoyageKit.Node("Element", meta);
            var le = VoyageKit.Le(box);
            le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 18f;
            _voyageFlower = VoyageKit.Flower(box, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_voyageFlower);
            _voyagePay = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_voyagePay).flexibleWidth = 1f;
        }

        SeatView BuildSeat(int seat)
        {
            var view = new SeatView { Root = VoyageKit.Node("Seat" + seat, _seats) };
            view.Card = _kit.Glass(view.Root, hit: false);
            var c = view.Card.Content;

            var top = VoyageKit.Node("Top", c);
            VoyageKit.Top(top, 20f, 48f);
            top.offsetMin = new Vector2(24f, top.offsetMin.y);
            top.offsetMax = new Vector2(-24f, top.offsetMax.y);
            VoyageKit.Column(top, 4f, TextAnchor.UpperLeft);
            view.Role = _kit.Label(top, 13, _kit.Sub);
            view.Who = _kit.Text(top, 18, _kit.Light, TextAlignmentOptions.Left);

            var hull = VoyageKit.Node("Hull", c);
            VoyageKit.Anchor(hull, 0f, 0.5f, 1f, 1f);
            hull.offsetMin = new Vector2(24f, 0f);
            hull.offsetMax = new Vector2(-24f, -76f);
            view.Hull = VoyageSeats.HullBox(hull);

            // The pilot's portrait in the hull's corner.
            var face = VoyageKit.Node("Pilot", c);
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(1f, 1f);
            face.sizeDelta = new Vector2(84f, 84f);
            face.anchoredPosition = new Vector2(-20f, -20f);
            view.Portrait = VoyageKit.Picture(face);

            var words = VoyageKit.Node("Words", c);
            VoyageKit.Anchor(words, 0f, 0f, 1f, 0.5f);
            words.offsetMin = new Vector2(24f, 76f);
            words.offsetMax = new Vector2(-24f, -8f);
            VoyageKit.Column(words, 6f, TextAnchor.UpperLeft);
            view.Vessel = _kit.Text(words, 22, _kit.Light, TextAlignmentOptions.Left);
            view.Pilot = _kit.Text(words, 16, _kit.Light, TextAlignmentOptions.Left);
            var element = VoyageKit.Node("Element", words);
            VoyageKit.Row(element, 8f);
            VoyageKit.Le(element).preferredHeight = 20f;
            var box = VoyageKit.Node("Flower", element);
            var fle = VoyageKit.Le(box);
            fle.preferredWidth = fle.minWidth = fle.preferredHeight = fle.minHeight = 18f;
            view.Flower = VoyageKit.Flower(box, Element.Charge, _kit.Light);
            VoyageKit.Stretch(view.Flower);
            view.Element = _kit.Text(element, 15, _kit.Sub, TextAlignmentOptions.Left);
            view.Artifact = _kit.Text(words, 15, _kit.Sub, TextAlignmentOptions.Left);
            view.Line = _kit.Text(words, 15, _kit.Sub, TextAlignmentOptions.Left);

            // The four settings, each opening the seat popup on its tab.
            view.Settings = VoyageKit.Node("Settings", c);
            VoyageKit.Foot(view.Settings, 20f, 44f);
            view.Settings.offsetMin = new Vector2(20f, view.Settings.offsetMin.y);
            view.Settings.offsetMax = new Vector2(-20f, view.Settings.offsetMax.y);
            VoyageKit.Row(view.Settings, 8f, TextAnchor.MiddleCenter, expandWidth: true);
            string[] names = { _lines?.CardSettingVessel, _lines?.CardSettingPilot, _lines?.CardSettingElement, _lines?.CardSettingArtifact };
            for (int kind = 0; kind < 4; kind++)
            {
                int k = kind;
                var b = VoyageKit.Node("Setting" + kind, view.Settings);
                VoyageKit.Le(b).preferredHeight = 44f;
                view.Buttons[kind] = _kit.Quiet(b, null, names[kind], () => OpenPicker(seat, k), out var label);
                label.fontSize = 14;
            }
            return view;
        }

        void BuildFoot()
        {
            var foot = VoyageKit.Node("Foot", Root);
            VoyageKit.Foot(foot, 0f, FootHeight);
            var back = VoyageKit.Node("Back", foot);
            back.anchorMin = back.anchorMax = back.pivot = new Vector2(0f, 0.5f);
            back.sizeDelta = new Vector2(220f, 48f);
            _kit.Quiet(back, _lines?.PageBackKey, _lines?.PageBack, _board.StepBack);
            _flySquadBox = VoyageKit.Node("FlySquad", foot);
            _flySquadBox.anchorMin = _flySquadBox.anchorMax = _flySquadBox.pivot = new Vector2(0f, 0.5f);
            _flySquadBox.sizeDelta = new Vector2(380f, 48f);
            _flySquadBox.anchoredPosition = new Vector2(244f, 0f);
            _kit.Quiet(_flySquadBox, _lines?.ArkFlySquadKey, _lines?.ArkFlySquad, () =>
            {
                _squadView = !_squadView;
                Refresh();
            }, out _flySquadText);
            var sail = VoyageKit.Node("SetSail", foot);
            sail.anchorMin = sail.anchorMax = sail.pivot = new Vector2(1f, 0.5f);
            sail.sizeDelta = new Vector2(380f, 56f);
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
            while (_presetButtons.Count < count)
            {
                int index = _presetButtons.Count;
                var rt = VoyageKit.Node("Squad" + index, _presets);
                var le = VoyageKit.Le(rt);
                le.preferredWidth = 220f;
                le.preferredHeight = PresetHeight;
                var b = _kit.Quiet(rt, null, null, () => _rules.PickSquad(index), out var label);
                label.fontSize = 15;
                var bar = VoyageKit.Node("Bar", rt);
                VoyageKit.Foot(bar, 0f, 2f);
                _presetBars.Add(VoyageKit.Block(bar, _kit.Cyan));
                _presetButtons.Add(b);
                _presetNames.Add(label);
            }
            for (int i = 0; i < _presetButtons.Count; i++)
            {
                bool on = i < count;
                _presetButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                bool active = i == book.ActiveIndex;
                VoyageKit.Set(_presetNames[i], _rules.SquadName(book.Squads[i]));
                _presetNames[i].color = active ? _kit.Light : _kit.Sub;
                _presetBars[i].enabled = active;
            }
        }

        void DrawVoyage()
        {
            var offer = _offer;
            if (offer == null) return;
            VoyageKit.Set(_voyageKind, TemplateText.Fill(_lines?.ArkVoyageLine, "kind", offer.Kind));
            VoyageKit.Set(_voyageTitle, offer.Title);
            _voyagePips.Lit = _difficulty;
            VoyageKit.Set(_voyageDifficulty, _difficulty.ToString(CultureInfo.InvariantCulture));
            _voyageFlower.parent.gameObject.SetActive(offer.Pays);
            if (offer.Pays) VoyageSeatPicker.Petals(_voyageFlower, offer.Element);
            VoyageKit.Set(_voyagePay, offer.Pays
                ? TemplateText.Fill(_lines?.SeaOfferPay, "pay", VoyageBoard.PayAt(offer, _difficulty).ToString(CultureInfo.InvariantCulture),
                    "element", _board.ElementName(offer.Element))
                : null);
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
        }

        // ── The seat popup ───────────────────────────────────────────────────

        void OpenPicker(int seat, int kind) => _picker.Open(_offer, seat, kind, LastKit());

        public void ClosePicker() => _picker.Close();

        public void Tick(float dt) { }

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
