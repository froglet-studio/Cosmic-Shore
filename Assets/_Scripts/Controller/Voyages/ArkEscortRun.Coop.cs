using System.Collections.Generic;
using CosmicShore.Data;
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

        /// <summary>The leader's escort ended: every friend's ends too.</summary>
        void AnnounceEnd()
        {
            if (_following || !VoyageCourseLink.IsLeader) return;
            VoyageCourseLink.Send(new VoyageCourse { Kind = VoyageCourseKind.EscortEnd });
        }
    }
}
