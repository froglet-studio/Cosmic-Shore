using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark escort in a party (Docs/Voyages/PARTY.md; ported by hand from Garrett's <c>claude/voyage-coop</c>, whose
    /// design this keeps). Each machine sails its own Ark through its own worlds and fauna (SLICE.md §5). The LEADER (the
    /// host, or a solo machine) tells the party with one <see cref="VoyageCourseKind.EscortSail"/> course: the difficulty,
    /// the voyage's seed (its roll of worlds and modifiers, each world's hostile roll and the corridor's headings), the
    /// host's home pose and the seat list; every friend's harbour sails the same escort from it as a FOLLOWER, in the seat
    /// the host's lobby gave them. The host's Ark pose keeps every friend's Ark where the host's is
    /// (<see cref="VoyageArkSync"/>). The host's end is everyone's (shared fate): a friend whose own Ark falls or comes
    /// through first holds for the host's outcome, and gives up waiting only when the session drops or the host never
    /// answers. Friends report their kills, so the host's scoreboard, sent back with the end, has every seat.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        /// <summary>A follower holding (its own Ark fell or came through) gives up waiting for the host after this long.</summary>
        const float HostOutcomeTimeout = 90f;

        readonly VoyageArkSync _arkSync = new();
        readonly List<long> _seatClients = new();
        readonly List<(IVesselStatus status, long client)> _humans = new();
        readonly List<VoyageRaidTally.Seat> _hostRows = new();
        bool _following;
        Pose? _home;
        float _holdSince = -1f;
        bool _reportsHooked;
        ArkEscortReport.Outcome _localOutcome;

        /// <summary>True when this escort follows the host's course (a friend's machine).</summary>
        public bool IsFollowing => _active && _following;

        /// <summary>The seats the host's last end named, with what each did (a friend's board shows these).</summary>
        public IReadOnlyList<VoyageRaidTally.Seat> HostRows => _hostRows;

        /// <summary>The leader picks the home and the seats; a follower takes the host's (and its seat in them).</summary>
        void BeginParty(IVesselStatus local, ref SavedSquad squad, VoyageCourse course)
        {
            _seatClients.Clear();
            _hostRows.Clear();
            _holdSince = -1f;
            _localOutcome = ArkEscortReport.Outcome.None;
            _following = course is { Kind: VoyageCourseKind.EscortSail };
            if (_following)
            {
                if (course.SeatClients != null) _seatClients.AddRange(course.SeatClients);
                squad = VoyageRules.WithSeat(squad, course.SeatOf(VoyageCourseLink.LocalClientId));
                _home = HomeOf(course);
                _arkSync.Listen();
                return;
            }
            _home = new Pose(local.Transform.position, local.Transform.rotation);
            _seatClients.AddRange(VoyageParty.SeatClients(Mathf.Clamp(squad.MySeat, 0, SavedSquad.SlotCount - 1)));
            HookReports(true);
        }

        static Pose? HomeOf(VoyageCourse course)
        {
            if (course is not { HasHome: true }) return null;
            var q = new Quaternion(course.HomeQx, course.HomeQy, course.HomeQz, course.HomeQw);
            if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f) q = Quaternion.identity;
            return new Pose(new Vector3(course.HomeX, course.HomeY, course.HomeZ), q.normalized);
        }

        /// <summary>The leader tells the party it set sail (no-op solo, on a follower, or with nobody connected).</summary>
        void AnnounceSail()
        {
            if (_report != null)
            {
                _report.Following = _following;
                int humans = 0;
                foreach (var c in _seatClients) if (c >= 0) humans++;
                _report.HumanSeats = Mathf.Max(1, humans);
            }
            if (_following || _aiLocal || !VoyageCourseLink.IsLeader) return;
            var course = new VoyageCourse
            {
                Kind = VoyageCourseKind.EscortSail,
                Difficulty = _difficulty,
                Seed = _voyageSeed,
                LegSerial = 1,
            };
            course.SeatClients.AddRange(_seatClients);
            if (_home is { } home)
                course.SetHome(home.position.x, home.position.y, home.position.z,
                    home.rotation.x, home.rotation.y, home.rotation.z, home.rotation.w);
            VoyageCourseLink.Send(course);
        }

        /// <summary>The leader's escort ended: every friend's ends with the same outcome, and sees every seat's row.</summary>
        void AnnounceEnd()
        {
            if (_following || _aiLocal || !VoyageCourseLink.IsLeader || _report == null) return;
            var course = new VoyageCourse
            {
                Kind = VoyageCourseKind.EscortEnd,
                Result = (int)_report.Result,
                FallenLeg = _report.FallenLeg,
                Overrun = _report.Overrun,
                LegSerial = 1,
            };
            foreach (var seat in _report.RunSeats)
            {
                if (seat == null || string.IsNullOrEmpty(seat.PlayerName)) continue;
                System.Enum.TryParse(seat.Vessel, true, out VesselClassType hull);
                course.Tally.Add(new VoyageRaidTally.Seat
                {
                    PlayerName = seat.PlayerName, Vessel = (int)hull, Kills = seat.Kills, Guarded = seat.KillsOnArk,
                });
            }
            VoyageCourseLink.Send(course);
        }

        /// <summary>Once a frame: the Ark's pose to or from the party, and a follower that lost its host ends on its own.</summary>
        void TickParty()
        {
            if (_aiLocal) return;
            _arkSync.Tick(Ark, leader: !_following, 1);
            if (!_following) return;
            bool lost = !VoyageCourseLink.IsFollower;
            bool waited = _holdSince >= 0f && Time.unscaledTime - _holdSince > HostOutcomeTimeout;
            if (!lost && !waited) return;
            // From here on this machine's own outcome stands.
            _following = false;
            _arkSync.Stop();
            if (_sea) _sea.EndsOnArkFall = true;
            switch (_localOutcome)
            {
                case ArkEscortReport.Outcome.Survived:
                    _survived = true;
                    _endAt = Time.time;
                    break;
                case ArkEscortReport.Outcome.Fallen:
                    _fallen = true;
                    CloseLeg(_leg);
                    _sea.End(returnToCell: true, lost ? "the session dropped" : "the host never answered");
                    break;
                default:
                    if (lost) GiveUp("the session dropped");
                    break;
            }
        }

        void EndParty()
        {
            _arkSync.Stop();
            HookReports(false);
            _holdSince = -1f;
        }

        // ── Shared fate ──────────────────────────────────────────────────────

        /// <summary>
        /// A follower's own Ark fell, was overrun or came through: it does not end the escort, the host's outcome does.
        /// Holds (the Ark stays where it is) and remembers what happened here. False on the leader, which ends as it always has.
        /// </summary>
        bool FollowerHolds(ArkEscortReport.Outcome local)
        {
            if (!_following) return false;
            if (_localOutcome != ArkEscortReport.Outcome.None) return true;
            _localOutcome = local;
            if (_report != null) _report.LocalOutcome = local;
            _holdSince = Time.unscaledTime;
            CloseLeg(_leg);
            var lines = _def && _def.Content ? _def.Content.Lines : null;
            Banner(local == ArkEscortReport.Outcome.Fallen ? ArkwayVoyageHud.Mood.Threat : ArkwayVoyageHud.Mood.Good,
                local == ArkEscortReport.Outcome.Fallen ? lines?.EscortHoldFallen : lines?.EscortHoldThrough);
            return true;
        }

        /// <summary>
        /// The host's escort ended (<see cref="VoyageCourseKind.EscortEnd"/>): this follower ends with the same outcome,
        /// whatever its own Ark did, and keeps the host's rows for its scoreboard.
        /// </summary>
        public void EndWithHost(VoyageCourse course)
        {
            if (!_active || !_following || course == null) return;
            _hostRows.Clear();
            if (course.Tally != null) _hostRows.AddRange(course.Tally);
            var outcome = (ArkEscortReport.Outcome)course.Result;
            switch (outcome)
            {
                case ArkEscortReport.Outcome.Survived:
                    _survived = true;
                    _fallen = false;
                    CloseLeg(_leg);
                    Banner(ArkwayVoyageHud.Mood.Good, TemplateText.Fill(_escort.SurvivedLine, "difficulty", _difficulty.ToString(),
                        "hull", Ark ? Mathf.RoundToInt(Ark.HealthFraction * 100f).ToString() : "0"));
                    break;
                case ArkEscortReport.Outcome.Fallen:
                    _fallen = true;
                    _survived = false;
                    _report.Overrun = course.Overrun;
                    int leg = Mathf.Clamp(course.FallenLeg, 0, Mathf.Max(0, _escort.Legs.Count - 1));
                    _report.FallenLeg = leg;
                    CloseLeg(_leg);
                    if (_escort.Legs.Count > 0)
                        Banner(ArkwayVoyageHud.Mood.Threat, TemplateText.Fill(_escort.FallenLine, "name", _escort.Legs[leg].Name,
                            "leg", (leg + 1).ToString(), "legs", _escort.Legs.Count.ToString()));
                    break;
                default:
                    GiveUp("the host's escort ended");
                    return;
            }
            if (_sea && _sea.IsRunning) _sea.End(returnToCell: true, "the host's escort ended");
            else Finish(_survived ? ArkEscortReport.Outcome.Survived : ArkEscortReport.Outcome.Fallen);
        }

        // ── Friends' kills reach the host's board ────────────────────────────

        void HookReports(bool on)
        {
            if (on == _reportsHooked) return;
            _reportsHooked = on;
            if (on) VoyageCourseLink.ReportReceived += OnFriendReport;
            else VoyageCourseLink.ReportReceived -= OnFriendReport;
        }

        /// <summary>The host heard a friend fell a creature in their own world: it goes on that friend's row.</summary>
        void OnFriendReport(ulong client, VoyagePartyReport report)
        {
            if (!_active || _following || _report == null || report is not { Kind: VoyagePartyReport.ReportKind.Kill }) return;
            if (string.IsNullOrEmpty(report.PlayerName)) return;
            int seat = SeatOfClient((long)client);
            var row = SeatStatsFor(_report.RunSeats, seat, ((VesselClassType)report.Vessel).ToString(), report.PlayerName);
            row.Kills++;
            if (report.NearArk) row.KillsOnArk++;
        }

        /// <summary>A follower's own kill in its own world, told to the host.</summary>
        void ReportKill(string killer, bool onArk)
        {
            if (!_following) return;
            var local = LocalVessel();
            if (local == null || local.PlayerName != killer) return;
            VoyageCourseLink.Report(new VoyagePartyReport
            {
                Kind = VoyagePartyReport.ReportKind.Kill,
                PlayerName = killer,
                Vessel = (int)local.VesselType,
                NearArk = onArk,
                LegSerial = 1,
            });
        }

        /// <summary>A toast on this escort's banner path (the harbour's, for a line that is not the escort's own).</summary>
        public void Toast(ArkwayVoyageHud.Mood mood, string line) => Banner(mood, line);

        // ── Everyone in the intro ────────────────────────────────────────────

        List<(IVesselStatus status, long client)> OtherHumans()
        {
            VoyageParty.OtherHumans(_context?.GameData, _humans);
            return _humans;
        }

        int SeatOfClient(long client)
        {
            for (int i = 0; i < _seatClients.Count; i++)
                if (_seatClients[i] == client) return i;
            return -1;
        }
    }
}
