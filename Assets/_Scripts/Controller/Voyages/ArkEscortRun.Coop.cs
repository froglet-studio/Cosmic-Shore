using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The escort in a party (Docs/Voyages/SLICE.md §5: friends sail one course, each machine runs its own Ark, worlds
    /// and fauna). The LEADER (the host, or a solo machine) picks the escort's seed and home pose and tells the party with
    /// one <see cref="VoyageCourseKind.EscortSail"/> course (<see cref="VoyageCourseLink"/>); every friend's harbour sails
    /// the same escort as a FOLLOWER from it: the same difficulty, the same corridor (seeded headings from the same
    /// origin), the same hostile rolls, and the Ark's pace is a pure function of position, so the Arks agree but for each
    /// machine's own load time. Humans hold the seats the leader gave them; the leader's AI fills only the seats nobody
    /// took. The leader's end ends every friend's escort.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        int _seed = 1;
        Pose _home;
        bool _following;
        readonly List<long> _seatClients = new();

        /// <summary>True when this escort follows the host's course (a friend's machine).</summary>
        public bool IsFollowing => _active && _following;

        /// <summary>Humans in the party's seats (the leader's included), as the course assigned them.</summary>
        public int HumanSeats
        {
            get
            {
                int n = 0;
                foreach (var c in _seatClients) if (c >= 0) n++;
                return Mathf.Max(1, n);
            }
        }

        void BeginCoop(IVesselStatus local, ref SavedSquad squad, ref int difficulty, VoyageCourse course)
        {
            _seatClients.Clear();
            _hostUnderwayAt = _myUnderwayAt = -1;
            _arkSynced = false;
            _holdSince = -1f;
            _following = course is { Kind: VoyageCourseKind.EscortSail };
            if (_following)
            {
                difficulty = course.Difficulty;
                _seed = course.Seed != 0 ? course.Seed : 1;
                var q = new Quaternion(course.HomeQx, course.HomeQy, course.HomeQz, course.HomeQw);
                if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f) q = Quaternion.identity;
                _home = new Pose(new Vector3(course.HomeX, course.HomeY, course.HomeZ), q.normalized);
                if (course.SeatClients != null) _seatClients.AddRange(course.SeatClients);
                int seat = LocalSeat();
                if (seat >= 0 && seat != squad.MySeat) squad = WithSeat(squad, seat);
                return;
            }

            _seed = System.Environment.TickCount;
            if (_seed == 0) _seed = 1;
            _home = new Pose(local.Transform.position, local.Transform.rotation);

            // Seats: the leader holds its own; every other human in the session takes the next free seat, in connection
            // order; what is left is the AI's.
            for (int i = 0; i < SavedSquad.SlotCount; i++) _seatClients.Add(-1);
            int mySeat = Mathf.Clamp(squad.MySeat, 0, SavedSquad.SlotCount - 1);
            var nm = NetworkManager.Singleton;
            bool online = nm != null && nm.IsListening;
            _seatClients[mySeat] = online ? (long)nm.LocalClientId : 0;
            if (!online || !nm.IsServer) return;
            var others = new List<ulong>(nm.ConnectedClientsIds);
            others.Sort();
            int next = 0;
            foreach (var id in others)
            {
                if (id == nm.LocalClientId) continue;
                while (next < SavedSquad.SlotCount && _seatClients[next] >= 0) next++;
                if (next >= SavedSquad.SlotCount) break;   // more friends than seats: they fly along without a seat
                _seatClients[next] = (long)id;
            }
        }

        /// <summary>This machine's seat in the course's seat list, or -1.</summary>
        int LocalSeat()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return -1;
            long me = (long)nm.LocalClientId;
            for (int i = 0; i < _seatClients.Count; i++)
                if (_seatClients[i] == me) return i;
            return -1;
        }

        /// <summary>A friend in the party flies this seat (not the leader's own: Deploy skips that one already).</summary>
        bool IsHumanSeat(int seat) => seat >= 0 && seat < _seatClients.Count && _seatClients[seat] >= 0;

        static SavedSquad WithSeat(SavedSquad squad, int seat) => new()
        {
            Id = squad.Id,
            Name = squad.Name,
            OwnerId = squad.OwnerId,
            OwnerName = squad.OwnerName,
            Domain = squad.Domain,
            Leader = squad.Leader,
            RogueOne = squad.RogueOne,
            RogueTwo = squad.RogueTwo,
            MySeat = seat,
        };

        /// <summary>The leader tells the party it set sail (no-op solo, on a follower, or with nobody connected).</summary>
        void AnnounceSail()
        {
            _report.Seed = _seed;
            _report.Following = _following;
            _report.HumanSeats = HumanSeats;
            if (_following || !VoyageCourseLink.IsLeader) return;
            VoyageCourseLink.Send(new VoyageCourse
            {
                Kind = VoyageCourseKind.EscortSail,
                Difficulty = _difficulty,
                Seed = _seed,
                HomeX = _home.position.x, HomeY = _home.position.y, HomeZ = _home.position.z,
                HomeQx = _home.rotation.x, HomeQy = _home.rotation.y, HomeQz = _home.rotation.z, HomeQw = _home.rotation.w,
                SeatClients = new List<long>(_seatClients),
            });
        }

        /// <summary>The leader's escort ended: every friend's ends with the same outcome (shared fate).</summary>
        void AnnounceEnd()
        {
            if (_following || !VoyageCourseLink.IsLeader) return;
            VoyageCourseLink.Send(new VoyageCourse
            {
                Kind = VoyageCourseKind.EscortEnd,
                Result = (int)_report.Result,
                FallenLeg = _report.FallenLeg,
                Overrun = _report.Overrun,
            });
        }

        // ── Ark sync: the host's departure time ──────────────────────────────

        const float SyncDeadband = 0.25f;
        /// <summary>A follower holding (its Ark fell or arrived) gives up waiting for the host after this long.</summary>
        const float HostOutcomeTimeout = 90f;

        double _hostUnderwayAt = -1, _myUnderwayAt = -1;
        bool _arkSynced;
        float _holdSince = -1f;

        static double ServerTimeNow()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.unscaledTimeAsDouble;
        }

        /// <summary>This machine's Ark got under way: the leader tells the party when; a follower notes its own time.</summary>
        void CoopOnUnderway()
        {
            if (_sea) _sea.EndsOnArkFall = !_following;
            double now = ServerTimeNow();
            if (!_following)
            {
                if (VoyageCourseLink.IsLeader)
                    VoyageCourseLink.Send(new VoyageCourse { Kind = VoyageCourseKind.EscortUnderway, ServerTime = now });
                return;
            }
            _myUnderwayAt = now;
            TrySyncArk();
        }

        /// <summary>The host's Ark got under way at <paramref name="serverTime"/> (a follower).</summary>
        public void OnHostUnderway(double serverTime)
        {
            if (!_active || !_following) return;
            _hostUnderwayAt = serverTime;
            TrySyncArk();
        }

        /// <summary>
        /// Bring this follower's Ark level with the host's: a world that built later fast-forwards its Ark by the
        /// difference (the pace has no state, so the same course stepped forward lands where the host's Ark is); one that
        /// set out earlier holds still for it. Server time is real time; the Ark sails in game time.
        /// </summary>
        void TrySyncArk()
        {
            var ark = Ark;
            if (_arkSynced || !ark || _hostUnderwayAt < 0 || _myUnderwayAt < 0) return;
            _arkSynced = true;
            float delta = (float)(_myUnderwayAt - _hostUnderwayAt);
            _report.ArkSyncSeconds = delta;
            if (delta > SyncDeadband) ark.AdvanceCourse(delta * Time.timeScale);
            else if (delta < -SyncDeadband) ark.HoldCourse(-delta * Time.timeScale);
        }

        // ── Shared fate ──────────────────────────────────────────────────────

        /// <summary>
        /// A follower's own Ark fell or arrived: it does not end the escort, the host's outcome does. Holds (the Ark
        /// stays where it is) and remembers what happened here; false on the leader, which ends as it always has.
        /// </summary>
        bool FollowerHolds(ArkEscortReport.Outcome local)
        {
            if (!_following) return false;
            if (_report.LocalOutcome == ArkEscortReport.Outcome.None) _report.LocalOutcome = local;
            if (_holdSince < 0f) _holdSince = Time.unscaledTime;
            Banner(local == ArkEscortReport.Outcome.Fallen
                ? "THE ARK IS DOWN - HOLDING FOR THE HOST"
                : "THROUGH - HOLDING FOR THE HOST");
            return true;
        }

        /// <summary>
        /// The host's escort ended with <paramref name="outcome"/>: this follower ends with it (survived, fallen in the
        /// host's leg, or abandoned), whatever its own Ark did.
        /// </summary>
        public void EndWithHost(ArkEscortReport.Outcome outcome, int fallenLeg, bool overrun)
        {
            if (!_active || !_following) return;
            _report.HostOutcome = outcome;
            switch (outcome)
            {
                case ArkEscortReport.Outcome.Survived:
                    _survived = true;
                    _fallen = false;
                    Banner(TemplateText.Fill(_escort.SurvivedLine, "difficulty", _difficulty.ToString(),
                        "hull", Ark ? Mathf.RoundToInt(Ark.HealthFraction * 100f).ToString() : "0"));
                    break;
                case ArkEscortReport.Outcome.Fallen:
                    _fallen = true;
                    _survived = false;
                    _report.Overrun = overrun;
                    _report.FallenLeg = fallenLeg;
                    int leg = Mathf.Clamp(fallenLeg, 0, _escort.Legs.Count - 1);
                    Banner(TemplateText.Fill(_escort.FallenLine, "name", _escort.Legs[leg].Name,
                        "leg", (leg + 1).ToString(), "legs", _escort.Legs.Count.ToString()));
                    break;
                default:
                    GiveUp("the host's escort ended");
                    return;
            }
            CloseLeg(_leg);
            if (_sea && _sea.IsRunning) _sea.End(returnToCell: true, "the host's escort ended");
            else Finish(_survived ? ArkEscortReport.Outcome.Survived : ArkEscortReport.Outcome.Fallen);
        }

        /// <summary>A follower that has held too long, or lost the session, ends with its own outcome.</summary>
        void TickCoop()
        {
            if (!_following || !_active) return;
            var nm = NetworkManager.Singleton;
            bool lost = nm == null || !nm.IsListening;
            bool waited = _holdSince >= 0f && Time.unscaledTime - _holdSince > HostOutcomeTimeout;
            if (!lost && !waited) return;
            var local = _report.LocalOutcome;
            _following = false;   // from here on this machine's own outcome stands
            if (local == ArkEscortReport.Outcome.Survived) { _survived = true; _endAt = Time.time; }
            else if (local == ArkEscortReport.Outcome.Fallen) { _fallen = true; CloseLeg(_leg); _sea.End(returnToCell: true, "the host never answered"); }
            else if (lost) { _sea.EndsOnArkFall = true; }
        }
    }
}
