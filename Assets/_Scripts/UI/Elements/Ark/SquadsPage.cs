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
    /// The Ark's SQUADS tab as THE DECK (Docs/MENU_REDESIGN_B.md section 93; the Rev 31 boards, Shombith 2026-10-27: "go
    /// with C, the deck"). A squad is three squad mates, each a card: the seat, who flies it, the hull, and the four picks -
    /// vessel, element, pilot and artifact - with the pilot stepping out of the card.
    ///
    /// <list type="bullet">
    /// <item><b>The list</b> (left): the player's squads, made and joined together, at most <see cref="SquadBook.MaxSquads"/>.
    /// Pressing one shows it and makes it the active squad.</item>
    /// <item><b>Make squad</b>: the cards step back (<see cref="makeScale"/>) and the picker opens under them. Press a card,
    /// then its vessel, pilot, element and artifact; a rogue card is an AI wingmate or held open for a friend. CREATE SQUAD
    /// adds it to the book. EDIT reopens a made squad the same way (SAVE SQUAD). Nothing is saved before that press.</item>
    /// <item><b>Take into Mission / Arena</b> shows only for a made squad: it makes the squad active, puts the player in their
    /// card's vessel and pilot, invites the online squad mates to the party and presses the Home hub's entry for that window
    /// (<see cref="MenuHubButton.Press"/>), so Mission refuses exactly as it does on Home. No Arcade (2026-10-27).</item>
    /// <item><b>Domain</b>: Jade, Ruby or Gold, the squad's team colour. A change plays the nanobot swap on each card
    /// (<see cref="SquadNanobotSwap"/>). Only the squad's leader changes it.</item>
    /// <item><b>Join a friend's squad</b> lists the friends' squads with a seat open.</item>
    /// </list>
    ///
    /// <para>The squads are the player's <c>SQUAD_DATA</c> record (<see cref="UGSDataService.SquadRepo"/>). Joining rides each
    /// player's PUBLIC squad record (<see cref="SquadDirectory"/>); <see cref="SquadMembership"/> holds the rules.</para>
    /// </summary>
    public class SquadsPage : MonoBehaviour, IArkTabPage
    {
        enum Mode { None = 0, View = 1, Make = 2 }

        [Header("Content")]
        [Tooltip("The vessels a squad card can fly (the Hangar's list); locked ones are skipped.")]
        [SerializeField] private SO_VesselList vessels;
        [Tooltip("Pilots and artifacts. Empty loads the one in Resources.")]
        [SerializeField] private PilotArtifactCatalogSO catalog;
        [Tooltip("The hull renders the cards and the vessel tiles show (the Hangar's card art); the vessel's icon without one.")]
        [SerializeField] private HangarCardArtSO cardArt;
        [Tooltip("The domains' colours (SO_ColorSet.GetDomainSignalColor).")]
        [SerializeField] private SO_ColorSet colors;

        [Header("List")]
        [SerializeField] private SquadRowView[] squadRows = new SquadRowView[SquadBook.MaxSquads];
        [SerializeField] private KitText countText;
        [SerializeField] private KitButton newButton;
        [SerializeField] private KitButton joinButton;

        [Header("Header")]
        [Tooltip("The squad's kicker, name and line, and the domain chips. Hidden while the player is in no squad.")]
        [SerializeField] private CanvasGroup headerGroup;
        [SerializeField] private TMP_Text kickerText;
        [SerializeField] private TMP_Text squadNameText;
        [SerializeField] private TMP_Text squadLineText;
        [Tooltip("One chip per domain, in the order of Domain Order.")]
        [SerializeField] private SquadPickChip[] domainChips = new SquadPickChip[3];
        [SerializeField] private Domains[] domainOrder = { Domains.Jade, Domains.Ruby, Domains.Gold };

        [Header("Deck")]
        [Tooltip("The three cards' parent: scaled down while Make squad's picker is open.")]
        [SerializeField] private RectTransform deckRoot;
        [SerializeField, Range(0.5f, 1f)] private float makeScale = 0.8f;
        [Tooltip("Leader, Rogue One, Rogue Two.")]
        [SerializeField] private SquadMateCard[] mateCards = new SquadMateCard[SavedSquad.SlotCount];
        [Tooltip("Shown while the player is in no squad.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private KitButton emptyMakeButton;
        [Tooltip("Seconds between one card's nanobot swap and the next's.")]
        [SerializeField, Min(0f)] private float swapStagger = 0.14f;

        [Header("Picker (Make squad)")]
        [SerializeField] private CanvasGroup pickerGroup;
        [SerializeField] private SquadPickChip[] vesselTiles = new SquadPickChip[7];
        [SerializeField] private KitButton vesselPrevButton;
        [SerializeField] private KitButton vesselNextButton;
        [SerializeField] private SquadPickChip[] pilotChips = new SquadPickChip[4];
        [Tooltip("AI wingmate / Open for a friend; shown for a rogue card.")]
        [SerializeField] private CanvasGroup seatModeGroup;
        [SerializeField] private SquadPickChip[] seatModeChips = new SquadPickChip[2];
        [SerializeField] private SquadPickChip[] elementChips = new SquadPickChip[4];
        [SerializeField] private SquadPickChip[] artifactChips = new SquadPickChip[8];

        [Header("Bars")]
        [SerializeField] private CanvasGroup makeBarGroup;
        [SerializeField] private KitButton cancelButton;
        [SerializeField] private KitButton createButton;
        [SerializeField] private TMP_Text createLabel;
        [SerializeField] private CanvasGroup viewBarGroup;
        [SerializeField] private KitButton removeButton;
        [SerializeField] private TMP_Text removeLabel;
        [SerializeField] private KitButton editButton;
        [SerializeField] private KitButton missionButton;
        [Tooltip("The padlock on Mission while Home's Mission entry is not open.")]
        [SerializeField] private Graphic missionLock;
        [SerializeField] private KitButton arenaButton;

        [Header("Join")]
        [SerializeField] private CanvasGroup joinGroup;
        [SerializeField] private SquadRowView[] joinRows = { };
        [SerializeField] private KitText joinStatusText;
        [SerializeField] private KitButton joinCloseButton;

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
        [Tooltip("{count} squads of {max}.")]
        [SerializeField] private string countLine = "{count} of {max}";
        [SerializeField] private string madeLine = "Yours";
        [Tooltip("{open} is how many seats are open.")]
        [SerializeField] private string madeOpenLine = "Yours - {open} seat open";
        [SerializeField] private string madeFullLine = "Yours - every seat filled";
        [Tooltip("A joined squad's row; {owner} is filled in.")]
        [SerializeField] private string joinedLine = "{owner}'s squad - joined";
        [SerializeField] private string makingLine = "Making...";
        [SerializeField] private string ownKicker = "Your squad - you lead";
        [Tooltip("{owner} and {seat}.")]
        [SerializeField] private string joinedKicker = "{owner}'s squad - you fly {seat}";
        [SerializeField] private string makeKicker = "Make squad";
        [SerializeField] private string editKicker = "Edit squad";
        [Tooltip("{open} is how many seats are open.")]
        [SerializeField] private string ownOpenDetail = "{open} seat open for a friend.";
        [SerializeField] private string ownFullDetail = "Every seat is filled.";
        [SerializeField] private string makeDetail = "Pick a card to edit it.";
        [Tooltip("For a joined squad, by its state; {owner} is filled in.")]
        [SerializeField] private string seatedLine = "Only {owner} changes the squad.";
        [SerializeField] private string pendingLine = "Joined. {owner} seats you when they next open the Ark.";
        [SerializeField] private string seatLostLine = "{owner} gave your seat to someone else. Leave the squad, or ask them to open it again.";
        [SerializeField] private string disbandedLine = "{owner} deleted this squad.";
        [SerializeField] private string offlineLine = "Offline - showing the last copy of {owner}'s squad.";
        [SerializeField] private string[] seatNames = { "Leader", "Rogue One", "Rogue Two" };
        [Tooltip("The card's number; {n} is its seat, 1 to 3.")]
        [SerializeField] private string numberLine = "0{n} / 03";
        [SerializeField] private string youLine = "You";
        [SerializeField] private string aiLine = "AI wingmate";
        [SerializeField] private string openLine = "Open seat";
        [SerializeField] private string unknownFriendLine = "A friend";
        [SerializeField] private string aiModeLabel = "AI wingmate";
        [SerializeField] private string openModeLabel = "Open for a friend";
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "Charge", "Mass", "Space", "Time", "Omni" };
        [Tooltip("Domain names, by Domains value (-, Jade, Ruby, Blue, Gold).")]
        [SerializeField] private string[] domainNames = { "", "Jade", "Ruby", "Blue", "Gold" };
        [SerializeField] private string basePilotLine = "Base kit";
        [SerializeField] private string basePilotDetail = "The vessel's own kit";
        [Tooltip("A pilot's line in the picker; {ability} is the ability it rewrites.")]
        [SerializeField] private string pilotDetail = "Rewrites {ability}";
        [SerializeField] private string noArtifactLine = "No artifact";
        [SerializeField] private string noArtifactChip = "None";
        [SerializeField] private string createWord = "Create squad";
        [SerializeField] private string saveWord = "Save squad";
        [SerializeField] private string deleteLabel = "Delete";
        [SerializeField] private string leaveLabel = "Leave";
        [SerializeField] private string confirmLabel = "Press again";
        [Tooltip("A new squad's name; {n} is its number. Empty names it \"Squad N\".")]
        [SerializeField] private string newSquadName = "";
        [SerializeField] private string fullLine = "You are in 4 squads. Leave or delete one first.";
        [Tooltip("Toast after Create squad; {squad} is filled in.")]
        [SerializeField] private string createdToast = "{squad} made. Take it into a mission or the arena.";
        [Tooltip("A domain press on a joined squad; {owner} is filled in.")]
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
        readonly List<PilotSO> _pilots = new();
        readonly List<ArtifactSO> _artifacts = new();
        readonly List<string> _slotted = new();
        readonly Dictionary<ScreenSwitcher.ModalWindows, MenuHubButton> _hubs = new();

        Mode _mode = Mode.None;
        SavedSquad _draft;
        int _editIndex = -1, _selected, _seat, _vesselPage;
        bool _shown, _refreshing, _joinOpen, _loadingOffers;
        float _lastRefresh = float.NegativeInfinity, _publishAt = -1f, _confirmUntil = -1f;

        SquadBook Book => _data != null && _data.SquadRepo?.Data?.Book != null ? _data.SquadRepo.Data.Book : _offlineBook;

        string MyId => _host && !string.IsNullOrEmpty(_host.LocalPlayerId) ? _host.LocalPlayerId : SquadDirectory.LocalPlayerId;
        string MyName => _host && !string.IsNullOrEmpty(_host.LocalDisplayName) ? _host.LocalDisplayName : youLine;

        void Awake()
        {
            if (!catalog) catalog = PilotArtifactCatalogSO.Load();
            for (int i = 0; i < squadRows.Length; i++)
            {
                int index = i;
                if (squadRows[i] && squadRows[i].Button) squadRows[i].Button.onClick.AddListener(() => Select(index));
            }
            for (int i = 0; i < joinRows.Length; i++)
            {
                int index = i;
                if (joinRows[i] && joinRows[i].Button) joinRows[i].Button.onClick.AddListener(() => JoinOffer(index));
            }
            for (int i = 0; i < mateCards.Length; i++)
            {
                int seat = i;
                if (mateCards[i]) mateCards[i].Pressed += () => PickSeat(seat);
            }
            Hook(domainChips, PickDomain);
            Hook(vesselTiles, PickVessel);
            Hook(pilotChips, PickPilot);
            Hook(seatModeChips, PickSeatMode);
            Hook(elementChips, PickElement);
            Hook(artifactChips, PickArtifact);
            Listen(newButton, Make);
            Listen(emptyMakeButton, Make);
            Listen(joinButton, OpenJoin);
            Listen(joinCloseButton, CloseJoin);
            Listen(vesselPrevButton, () => PageVessels(-1));
            Listen(vesselNextButton, () => PageVessels(1));
            Listen(cancelButton, Cancel);
            Listen(createButton, Create);
            Listen(removeButton, Remove);
            Listen(editButton, Edit);
            Listen(missionButton, () => Play(ScreenSwitcher.ModalWindows.MISSION));
            Listen(arenaButton, () => Play(ScreenSwitcher.ModalWindows.ARENA));
            if (joinGroup) KitVisibility.SetShown(joinGroup, false);
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
                CloseJoin();
                return;
            }
            if (_mode != Mode.Make)
            {
                _selected = Mathf.Clamp(Book.ActiveIndex, 0, Mathf.Max(0, Book.Squads.Count - 1));
                _mode = Book.Squads.Count > 0 ? Mode.View : Mode.None;
            }
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
                DrawBars();
            }
        }

        // ---- the list ----

        void Select(int index)
        {
            if (index < 0 || index >= Book.Squads.Count) return;
            _mode = Mode.View;
            _draft = null;
            _selected = index;
            _confirmUntil = -1f;
            CloseJoin();
            if (Book.TrySetActive(index)) Saved(publish: false);
            Draw(snapDomain: true);
        }

        // ---- make / edit ----

        void Make()
        {
            if (!Book.CanAdd())
            {
                Refuse(fullLine);
                return;
            }
            CloseJoin();
            var mine = MyDefaultLoadout();
            Offered();
            SquadSlot Rogue(int n, SquadSeat seat)
            {
                var v = _offered.Count > 0 ? _offered[(Mathf.Max(0, _offered.IndexOf(Vessel(mine.Vessel))) + n) % _offered.Count] : null;
                return new SquadSlot(v ? v.Class : mine.Vessel, ElementOf(v, ElementCycle[n % ElementCycle.Length])).WithSeat(seat);
            }
            string name = string.IsNullOrEmpty(newSquadName) ? null
                : TemplateText.Fill(newSquadName, "n", (Book.Squads.Count + 1).ToString());
            _draft = new SavedSquad(SquadBook.SanitizeName(name, Book.Squads.Count), mine.WithMember(MyId, MyName),
                Rogue(1, SquadSeat.Open), Rogue(2, SquadSeat.Ai)) { Domain = newSquadDomain };
            _editIndex = -1;
            EnterMake();
        }

        void Edit()
        {
            var squad = Selected();
            if (squad == null) return;
            if (squad.IsJoined())
            {
                Refuse(TemplateText.Fill(notLeaderLine, "owner", OwnerName(squad)));
                return;
            }
            _draft = SquadMembership.Copy(squad);
            _editIndex = _selected;
            EnterMake();
        }

        void EnterMake()
        {
            _mode = Mode.Make;
            _seat = 0;
            ShowVesselPageOf(_draft.Leader.Vessel);
            Draw(snapDomain: true);
        }

        void Cancel()
        {
            _draft = null;
            _mode = Book.Squads.Count > 0 ? Mode.View : Mode.None;
            Draw(snapDomain: true);
        }

        void Create()
        {
            if (_draft == null) return;
            var book = Book;
            if (_editIndex >= 0 && _editIndex < book.Squads.Count)
            {
                book.Squads[_editIndex] = _draft;
                _selected = _editIndex;
            }
            else
            {
                if (!book.CanAdd())
                {
                    Refuse(fullLine);
                    return;
                }
                book.Add(_draft);
                _selected = book.Squads.Count - 1;
                if (!string.IsNullOrWhiteSpace(createdToast))
                    ToastNotificationAPI.Show(TemplateText.Fill(createdToast, "squad", _draft.Name), ToastKind.Success);
            }
            book.TrySetActive(_selected);
            _draft = null;
            _mode = Mode.View;
            Saved(publish: true);
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
                DrawBars();
                return;
            }
            _confirmUntil = -1f;
            _joinStates.Remove(squad.Id);
            Book.RemoveAt(_selected);
            _selected = Mathf.Clamp(Book.ActiveIndex, 0, Mathf.Max(0, Book.Squads.Count - 1));
            _mode = Book.Squads.Count > 0 ? Mode.View : Mode.None;
            Saved(publish: true);
            Draw(snapDomain: true);
        }

        void PickSeat(int seat)
        {
            if (_mode != Mode.Make || !CanEdit(_draft, seat)) return;
            _seat = seat;
            ShowVesselPageOf(_draft.GetSlot(seat).Vessel);
            Draw();
        }

        void EditSlot(System.Func<SquadSlot, SquadSlot> change)
        {
            if (_mode != Mode.Make || _draft == null) return;
            _draft.SetSlot(_seat, change(_draft.GetSlot(_seat)));
            Draw();
        }

        void PickVessel(int tile)
        {
            int index = _vesselPage * vesselTiles.Length + tile;
            if (index < 0 || index >= _offered.Count) return;
            var v = _offered[index];
            bool mine = IsMySeat(_draft, _seat);
            EditSlot(s =>
            {
                var moved = s.WithVessel(v.Class);
                // The player's own card starts on the pilot they keep in that vessel's Hangar seat.
                if (mine) moved.PilotId = PilotLoadoutStore.GetPilot(v.Class);
                return moved;
            });
        }

        void PageVessels(int step)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(_offered.Count / (float)Mathf.Max(1, vesselTiles.Length)));
            _vesselPage = (_vesselPage + step + pages) % pages;
            DrawPicker();
        }

        void ShowVesselPageOf(VesselClassType cls)
        {
            Offered();
            int at = _offered.FindIndex(v => v.Class == cls);
            _vesselPage = at < 0 ? 0 : at / Mathf.Max(1, vesselTiles.Length);
        }

        void PickPilot(int chip)
        {
            PilotsFor(_draft.GetSlot(_seat).Vessel);
            // Chip 0 is the vessel's base kit (an empty id), then the roster.
            EditSlot(s =>
            {
                s.PilotId = chip == 0 || chip > _pilots.Count ? null : _pilots[chip - 1].Id;
                return s;
            });
        }

        void PickElement(int chip)
        {
            if (chip < 0 || chip >= ElementCycle.Length) return;
            EditSlot(s =>
            {
                s.Element = ElementCycle[chip];
                return s;
            });
        }

        void PickArtifact(int chip)
        {
            var slot = _draft.GetSlot(_seat);
            ArtifactsFor(slot.Vessel, IsMySeat(_draft, _seat));
            EditSlot(s =>
            {
                s.ArtifactId = chip == 0 || chip > _artifacts.Count ? null : _artifacts[chip - 1].Id;
                return s;
            });
        }

        void PickSeatMode(int chip)
        {
            if (_seat == 0) return;
            EditSlot(s => s.WithSeat(chip == 0 ? SquadSeat.Ai : SquadSeat.Open));
        }

        void PickDomain(int chip)
        {
            if (chip < 0 || chip >= domainOrder.Length) return;
            var domain = domainOrder[chip];
            var squad = Shown();
            if (squad == null || squad.Domain == domain) return;
            if (squad.IsJoined())
            {
                Refuse(TemplateText.Fill(notLeaderLine, "owner", OwnerName(squad)));
                return;
            }
            squad.Domain = domain;
            if (_mode == Mode.View) Saved(publish: true);
            var color = DomainColor(domain);
            for (int i = 0; i < mateCards.Length; i++)
                if (mateCards[i] && mateCards[i].Nanobots) mateCards[i].Nanobots.Play(color, i * swapStagger);
            Draw();
        }

        // ---- join ----

        void OpenJoin()
        {
            if (!Book.CanAdd())
            {
                Refuse(fullLine);
                return;
            }
            _joinOpen = true;
            if (joinGroup) KitVisibility.SetShown(joinGroup, true);
            LoadOffersAsync().Forget();
            DrawJoin();
        }

        void CloseJoin()
        {
            _joinOpen = false;
            if (joinGroup) KitVisibility.SetShown(joinGroup, false);
        }

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
            _mode = Mode.View;
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
            if (_mode != Mode.View || squad == null) return;
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
            if (!mine.UsesDefaultPilot()) PilotLoadoutStore.SetPilot(mine.Vessel, mine.PilotId);
            InviteSquadMates(squad);
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
            if (_shown && _mode != Mode.Make) Draw(snapDomain: true);
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

        /// <param name="snapDomain">True when a different squad comes on screen: its hulls take its colour at once, with no swap.</param>
        void Draw(bool snapDomain = false)
        {
            DrawList();
            var squad = Shown();
            bool has = squad != null;
            if (headerGroup) KitVisibility.SetShown(headerGroup, has);
            if (emptyGroup) KitVisibility.SetShown(emptyGroup, !has);
            if (deckRoot)
            {
                deckRoot.gameObject.SetActive(has);
                float k = _mode == Mode.Make ? makeScale : 1f;
                deckRoot.localScale = new Vector3(k, k, 1f);
            }
            if (has)
            {
                DrawHeader(squad);
                var color = DomainColor(squad.Domain);
                for (int i = 0; i < mateCards.Length && i < SavedSquad.SlotCount; i++)
                {
                    if (!mateCards[i]) continue;
                    DrawMate(mateCards[i], squad, i, color);
                    if (snapDomain && mateCards[i].Nanobots) mateCards[i].Nanobots.SetColor(color);
                }
            }
            if (pickerGroup) KitVisibility.SetShown(pickerGroup, _mode == Mode.Make);
            if (_mode == Mode.Make) DrawPicker();
            DrawBars();
            if (_joinOpen) DrawJoin();
        }

        void DrawList()
        {
            var book = Book;
            int count = book.Squads.Count;
            if (countText)
                countText.SetText(TemplateText.Fill(countLine, "count", count.ToString(), "max", SquadBook.MaxSquads.ToString()));
            bool making = _mode == Mode.Make;
            for (int i = 0; i < squadRows.Length; i++)
            {
                var row = squadRows[i];
                if (!row) continue;
                // A new squad being made stands in the first free row until it is created.
                if (making && _editIndex < 0 && i == count)
                {
                    row.Draw(true, _draft.Name, makingLine, true);
                    row.DrawDomain(DomainName(_draft.Domain), DomainColor(_draft.Domain));
                    continue;
                }
                var s = i < count ? book.Squads[i] : null;
                bool active = s != null && !making && i == _selected;
                row.Draw(s != null, s?.Name, s == null ? string.Empty : RowLine(s), active);
                if (s != null) row.DrawDomain(DomainName(s.Domain), DomainColor(s.Domain));
            }
            bool full = !book.CanAdd();
            if (newButton) newButton.interactable = !full && !making;
            if (joinButton) joinButton.interactable = !full && !making;
        }

        string RowLine(SavedSquad s)
        {
            if (s.IsJoined()) return TemplateText.Fill(joinedLine, "owner", OwnerName(s));
            int open = OpenSeats(s);
            return open > 0 ? TemplateText.Fill(madeOpenLine, "open", open.ToString()) : madeFullLine;
        }

        void DrawHeader(SavedSquad squad)
        {
            if (squadNameText) squadNameText.text = squad.Name;
            string kicker, line;
            if (_mode == Mode.Make)
            {
                kicker = _editIndex >= 0 ? editKicker : makeKicker;
                line = makeDetail;
            }
            else if (squad.IsJoined())
            {
                kicker = TemplateText.Fill(joinedKicker, "owner", OwnerName(squad), "seat", SeatName(squad.MySeat));
                var state = _joinStates.TryGetValue(squad.Id, out var s) ? s : SquadJoinState.Unknown;
                line = TemplateText.Fill(state switch
                {
                    SquadJoinState.Seated => seatedLine,
                    SquadJoinState.Pending => pendingLine,
                    SquadJoinState.SeatLost => seatLostLine,
                    SquadJoinState.Disbanded => disbandedLine,
                    _ => offlineLine,
                }, "owner", OwnerName(squad));
            }
            else
            {
                kicker = ownKicker;
                int open = OpenSeats(squad);
                line = open > 0 ? TemplateText.Fill(ownOpenDetail, "open", open.ToString()) : ownFullDetail;
            }
            if (kickerText) kickerText.text = kicker;
            if (squadLineText) squadLineText.text = line;

            for (int i = 0; i < domainChips.Length && i < domainOrder.Length; i++)
            {
                if (!domainChips[i]) continue;
                var d = domainOrder[i];
                domainChips[i].Draw(true, DomainName(d), null, null, squad.Domain == d);
                domainChips[i].Tint(DomainColor(d));
            }
        }

        void DrawMate(SquadMateCard card, SavedSquad squad, int seat, Color domain)
        {
            var slot = squad.GetSlot(seat);
            string who = IsMySeat(squad, seat) ? youLine
                : slot.IsPlayer() ? (string.IsNullOrEmpty(slot.MemberName) ? unknownFriendLine : slot.MemberName)
                : seat == 0 && squad.IsJoined() ? OwnerName(squad)
                : slot.IsOpen() ? openLine : aiLine;
            var vessel = Vessel(slot.Vessel);
            var keywords = vessel ? vessel.Keywords : null;
            var pilot = catalog && vessel ? catalog.ResolvePilot(slot, keywords) : null;
            var artifact = catalog && vessel ? catalog.ResolveArtifact(slot, keywords) : null;
            card.Draw(SeatName(seat), TemplateText.Fill(numberLine, "n", (seat + 1).ToString()), who, HullArt(vessel),
                vessel ? vessel.CardName : string.Empty, ElementName(slot.Element),
                pilot ? pilot.Portrait : null, pilot && !pilot.IsDefault ? pilot.DisplayName : basePilotLine,
                artifact ? artifact.DisplayName : noArtifactLine, artifact, slot.IsOpen());
            bool making = _mode == Mode.Make;
            card.SetEditing(making && seat == _seat, domain);
            card.SetPressable(making && CanEdit(squad, seat));
        }

        void DrawPicker()
        {
            if (_draft == null) return;
            var slot = _draft.GetSlot(_seat);
            bool mine = IsMySeat(_draft, _seat);

            Offered();
            int start = _vesselPage * vesselTiles.Length;
            for (int i = 0; i < vesselTiles.Length; i++)
            {
                if (!vesselTiles[i]) continue;
                var v = start + i < _offered.Count ? _offered[start + i] : null;
                vesselTiles[i].Draw(v, v ? v.CardName : null, null, HullArt(v), v && v.Class == slot.Vessel);
            }
            bool paged = _offered.Count > vesselTiles.Length;
            if (vesselPrevButton) vesselPrevButton.interactable = paged;
            if (vesselNextButton) vesselNextButton.interactable = paged;

            PilotsFor(slot.Vessel);
            for (int i = 0; i < pilotChips.Length; i++)
            {
                if (!pilotChips[i]) continue;
                if (i == 0)
                {
                    pilotChips[i].Draw(true, basePilotLine, basePilotDetail, null, slot.UsesDefaultPilot());
                    continue;
                }
                var p = i - 1 < _pilots.Count ? _pilots[i - 1] : null;
                pilotChips[i].Draw(p, p ? p.DisplayName : null, p ? TemplateText.Fill(pilotDetail, "ability", p.Ability) : null,
                    p ? p.Portrait : null, p && p.Id == slot.PilotId);
            }

            bool rogue = _seat > 0;
            if (seatModeGroup) KitVisibility.SetShown(seatModeGroup, rogue);
            if (seatModeChips.Length > 0 && seatModeChips[0]) seatModeChips[0].Draw(rogue, aiModeLabel, null, null, slot.Seat == SquadSeat.Ai);
            if (seatModeChips.Length > 1 && seatModeChips[1]) seatModeChips[1].Draw(rogue, openModeLabel, null, null, slot.IsOpen());

            for (int i = 0; i < elementChips.Length; i++)
                if (elementChips[i])
                    elementChips[i].Draw(i < ElementCycle.Length, i < ElementCycle.Length ? ElementName(ElementCycle[i]) : null, null, null,
                        i < ElementCycle.Length && slot.Element == ElementCycle[i]);

            ArtifactsFor(slot.Vessel, mine);
            for (int i = 0; i < artifactChips.Length; i++)
            {
                if (!artifactChips[i]) continue;
                if (i == 0)
                {
                    artifactChips[i].Draw(true, noArtifactChip, null, null, !slot.CarriesArtifact());
                    continue;
                }
                var a = i - 1 < _artifacts.Count ? _artifacts[i - 1] : null;
                artifactChips[i].Draw(a, a ? a.DisplayName : null, null, null, a && a.Id == slot.ArtifactId);
            }
        }

        void DrawBars()
        {
            bool making = _mode == Mode.Make, viewing = _mode == Mode.View;
            if (makeBarGroup) KitVisibility.SetShown(makeBarGroup, making);
            if (viewBarGroup) KitVisibility.SetShown(viewBarGroup, viewing);
            if (createLabel) createLabel.text = _editIndex >= 0 ? saveWord : createWord;
            var squad = Selected();
            if (viewing && squad != null)
            {
                if (removeLabel) removeLabel.text = _confirmUntil >= 0f ? confirmLabel : squad.IsJoined() ? leaveLabel : deleteLabel;
                if (editButton) editButton.interactable = !squad.IsJoined();
            }
            if (missionLock)
            {
                var hub = viewing ? FindHub(ScreenSwitcher.ModalWindows.MISSION, reportMissing: false) : null;
                missionLock.enabled = hub && hub.TryGetComponent<MenuAvailabilityView>(out var gate) && !gate.IsAvailable;
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

        // ---- helpers ----

        SavedSquad Selected()
        {
            var book = Book;
            return _selected >= 0 && _selected < book.Squads.Count ? book.Squads[_selected] : null;
        }

        /// <summary>The squad on the cards: the draft in Make squad, else the selected one.</summary>
        SavedSquad Shown() => _mode == Mode.Make ? _draft : _mode == Mode.View ? Selected() : null;

        static bool IsMySeat(SavedSquad squad, int seat) => squad.IsJoined() ? seat == squad.MySeat : seat == 0;

        /// <summary>The player's own card, and the AI or open cards of a squad they made.</summary>
        static bool CanEdit(SavedSquad squad, int seat) =>
            squad != null && (IsMySeat(squad, seat) || (!squad.IsJoined() && seat > 0 && !squad.GetSlot(seat).IsPlayer()));

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

        Color DomainColor(Domains d) => colors ? colors.GetDomainSignalColor(d) : Color.white;

        SO_Vessel Vessel(VesselClassType cls) =>
            vessels && vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        Sprite HullArt(SO_Vessel v)
        {
            if (!v) return null;
            var art = cardArt ? cardArt.For(v) : null;
            return art ? art : v.IconActive;
        }

        void Offered()
        {
            _offered.Clear();
            if (!vessels || vessels.VesselList == null) return;
            foreach (var v in vessels.VesselList)
                if (v && !v.IsLocked) _offered.Add(v);
        }

        void PilotsFor(VesselClassType cls)
        {
            _pilots.Clear();
            var vessel = Vessel(cls);
            if (catalog && vessel) catalog.GetPilotsFor(vessel, _pilots);
            _pilots.RemoveAll(p => !p || p.IsDefault);
        }

        void ArtifactsFor(VesselClassType cls, bool mine)
        {
            _artifacts.Clear();
            var vessel = Vessel(cls);
            if (catalog && vessel) catalog.GetArtifactsFor(vessel, _artifacts);
            // The player carries only what they have found; an AI wingmate may carry anything its vessel takes.
            if (mine) _artifacts.RemoveAll(a => !a || PilotLoadoutStore.FoundRank(a.Id) <= 0);
        }

        static Element ElementOf(SO_Vessel v, Element fallback) =>
            v && v.PrimaryElement != Element.None && v.PrimaryElement != Element.Omni ? v.PrimaryElement : fallback;

        /// <summary>The player's card for a new or joined squad: the hull they last picked and its Hangar pilot and artifact.</summary>
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
