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

namespace CosmicShore.UI
{
    /// <summary>
    /// The Ark's SQUADS tab (Shombith, 2026-10-27): "Users can make / join up to 4 squads and they can use the squad to go
    /// into a mission or arena or arcade as they want. Inside the ark they will see the squad members with their selected
    /// vessel, element and pilot and artifact for each squad mate."
    ///
    /// <list type="bullet">
    /// <item><b>The list</b>: the player's squads, made and joined together, at most <see cref="SquadBook.MaxSquads"/>.
    /// Pressing one makes it the active squad - the one PLAY takes into a mode.</item>
    /// <item><b>The squad</b>: three <see cref="SquadMateCard"/>s - the leader and the two rogues - each with who flies it
    /// and their vessel, element, pilot and artifact. The player changes their own seat's four picks in any squad, and
    /// the AI seats of a squad they made; a seat in a squad they made steps AI, open for a friend, or (a friend's) open
    /// again.</item>
    /// <item><b>NEW</b> makes a squad from the player's current Hangar picks; <b>JOIN</b> lists the friends' squads with
    /// a seat open; <b>DELETE</b> / <b>LEAVE</b> removes the selected one (a second press within
    /// <see cref="confirmSeconds"/> confirms).</item>
    /// <item><b>PLAY ARCADE / ARENA / MISSION</b> makes the squad active, puts the player in their seat's vessel and pilot,
    /// invites the online squad mates to the party, and presses the Home hub's entry for that window
    /// (<see cref="MenuHubButton.Press"/>), so a locked window refuses exactly as it does on Home.</item>
    /// </list>
    ///
    /// <para>The squads are the player's <c>SQUAD_DATA</c> record (<see cref="UGSDataService.SquadRepo"/>). Joining rides
    /// each player's PUBLIC squad record (<see cref="SquadDirectory"/>), read and published when the tab opens and after
    /// each change; <see cref="SquadMembership"/> holds the rules, so this page only draws and asks.</para>
    /// </summary>
    public class SquadsPage : MonoBehaviour, IArkTabPage
    {
        [Header("Content")]
        [Tooltip("The vessels a squad slot can fly (the Hangar's list); locked ones are skipped.")]
        [SerializeField] private SO_VesselList vessels;
        [Tooltip("Pilots and artifacts. Empty loads the one in Resources.")]
        [SerializeField] private PilotArtifactCatalogSO catalog;

        [Header("List")]
        [SerializeField] private SquadRowView[] squadRows = new SquadRowView[SquadBook.MaxSquads];
        [SerializeField] private KitText countText;
        [SerializeField] private KitButton newButton;
        [SerializeField] private KitButton joinButton;

        [Header("Squad")]
        [Tooltip("Shown while a squad is selected.")]
        [SerializeField] private CanvasGroup detailGroup;
        [Tooltip("Shown while the player is in no squad.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [SerializeField] private TMP_Text squadNameText;
        [SerializeField] private TMP_Text squadLineText;
        [SerializeField] private SquadMateCard[] mateCards = new SquadMateCard[SavedSquad.SlotCount];
        [SerializeField] private KitButton removeButton;
        [SerializeField] private TMP_Text removeLabel;

        [Header("Play")]
        [SerializeField] private KitButton playArcadeButton;
        [SerializeField] private KitButton playArenaButton;
        [SerializeField] private KitButton playMissionButton;

        [Header("Join")]
        [Tooltip("The friends' squads with a seat open; shown over the squad while JOIN is up.")]
        [SerializeField] private CanvasGroup joinGroup;
        [SerializeField] private SquadRowView[] joinRows = { };
        [SerializeField] private KitText joinStatusText;
        [SerializeField] private KitButton joinCloseButton;

        [Header("Timing")]
        [Tooltip("Seconds between two reads of the friends' squad records (opening the tab again inside it reuses the last read).")]
        [SerializeField, Min(0f)] private float refreshSeconds = 20f;
        [Tooltip("Seconds after a change before the public record is written (changes inside it are written once).")]
        [SerializeField, Min(0f)] private float publishDelaySeconds = 2f;
        [Tooltip("Seconds a first DELETE / LEAVE press waits for the second.")]
        [SerializeField, Min(0.5f)] private float confirmSeconds = 3f;
        [Tooltip("Most friends' records read per refresh.")]
        [SerializeField, Min(1)] private int maxFriendsRead = 30;

        [Header("Words (empty shows nothing)")]
        [Tooltip("{count} squads of {max}.")]
        [SerializeField] private string countLine = "{count} of {max} squads";
        [SerializeField] private string madeLine = "Yours";
        [Tooltip("A joined squad's row; {owner} is filled in.")]
        [SerializeField] private string joinedLine = "{owner}'s squad";
        [SerializeField] private string activeSuffix = " - active";
        [Tooltip("The squad's line under its name for a squad the player made; {open} is how many seats are open.")]
        [SerializeField] private string madeDetailLine = "You lead this squad. {open} open for friends.";
        [Tooltip("For a joined squad, by its state; {owner} is filled in.")]
        [SerializeField] private string seatedLine = "You fly in {owner}'s squad.";
        [SerializeField] private string pendingLine = "Joined. {owner} seats you when they next open the Ark.";
        [SerializeField] private string seatLostLine = "{owner} gave your seat to someone else. Leave the squad, or ask them to open it again.";
        [SerializeField] private string disbandedLine = "{owner} deleted this squad.";
        [SerializeField] private string offlineLine = "Offline - showing the last copy of {owner}'s squad.";
        [SerializeField] private string[] seatNames = { "Leader", "Rogue One", "Rogue Two" };
        [SerializeField] private string youLine = "You";
        [SerializeField] private string aiLine = "AI wingmate";
        [SerializeField] private string openLine = "Open for a friend";
        [SerializeField] private string unknownFriendLine = "A friend";
        [Tooltip("The seat button on an AI seat of a squad the player made.")]
        [SerializeField] private string openSeatAction = "Open seat";
        [Tooltip("The seat button on an open seat.")]
        [SerializeField] private string aiSeatAction = "Use AI";
        [Tooltip("The seat button on a friend's seat.")]
        [SerializeField] private string removeFriendAction = "Remove";
        [Tooltip("Element names, by Element value (None, Charge, Mass, Space, Time, Omni).")]
        [SerializeField] private string[] elementNames = { "", "Charge", "Mass", "Space", "Time", "Omni" };
        [Tooltip("The pilot line when the vessel has no pilot authored.")]
        [SerializeField] private string noPilotLine = "Base kit";
        [SerializeField] private string noArtifactLine = "No artifact";
        [SerializeField] private string deleteLabel = "Delete squad";
        [SerializeField] private string leaveLabel = "Leave squad";
        [SerializeField] private string confirmLabel = "Press again";
        [Tooltip("A new squad's name; {n} is its number. Empty names it \"Squad N\".")]
        [SerializeField] private string newSquadName = "";
        [SerializeField] private string fullLine = "You are in 4 squads. Leave or delete one first.";
        [SerializeField] private string noSquadLine = "Make or join a squad first.";
        [SerializeField] private string joinLookingLine = "Looking at your friends' squads...";
        [SerializeField] private string joinNoneLine = "No friend has a seat open right now.";
        [SerializeField] private string joinOfflineLine = "Sign in to join a friend's squad.";
        [Tooltip("A friend's squad on the join list; {squad}, {owner} are filled in.")]
        [SerializeField] private string joinRowTitle = "{squad}";
        [Tooltip("{owner} and {seat} (the seat the player would take).")]
        [SerializeField] private string joinRowLine = "{owner} - {seat} open";
        [Tooltip("Toast after a join; {squad} and {owner}.")]
        [SerializeField] private string joinedToast = "You joined {owner}'s {squad}.";
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
        readonly List<PilotSO> _pilots = new();
        readonly List<ArtifactSO> _artifacts = new();
        readonly List<string> _slotted = new();

        bool _shown, _refreshing, _joinOpen, _loadingOffers;
        float _lastRefresh = float.NegativeInfinity, _publishAt = -1f, _confirmUntil = -1f;
        int _selected;

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
                if (mateCards[i]) mateCards[i].Pressed += pick => Change(seat, pick);
            }
            Listen(newButton, NewSquad);
            Listen(joinButton, OpenJoin);
            Listen(joinCloseButton, CloseJoin);
            Listen(removeButton, Remove);
            Listen(playArcadeButton, () => Play(ScreenSwitcher.ModalWindows.ARCADE));
            Listen(playArenaButton, () => Play(ScreenSwitcher.ModalWindows.ARENA));
            Listen(playMissionButton, () => Play(ScreenSwitcher.ModalWindows.MISSION));
            if (joinGroup) KitVisibility.SetShown(joinGroup, false);
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
            _selected = Mathf.Clamp(Book.ActiveIndex, 0, Mathf.Max(0, Book.Squads.Count - 1));
            Draw();
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
                DrawRemoveLabel();
            }
        }

        // ---- the list ----

        void Select(int index)
        {
            if (index < 0 || index >= Book.Squads.Count) return;
            _selected = index;
            _confirmUntil = -1f;
            if (Book.TrySetActive(index)) Saved(publish: false);
            Draw();
        }

        void NewSquad()
        {
            var book = Book;
            if (!book.CanAdd())
            {
                Refuse(fullLine);
                return;
            }
            var mine = MyDefaultLoadout();
            var offered = OfferedVessels();
            SquadSlot Rogue(int n, SquadSeat seat)
            {
                var v = offered.Count > 0 ? offered[(offered.IndexOf(Vessel(mine.Vessel)) + n + offered.Count) % offered.Count] : null;
                var cls = v ? v.Class : mine.Vessel;
                return new SquadSlot(cls, ElementOf(v, ElementCycle[n % ElementCycle.Length])).WithSeat(seat);
            }
            string name = string.IsNullOrEmpty(newSquadName) ? null
                : TemplateText.Fill(newSquadName, "n", (book.Squads.Count + 1).ToString());
            var squad = new SavedSquad(name, mine.WithMember(MyId, MyName), Rogue(1, SquadSeat.Open), Rogue(2, SquadSeat.Ai));
            book.Add(squad);
            _selected = book.Squads.Count - 1;
            book.TrySetActive(_selected);
            Saved(publish: true);
            Draw();
        }

        void Remove()
        {
            var squad = Selected();
            if (squad == null) return;
            float now = Time.unscaledTime;
            if (_confirmUntil < 0f || now > _confirmUntil)
            {
                _confirmUntil = now + confirmSeconds;
                DrawRemoveLabel();
                return;
            }
            _confirmUntil = -1f;
            _joinStates.Remove(squad.Id);
            Book.RemoveAt(_selected);
            _selected = Mathf.Clamp(Book.ActiveIndex, 0, Mathf.Max(0, Book.Squads.Count - 1));
            Saved(publish: true);
            Draw();
        }

        // ---- a squad mate's picks ----

        void Change(int seat, SquadMateCard.Pick pick)
        {
            var squad = Selected();
            if (squad == null) return;
            var slot = squad.GetSlot(seat);
            bool mine = IsMySeat(squad, seat);

            if (pick == SquadMateCard.Pick.Seat)
            {
                if (squad.IsJoined() || seat == 0) return;
                slot = slot.IsPlayer() || slot.Seat == SquadSeat.Ai ? slot.WithSeat(SquadSeat.Open) : slot.WithSeat(SquadSeat.Ai);
            }
            else
            {
                if (!CanEdit(squad, seat)) return;
                slot = pick switch
                {
                    SquadMateCard.Pick.Vessel => NextVessel(slot, mine),
                    SquadMateCard.Pick.Element => NextElement(slot),
                    SquadMateCard.Pick.Pilot => NextPilot(slot),
                    SquadMateCard.Pick.Artifact => NextArtifact(slot, mine),
                    _ => slot,
                };
            }
            squad.SetSlot(seat, slot);
            Saved(publish: true);
            Draw();
        }

        SquadSlot NextVessel(SquadSlot slot, bool mine)
        {
            var offered = OfferedVessels();
            if (offered.Count == 0) return slot;
            int at = offered.IndexOf(Vessel(slot.Vessel));
            var next = offered[(at + 1) % offered.Count];
            var moved = slot.WithVessel(next.Class);
            // The player's own seat starts on the pilot they keep in that vessel's Hangar seat.
            if (mine) moved.PilotId = PilotLoadoutStore.GetPilot(next.Class);
            return moved;
        }

        static SquadSlot NextElement(SquadSlot slot)
        {
            int at = System.Array.IndexOf(ElementCycle, slot.Element);
            slot.Element = ElementCycle[(at + 1) % ElementCycle.Length];
            return slot;
        }

        SquadSlot NextPilot(SquadSlot slot)
        {
            _pilots.Clear();
            var vessel = Vessel(slot.Vessel);
            if (catalog && vessel) catalog.GetPilotsFor(vessel, _pilots);
            _pilots.RemoveAll(p => !p || p.IsDefault);
            // The vessel's default pilot (an empty id) comes first, then the roster.
            int at = slot.UsesDefaultPilot() ? -1 : _pilots.FindIndex(p => p.Id == slot.PilotId);
            int next = at + 1;
            slot.PilotId = next < _pilots.Count ? _pilots[next].Id : null;
            return slot;
        }

        SquadSlot NextArtifact(SquadSlot slot, bool mine)
        {
            _artifacts.Clear();
            var vessel = Vessel(slot.Vessel);
            if (catalog && vessel) catalog.GetArtifactsFor(vessel, _artifacts);
            // The player carries only what they have found; an AI wingmate may carry anything its vessel takes.
            if (mine) _artifacts.RemoveAll(a => !a || PilotLoadoutStore.FoundRank(a.Id) <= 0);
            int at = slot.CarriesArtifact() ? _artifacts.FindIndex(a => a.Id == slot.ArtifactId) : -1;
            int next = at + 1;
            slot.ArtifactId = next < _artifacts.Count ? _artifacts[next].Id : null;
            return slot;
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
            Book.TrySetActive(_selected);
            Saved(publish: true);
            if (!string.IsNullOrWhiteSpace(joinedToast))
                ToastNotificationAPI.Show(TemplateText.Fill(joinedToast, "owner", ownerName, "squad", joined.Name), ToastKind.Success);
            CloseJoin();
            Draw();
        }

        // ---- play ----

        void Play(ScreenSwitcher.ModalWindows target)
        {
            var squad = Selected();
            if (squad == null)
            {
                Refuse(noSquadLine);
                return;
            }
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

        static MenuHubButton FindHub(ScreenSwitcher.ModalWindows target)
        {
            foreach (var hub in FindObjectsByType<MenuHubButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hub.Target == target) return hub;
            CSDebug.LogWarning($"[SquadsPage] No Home hub entry opens {target}; the squad cannot be taken there.");
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
                    changed |= !SameSlots(before, squad) || before.Name != squad.Name;
                }

                // The squads the player made: seat the claims of friends and refresh the friends already seated.
                var ids = new List<string>();
                foreach (var f in FriendIds()) ids.Add(f.id);
                foreach (var squad in book.Squads)
                    for (int i = 1; squad != null && !squad.IsJoined() && i < SavedSquad.SlotCount; i++)
                        if (squad.GetSlot(i).IsPlayer() && !ids.Contains(squad.GetSlot(i).MemberId)) ids.Add(squad.GetSlot(i).MemberId);

                bool anyOwned = book.Squads.Exists(s => s != null && !s.IsJoined() && NeedsReconcile(s));
                if (anyOwned)
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
            if (_shown) Draw();
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

        void Draw()
        {
            var book = Book;
            int count = book.Squads.Count;
            if (countText)
                countText.SetText(TemplateText.Fill(countLine, "count", count.ToString(), "max", SquadBook.MaxSquads.ToString()));
            for (int i = 0; i < squadRows.Length; i++)
            {
                if (!squadRows[i]) continue;
                var s = i < count ? book.Squads[i] : null;
                bool active = s != null && i == book.ActiveIndex;
                string line = s == null ? string.Empty
                    : (s.IsJoined() ? TemplateText.Fill(joinedLine, "owner", OwnerName(s)) : madeLine) + (active ? activeSuffix : string.Empty);
                squadRows[i].Draw(s != null, s?.Name, line, active);
            }
            bool full = !book.CanAdd();
            if (newButton) newButton.interactable = !full;
            if (joinButton) joinButton.interactable = !full;

            var squad = Selected();
            if (detailGroup) KitVisibility.SetShown(detailGroup, squad != null);
            if (emptyGroup) KitVisibility.SetShown(emptyGroup, squad == null);
            foreach (var b in new[] { playArcadeButton, playArenaButton, playMissionButton, removeButton })
                if (b) b.interactable = squad != null;
            if (squad == null) return;

            if (squadNameText) squadNameText.text = squad.Name;
            if (squadLineText) squadLineText.text = DetailLine(squad);
            for (int i = 0; i < mateCards.Length && i < SavedSquad.SlotCount; i++)
                if (mateCards[i]) DrawMate(mateCards[i], squad, i);
            DrawRemoveLabel();
            if (_joinOpen) DrawJoin();
        }

        void DrawMate(SquadMateCard card, SavedSquad squad, int seat)
        {
            var slot = squad.GetSlot(seat);
            string who = IsMySeat(squad, seat) ? youLine
                : slot.IsPlayer() ? (string.IsNullOrEmpty(slot.MemberName) ? unknownFriendLine : slot.MemberName)
                : seat == 0 && squad.IsJoined() ? OwnerName(squad)
                : slot.IsOpen() ? openLine : aiLine;
            string action = squad.IsJoined() || seat == 0 ? null
                : slot.IsPlayer() ? removeFriendAction : slot.IsOpen() ? aiSeatAction : openSeatAction;
            card.DrawWho(seat < seatNames.Length ? seatNames[seat] : string.Empty, who, action);

            var vessel = Vessel(slot.Vessel);
            var keywords = vessel ? vessel.Keywords : null;
            var pilot = catalog && vessel ? catalog.ResolvePilot(slot, keywords) : null;
            var artifact = catalog && vessel ? catalog.ResolveArtifact(slot, keywords) : null;
            card.DrawPicks(
                vessel ? (vessel.SquadImage ? vessel.SquadImage : vessel.IconActive) : null,
                vessel ? vessel.CardName : string.Empty,
                ElementName(slot.Element),
                pilot ? (pilot.Portrait ? pilot.Portrait : pilot.Icon) : null,
                pilot ? pilot.DisplayName : noPilotLine,
                artifact ? artifact.Icon : null,
                artifact ? artifact.DisplayName : noArtifactLine,
                CanEdit(squad, seat));
        }

        string DetailLine(SavedSquad squad)
        {
            if (!squad.IsJoined())
            {
                int open = 0;
                for (int i = 1; i < SavedSquad.SlotCount; i++) if (squad.GetSlot(i).IsOpen()) open++;
                return TemplateText.Fill(madeDetailLine, "open", open.ToString());
            }
            var state = _joinStates.TryGetValue(squad.Id, out var s) ? s : SquadJoinState.Unknown;
            string template = state switch
            {
                SquadJoinState.Seated => seatedLine,
                SquadJoinState.Pending => pendingLine,
                SquadJoinState.SeatLost => seatLostLine,
                SquadJoinState.Disbanded => disbandedLine,
                _ => offlineLine,
            };
            return TemplateText.Fill(template, "owner", OwnerName(squad));
        }

        void DrawRemoveLabel()
        {
            var squad = Selected();
            if (!removeLabel || squad == null) return;
            removeLabel.text = _confirmUntil >= 0f ? confirmLabel : squad.IsJoined() ? leaveLabel : deleteLabel;
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
                joinRows[i].Draw(true,
                    TemplateText.Fill(joinRowTitle, "squad", squad.Name, "owner", owner),
                    TemplateText.Fill(joinRowLine, "owner", owner, "seat", seat >= 0 && seat < seatNames.Length ? seatNames[seat] : string.Empty),
                    false);
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

        static bool IsMySeat(SavedSquad squad, int seat) => squad.IsJoined() ? seat == squad.MySeat : seat == 0;

        /// <summary>The player's own seat, and the AI or open seats of a squad they made.</summary>
        static bool CanEdit(SavedSquad squad, int seat) =>
            IsMySeat(squad, seat) || (!squad.IsJoined() && seat > 0 && !squad.GetSlot(seat).IsPlayer());

        string OwnerName(SavedSquad s) => string.IsNullOrEmpty(s.OwnerName) ? unknownFriendLine : s.OwnerName;

        string ElementName(Element e)
        {
            int i = (int)e;
            return i >= 0 && i < elementNames.Length ? elementNames[i] : string.Empty;
        }

        SO_Vessel Vessel(VesselClassType cls) =>
            vessels && vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        List<SO_Vessel> OfferedVessels()
        {
            var list = new List<SO_Vessel>();
            if (!vessels || vessels.VesselList == null) return list;
            foreach (var v in vessels.VesselList)
                if (v && !v.IsLocked) list.Add(v);
            return list;
        }

        static Element ElementOf(SO_Vessel v, Element fallback) =>
            v && v.PrimaryElement != Element.None && v.PrimaryElement != Element.Omni ? v.PrimaryElement : fallback;

        /// <summary>The player's seat for a new or joined squad: the hull they last picked and its Hangar pilot and artifact.</summary>
        SquadSlot MyDefaultLoadout()
        {
            var offered = OfferedVessels();
            SO_Vessel vessel = null;
            if (_gameData && _gameData.selectedVesselClass)
                vessel = offered.Find(v => v.Class == _gameData.selectedVesselClass.Value);
            if (!vessel && offered.Count > 0) vessel = offered[0];
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
