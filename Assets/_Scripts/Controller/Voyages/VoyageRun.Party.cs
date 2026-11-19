using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A job voyage in a party (Docs/Voyages/PARTY.md). Each machine still sails its own Ark through its own worlds and
    /// fauna (SLICE.md §5); what makes it one voyage is what the host sends:
    /// <list type="bullet">
    /// <item><b>The same worlds in the same places.</b> The voyage's seed and the host's home pose ride every course, so a
    /// friend's corridor stands where the host's does, cell for cell.</item>
    /// <item><b>The same Ark.</b> The host's Ark pose, a few times a second (<see cref="VoyageArkSync"/>): each friend's
    /// own Ark is kept where the host's is, whatever their load times were.</item>
    /// <item><b>The seats.</b> The host's seat list: each friend sails the seat the lobby showed them in.</item>
    /// <item><b>One count.</b> On a Crystal Run each friend reports what they take in their own world; the host keeps the
    /// party's count and sends it back, so every objective box and scoreboard shows the same number and the goal is the
    /// party's.</item>
    /// <item><b>One fate.</b> The host's end is everyone's: a fallen Ark shows every friend the lost board, a docked one
    /// docks every friend sailing home, and each friend banks what they carry.</item>
    /// </list>
    /// A friend whose own Ark falls is out until the host's next leg, which they rejoin with a fresh Ark (no shared
    /// corridor then: the host's has moved on).
    /// </summary>
    public sealed partial class VoyageRun
    {
        const float TallySendSeconds = 0.25f;
        /// <summary>A friend's Ark that anchored while the host's sails on is held this long for the host's to arrive (the
        /// two Arks reach a cell a moment apart), then weighs anchor with it.</summary>
        const float AnchorGraceSeconds = 3f;

        readonly VoyageArkSync _arkSync = new();
        readonly List<long> _seatClients = new();
        readonly List<(IVesselStatus status, long client)> _humans = new();
        int _seed = 1;
        Pose? _home;
        int _legSerial;
        bool _shareCorridor = true;
        bool _reportsHooked;
        bool _tallyDirty;
        float _nextTallySend;

        static int NewSeed()
        {
            int s = System.Environment.TickCount ^ (int)(Random.value * int.MaxValue);
            return s == 0 ? 1 : s;
        }

        /// <summary>The leader picks the voyage's seed and seats; a friend takes them from the host's course.</summary>
        void BeginParty(VoyageCourse course)
        {
            _legSerial = 0;
            _home = null;
            _tallyDirty = false;
            _seatClients.Clear();
            if (_following && course != null)
            {
                _seed = course.Seed != 0 ? course.Seed : 1;
                if (course.SeatClients != null) _seatClients.AddRange(course.SeatClients);
                // A friend who embarks on a later leg (their own Ark fell, or they came late) sails their own corridor:
                // the host's has moved on, so neither its worlds nor its Ark can be matched.
                _shareCorridor = course.Kind == VoyageCourseKind.Embark || course.LegSerial <= 1;
                _arkSync.Listen();
                return;
            }
            _seed = NewSeed();
            _shareCorridor = true;
            _seatClients.AddRange(VoyageParty.SeatClients(Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1)));
            HookReports(true);
        }

        /// <summary>The seat this machine's pilot holds in the host's seat list, or -1.</summary>
        static int SeatIn(VoyageCourse course)
        {
            long me = VoyageCourseLink.LocalClientId;
            return course == null || me < 0 ? -1 : course.SeatOf(me);
        }

        /// <summary>A course the leader sends, carrying what a friend needs to sail the same voyage from any point.</summary>
        VoyageCourse Course(VoyageCourseKind kind)
        {
            var course = new VoyageCourse { Kind = kind, Seed = _seed, LegSerial = _legSerial };
            course.SeatClients.AddRange(_seatClients);
            if (_home is { } home)
                course.SetHome(home.position.x, home.position.y, home.position.z,
                    home.rotation.x, home.rotation.y, home.rotation.z, home.rotation.w);
            return course;
        }

        static Pose? HomeOf(VoyageCourse course)
        {
            if (course is not { HasHome: true }) return null;
            var q = new Quaternion(course.HomeQx, course.HomeQy, course.HomeQz, course.HomeQw);
            if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f) q = Quaternion.identity;
            return new Pose(new Vector3(course.HomeX, course.HomeY, course.HomeZ), q.normalized);
        }

        /// <summary>The first leg's corridor, seeded and placed: the leader from its own pose, a friend from the host's.</summary>
        void PlaceCorridor(IVesselStatus local, VoyageCourse course)
        {
            if (_sea.IsRunning) return;
            if (_following)
            {
                if (!_shareCorridor) return;
                if (_arkCfg != null) _arkCfg.Seed = _seed;
                var home = HomeOf(course);
                if (home.HasValue) _sea.HomeOverride = home;
                return;
            }
            if (_arkCfg != null) _arkCfg.Seed = _seed;
            _home = new Pose(local.Transform.position, local.Transform.rotation);
            _sea.HomeOverride = _home;
        }

        /// <summary>Once a frame: the Ark's pose to or from the party, and the count to it.</summary>
        void TickParty()
        {
            if (!_following || _shareCorridor)
            {
                _arkSync.Tick(Ark, leader: !_following, _legSerial, _sea ? _sea.AnchorRemaining : 0f);
                if (_following) FollowHostAnchor();
            }
            if (_following || !_tallyDirty || Time.unscaledTime < _nextTallySend) return;
            _tallyDirty = false;
            _nextTallySend = Time.unscaledTime + TallySendSeconds;
            var course = Course(VoyageCourseKind.Tally);
            course.Tally = _raid.Snapshot();
            VoyageCourseLink.Send(course);
        }

        /// <summary>A friend's anchor window is the host's (Docs/Voyages/PARTY.md §4): it ends when the host's ends.</summary>
        void FollowHostAnchor()
        {
            if (!_sea || !_sea.IsAnchored || !_arkSync.TryHostAnchor(_legSerial, out float hostLeft)) return;
            if (hostLeft > 0f)
            {
                if (Mathf.Abs(hostLeft - _sea.AnchorRemaining) > 0.25f) _sea.SetAnchorRemaining(hostLeft);
                return;
            }
            // The host sails: this Ark weighs anchor too, unless it only just anchored (the host's is still on its way).
            if (_sea.AnchorTotal - _sea.AnchorRemaining > AnchorGraceSeconds) _sea.SetAnchorRemaining(0f);
        }

        void EndParty()
        {
            _arkSync.Stop();
            HookReports(false);
            _tallyDirty = false;
        }

        // ── One count: friends report, the host counts and tells ─────────────

        void HookReports(bool on)
        {
            if (on == _reportsHooked) return;
            _reportsHooked = on;
            if (on) VoyageCourseLink.ReportReceived += OnFriendReport;
            else VoyageCourseLink.ReportReceived -= OnFriendReport;
        }

        /// <summary>The host heard a friend take a crystal or a creature in their own world.</summary>
        void OnFriendReport(ulong client, VoyagePartyReport report)
        {
            if (!_active || _following || !Raiding || report == null || report.LegSerial != _legSerial) return;
            if (string.IsNullOrEmpty(report.PlayerName)) return;
            var seat = _raid.SeatOf(report.PlayerName);
            if (seat == null) return;
            seat.Vessel = report.Vessel;
            switch (report.Kind)
            {
                case VoyagePartyReport.ReportKind.Crystal:
                    if (!_raid.Add(report.PlayerName, (Element)report.Element)) return;
                    CheckGoalToast();
                    break;
                case VoyagePartyReport.ReportKind.Kill:
                    _raid.Kill(report.PlayerName, report.NearArk);
                    break;
                default:
                    return;
            }
            _tallyDirty = true;
        }

        /// <summary>A friend's own take, sent to the host (counted here too, until the host's count replaces it).</summary>
        void ReportToHost(VoyagePartyReport.ReportKind kind, string playerName, Element element, bool nearArk)
        {
            if (!_following) return;
            var local = LocalVessel();
            VoyageCourseLink.Report(new VoyagePartyReport
            {
                Kind = kind,
                PlayerName = playerName ?? string.Empty,
                Vessel = local != null ? (int)local.VesselType : 0,
                Element = (int)element,
                NearArk = nearArk,
                LegSerial = _legSerial,
            });
        }

        /// <summary>The host's count reached a friend: it is this machine's count now.</summary>
        void TakeHostTally(VoyageCourse course)
        {
            if (!_active || !_following || course == null || course.LegSerial != _legSerial) return;
            _raid.Replace(course.Tally);
            CheckGoalToast();
        }

        // ── One fate ─────────────────────────────────────────────────────────

        /// <summary>
        /// The host's voyage ended. Docked: a friend sailing home docks now and banks what they carry. Fallen: the lost
        /// board, and the voyage ends. Anything else: a friend sailing home docks on their own; anywhere else their voyage
        /// ends with the host's.
        /// </summary>
        void EndWithHost(VoyageCourse course)
        {
            if (!_active) return;
            bool sailingHome = _state.Current is { IsReturn: true };
            switch (course.Outcome)
            {
                case VoyageCourseEnd.Docked when sailingHome && _state.AtSea && !_docked && !_fallen:
                    // The host is home: so is everyone escorting the same Ark.
                    OnRouteEnded();
                    if (_active && !_docked) GiveUp("the host's Ark docked");
                    return;
                case VoyageCourseEnd.Fallen when !_fallen:
                    _fallen = true;
                    StopIntro();
                    ShowLost(StakeByElement(), _state.Current);
                    GiveUp("the host's Ark fell");
                    return;
                default:
                    if (!sailingHome) GiveUp("the host's voyage ended");
                    return;
            }
        }

        /// <summary>A friend's open stop board (Crystal Run) closes when the host's next leg reaches them.</summary>
        static void CloseStopBoard()
        {
            if (VoyageScoreboard.Current is { Board: VoyageScoreboard.Kind.CrystalRun }) VoyageScoreboard.Hide();
        }

        /// <summary>A toast on this voyage's banner path (the harbour's, for a line that is not the run's own).</summary>
        public void Toast(ArkwayVoyageHud.Mood mood, string line) => VoyageToasts.Show(_sea, mood, line);

        // ── Everyone on the board and in the intro ───────────────────────────

        /// <summary>The other humans flying this voyage (not this machine's pilot, not an AI), with their seats.</summary>
        List<(IVesselStatus status, long client)> OtherHumans()
        {
            VoyageParty.OtherHumans(_context?.GameData, _humans);
            return _humans;
        }

        /// <summary>The seat <paramref name="client"/> flies in the host's seat list, or -1.</summary>
        int SeatOfClient(long client)
        {
            for (int i = 0; i < _seatClients.Count; i++)
                if (_seatClients[i] == client) return i;
            return -1;
        }
    }
}
