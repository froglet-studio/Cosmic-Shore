using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where a human sits: a hull (its vessel NetworkObjectId) and a seat on it (0 the helm). The default, hull 0, means
    /// "flying my own hull" - the station every human starts at and returns to. Held per human on
    /// <see cref="Player.NetStation"/>, written by the server only (<see cref="CrewStations"/>).
    /// </summary>
    public struct CrewStation : INetworkSerializable, IEquatable<CrewStation>
    {
        public ulong Hull;
        public int Seat;

        public CrewStation(ulong hull, int seat)
        {
            Hull = hull;
            Seat = seat;
        }

        /// <summary>Flying one's own hull from its helm.</summary>
        public bool IsHome => Hull == 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Hull);
            serializer.SerializeValue(ref Seat);
        }

        public bool Equals(CrewStation other) => Hull == other.Hull && Seat == other.Seat;
        public override bool Equals(object obj) => obj is CrewStation other && Equals(other);
        public override int GetHashCode() => Hull.GetHashCode() * 31 + Seat;
        public override string ToString() => IsHome ? "own helm" : $"hull {Hull} seat {Seat}";
    }

    /// <summary>
    /// A hull with more than one seat a person can sit in: the crew vessels (the Orca's helm and three gunners, the
    /// Honeyguide and the Ratel, the Grouper and the Wrasse, the Goby and the Pistol) and the Jacks (fly or herd). The
    /// component that implements it decides what a seat change does on its own hull (who it hands the hull to, what it
    /// pauses); <see cref="CrewStations"/> decides which seat a person goes to.
    /// </summary>
    public interface ICrewStations
    {
        /// <summary>Every seat on the hull, the helm (0) included.</summary>
        int SeatCount { get; }

        /// <summary>The seat the local person holds on this hull (0 the helm), or -1 while no local person is aboard
        /// (an AI hull, a remote copy, a hull a menu parked on its AI).</summary>
        int LocalSeat { get; }

        /// <summary>True when a person on ANOTHER machine can hold a seat on this hull while someone else flies it (the
        /// crew runs that seat on the sitter's machine). A crew that cannot yet still takes one person at a time.</summary>
        bool HostsRiders { get; }

        /// <summary>Move the local person to <paramref name="seat"/> on this hull, by the crew's own rules. Only called
        /// while <see cref="LocalSeat"/> is not -1.</summary>
        void TakeSeat(int seat);
    }

    /// <summary>
    /// <b>Seats, then hulls: one ring for every person in a match or in freestyle</b> (Garrett, 2026-10-23: "any other
    /// player in freestyle should be able to press left or right on the dpad, or use a two finger swipe on mobile, to
    /// change seats ... if ai vessels are added to the spawner then any player on the same domain can switch to those as
    /// well. there should be no difference between a vessel spawned in and a vessel abandoned by switching").
    ///
    /// <para><b>The ring.</b> Every hull on the person's domain, ordered by vessel NetworkObjectId (every peer agrees),
    /// each expanded into its seats (<see cref="ICrewStations.SeatCount"/>; a plain hull has one). The swap gesture
    /// (<see cref="PilotSwapGesture"/>: D-pad left / right, keys 1 / 2, a two-finger swipe) steps round it, skipping
    /// every station that is not free for this person (<see cref="Classify"/>). Outside freestyle and a running arena
    /// match the ring is the person's own hull only, so a crew vessel still changes seats in any mode.</para>
    ///
    /// <para><b>One rule for every hull without a human at its helm.</b> A hull is <i>abandoned</i> when its pilot is an
    /// AI (a Spawn Matrix companion, an arena backfill) or a human who sits on some OTHER hull. Both are the same kind of
    /// seat: taking its helm (or any seat of it) is the arena's <see cref="PilotSwap"/> - the two pilots exchange hulls,
    /// so the hull you leave goes to whoever flew the one you took, and is abandoned in its turn. Nothing is spawned and
    /// nothing is destroyed, so a hull you left is exactly as free as a companion the spawner released.</para>
    ///
    /// <para><b>Riding.</b> A free seat other than the helm on a hull a human flies (or sits aboard) is taken as a
    /// <i>rider</i>: the person's own hull is parked on its AI on their machine, their camera and HUD follow the ridden
    /// hull, and the crew runs their seat on their machine (<see cref="ICrewStations.HostsRiders"/>). The helm of a hull
    /// whose own pilot is aboard is theirs: it is never offered.</para>
    ///
    /// <para><b>Authority.</b> The server writes every <see cref="Player.NetStation"/> and re-checks every move against its
    /// own state (<see cref="Classify"/>); a person's own-hull seat is decided on their machine (the crew moves them at
    /// once) and reported, so other people see it taken.</para>
    /// </summary>
    public static class CrewStations
    {
        /// <summary>Raised on every peer while Menu_Main's vessel initializer is spawned: freestyle and the lava lamp,
        /// where any person may move to any free hull of their domain.</summary>
        internal static bool MenuLive { get; set; }

        /// <summary>How freestyle configures the AI that takes a hull a person left (set by Menu_Main's initializer): the
        /// Spawn Matrix companion's configuration, so a hull you leave flies exactly as a released companion does.</summary>
        internal static Action<AIPilot> ConfigureFreestyleAI { get; set; }

        /// <summary>The match state every <see cref="Player"/> shares (set as one spawns), for the crews' own reads.</summary>
        internal static GameDataSO GameData { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            MenuLive = false;
            GameData = null;
            ConfigureFreestyleAI = null;
        }

        /// <summary>Where hulls may change hands: freestyle (Menu_Main) and a running arena match.</summary>
        public static bool HullHoppingAllowed(GameDataSO gameData) =>
            MenuLive || (gameData != null && gameData.IsArenaMatch && gameData.IsTurnRunning);

        public enum Move
        {
            None = 0,
            /// <summary>A seat on the person's own hull (the helm brings a rider home).</summary>
            OwnSeat = 1,
            /// <summary>A free seat on a hull a human flies or sits aboard: ride it.</summary>
            Ride = 2,
            /// <summary>A seat on an abandoned hull: exchange hulls with its pilot (<see cref="PilotSwap"/>), then sit.</summary>
            TakeHull = 3,
        }

        /// <summary>The crew on a hull, or null for a one-seat hull.</summary>
        public static ICrewStations CrewOf(IVessel hull) =>
            hull is Component c && c && c.GetComponentInChildren<ICrewStations>(true) is { } crew ? crew : null;

        /// <summary>The hull's seats, the helm included (1 for a plain hull).</summary>
        public static int SeatCount(IVessel hull) => Mathf.Max(1, CrewOf(hull)?.SeatCount ?? 1);

        /// <summary>A spawned vessel by its NetworkObjectId on this machine.</summary>
        public static bool TryFindHull(ulong hullId, out IVessel hull)
        {
            hull = null;
            var nm = NetworkManager.Singleton;
            if (hullId == 0 || nm == null || nm.SpawnManager == null ||
                !nm.SpawnManager.SpawnedObjects.TryGetValue(hullId, out var obj) || !obj) return false;
            return obj.TryGetComponent(out hull) && hull.VesselStatus != null;
        }

        /// <summary>The station <paramref name="p"/> holds, resolved: their own hull's id for the home helm.</summary>
        public static CrewStation Resolve(Player p)
        {
            var st = p.NetStation.Value;
            if (!st.IsHome) return st;
            return new CrewStation(IsAlive(p.Vessel) ? p.Vessel.VesselNetId : 0, 0);
        }

        /// <summary>True while <paramref name="p"/> sits on a hull that is not their own (riding, or on their way to it).</summary>
        public static bool IsRiding(Player p) =>
            p && !p.NetStation.Value.IsHome && (!IsAlive(p.Vessel) || p.NetStation.Value.Hull != p.Vessel.VesselNetId);

        /// <summary>True when a human is aboard their own hull (at its helm or one of its seats): its helm is theirs.</summary>
        static bool OwnerAboard(Player pilot, ulong hullId)
        {
            var st = pilot.NetStation.Value;
            return st.IsHome || st.Hull == hullId;
        }

        /// <summary>
        /// The human other than <paramref name="self"/> who holds <paramref name="seat"/> on the hull
        /// <paramref name="hullId"/>, or null. Read off replicated state, so it is the same on every peer.
        /// </summary>
        public static Player HumanAt(IList<IPlayer> players, Player self, ulong hullId, int seat)
        {
            if (players == null) return null;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is not Player p || !p || p == self || !p.IsSpawned || p.NetIsAI.Value) continue;
                var st = Resolve(p);
                if (st.Hull == hullId && st.Seat == seat) return p;
            }
            return null;
        }

        /// <summary>
        /// True when a person on another machine holds <paramref name="seat"/> on <paramref name="hull"/>: the crew there
        /// leaves that seat to them (its AI stands down, the local person's own picks skip it).
        /// </summary>
        public static bool HeldByRemoteHuman(Component hull, int seat)
        {
            if (GameData == null || !hull || !hull.TryGetComponent(out IVessel vessel)) return false;
            var holder = HumanAt(GameData.Players, null, vessel.VesselNetId, seat);
            return holder && !holder.IsLocalUser;
        }

        /// <summary>True while any person on another machine rides <paramref name="hull"/>.</summary>
        public static bool HasRemoteRider(Component hull)
        {
            if (GameData == null || !hull || !hull.TryGetComponent(out IVessel vessel)) return false;
            var players = GameData.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] is Player p && p && !p.IsLocalUser && IsRiding(p) && p.NetStation.Value.Hull == vessel.VesselNetId)
                    return true;
            return false;
        }

        /// <summary>The seat the person on this machine rides <paramref name="hull"/> at (never their own hull), or -1.</summary>
        public static int LocalRiderSeat(Component hull)
        {
            var local = GameData != null ? GameData.LocalPlayer as Player : null;
            if (!local || !hull || !IsRiding(local) || !hull.TryGetComponent(out IVessel vessel)) return -1;
            var st = local.NetStation.Value;
            return st.Hull == vessel.VesselNetId ? st.Seat : -1;
        }

        /// <summary>
        /// True for the local person's OWN hull while they ride another one: it is parked on its AI here, and its crew runs
        /// as an AI crew, exactly as on a hull an AI flies (an abandoned hull is no different from a spawned one).
        /// </summary>
        public static bool IsParkedHere(IVessel hull) =>
            hull?.VesselStatus?.Player is Player p && p && p.IsLocalUser && IsRiding(p);

        /// <summary>
        /// What taking <paramref name="seat"/> on <paramref name="hull"/> would be for <paramref name="self"/>, or
        /// <see cref="Move.None"/> when it is not free for them. The server's check and the ring's skip are this one test.
        /// </summary>
        public static Move Classify(Player self, IVessel hull, int seat, IList<IPlayer> players, out Player pilot)
        {
            pilot = null;
            if (!self || !IsAlive(hull) || hull.VesselStatus == null) return Move.None;
            if (seat < 0 || seat >= SeatCount(hull)) return Move.None;
            ulong id = hull.VesselNetId;
            if (HumanAt(players, self, id, seat)) return Move.None;

            pilot = hull.VesselStatus.Player as Player;
            if (pilot == self) return Move.OwnSeat;
            if (!pilot || !pilot.IsSpawned) return Move.None;
            if (pilot.NetDomain.Value != self.NetDomain.Value) return Move.None;
            if (!ReferenceEquals(pilot.Vessel, hull)) return Move.None;   // a pairing a swap is still moving

            bool abandoned = pilot.NetIsAI.Value || !OwnerAboard(pilot, id);
            if (abandoned) return Move.TakeHull;
            return seat > 0 && CrewOf(hull) is { HostsRiders: true } ? Move.Ride : Move.None;
        }

        /// <summary>
        /// Step <paramref name="direction"/> (+1 next, -1 previous) round the ring from <paramref name="fromSeat"/> on
        /// <paramref name="fromHull"/> to the next station that is free for <paramref name="self"/>.
        /// <paramref name="hopHulls"/> false keeps the ring to <paramref name="fromHull"/> (a crew vessel's seats in a mode
        /// where hulls do not change hands). False when there is nowhere to go.
        /// </summary>
        public static bool TryStep(Player self, IList<IPlayer> players, IVessel fromHull, int fromSeat, int direction,
                                   bool hopHulls, List<IVessel> hullScratch, List<int> seatScratch, out IVessel hull, out int seat)
        {
            hull = null;
            seat = 0;
            if (!self || direction == 0 || players == null || !IsAlive(fromHull)) return false;

            hullScratch.Clear();
            seatScratch.Clear();
            var domain = self.NetDomain.Value;
            if (hopHulls)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] is not Player p || !p || !p.IsSpawned || p.NetDomain.Value != domain) continue;
                    if (IsAlive(p.Vessel) && !hullScratch.Contains(p.Vessel)) hullScratch.Add(p.Vessel);
                }
            }
            if (!hullScratch.Contains(fromHull)) hullScratch.Add(fromHull);
            hullScratch.Sort((a, b) => a.VesselNetId.CompareTo(b.VesselNetId));

            // the ring, flattened: seatScratch[k] = hull index * 64 + seat (no hull has 64 seats)
            int own = -1;
            for (int h = 0; h < hullScratch.Count; h++)
            {
                int seats = Mathf.Min(64, SeatCount(hullScratch[h]));
                for (int s = 0; s < seats; s++)
                {
                    if (ReferenceEquals(hullScratch[h], fromHull) && s == fromSeat) own = seatScratch.Count;
                    seatScratch.Add(h * 64 + s);
                }
            }
            int n = seatScratch.Count;
            if (own < 0 || n < 2) return false;

            int step = direction > 0 ? 1 : -1;
            for (int k = 1; k < n; k++)
            {
                int code = seatScratch[((own + step * k) % n + n) % n];
                var h = hullScratch[code / 64];
                int s = code % 64;
                if (Classify(self, h, s, players, out _) == Move.None) continue;
                hull = h;
                seat = s;
                return true;
            }
            return false;
        }

        static bool IsAlive(IVessel vessel) => vessel is UnityEngine.Object o && o;
    }

    /// <summary>
    /// A crew whose seat a person on another machine can hold while the crew itself runs on the hull's machine (the
    /// Ratel's board: the Dynamo and every effect it fires stay with the hull, so nothing it decides needs relaying).
    /// The rider's crew sends its seat's input (<see cref="CrewRelay.SendInput"/>), the hull's crew answers with its
    /// state (<see cref="CrewRelay.SendState"/>); both travel through the server, which checks who may speak.
    /// </summary>
    public interface ICrewRelay
    {
        /// <summary>On the hull's machine: input from the person who rides <paramref name="seat"/>.</summary>
        void OnRiderInput(int seat, int[] data);

        /// <summary>On a rider's machine: the crew's state, from the hull's machine.</summary>
        void OnCrewState(int[] data);
    }

    /// <summary>The two directions of <see cref="ICrewRelay"/>, over the senders' own <see cref="Player"/> objects.</summary>
    public static class CrewRelay
    {
        /// <summary>From the rider's machine to the crew on the hull it rides.</summary>
        public static void SendInput(Component hull, int seat, int[] data)
        {
            var local = CrewStations.GameData != null ? CrewStations.GameData.LocalPlayer as Player : null;
            if (local && hull && hull.TryGetComponent(out IVessel vessel)) local.SendCrewInput(vessel.VesselNetId, seat, data);
        }

        /// <summary>From the hull's machine to everyone riding it.</summary>
        public static void SendState(Component hull, int[] data)
        {
            if (!hull || !hull.TryGetComponent(out IVessel vessel) || vessel.VesselStatus?.Player is not Player pilot || !pilot) return;
            pilot.SendCrewState(vessel.VesselNetId, data);
        }

        internal static ClientRpcParams To(ulong clientId) =>
            new() { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };

        internal static ulong[] RidersOf(IList<IPlayer> players, ulong hullId)
        {
            int n = 0;
            for (int i = 0; i < players.Count; i++)
                if (IsRiderOf(players[i], hullId)) n++;
            var ids = new ulong[n];
            n = 0;
            for (int i = 0; i < players.Count; i++)
                if (IsRiderOf(players[i], hullId)) ids[n++] = ((Player)players[i]).OwnerClientId;
            return ids;
        }

        static bool IsRiderOf(IPlayer player, ulong hullId) =>
            player is Player p && p && p.IsSpawned && !p.NetIsAI.Value && CrewStations.IsRiding(p) && p.NetStation.Value.Hull == hullId;

        internal static void DeliverInput(ulong hullId, int seat, int[] data)
        {
            if (data != null && CrewStations.TryFindHull(hullId, out var hull) && hull.Transform.GetComponentInChildren<ICrewRelay>() is { } crew)
                crew.OnRiderInput(seat, data);
        }

        internal static void DeliverState(ulong hullId, int[] data)
        {
            if (data != null && CrewStations.TryFindHull(hullId, out var hull) && hull.Transform.GetComponentInChildren<ICrewRelay>() is { } crew)
                crew.OnCrewState(data);
        }
    }

    /// <summary>
    /// The local person's half of <see cref="CrewStations"/>, ticked by their own <see cref="Player"/>: reads the swap
    /// gesture, asks the server for a move, reports the seat their own crew put them in, and while they ride another hull
    /// parks their own on its AI and points their camera, occlusion corridor and HUD at the ridden one.
    /// </summary>
    internal sealed class LocalCrewStation
    {
        readonly Player _player;
        readonly List<IVessel> _hulls = new();
        readonly List<int> _seats = new();

        float _readyAt;
        // the own hull this machine parked while its person rides another, and whether it started the autopilot
        IVessel _parked;
        bool _parkedAI;
        // the hull whose camera and HUD this machine took for a ride
        IVessel _viewHull;
        // the seat to take once a hull this person asked for is theirs (taking an AI hull at a gunner's seat)
        ulong _entryHull;
        int _entrySeat;
        float _entryUntil;
        // the own-hull seat last reported to the server
        int _reportedSeat;
        // the own hull at the last tick (an exchange changes it)
        IVessel _lastHome;
        // a menu sent this person home: their ride ends here while the server's answer travels
        bool _sentHome;
        // the crew on the own hull, cached per hull
        IVessel _crewHull;
        ICrewStations _crew;

        public LocalCrewStation(Player player) => _player = player;

        ICrewStations HomeCrew(IVessel home)
        {
            if (!ReferenceEquals(_crewHull, home))
            {
                _crewHull = home;
                _crew = CrewStations.CrewOf(home);
                _reportedSeat = 0;
            }
            return _crew;
        }

        public void Tick(GameDataSO gameData)
        {
            var home = _player.Vessel;
            if (!IsAlive(home)) { LetGo(null); return; }

            // an exchange made this person's own hull one they already sit on (someone took the hull they had left, and
            // they were handed the one they ride): they stay in their seat, which is now their own crew's to give them
            var st0 = _player.NetStation.Value;
            if (!ReferenceEquals(home, _lastHome))
            {
                _lastHome = home;
                if (!st0.IsHome && st0.Hull == home.VesselNetId && st0.Seat > 0)
                {
                    _entryHull = home.VesselNetId;
                    _entrySeat = st0.Seat;
                    _entryUntil = Time.unscaledTime + 3f;
                }
            }

            var st = _player.NetStation.Value;
            if (_sentHome && (st.IsHome || st.Hull == home.VesselNetId)) _sentHome = false;
            bool riding = !_sentHome && CrewStations.IsRiding(_player);
            IVessel ride = null;
            if (riding && !CrewStations.TryFindHull(st.Hull, out ride)) riding = false;   // gone: the server sends us home

            if (riding) Ride(home, ride);
            else LetGo(home);

            var crew = HomeCrew(home);
            if (!riding)
            {
                ApplyEntry(home, crew);
                if (_entryHull == 0) ReportOwnSeat(home, crew);   // a seat still to take is not one to report yet
            }

            if (PauseSystem.Paused) return;
            int localSeat = crew?.LocalSeat ?? -1;
            // the gesture is live while the person flies, sits at a seat of their own hull, or rides; a hull a menu parked
            // on its AI (the lava lamp, the app shell) has its input paused and no seat, so it reads nothing
            bool live = riding || localSeat > 0 || (_player.InputStatus != null && !_player.InputStatus.Paused);
            if (!live) return;

            int dir = PilotSwapGesture.DirectionThisFrame();
            if (dir == 0 || Time.unscaledTime < _readyAt) return;

            var fromHull = riding ? ride : home;
            int fromSeat = riding ? st.Seat : Mathf.Max(0, localSeat);
            bool hop = _player.IsSpawned && CrewStations.HullHoppingAllowed(gameData);
            if (!CrewStations.TryStep(_player, gameData.Players, fromHull, fromSeat, dir, hop, _hulls, _seats, out var hull, out int seat))
                return;
            _readyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;

            if (!riding && ReferenceEquals(hull, home))
            {
                // a seat on the person's own hull: the crew moves them now, the report follows
                crew?.TakeSeat(seat);
                return;
            }
            // leaving the own hull from one of its seats: back to its helm first, so the crew gives the hull back by its
            // own rules before the hull changes hands (a crew that let go of a hull it had handed to its AI after the
            // exchange would stop the autopilot of the person or AI who now flies it)
            if (!riding && localSeat > 0) crew?.TakeSeat(0);
            // anywhere else is the server's to grant; a seat past the helm of a hull that becomes ours is taken on arrival
            if (seat > 0)
            {
                _entryHull = hull.VesselNetId;
                _entrySeat = seat;
                _entryUntil = Time.unscaledTime + 3f;
            }
            _player.RequestStation(hull.VesselNetId, seat);
        }

        /// <summary>A menu parks the own hull (<see cref="CrewSeat.ReturnHumansToHull"/>): a ride ends without this machine
        /// stopping the autopilot or giving the input back - the parker holds both now.</summary>
        public void ReturnHome()
        {
            _entryHull = 0;
            if (!CrewStations.IsRiding(_player) && _parked == null) return;
            _sentHome = true;
            _parked = null;   // the parker owns the autopilot and the pause from here
            ReleaseView(_player.Vessel);
            if (IsAlive(_player.Vessel)) _player.RequestStation(_player.Vessel.VesselNetId, 0);
        }

        void ApplyEntry(IVessel home, ICrewStations crew)
        {
            if (_entryHull == 0) return;
            if (Time.unscaledTime > _entryUntil) { _entryHull = 0; return; }
            if (home.VesselNetId != _entryHull || crew == null || crew.LocalSeat < 0) return;
            if (crew.LocalSeat != _entrySeat) crew.TakeSeat(_entrySeat);
            _entryHull = 0;
        }

        void ReportOwnSeat(IVessel home, ICrewStations crew)
        {
            int seat = Mathf.Max(0, crew?.LocalSeat ?? 0);
            var st = _player.NetStation.Value;
            int held = st.IsHome ? 0 : st.Hull == home.VesselNetId ? st.Seat : -1;
            if (held < 0) return;   // a move the server is still answering
            if (seat == held) { _reportedSeat = seat; return; }
            if (seat == _reportedSeat) return;   // sent; the answer is on its way
            _reportedSeat = seat;
            _player.ReportOwnSeat(seat);
        }

        void Ride(IVessel home, IVessel ride)
        {
            if (!ReferenceEquals(_parked, home))
            {
                _parked = home;
                // the person leaves their own hull: their own crew lets them go first (a gunner's seat on the own hull
                // left its autopilot running, which is this ride's to stop again), then the hull is parked on its AI
                var crew = HomeCrew(home);
                bool crewHadSeat = crew != null && crew.LocalSeat > 0;
                if (crewHadSeat && home is Component c && c)
                    foreach (var seat in c.GetComponentsInChildren<IHullParkAware>(true)) seat.ReturnHumanToHull();
                _parkedAI = crewHadSeat || !home.VesselStatus.AutoPilotEnabled;
                CrewSeat.SetFlightInputPaused(_player, true);
                if (!home.VesselStatus.AutoPilotEnabled) home.ToggleAIPilot(true);
                home.VesselStatus.VesselHUDController?.HideHUD();
                _viewHull = null;   // re-take the view: a hull exchange re-pointed it at the new own hull
            }

            var follow = ride.VesselStatus.CameraFollowTarget;
            var cm = CameraManager.Instance;
            if (!ReferenceEquals(_viewHull, ride) || (cm && follow && cm.PlayerFollowTarget != follow))
            {
                if (IsAlive(_viewHull) && !ReferenceEquals(_viewHull, ride)) _viewHull.VesselStatus.VesselHUDController?.HideHUD();
                _viewHull = ride;
                PointLocalViewAt(ride);
                ride.VesselStatus.VesselHUDController?.ShowHUD();
            }
        }

        void LetGo(IVessel home)
        {
            ReleaseView(home);
            if (_parked == null) return;
            var parked = _parked;
            _parked = null;
            // only the hull still ours is given back; one an exchange handed to someone else is theirs to fly
            if (!ReferenceEquals(parked, home) || !IsAlive(home)) return;
            if (_parkedAI && home.VesselStatus.AutoPilotEnabled) home.ToggleAIPilot(false);
            home.VesselStatus.ActionHandler?.ReleaseHeldInputs();
            CrewSeat.SetFlightInputPaused(_player, false);
            home.VesselStatus.VesselHUDController?.ShowHUD();
        }

        void ReleaseView(IVessel home)
        {
            if (_viewHull == null) return;
            if (IsAlive(_viewHull) && !ReferenceEquals(_viewHull, home)) _viewHull.VesselStatus.VesselHUDController?.HideHUD();
            _viewHull = null;
            if (IsAlive(home)) PointLocalViewAt(home);
        }

        // what VesselController.ChangePlayer points at the local pilot's hull - the camera (with the hull's own camera
        // settings), the occlusion corridor, the speed tunnel and the rear view - pointed at the hull this person sits on
        static void PointLocalViewAt(IVessel hull)
        {
            var status = hull.VesselStatus;
            var cm = CameraManager.Instance;
            if (cm && status.CameraFollowTarget) cm.SetupGamePlayCameras(status.CameraFollowTarget);
            if (cm && status.VesselCameraCustomizer) status.VesselCameraCustomizer.RetargetAndApply(hull);
            PrismOcclusionCorridor.SetTarget(hull.Transform);
            VesselSpeedTunnel.SetTarget(status, hull.Transform);
            VesselRearView.SetTarget(hull.Transform);
        }

        static bool IsAlive(IVessel vessel) => vessel is UnityEngine.Object o && o;
    }
}
