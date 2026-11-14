using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Ark's SQUADS tab as THE HOLO TABLE (Rev 106 B, every setting drawn as Rev 110 B; Shombith 2026-11-13: "B is
    /// good"; Docs/MENU_REDESIGN_B.md sections 194, 200 and 206). The hangar is the screen: the squad's real vessels stand on
    /// its pads (<see cref="SquadVesselLayer"/>), and in front of them a dome of light carries the three seats as plates.
    ///
    /// <list type="bullet">
    /// <item><b>The plates</b> (<see cref="SquadSeatPlate"/>): each seat's pilot, who flies it, its vessel and element and
    /// whether they are here, under its own pad. Pressing one raises it and the table projects the seat over it.</item>
    /// <item><b>The projection</b>: the seat's Vessel, Element, Pilot and Artifact (and who flies a rogue seat), each a line
    /// that opens only that list in place; a pick writes the squad at once. Under them the seat's one action: CHANGE VESSEL
    /// on yours, WHO FLIES IT on an open seat, OPEN THE SEAT on an offline friend's. A joined squad is read-only (its
    /// leader sets the seats).</item>
    /// <item><b>The rail</b> (the brass rail of the captains' bridges): the squad dial (the player's squads, made and
    /// joined, at most <see cref="SquadBook.MaxSquads"/>), MAKE (a new squad: you and two open seats), JOIN (friends'
    /// squads with a seat open, projected over the table), DELETE or LEAVE; the squad's light (Jade, Ruby or Gold, the
    /// team colour, only its leader changes it); and Take into MISSION or ARENA.</item>
    /// </list>
    ///
    /// <para>Take into makes the squad active, puts the player in their seat's vessel and pilot, invites the online squad
    /// mates to the party and presses the Home hub's entry for that window (<see cref="MenuHubButton.Press"/>), so Mission
    /// refuses exactly as it does on Home. The squad's domain rides to the launch panel (<see cref="SquadLaunchIntent"/>).</para>
    ///
    /// <para>The squads are the player's <c>SQUAD_DATA</c> record (<see cref="UGSDataService.SquadRepo"/>). Joining rides each
    /// player's PUBLIC squad record (<see cref="SquadDirectory"/>); <see cref="SquadMembership"/> holds the rules.</para>
    ///
    /// <para><b>Pilots are people, artifacts are one of a kind</b> (Garrett 2026-11-07, Docs/Pilots/INVENTORY.md). A slot's
    /// pilot id names a person in a crew (<see cref="PilotRecord"/>) and its artifact id one artifact in a hoard
    /// (<see cref="ArtifactRecord"/>). The pilot list offers the people in the player's crew whose callsign can fly the
    /// seat's hull, the artifact list the hoard's artifacts the hull can carry. Only a slot whose ids are the player's
    /// (<see cref="IdsAreMine"/>: their own seat, and the AI or open seats of a squad they made) is looked up in the
    /// player's inventory; a friend's seat is drawn through the catalog only.</para>
    /// </summary>
    public class SquadsPage : MonoBehaviour, IArkTabPage, IArkTabBack
    {
        enum Pick { None = 0, Who = 1, Vessel = 2, Element = 3, Pilot = 4, Artifact = 5 }

        [Header("Content")]
        [Tooltip("The vessels a seat can fly (the Hangar's list); locked ones are skipped.")]
        [SerializeField] private SO_VesselList vessels;
        [Tooltip("Pilots and artifacts. Empty loads the one in Resources.")]
        [SerializeField] private PilotArtifactCatalogSO catalog;
        [Tooltip("The domains' colours (SO_ColorSet.GetDomainSignalColor).")]
        [SerializeField] private SO_ColorSet colors;

        [Header("The rail")]
        [SerializeField] private KitButton prevSquadButton;
        [SerializeField] private KitButton nextSquadButton;
        [SerializeField] private TMP_Text squadNameText;
        [Tooltip("Whose squad it is, under its name.")]
        [SerializeField] private TMP_Text whoseText;
        [SerializeField] private KitButton newButton;
        [SerializeField] private KitButton joinButton;
        [SerializeField] private KitButton removeButton;
        [SerializeField] private TMP_Text removeLabel;
        [Tooltip("The squad's light: one per domain, in the order of Domain Order.")]
        [SerializeField] private SquadPickChip[] domainChips = new SquadPickChip[3];
        [SerializeField] private Domains[] domainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold };
        [Tooltip("The rail's squad controls (the light, delete, take into): hidden while the player is in no squad.")]
        [SerializeField] private CanvasGroup squadGroup;
        [SerializeField] private KitButton missionButton;
        [Tooltip("The padlock on Mission while Home's Mission entry is not open.")]
        [SerializeField] private Graphic missionLock;
        [SerializeField] private KitButton arenaButton;

        [Header("The table")]
        [Tooltip("Lit in the squad's domain colour, each keeping its own alpha: the dome, its rim and the projection's light.")]
        [SerializeField] private Graphic[] domainLit = { };
        [Tooltip("Leader, Rogue One, Rogue Two: each under its own pad (Rogue One left, the leader in the middle, Rogue Two right).")]
        [SerializeField] private SquadSeatPlate[] plates = new SquadSeatPlate[SavedSquad.SlotCount];
        [Tooltip("Shown while the player is in no squad.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private KitButton emptyMakeButton;

        [Header("The projection")]
        [Tooltip("What the table projects over the picked plate: the seat, one of its lists, or the friends' squads.")]
        [SerializeField] private CanvasGroup projectionGroup;
        [Tooltip("Moved across to stand over the picked plate (the join list stands in the middle); its height follows what it shows.")]
        [SerializeField] private RectTransform projectionRoot;
        [SerializeField] private TMP_Text projectionTitle;
        [Tooltip("Back from a list to the seat, or closes the join list.")]
        [SerializeField] private KitButton backButton;
        [Tooltip("Who flies it, Vessel, Element, Pilot, Artifact: a line each (the label is its value, the line its name).")]
        [SerializeField] private SquadPickChip[] tiles = new SquadPickChip[5];
        [Tooltip("The chevron on each line: hidden on a seat this player cannot change.")]
        [SerializeField] private Graphic[] tileChevrons = new Graphic[5];
        [SerializeField] private KitButton actionButton;
        [SerializeField] private TMP_Text actionLabel;
        [Tooltip("Under a joined squad's seat: who sets it.")]
        [SerializeField] private TMP_Text readOnlyText;
        [Tooltip("A list's words: vessels, elements, artifacts, who flies it.")]
        [SerializeField] private CanvasGroup choicesGroup;
        [SerializeField] private SquadPickChip[] choiceChips = new SquadPickChip[12];
        [Tooltip("The pilot list: the crew's faces and names.")]
        [SerializeField] private CanvasGroup pilotChoicesGroup;
        [SerializeField] private SquadPickChip[] pilotChips = new SquadPickChip[8];
        [SerializeField] private KitButton choicePrevButton;
        [SerializeField] private KitButton choiceNextButton;
        [Tooltip("One line under a list on what the pick changes.")]
        [SerializeField] private TMP_Text choiceHintText;
        [Tooltip("The friends' squads with an open seat.")]
        [SerializeField] private CanvasGroup joinGroup;
        [SerializeField] private SquadRowView[] joinRows = { };
        [SerializeField] private KitText joinStatusText;

        [Header("The projection's layout (reference pixels)")]
        [Tooltip("Top of the first line under the title.")]
        [SerializeField] private float projectionHead = 46f;
        [Tooltip("One line's step down.")]
        [SerializeField] private float tileStep = 46f;
        [SerializeField] private float actionHeight = 44f;
        [SerializeField] private float readOnlyHeight = 44f;
        [SerializeField] private float projectionPad = 18f;
        [Tooltip("The panel's height while it shows a list of words / of pilots / the friends' squads.")]
        [SerializeField] private float choicesHeight = 380f;
        [SerializeField] private float pilotChoicesHeight = 330f;
        [SerializeField] private float joinHeight = 330f;

        [Header("Timing")]
        [Tooltip("Seconds between two reads of the friends' squad records.")]
        [SerializeField, Min(0f)] private float refreshSeconds = 20f;
        [Tooltip("Seconds after a change before the public record is written.")]
        [SerializeField, Min(0f)] private float publishDelaySeconds = 2f;
        [Tooltip("Seconds a first DELETE / LEAVE press waits for the second.")]
        [SerializeField, Min(0.5f)] private float confirmSeconds = 3f;
        [Tooltip("Most friends' records read per refresh.")]
        [SerializeField, Min(1)] private int maxFriendsRead = 30;
        [Tooltip("The domain a new squad starts in.")]
        [SerializeField] private Domains newSquadDomain = Domains.Gold;

        [Header("Words (empty shows nothing)")]
        [SerializeField] private string ownWhose = "Your squad";
        [Tooltip("A joined squad; {owner} is filled in.")]
        [SerializeField] private string joinedWhose = "{owner}'s squad - joined";
        [Tooltip("Toast when GIVE TO A SEAT brings an artifact here ({artifact} = its name): the next seat picked carries it.")]
        [SerializeField] private string giveHintToast = "Pick the seat that carries {artifact}.";
        [Tooltip("Toast when the picked seat takes the artifact ({artifact}, {seat}).")]
        [SerializeField] private string givenToast = "{seat} carries {artifact}.";
        [Tooltip("Toast when OPEN THE SEAT opens an offline friend's seat ({seat} = the seat's name). Empty shows none.")]
        [SerializeField] private string seatOpenedToast = "{seat} is open: the pad stands empty until someone takes it.";
        [Tooltip("Under a joined squad's seat, by its state; {owner} is filled in.")]
        [SerializeField] private string seatedLine = "Only {owner} changes the squad.";
        [SerializeField] private string pendingLine = "Joined. {owner} seats you when they next open the Ark.";
        [SerializeField] private string seatLostLine = "{owner} gave your seat to someone else. Leave the squad, or ask them to open it again.";
        [SerializeField] private string disbandedLine = "{owner} deleted this squad.";
        [SerializeField] private string offlineLine = "Offline - showing the last copy of {owner}'s squad.";
        [SerializeField] private string[] seatNames = { "Leader", "Rogue One", "Rogue Two" };
        [SerializeField] private string youLine = "You";
        [SerializeField] private string aiLine = "AI wingmate";
        [SerializeField] private string openLine = "Open seat";
        [SerializeField] private string unknownFriendLine = "A friend";
        [SerializeField] private string aiModeLabel = "AI wingmate";
        [SerializeField] private string openModeLabel = "Open for a friend";
        [Tooltip("A plate's second line: {vessel} and {element}.")]
        [SerializeField] private string plateLine = "{vessel} - {element}";
        [Tooltip("An open seat's plate in a squad the player leads.")]
        [SerializeField] private string openPlateLine = "Pick who flies it";
        [Tooltip("An open seat's plate in a joined squad.")]
        [SerializeField] private string openPlateJoinedLine = "Anyone in the squad can take it";
        [Tooltip("The projected seat's title: {who} and {state}.")]
        [SerializeField] private string seatTitle = "{who} - {state}";
        [SerializeField] private string onlineState = "Online";
        [SerializeField] private string offlineState = "Offline";
        [SerializeField] private string openState = "Open";
        [Tooltip("A list's title; {list} is its name.")]
        [SerializeField] private string pickTitle = "Pick - {list}";
        [Tooltip("The lines' names: Who flies it, Vessel, Element, Pilot, Artifact.")]
        [SerializeField] private string[] tileNames = { "Who flies it", "Vessel", "Element", "Pilot", "Artifact" };
        [Tooltip("One line under each list: Who flies it, Vessel, Element, Pilot, Artifact.")]
        [SerializeField] private string[] choiceHints =
        {
            "An AI wingmate flies the seat now; a friend who joins takes it from them.",
            "The seat's pad in the hangar changes to the vessel you pick.",
            "The vessel's element: which of its four abilities it leads with.",
            "The pilot's callsign biases how an AI flies this seat.",
            "Carried into the match; None leaves the slot empty.",
        };
        [SerializeField] private string changeVesselWord = "Change vessel";
        [SerializeField] private string whoFliesWord = "Who flies it";
        [SerializeField] private string openSeatWord = "Open the seat";
        [SerializeField] private string joinTitle = "Friends' squads with an open seat";
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "Charge", "Mass", "Space", "Time", "Omni" };
        [Tooltip("Domain names, by Domains value (-, Jade, Ruby, Blue, Gold).")]
        [SerializeField] private string[] domainNames = { "", "Jade", "Ruby", "Blue", "Gold" };
        [Tooltip("A seat's pilot word when the catalog offers its vessel no pilot at all.")]
        [SerializeField] private string basePilotLine = "No pilot";
        [Tooltip("A pilot chip's word: {name} is the person, {callsign} the callsign word (VOLT).")]
        [SerializeField] private string pilotChipWord = "{name} - {callsign}";
        [Tooltip("A pilot chip's second line; {sight} is what its callsign lights on LB, {callsign} its word, {species} the person's animals.")]
        [SerializeField] private string pilotDetail = "{sight}";
        [Tooltip("An artifact chip's word is the artifact's name, cut at a word to at most this many characters.")]
        [SerializeField, Min(4)] private int artifactChipChars = 16;
        [SerializeField] private string noArtifactLine = "No artifact";
        [SerializeField] private string noArtifactChip = "None";
        [SerializeField] private string deleteLabel = "Delete";
        [SerializeField] private string leaveLabel = "Leave";
        [SerializeField] private string confirmLabel = "Press again";
        [Tooltip("A new squad's name; {n} is its number. Empty names it \"Squad N\".")]
        [SerializeField] private string newSquadName = "";
        [SerializeField] private string fullLine = "You are in 4 squads. Leave or delete one first.";
        [Tooltip("Toast after MAKE; {squad} is filled in.")]
        [SerializeField] private string createdToast = "{squad}: you, and two open seats.";
        [Tooltip("A press that changes a joined squad; {owner} is filled in.")]
        [SerializeField] private string notLeaderLine = "Only {owner} changes the squad.";
        [SerializeField] private string joinLookingLine = "Looking at your friends' squads...";
        [SerializeField] private string joinNoneLine = "No friend has a seat open right now.";
        [SerializeField] private string joinOfflineLine = "Sign in to join a friend's squad.";
        [Tooltip("{squad} and {owner}.")]
        [SerializeField] private string joinRowTitle = "{owner}'s {squad}";
        [Tooltip("{seat} is the seat the player would take.")]
        [SerializeField] private string joinRowLine = "{seat} open";
        [Tooltip("Toast after a join; {squad} and {owner}.")]
        [SerializeField] private string joinedToast = "Joined. {owner} seats you when they next open the Ark.";
        [SerializeField] private string alreadyInLine = "You are already in that squad.";
        [SerializeField] private string seatGoneLine = "That seat was just taken.";

        [Inject] UGSDataService _data;
        [Inject] FriendsDataSO _friends;
        [Inject] HostConnectionDataSO _host;
        [Inject] GameDataSO _gameData;

        static readonly Element[] ElementCycle = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        readonly SquadBook _offlineBook = new();
        readonly Dictionary<string, SquadJoinState> _joinStates = new();
        readonly List<(string ownerId, string ownerName, SavedSquad squad)> _offers = new();
        readonly List<SO_Vessel> _offered = new();
        // The lists' offers: crew people with their callsign assets (index for index), and hoard artifacts. Read into these on each draw (no allocation per draw).
        readonly List<PilotRecord> _crew = new();
        readonly List<PilotRecord> _pilots = new();
        readonly List<PilotSO> _pilotCallsigns = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<ArtifactRecord> _artifacts = new();
        readonly List<string> _slotted = new();
        readonly Dictionary<ScreenSwitcher.ModalWindows, MenuHubButton> _hubs = new();

        // The seat projected (-1: none), the list open on it, and the list's page.
        int _selected, _seat = -1, _page;
        Pick _pick;
        bool _shown, _refreshing, _joinOpen, _loadingOffers;
        float _lastRefresh = float.NegativeInfinity, _publishAt = -1f, _confirmUntil = -1f;
        string _giveArtifactId, _giveArtifactName;

        SquadBook Book => _data != null && _data.SquadRepo?.Data?.Book != null ? _data.SquadRepo.Data.Book : _offlineBook;

        string MyId => _host && !string.IsNullOrEmpty(_host.LocalPlayerId) ? _host.LocalPlayerId : SquadDirectory.LocalPlayerId;
        string MyName => _host && !string.IsNullOrEmpty(_host.LocalDisplayName) ? _host.LocalDisplayName : youLine;

        void Awake()
        {
            if (!catalog) catalog = PilotArtifactCatalogSO.Load();
            for (int i = 0; i < joinRows.Length; i++)
            {
                int index = i;
                if (joinRows[i] && joinRows[i].Button) joinRows[i].Button.onClick.AddListener(() => JoinOffer(index));
            }
            for (int i = 0; i < plates.Length; i++)
            {
                int seat = i;
                if (plates[i] && plates[i].Button) plates[i].Button.onClick.AddListener(() => PressPlate(seat));
            }
            Hook(domainChips, PickDomain);
            Hook(tiles, PressTile);
            Hook(choiceChips, PickChoice);
            Hook(pilotChips, PickPilotChip);
            Listen(prevSquadButton, () => StepSquad(-1));
            Listen(nextSquadButton, () => StepSquad(1));
            Listen(newButton, Make);
            Listen(emptyMakeButton, Make);
            Listen(joinButton, ToggleJoin);
            Listen(backButton, Back);
            Listen(actionButton, Act);
            Listen(choicePrevButton, () => PageChoices(-1));
            Listen(choiceNextButton, () => PageChoices(1));
            Listen(removeButton, Remove);
            Listen(missionButton, () => Play(ScreenSwitcher.ModalWindows.MISSION));
            Listen(arenaButton, () => Play(ScreenSwitcher.ModalWindows.ARENA));
        }

        static void Hook(SquadPickChip[] chips, System.Action<int> pick)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                int index = i;
                if (chips[i] && chips[i].Button) chips[i].Button.onClick.AddListener(() => pick(index));
            }
        }

        static void Listen(KitButton b, UnityEngine.Events.UnityAction action)
        {
            if (b) b.onClick.AddListener(action);
        }

        public void SetPageShown(bool shown)
        {
            _shown = shown;
            if (!shown)
            {
                _joinOpen = false;
                _seat = -1;
                _pick = Pick.None;
                return;
            }
            _selected = Mathf.Clamp(Book.ActiveIndex, 0, Mathf.Max(0, Book.Squads.Count - 1));
            Draw(snapDomain: true);
            RefreshAsync(force: false).Forget();
        }

        void Update()
        {
            // The menu is paused on every screen but Home: unscaled time.
            float now = Time.unscaledTime;
            if (_publishAt >= 0f && now >= _publishAt)
            {
                _publishAt = -1f;
                PublishAsync().Forget();
            }
            if (_confirmUntil >= 0f && now >= _confirmUntil)
            {
                _confirmUntil = -1f;
                DrawRail(Selected());
            }
        }

        // ---- the squad dial ----

        void Select(int index)
        {
            if (index < 0 || index >= Book.Squads.Count) return;
            _selected = index;
            _seat = -1;
            _pick = Pick.None;
            _joinOpen = false;
            _confirmUntil = -1f;
            if (Book.TrySetActive(index)) Saved(publish: false);
            Draw(snapDomain: true);
        }

        void StepSquad(int step)
        {
            int n = Book.Squads.Count;
            if (n == 0) return;
            Select(((_selected + step) % n + n) % n);
        }

        // ---- make / delete ----

        /// <summary>MAKE: a new squad at once - you in the leader's seat on your last hull, and two open seats - and the
        /// table opens on Rogue One so its rider can be picked.</summary>
        void Make()
        {
            if (!Book.CanAdd())
            {
                Refuse(fullLine);
                return;
            }
            var mine = MyDefaultLoadout();
            Offered();
            SquadSlot Rogue(int n)
            {
                var v = _offered.Count > 0 ? _offered[(Mathf.Max(0, _offered.IndexOf(Vessel(mine.Vessel))) + n) % _offered.Count] : null;
                return new SquadSlot(v ? v.Class : mine.Vessel, ElementOf(v, ElementCycle[n % ElementCycle.Length])).WithSeat(SquadSeat.Open);
            }
            var book = Book;
            string name = string.IsNullOrEmpty(newSquadName) ? null
                : TemplateText.Fill(newSquadName, "n", (book.Squads.Count + 1).ToString());
            var squad = new SavedSquad(SquadBook.SanitizeName(name, book.Squads.Count), mine.WithMember(MyId, MyName),
                Rogue(1), Rogue(2)) { Domain = newSquadDomain };
            FillPilots(squad);
            book.Add(squad);
            _selected = book.Squads.Count - 1;
            book.TrySetActive(_selected);
            Saved(publish: true);
            if (!string.IsNullOrWhiteSpace(createdToast))
                ToastNotificationAPI.Show(TemplateText.Fill(createdToast, "squad", squad.Name), ToastKind.Success);
            _joinOpen = false;
            _seat = 1;
            _pick = Pick.None;
            Draw(snapDomain: true);
        }

        void Remove()
        {
            var squad = Selected();
            if (squad == null) return;
            float now = Time.unscaledTime;
            if (_confirmUntil < 0f || now > _confirmUntil)
            {
                _confirmUntil = now + confirmSeconds;
                DrawRail(squad);
                return;
            }
            _confirmUntil = -1f;
            _joinStates.Remove(squad.Id);
            Book.RemoveAt(_selected);
            _selected = Mathf.Clamp(Book.ActiveIndex, 0, Mathf.Max(0, Book.Squads.Count - 1));
            _seat = -1;
            _pick = Pick.None;
            Saved(publish: true);
            Draw(snapDomain: true);
        }

        // ---- the table ----

        void PressPlate(int seat)
        {
            var squad = Selected();
            if (squad == null) return;
            _joinOpen = false;
            if (!string.IsNullOrEmpty(_giveArtifactId))
            {
                Give(squad, seat);
                return;
            }
            _seat = _seat == seat ? -1 : seat;
            _pick = Pick.None;
            Draw();
        }

        /// <summary>A line on the projected seat: opens its list on the page of the seat's own pick.</summary>
        void PressTile(int tile)
        {
            var squad = Selected();
            if (squad == null || _seat < 0) return;
            if (!Editable(squad, _seat))
            {
                Refuse(TemplateText.Fill(notLeaderLine, "owner", OwnerName(squad)));
                return;
            }
            Open((Pick)(tile + 1));
        }

        void Open(Pick pick)
        {
            _pick = pick;
            _page = PageOfCurrent();
            Draw();
        }

        /// <summary>B / Escape (ArkTabsView): a list back to its seat, the seat or the join list back to the table.</summary>
        public bool TryBack()
        {
            if (!_joinOpen && _pick == Pick.None && _seat < 0) return false;
            Back();
            return true;
        }

        void Back()
        {
            if (_joinOpen) _joinOpen = false;
            else if (_pick != Pick.None) _pick = Pick.None;
            else _seat = -1;
            Draw();
        }

        /// <summary>The seat's one action: CHANGE VESSEL on yours, WHO FLIES IT on an open seat, OPEN THE SEAT on an offline
        /// friend's in a squad you lead.</summary>
        void Act()
        {
            var squad = Selected();
            if (squad == null || _seat < 0) return;
            switch (ActionOf(squad, _seat))
            {
                case 1: Open(Pick.Vessel); break;
                case 2: Open(Pick.Who); break;
                case 3: OpenSeat(squad, _seat); break;
            }
        }

        /// <summary>0 none, 1 change vessel, 2 who flies it, 3 open the seat.</summary>
        int ActionOf(SavedSquad squad, int seat)
        {
            if (squad.IsJoined()) return 0;
            var slot = squad.GetSlot(seat);
            if (IsMySeat(squad, seat)) return 1;
            if (slot.IsOpen()) return 2;
            if (seat > 0 && slot.IsPlayer() && !OnlineFriend(slot.MemberId)) return 3;
            return 0;
        }

        /// <summary>OPEN THE SEAT on an offline friend's seat in a squad you lead: the seat opens (its picks kept), its pad
        /// stands empty, and the first friend who joins takes it.</summary>
        void OpenSeat(SavedSquad squad, int seat)
        {
            if (squad.IsJoined() || seat == 0) return;
            var slot = squad.GetSlot(seat);
            if (!slot.IsPlayer() || OnlineFriend(slot.MemberId)) return;
            squad.SetSlot(seat, slot.WithSeat(SquadSeat.Open));
            Saved(publish: true);
            if (!string.IsNullOrWhiteSpace(seatOpenedToast))
                ToastNotificationAPI.Show(TemplateText.Fill(seatOpenedToast, "seat", SeatName(seat)), ToastKind.Success);
            Draw();
        }

        /// <summary>
        /// GIVE TO A SEAT (the Artifacts tab): the next seat pressed on the table carries <paramref name="artifactId"/> (it
        /// leaves any other seat that carried it). A joined squad is the leader's to change. False when it cannot start (no
        /// squad, a joined one).
        /// </summary>
        public bool BeginGive(string artifactId, string artifactName)
        {
            var squad = Selected();
            if (string.IsNullOrEmpty(artifactId) || squad == null) return false;
            if (squad.IsJoined())
            {
                Refuse(TemplateText.Fill(notLeaderLine, "owner", OwnerName(squad)));
                return false;
            }
            _giveArtifactId = artifactId;
            _giveArtifactName = artifactName;
            _seat = -1;
            _pick = Pick.None;
            _joinOpen = false;
            if (!string.IsNullOrWhiteSpace(giveHintToast))
                ToastNotificationAPI.Show(TemplateText.Fill(giveHintToast, "artifact", artifactName), ToastKind.Goal);
            if (_shown) Draw();
            return true;
        }

        void Give(SavedSquad squad, int seat)
        {
            if (!Editable(squad, seat))
            {
                Refuse(TemplateText.Fill(notLeaderLine, "owner", OwnerName(squad)));
                return;
            }
            string id = _giveArtifactId, name = _giveArtifactName;
            _giveArtifactId = null;
            _seat = seat;
            _pick = Pick.None;
            EditSlot(squad, seat, s => { s.ArtifactId = id; return s; });
            if (!string.IsNullOrWhiteSpace(givenToast))
                ToastNotificationAPI.Show(TemplateText.Fill(givenToast, "seat", SeatName(seat), "artifact", name), ToastKind.Success);
        }

        /// <summary>A seat this player changes here: their own seat, and the AI or open seats, of a squad they lead.</summary>
        static bool Editable(SavedSquad squad, int seat) => squad != null && !squad.IsJoined() && CanEdit(squad, seat);

        /// <summary>Writes the seat and saves the squad: every pick on the table is kept at once.</summary>
        void EditSlot(SavedSquad squad, int seat, System.Func<SquadSlot, SquadSlot> change)
        {
            if (!Editable(squad, seat)) return;
            string previousPilot = squad.GetSlot(seat).PilotId;
            squad.SetSlot(seat, change(squad.GetSlot(seat)));
            // Each vessel has its own pilot and artifact: one moved onto this vessel leaves the others (the pilot swaps).
            squad.KeepCardsOwn(seat, previousPilot);
            FillPilots(squad);
            Saved(publish: true);
            Draw();
        }

        // ---- the lists ----

        int PerPage => _pick == Pick.Pilot ? Mathf.Max(1, pilotChips.Length) : Mathf.Max(1, choiceChips.Length);

        /// <summary>How many choices the open list has (the artifact list's first is None).</summary>
        int ChoiceCount(SquadSlot slot)
        {
            switch (_pick)
            {
                case Pick.Who: return 2;
                case Pick.Vessel: Offered(); return _offered.Count;
                case Pick.Element: return ElementCycle.Length;
                case Pick.Pilot: PilotsFor(slot.Vessel); return _pilots.Count;
                case Pick.Artifact: ArtifactsFor(slot.Vessel); return _artifacts.Count + 1;
                default: return 0;
            }
        }

        int PageOfCurrent()
        {
            var squad = Selected();
            if (squad == null || _seat < 0) return 0;
            var slot = squad.GetSlot(_seat);
            int at = -1;
            switch (_pick)
            {
                case Pick.Vessel: Offered(); at = _offered.FindIndex(v => v.Class == slot.Vessel); break;
                case Pick.Pilot: PilotsFor(slot.Vessel); at = IndexOfPilot(slot.PilotId); break;
                case Pick.Artifact:
                    ArtifactsFor(slot.Vessel);
                    if (PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var carried))
                        at = 1 + _artifacts.FindIndex(a => a.Id == carried.Id);
                    break;
            }
            return at < 0 ? 0 : at / PerPage;
        }

        void PageChoices(int step)
        {
            var squad = Selected();
            if (squad == null || _seat < 0 || _pick == Pick.None) return;
            int pages = Mathf.Max(1, Mathf.CeilToInt(ChoiceCount(squad.GetSlot(_seat)) / (float)PerPage));
            _page = ((_page + step) % pages + pages) % pages;
            Draw();
        }

        void PickPilotChip(int chip) => PickChoice(chip);

        void PickChoice(int chip)
        {
            var squad = Selected();
            if (squad == null || _seat < 0 || !Editable(squad, _seat)) return;
            int index = _page * PerPage + chip;
            var slot = squad.GetSlot(_seat);
            int seat = _seat;
            var pick = _pick;
            _pick = Pick.None;
            switch (pick)
            {
                case Pick.Who:
                    if (seat == 0 || index > 1) return;
                    EditSlot(squad, seat, s => s.WithSeat(index == 0 ? SquadSeat.Ai : SquadSeat.Open));
                    break;
                case Pick.Vessel:
                    Offered();
                    if (index >= _offered.Count) return;
                    var v = _offered[index];
                    bool mine = IsMySeat(squad, seat);
                    EditSlot(squad, seat, s =>
                    {
                        var moved = s.WithVessel(v.Class);
                        // The player's own seat starts on the person they keep in that vessel's Hangar seat (a crew id).
                        if (mine) moved.PilotId = PilotLoadoutStore.GetPilot(v.Class);
                        return moved;
                    });
                    break;
                case Pick.Element:
                    if (index >= ElementCycle.Length) return;
                    EditSlot(squad, seat, s => { s.Element = ElementCycle[index]; return s; });
                    break;
                case Pick.Pilot:
                    PilotsFor(slot.Vessel);
                    if (index >= _pilots.Count) return;
                    string pilotId = _pilots[index].Id;
                    EditSlot(squad, seat, s => { s.PilotId = pilotId; return s; });
                    break;
                case Pick.Artifact:
                    ArtifactsFor(slot.Vessel);
                    if (index > _artifacts.Count) return;
                    string artifactId = index == 0 ? null : _artifacts[index - 1].Id;
                    EditSlot(squad, seat, s => { s.ArtifactId = artifactId; return s; });
                    break;
            }
        }

        void PickDomain(int chip)
        {
            if (chip < 0 || chip >= domainOrder.Length) return;
            var domain = domainOrder[chip];
            var squad = Selected();
            if (squad == null || squad.Domain == domain) return;
            if (squad.IsJoined())
            {
                Refuse(TemplateText.Fill(notLeaderLine, "owner", OwnerName(squad)));
                return;
            }
            squad.Domain = domain;
            Saved(publish: true);
            Draw();
        }

        // ---- join ----

        void ToggleJoin()
        {
            if (_joinOpen)
            {
                _joinOpen = false;
                Draw();
                return;
            }
            if (!Book.CanAdd())
            {
                Refuse(fullLine);
                return;
            }
            _joinOpen = true;
            _seat = -1;
            _pick = Pick.None;
            LoadOffersAsync().Forget();
            Draw();
        }

        void CloseJoin() => _joinOpen = false;

        async UniTaskVoid LoadOffersAsync()
        {
            _offers.Clear();
            if (!SquadDirectory.IsReady || _loadingOffers) { DrawJoin(); return; }
            _loadingOffers = true;
            DrawJoin();
            try
            {
                // A snapshot: the friends list is re-synced while the reads are awaited.
                foreach (var friend in new List<(string id, string name)>(FriendIds()))
                {
                    var record = await SquadDirectory.LoadAsync(friend.id);
                    if (!this || !_joinOpen) return;
                    if (record?.Squads == null) continue;
                    foreach (var squad in record.Squads)
                    {
                        if (squad == null || SquadMembership.OpenSeat(squad) < 0) continue;
                        if (SquadMembership.Contains(Book, friend.id, squad.Id)) continue;
                        _offers.Add((friend.id, string.IsNullOrEmpty(record.PlayerName) ? friend.name : record.PlayerName, squad));
                    }
                    DrawJoin();
                }
            }
            finally
            {
                _loadingOffers = false;
            }
            if (this) DrawJoin();
        }

        void JoinOffer(int index)
        {
            if (index < 0 || index >= _offers.Count) return;
            var (ownerId, ownerName, squad) = _offers[index];
            var refusal = SquadMembership.TryJoin(Book, MyId, MyName, ownerId, ownerName, squad, MyDefaultLoadout(), out var joined);
            switch (refusal)
            {
                case SquadJoinRefusal.BookFull: Refuse(fullLine); return;
                case SquadJoinRefusal.AlreadyIn: Refuse(alreadyInLine); return;
                case SquadJoinRefusal.NoOpenSeat:
                case SquadJoinRefusal.OwnSquad: Refuse(seatGoneLine); return;
            }
            _joinStates[joined.Id] = SquadJoinState.Pending;
            _selected = Book.Squads.Count - 1;
            _seat = -1;
            _pick = Pick.None;
            Book.TrySetActive(_selected);
            Saved(publish: true);
            if (!string.IsNullOrWhiteSpace(joinedToast))
                ToastNotificationAPI.Show(TemplateText.Fill(joinedToast, "owner", ownerName, "squad", joined.Name), ToastKind.Success);
            CloseJoin();
            Draw(snapDomain: true);
        }

        // ---- take into ----

        void Play(ScreenSwitcher.ModalWindows target)
        {
            var squad = Selected();
            if (squad == null) return;
            var hub = FindHub(target);
            if (!hub) return;
            // A window Home refuses is refused here the same way, before anything changes.
            if (hub.TryGetComponent<MenuAvailabilityView>(out var gate) && !gate.IsAvailable)
            {
                hub.Press();
                return;
            }

            if (Book.TrySetActive(_selected)) Saved(publish: false);
            var mine = squad.GetSlot(squad.IsJoined() ? squad.MySeat : 0);
            // The configure window opens on the hull last selected this session (unless the card remembers its own).
            if (_gameData && _gameData.selectedVesselClass) _gameData.selectedVesselClass.Value = mine.Vessel;
            // The card holds the player's own person id (or a legacy callsign id); one the crew cannot resolve would empty
            // the Hangar seat, so it is left alone.
            if (!mine.UsesDefaultPilot() && PilotLoadoutStore.TryGetPilot(mine.PilotId, out _))
                PilotLoadoutStore.SetPilot(mine.Vessel, mine.PilotId);
            // The card's artifact is what that vessel carries into the match (VesselPilotLoadout reads the slot).
            PilotLoadoutStore.SetSlottedArtifact(mine.Vessel, mine.ArtifactId);
            InviteSquadMates(squad);
            // The launch panel opens the player on the squad's domain while this trip lasts (SquadLaunchIntent).
            SquadLaunchIntent.Set(squad);
            hub.Press();
        }

        /// <summary>
        /// Home's hub entry for <paramref name="target"/>, found once and kept. <paramref name="reportMissing"/> false is the
        /// probe form (drawing the padlock asks every redraw); the press that is about to spend the result reports a miss.
        /// </summary>
        MenuHubButton FindHub(ScreenSwitcher.ModalWindows target, bool reportMissing = true)
        {
            if (_hubs.TryGetValue(target, out var cached) && cached) return cached;
            foreach (var hub in FindObjectsByType<MenuHubButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hub.Target != target) continue;
                _hubs[target] = hub;
                return hub;
            }
            if (reportMissing) CSDebug.LogWarning($"[SquadsPage] No Home hub entry opens {target}; the squad cannot be taken there.");
            return null;
        }

        /// <summary>Every online friend in the squad who is not already in the party gets the party invite.</summary>
        void InviteSquadMates(SavedSquad squad)
        {
            var service = HostConnectionService.Instance;
            if (service == null || _friends == null || _friends.Friends == null) return;
            string myId = MyId;
            var ids = new List<string>();
            if (squad.IsJoined()) ids.Add(squad.OwnerId);
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            {
                var s = squad.GetSlot(i);
                if (s.IsPlayer() && s.MemberId != myId && !ids.Contains(s.MemberId)) ids.Add(s.MemberId);
            }
            foreach (var id in ids)
            {
                if (InParty(id) || !OnlineFriend(id)) continue;
                InviteAsync(service, id).Forget();
            }
        }

        static async UniTaskVoid InviteAsync(HostConnectionService service, string playerId)
        {
            try
            {
                await service.SendInviteAsync(playerId);
            }
            catch (System.Exception e)
            {
                CSDebug.LogWarning($"[SquadsPage] Inviting a squad mate failed: {e.Message}");
            }
        }

        bool InParty(string id)
        {
            if (_host == null || _host.PartyMembers == null) return false;
            foreach (var m in _host.PartyMembers)
                if (m.PlayerId == id) return true;
            return false;
        }

        bool OnlineFriend(string id)
        {
            foreach (var f in _friends.Friends)
                if (f.PlayerId == id) return f.IsOnline;
            return false;
        }

        // ---- reading and publishing ----

        async UniTaskVoid RefreshAsync(bool force)
        {
            if (_refreshing || !SquadDirectory.IsReady) return;
            if (!force && Time.unscaledTime - _lastRefresh < refreshSeconds) return;
            _refreshing = true;
            _lastRefresh = Time.unscaledTime;
            bool changed = false;
            try
            {
                string myId = MyId;
                var book = Book;
                var cache = new Dictionary<string, SquadPublicRecord>();

                async UniTask<SquadPublicRecord> Read(string id)
                {
                    if (cache.TryGetValue(id, out var r)) return r;
                    r = await SquadDirectory.LoadAsync(id);
                    cache[id] = r;
                    return r;
                }

                // The squads the player joined: read each owner's record.
                foreach (var squad in book.Squads.ToArray())
                {
                    if (squad == null || !squad.IsJoined()) continue;
                    var before = SquadMembership.Copy(squad);
                    var state = SquadMembership.RefreshJoined(squad, myId, await Read(squad.OwnerId));
                    if (!this) return;
                    _joinStates[squad.Id] = state;
                    changed |= !SameSlots(before, squad) || before.Name != squad.Name || before.Domain != squad.Domain;
                }

                // The squads the player made: seat the claims of friends and refresh the friends already seated.
                var ids = new List<string>();
                foreach (var f in FriendIds()) ids.Add(f.id);
                foreach (var squad in book.Squads)
                    for (int i = 1; squad != null && !squad.IsJoined() && i < SavedSquad.SlotCount; i++)
                        if (squad.GetSlot(i).IsPlayer() && !ids.Contains(squad.GetSlot(i).MemberId)) ids.Add(squad.GetSlot(i).MemberId);

                if (book.Squads.Exists(s => s != null && !s.IsJoined() && NeedsReconcile(s)))
                {
                    var records = new List<(string, SquadPublicRecord)>();
                    foreach (var id in ids)
                    {
                        var r = await Read(id);
                        if (!this) return;
                        if (r != null) records.Add((id, r));
                    }
                    foreach (var squad in book.Squads)
                        if (squad != null && !squad.IsJoined())
                            changed |= SquadMembership.ReconcileOwned(squad, myId, records);
                }
            }
            finally
            {
                _refreshing = false;
            }
            if (changed) Saved(publish: false);
            // Publish after every read: it backfills a record never written, and carries what reconciling seated.
            PublishAsync().Forget();
            if (_shown) Draw(snapDomain: true);
        }

        static bool NeedsReconcile(SavedSquad s)
        {
            for (int i = 1; i < SavedSquad.SlotCount; i++)
                if (s.GetSlot(i).Seat != SquadSeat.Ai) return true;
            return false;
        }

        static bool SameSlots(SavedSquad a, SavedSquad b)
        {
            for (int i = 0; i < SavedSquad.SlotCount; i++)
                if (!a.GetSlot(i).Equals(b.GetSlot(i))) return false;
            return true;
        }

        async UniTaskVoid PublishAsync()
        {
            if (!SquadDirectory.IsReady) return;
            await SquadDirectory.PublishAsync(SquadMembership.BuildPublicRecord(Book, MyName));
        }

        void Saved(bool publish)
        {
            _data?.SquadRepo?.MarkDirty();
            if (publish) _publishAt = Time.unscaledTime + publishDelaySeconds;
        }

        IEnumerable<(string id, string name)> FriendIds()
        {
            if (_friends == null || _friends.Friends == null) yield break;
            int n = 0;
            foreach (var f in _friends.Friends)
            {
                if (string.IsNullOrEmpty(f.PlayerId)) continue;
                if (++n > maxFriendsRead) yield break;
                yield return (f.PlayerId, f.DisplayName);
            }
        }

        // ---- drawing ----

        /// <summary>What stands on one of the hangar's pads: the seat's vessel, whether anyone holds the seat (an open seat's
        /// pad stands empty) and whether its pilot is here (an offline mate's hull is powered down).</summary>
        public readonly struct HangarSeat : System.IEquatable<HangarSeat>
        {
            public readonly VesselClassType Vessel;
            public readonly bool Filled;
            public readonly bool Live;

            public HangarSeat(VesselClassType vessel, bool filled, bool live)
            {
                Vessel = vessel;
                Filled = filled;
                Live = live;
            }

            public bool Equals(HangarSeat o) => Vessel == o.Vessel && Filled == o.Filled && Live == o.Live;
            public override bool Equals(object obj) => obj is HangarSeat o && Equals(o);
            public override int GetHashCode() => ((int)Vessel * 4) ^ (Filled ? 1 : 0) ^ (Live ? 2 : 0);
        }

        /// <summary>The shown squad's domain and line-up: the Ark's hangar backdrop plays in the domain (Docs/MENU_REDESIGN_B.md
        /// section 190) and the seats' real vessels stand on its pads (SquadVesselLayer, section 202). Raised when either
        /// changes.</summary>
        public event System.Action HangarChanged;
        /// <summary>False until a squad has been drawn (or while none is shown).</summary>
        public bool HasHangar { get; private set; }
        public Domains HangarDomain { get; private set; } = Domains.Jade;

        readonly HangarSeat[] _hangarSeats = new HangarSeat[SavedSquad.SlotCount];

        /// <summary>Seat <paramref name="seat"/> of the shown squad as its pad shows it (an empty pad out of range).</summary>
        public HangarSeat GetHangarSeat(int seat) =>
            HasHangar && seat >= 0 && seat < _hangarSeats.Length ? _hangarSeats[seat] : default;

        void PublishHangar(SavedSquad squad)
        {
            bool changed = HasHangar != (squad != null) || squad != null && HangarDomain != squad.Domain;
            HasHangar = squad != null;
            if (squad != null) HangarDomain = squad.Domain;
            for (int i = 0; i < _hangarSeats.Length; i++)
            {
                var seat = default(HangarSeat);
                if (squad != null)
                {
                    var slot = squad.GetSlot(i);
                    seat = new HangarSeat(slot.Vessel, !slot.IsOpen(), SeatLive(squad, i, slot));
                }
                if (seat.Equals(_hangarSeats[i])) continue;
                _hangarSeats[i] = seat;
                changed = true;
            }
            if (changed) HangarChanged?.Invoke();
        }

        /// <summary>Whether the seat's pilot is here: the player, an online friend, or an AI (always).</summary>
        bool SeatLive(SavedSquad squad, int seat, SquadSlot slot) =>
            IsMySeat(squad, seat) || (slot.IsPlayer() ? OnlineFriend(slot.MemberId)
                : seat == 0 && squad.IsJoined() ? OnlineFriend(squad.OwnerId) : true);


        readonly List<string> _pilotIds = new();

        /// <summary>
        /// Every vessel needs a pilot: a slot with none (or one another slot flies) takes the next free person of the
        /// crew, in crew order. First the seats whose ids are the player's are adopted: a legacy callsign id becomes the
        /// person it resolves to, so the "another slot flies them" test compares people, not a person against a callsign.
        /// A friend's seated card keeps its owner's ids.
        /// </summary>
        void FillPilots(SavedSquad squad)
        {
            if (squad == null) return;
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                if (!IdsAreMine(squad, seat)) continue;
                var slot = squad.GetSlot(seat);
                if (slot.UsesDefaultPilot() || !PilotLoadoutStore.TryGetPilot(slot.PilotId, out var person) || person.Id == slot.PilotId) continue;
                slot.PilotId = person.Id;
                squad.SetSlot(seat, slot);
            }
            PilotLoadoutStore.GetCrew(_crew);
            _pilotIds.Clear();
            foreach (var p in _crew)
                if (!string.IsNullOrEmpty(p.Id)) _pilotIds.Add(p.Id);
            squad.FillPilots(_pilotIds);
        }

        /// <param name="snapDomain">True when a different squad comes on screen (kept for the callers; the table relights at once).</param>
        void Draw(bool snapDomain = false)
        {
            var squad = Selected();
            if (squad == null)
            {
                _seat = -1;
                _pick = Pick.None;
            }
            PublishHangar(squad);
            var color = squad != null ? DomainColor(squad.Domain) : DomainColor(newSquadDomain);
            foreach (var g in domainLit)
                if (g) g.color = new Color(color.r, color.g, color.b, g.color.a);
            if (emptyGroup) KitVisibility.SetShown(emptyGroup, squad == null && !_joinOpen);
            DrawRail(squad);
            for (int i = 0; i < plates.Length; i++)
            {
                if (!plates[i]) continue;
                if (squad == null || i >= SavedSquad.SlotCount) plates[i].Hide();
                else DrawPlate(plates[i], squad, i, color);
            }
            DrawProjection(squad);
        }

        void DrawRail(SavedSquad squad)
        {
            var book = Book;
            bool has = squad != null;
            if (squadNameText) squadNameText.text = has ? squad.Name : string.Empty;
            if (whoseText) whoseText.text = !has ? string.Empty
                : squad.IsJoined() ? TemplateText.Fill(joinedWhose, "owner", OwnerName(squad)) : ownWhose;
            bool several = book.Squads.Count > 1;
            if (prevSquadButton) prevSquadButton.interactable = several;
            if (nextSquadButton) nextSquadButton.interactable = several;
            bool full = !book.CanAdd();
            if (newButton) newButton.interactable = !full;
            if (joinButton) joinButton.interactable = !full;
            if (squadGroup) KitVisibility.SetShown(squadGroup, has);
            if (!has) return;
            if (removeLabel) removeLabel.text = _confirmUntil >= 0f ? confirmLabel : squad.IsJoined() ? leaveLabel : deleteLabel;
            for (int i = 0; i < domainChips.Length && i < domainOrder.Length; i++)
            {
                if (!domainChips[i]) continue;
                var d = domainOrder[i];
                domainChips[i].Draw(true, DomainName(d), null, null, squad.Domain == d);
                domainChips[i].Tint(DomainColor(d));
            }
            if (missionLock)
            {
                var hub = FindHub(ScreenSwitcher.ModalWindows.MISSION, reportMissing: false);
                missionLock.enabled = hub && hub.TryGetComponent<MenuAvailabilityView>(out var gate) && !gate.IsAvailable;
            }
        }

        string WhoOf(SavedSquad squad, int seat)
        {
            var slot = squad.GetSlot(seat);
            return IsMySeat(squad, seat) ? youLine
                : slot.IsPlayer() ? (string.IsNullOrEmpty(slot.MemberName) ? unknownFriendLine : slot.MemberName)
                : seat == 0 && squad.IsJoined() ? OwnerName(squad)
                : slot.IsOpen() ? openLine : aiLine;
        }

        void DrawPlate(SquadSeatPlate plate, SavedSquad squad, int seat, Color domain)
        {
            var slot = squad.GetSlot(seat);
            var vessel = Vessel(slot.Vessel);
            bool open = slot.IsOpen();
            var pilot = catalog && vessel ? catalog.ResolvePilot(slot, vessel.Keywords) : null;
            string line = open ? (squad.IsJoined() ? openPlateJoinedLine : openPlateLine)
                : TemplateText.Fill(plateLine, "vessel", vessel ? vessel.CardName : string.Empty, "element", ElementName(slot.Element));
            plate.Draw(WhoOf(squad, seat), line, pilot ? pilot.Portrait : null, open, SeatLive(squad, seat, slot), seat == _seat, domain);
        }

        void DrawProjection(SavedSquad squad)
        {
            bool seatOn = squad != null && _seat >= 0 && _seat < SavedSquad.SlotCount;
            bool on = _joinOpen || seatOn;
            if (projectionGroup) KitVisibility.SetShown(projectionGroup, on);
            if (!on) return;
            bool lists = !_joinOpen && _pick != Pick.None, pilotList = lists && _pick == Pick.Pilot;
            if (joinGroup) KitVisibility.SetShown(joinGroup, _joinOpen);
            if (choicesGroup) KitVisibility.SetShown(choicesGroup, lists && !pilotList);
            if (pilotChoicesGroup) KitVisibility.SetShown(pilotChoicesGroup, pilotList);
            if (backButton) backButton.gameObject.SetActive(_joinOpen || lists);
            float height;
            if (_joinOpen)
            {
                if (projectionTitle) projectionTitle.text = joinTitle;
                SetTiles(0, false);
                SetAction(null);
                SetReadOnly(null);
                DrawJoin();
                height = joinHeight;
                MoveProjection(plates.Length > 0 && plates[0] ? plates[0].CentreX : 0f);
            }
            else if (lists)
            {
                SetTiles(0, false);
                SetAction(null);
                SetReadOnly(null);
                if (projectionTitle) projectionTitle.text = TemplateText.Fill(pickTitle, "list", Name(tileNames, (int)_pick - 1));
                DrawChoices(squad, squad.GetSlot(_seat));
                height = pilotList ? pilotChoicesHeight : choicesHeight;
                MoveProjection(PlateX(_seat));
            }
            else
            {
                height = DrawSeat(squad, _seat);
                MoveProjection(PlateX(_seat));
            }
            if (choiceHintText) choiceHintText.gameObject.SetActive(lists);
            if (projectionRoot) projectionRoot.sizeDelta = new Vector2(projectionRoot.sizeDelta.x, height);
        }

        float PlateX(int seat) => seat >= 0 && seat < plates.Length && plates[seat] ? plates[seat].CentreX : 0f;

        void MoveProjection(float x)
        {
            if (projectionRoot) projectionRoot.anchoredPosition = new Vector2(x, projectionRoot.anchoredPosition.y);
        }

        /// <summary>The projected seat: its title, its lines (Who flies it only on a rogue seat an AI or nobody flies), its
        /// action and, on a joined squad, who sets it. Returns the panel's height.</summary>
        float DrawSeat(SavedSquad squad, int seat)
        {
            var slot = squad.GetSlot(seat);
            bool live = SeatLive(squad, seat, slot), open = slot.IsOpen();
            if (projectionTitle)
                projectionTitle.text = TemplateText.Fill(seatTitle, "who", WhoOf(squad, seat), "state",
                    open ? openState : !slot.IsPlayer() && seat > 0 ? aiLine : live ? onlineState : offlineState);
            var vessel = Vessel(slot.Vessel);
            var keywords = vessel ? vessel.Keywords : null;
            var pilot = catalog && vessel ? catalog.ResolvePilot(slot, keywords) : null;
            var artifact = catalog && vessel ? catalog.ResolveArtifact(slot, keywords) : null;
            bool ids = IdsAreMine(squad, seat);
            string pilotName = !pilot ? basePilotLine
                : ids && PilotLoadoutStore.TryGetPilot(slot.PilotId, out var person) && person.CallsignOrTemplate() == pilot.Id
                    ? person.DisplayName() : pilot.DisplayName;
            string artifactName = !artifact ? noArtifactLine
                : ids && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var held) && held.Kind() == artifact.Id
                    ? held.Name : artifact.DisplayName;
            bool rogue = seat > 0 && !slot.IsPlayer();
            string[] values =
            {
                open ? openModeLabel : aiModeLabel,
                vessel ? vessel.CardName : string.Empty,
                ElementName(slot.Element),
                pilotName,
                artifactName,
            };
            bool editable = Editable(squad, seat);
            int row = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                bool shown = i < values.Length && (i > 0 || rogue);
                if (tiles[i])
                {
                    tiles[i].Draw(shown, shown ? values[i] : null, Name(tileNames, i), null, false);
                    if (shown && tiles[i].transform is RectTransform rt)
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(projectionHead + row * tileStep));
                }
                if (i < tileChevrons.Length && tileChevrons[i]) tileChevrons[i].enabled = shown && editable;
                if (shown) row++;
            }
            float y = projectionHead + row * tileStep + 8f;
            int action = ActionOf(squad, seat);
            SetAction(action switch { 1 => changeVesselWord, 2 => whoFliesWord, 3 => openSeatWord, _ => null }, y);
            if (action != 0) y += actionHeight + 10f;
            string readOnly = null;
            if (squad.IsJoined())
            {
                var state = _joinStates.TryGetValue(squad.Id, out var s) ? s : SquadJoinState.Unknown;
                readOnly = TemplateText.Fill(state switch
                {
                    SquadJoinState.Seated => seatedLine,
                    SquadJoinState.Pending => pendingLine,
                    SquadJoinState.SeatLost => seatLostLine,
                    SquadJoinState.Disbanded => disbandedLine,
                    _ => offlineLine,
                }, "owner", OwnerName(squad));
            }
            SetReadOnly(readOnly, y);
            if (!string.IsNullOrEmpty(readOnly)) y += readOnlyHeight;
            return y + projectionPad;
        }

        void SetTiles(int from, bool shown)
        {
            for (int i = from; i < tiles.Length; i++)
            {
                if (tiles[i]) tiles[i].Draw(shown, null, null, null, false);
                if (i < tileChevrons.Length && tileChevrons[i]) tileChevrons[i].enabled = shown;
            }
        }

        void SetAction(string word, float y = 0f)
        {
            if (!actionButton) return;
            bool shown = !string.IsNullOrEmpty(word);
            actionButton.gameObject.SetActive(shown);
            if (!shown) return;
            if (actionLabel) actionLabel.text = word;
            if (actionButton.transform is RectTransform rt) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
        }

        void SetReadOnly(string line, float y = 0f)
        {
            if (!readOnlyText) return;
            bool shown = !string.IsNullOrEmpty(line);
            readOnlyText.gameObject.SetActive(shown);
            if (!shown) return;
            readOnlyText.text = line;
            if (readOnlyText.transform is RectTransform rt) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
        }

        void DrawChoices(SavedSquad squad, SquadSlot slot)
        {
            if (choiceHintText) choiceHintText.text = Name(choiceHints, (int)_pick - 1);
            int count = ChoiceCount(slot), per = PerPage, start = _page * per;
            bool paged = count > per;
            if (choicePrevButton) choicePrevButton.gameObject.SetActive(paged);
            if (choiceNextButton) choiceNextButton.gameObject.SetActive(paged);
            if (_pick == Pick.Pilot)
            {
                // The seat may hold a legacy callsign id: the chip lit is the person it resolves to.
                string seatedId = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var seated) ? seated.Id : null;
                for (int i = 0; i < pilotChips.Length; i++)
                {
                    if (!pilotChips[i]) continue;
                    int index = start + i;
                    if (index >= _pilots.Count)
                    {
                        pilotChips[i].Draw(false, null, null, null, false);
                        continue;
                    }
                    var person = _pilots[index];
                    var p = _pilotCallsigns[index];
                    string word = CallsignWord(p);
                    pilotChips[i].Draw(true, TemplateText.Fill(pilotChipWord, "name", person.DisplayName(), "callsign", word),
                        TemplateText.Fill(pilotDetail, "sight", SightOf(p), "callsign", word, "species", person.SpeciesCombo()),
                        p.Portrait, person.Id == seatedId);
                }
                return;
            }
            string carriedId = _pick == Pick.Artifact && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var carried) ? carried.Id : null;
            for (int i = 0; i < choiceChips.Length; i++)
            {
                if (!choiceChips[i]) continue;
                int index = start + i;
                string word = null;
                bool picked = false;
                if (index < count)
                {
                    switch (_pick)
                    {
                        case Pick.Who:
                            word = index == 0 ? aiModeLabel : openModeLabel;
                            picked = index == 0 ? slot.Seat == SquadSeat.Ai : slot.IsOpen();
                            break;
                        case Pick.Vessel:
                            word = _offered[index].CardName;
                            picked = _offered[index].Class == slot.Vessel;
                            break;
                        case Pick.Element:
                            word = ElementName(ElementCycle[index]);
                            picked = slot.Element == ElementCycle[index];
                            break;
                        case Pick.Artifact:
                            word = index == 0 ? noArtifactChip : ShortName(_artifacts[index - 1].Name, artifactChipChars);
                            picked = index == 0 ? !slot.CarriesArtifact() : _artifacts[index - 1].Id == carriedId;
                            break;
                    }
                }
                choiceChips[i].Draw(word != null, word, null, null, picked);
            }
        }

        void DrawJoin()
        {
            if (!_joinOpen) return;
            for (int i = 0; i < joinRows.Length; i++)
            {
                if (!joinRows[i]) continue;
                if (i >= _offers.Count)
                {
                    joinRows[i].Draw(false, null, null, false);
                    continue;
                }
                var (_, owner, squad) = _offers[i];
                int seat = SquadMembership.OpenSeat(squad);
                joinRows[i].Draw(true, TemplateText.Fill(joinRowTitle, "squad", squad.Name, "owner", owner),
                    TemplateText.Fill(joinRowLine, "seat", SeatName(seat)), false);
                joinRows[i].DrawDomain(DomainName(squad.Domain), DomainColor(squad.Domain));
            }
            if (joinStatusText)
                joinStatusText.SetText(!SquadDirectory.IsReady ? joinOfflineLine
                    : _loadingOffers && _offers.Count == 0 ? joinLookingLine
                    : _offers.Count == 0 ? joinNoneLine : string.Empty);
        }

        static string Name(string[] names, int i) => i >= 0 && i < names.Length ? names[i] : string.Empty;

        // ---- helpers ----

        SavedSquad Selected()
        {
            var book = Book;
            return _selected >= 0 && _selected < book.Squads.Count ? book.Squads[_selected] : null;
        }

        static bool IsMySeat(SavedSquad squad, int seat) => squad.IsJoined() ? seat == squad.MySeat : seat == 0;

        /// <summary>The player's own card, and the AI or open cards of a squad they made.</summary>
        static bool CanEdit(SavedSquad squad, int seat) =>
            squad != null && (IsMySeat(squad, seat) || (!squad.IsJoined() && seat > 0 && !squad.GetSlot(seat).IsPlayer()));

        /// <summary>
        /// True when the card's pilot and artifact ids are this player's (their crew and hoard): exactly the cards they may
        /// edit. A friend's card in a squad the player made, and every card of a joined squad but the player's own, hold
        /// their owner's ids, which are drawn through the catalog only.
        /// </summary>
        static bool IdsAreMine(SavedSquad squad, int seat) => CanEdit(squad, seat);

        static int OpenSeats(SavedSquad s)
        {
            int open = 0;
            for (int i = 1; i < SavedSquad.SlotCount; i++) if (s.GetSlot(i).IsOpen()) open++;
            return open;
        }

        string OwnerName(SavedSquad s) => string.IsNullOrEmpty(s.OwnerName) ? unknownFriendLine : s.OwnerName;

        string SeatName(int seat) => seat >= 0 && seat < seatNames.Length ? seatNames[seat] : string.Empty;

        string ElementName(Element e)
        {
            int i = (int)e;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : string.Empty;
        }

        string DomainName(Domains d)
        {
            int i = (int)d;
            return i >= 0 && i < domainNames.Length ? domainNames[i] : string.Empty;
        }

        // The player's colour set when the theme is up (Docs/MENU_REDESIGN_B.md section 123).
        Color DomainColor(Domains d)
        {
            var palette = ThemeManager.LiveOr(colors);
            return palette ? palette.GetDomainSignalColor(d) : Color.white;
        }

        SO_Vessel Vessel(VesselClassType cls) =>
            vessels && vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        void Offered()
        {
            _offered.Clear();
            if (!vessels || vessels.VesselList == null) return;
            foreach (var v in vessels.VesselList)
                if (v && !v.IsLocked) _offered.Add(v);
        }

        /// <summary>
        /// The crew's people a <paramref name="cls"/> hull can fly, in crew order, with their callsign assets (the rule
        /// <see cref="PilotArtifactCatalogSO.GetPilotsFor(SO_Vessel, List{PilotSO})"/> applies to callsigns). A crew holds
        /// one person per callsign at most, so this is never longer than the callsign list was.
        /// </summary>
        void PilotsFor(VesselClassType cls)
        {
            _pilots.Clear();
            _pilotCallsigns.Clear();
            var vessel = Vessel(cls);
            if (!catalog || !vessel) return;
            PilotLoadoutStore.GetCrew(_crew);
            foreach (var person in _crew)
            {
                if (!catalog.TryGetPilot(person.Id, out var callsign) || !callsign) continue;
                if (callsign.Requirement == null || !callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords)) continue;
                _pilots.Add(person);
                _pilotCallsigns.Add(callsign);
            }
        }

        /// <summary>The offered person <paramref name="slotPilotId"/> names (a crew id or a legacy callsign id), or -1.</summary>
        int IndexOfPilot(string slotPilotId)
        {
            if (!PilotLoadoutStore.TryGetPilot(slotPilotId, out var person)) return -1;
            for (int i = 0; i < _pilots.Count; i++)
                if (_pilots[i].Id == person.Id) return i;
            return -1;
        }

        static string SightOf(PilotSO p) => PilotSight.Set ? PilotSight.Set.For(p.Vision)?.meaning ?? string.Empty : string.Empty;

        static string CallsignWord(PilotSO p) => PilotSight.Set ? PilotSight.Set.For(p.Vision)?.word ?? string.Empty : string.Empty;

        /// <summary>
        /// The hoard's artifacts a <paramref name="cls"/> hull can carry (their kind's requirement), in the order they arrived.
        /// Every card the picker edits is the player's (their own, or an AI or open card of a squad they made), and each
        /// artifact is one of a kind, so only what the hoard holds is offered - to an AI wingmate too
        /// (<see cref="AccountInventory.AdoptSquads"/> drops an artifact no longer held from any card of a made squad).
        /// </summary>
        void ArtifactsFor(VesselClassType cls)
        {
            _artifacts.Clear();
            var vessel = Vessel(cls);
            if (!catalog || !vessel) return;
            PilotLoadoutStore.GetHoard(_hoard);
            foreach (var artifact in _hoard)
            {
                if (!catalog.TryGetArtifact(artifact.Id, out var kind) || !kind) continue;
                if (kind.Requirement == null || !kind.Requirement.IsMetBy(vessel.Class, vessel.Keywords)) continue;
                _artifacts.Add(artifact);
            }
        }

        /// <summary><paramref name="name"/> cut at a word to at most <paramref name="max"/> characters (a narrow chip):
        /// "Hollow Javelin of Tarn" becomes "Hollow Javelin".</summary>
        static string ShortName(string name, int max)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= max) return name ?? string.Empty;
            int cut = name.LastIndexOf(' ', max);
            return cut > 0 ? name.Substring(0, cut) : name.Substring(0, max);
        }

        static Element ElementOf(SO_Vessel v, Element fallback) =>
            v && v.PrimaryElement != Element.None && v.PrimaryElement != Element.Omni ? v.PrimaryElement : fallback;

        /// <summary>The player's card for a new or joined squad: the hull they last picked and its Hangar pilot and artifact
        /// (a crew person's id and a hoard artifact's id, Docs/Pilots/INVENTORY.md).</summary>
        SquadSlot MyDefaultLoadout()
        {
            Offered();
            SO_Vessel vessel = null;
            if (_gameData && _gameData.selectedVesselClass)
                vessel = _offered.Find(v => v.Class == _gameData.selectedVesselClass.Value);
            if (!vessel && _offered.Count > 0) vessel = _offered[0];
            var cls = vessel ? vessel.Class : VesselClassType.Squirrel;
            PilotLoadoutStore.GetSlotted(cls, _slotted);
            return new SquadSlot(cls, ElementOf(vessel, Element.Charge), PilotLoadoutStore.GetPilot(cls),
                _slotted.Count > 0 ? _slotted[0] : null);
        }

        static void Refuse(string line)
        {
            if (!string.IsNullOrWhiteSpace(line)) ToastNotificationAPI.Show(line, ToastKind.Refusal);
        }
    }
}
