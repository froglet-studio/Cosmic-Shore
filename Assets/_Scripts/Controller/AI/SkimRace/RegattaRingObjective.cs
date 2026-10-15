using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta for the Skim Race pilot: Skim Race with the crystals swapped for RINGS.
    ///
    /// <list type="bullet">
    /// <item><b>Course</b> - the pilot's OWN domain's rail, from the prisms
    /// <see cref="SpawnableRegattaRails"/> actually laid, in lay order. The rails are the racing
    /// line: each lane runs within <c>laneOffset</c> (22 u) of the spine, and the spine crosses
    /// every ring plane at the centre along its axis, so skimming the rail threads every ring.
    /// They are super-shielded, which is what the pilot's shell-clearance model already assumes of
    /// Skim Race's track. A pilot whose domain has no lane (none ships) falls back to the first.</item>
    /// <item><b>Target</b> - the pilot's next ring (<see cref="GateRaceController.TryGetNextGate"/>,
    /// off its replicated gate count, so lapped circuits wrap), aimed where the pilot's OWN rail
    /// crosses the ring plane, lifted just clear of the rail's contact shell on its outer face
    /// (<see cref="TryLaneCrossing"/>) - never the ring's centre. The centre sits on the braid's
    /// spine, so a pilot flying at it cut across both rival rails; the rail runs 22 u from the
    /// centre, well inside every mouth (54-110 u), so skimming the rail threads the ring. The
    /// capture radius is a fraction of the mouth. A new leg is a new id.</item>
    /// <item><b>Rivals' rails are mass</b> (<see cref="IsCourseMass"/>): only this pilot's lane is
    /// the course, so the mass guard steers round the other two, and direct flight is off
    /// (<see cref="AllowsDirectFlight"/>) - a straight line between rings crosses the braid.</item>
    /// <item><b>Progress</b> - the pilot's own gates threaded, against the race length the
    /// turn monitor published (<c>GameDataSO.SwitchTargetCount</c>). Gates are flown
    /// individually, so the pilot's next ring is its own, never its team's.</item>
    /// </list>
    /// </summary>
    public sealed class RegattaRingObjective : SkimRaceObjective
    {
        /// <summary>Fraction of the mouth radius the pilot treats as "hit". The crossing test is
        /// the full mouth; aiming inside it is the margin a hull at 300 u/s needs.</summary>
        internal const float CaptureFractionOfMouth = 0.7f;

        /// <summary>How far outside the rail's contact shell the aim point sits, on the rail's outer
        /// face (its prisms' up points away from the spine). About the skim line's own gap.</summary>
        internal const float AimClearanceAboveShell = 3f;

        GateRaceController _race;
        Transform _ringTransform;
        RaceGateRing _ring;
        bool _aimResolved;
        Vector3 _aim;

        // The lane this pilot rides, kept from the course build: its domain names its prisms, and its
        // points find where it crosses each ring.
        bool _haveLane;
        Domains _laneDomain;
        readonly List<Vector3> _lanePoints = new();
        readonly List<Vector3> _laneUps = new();
        readonly List<float> _laneShellUp = new();

        public RegattaRingObjective(GameDataSO gameData) : base(gameData) { }

        public override bool TryBuildCourse(IVesselStatus status, out SkimRaceCourse course)
        {
            course = null;
            // The arena-ready gate holds the countdown until the rails are laid, and the pilot only
            // asks during the turn - but a partial lane would be a course with a hole in it, so a
            // lay still in progress is "not yet".
            if (PrismTrailBuilder.IsLayingInProgress) return false;

            var rails = Object.FindAnyObjectByType<SpawnableRegattaRails>();
            if (rails == null) return false;

            var lane = FindLane(rails.GetTrails(), status.Domain);
            if (lane == null) return false;

            var pts = new List<Vector3>(lane.Count);
            var nrm = new List<Vector3>(lane.Count);
            var rot = new List<Quaternion>(lane.Count);
            var half = new List<Vector3>(lane.Count);
            for (int i = 0; i < lane.Count; i++)
            {
                var prism = lane[i];
                if (prism == null) continue;
                var tr = prism.transform;
                pts.Add(tr.position);
                nrm.Add(tr.up);
                rot.Add(tr.rotation);
                // Super-shielded: the contact shell reaches 1.5 x the leaf (SkimRaceCourseSource).
                half.Add(tr.lossyScale * SkimRaceShell.SuperShieldHalfScale);
            }

            if (pts.Count < 3) return false;
            course = new SkimRaceCourse(pts, nrm, rot, half);

            _laneDomain = LaneDomain(lane);
            _lanePoints.Clear(); _lanePoints.AddRange(pts);
            _laneUps.Clear(); _laneUps.AddRange(nrm);
            _laneShellUp.Clear();
            for (int i = 0; i < half.Count; i++) _laneShellUp.Add(half[i].y);
            _haveLane = true;
            _aimResolved = false;
            return true;
        }

        /// <summary>Only this pilot's own lane is its course; a rival's super-shielded rail is mass.
        /// Before the lane is known every super-shielded prism counts as course (Skim Race's rule).</summary>
        public override bool IsCourseMass(IVesselStatus status, Prism prism) =>
            !_haveLane || prism.Domain == _laneDomain;

        /// <summary>Off: a straight line from rail to ring crosses the braid.</summary>
        public override bool AllowsDirectFlight => false;

        static Domains LaneDomain(List<Prism> lane)
        {
            for (int i = 0; i < lane.Count; i++)
                if (lane[i] != null) return lane[i].Domain;
            return default;
        }

        /// <summary>
        /// Where a closed lane crosses the plane of a ring (centre <paramref name="centre"/>, normal
        /// <paramref name="axis"/>) inside its mouth: the crossing nearest the centre, as the segment
        /// index it lies on and the interpolated point. False when the lane does not cross the plane
        /// within <paramref name="radius"/> - the caller then aims at the centre.
        /// </summary>
        public static bool TryLaneCrossing(IReadOnlyList<Vector3> lane, Vector3 centre, Vector3 axis, float radius,
            out int segment, out Vector3 point)
        {
            segment = -1;
            point = centre;
            int n = lane.Count;
            if (n < 2 || axis.sqrMagnitude < 1e-8f) return false;
            axis.Normalize();
            float best = radius * radius;
            float d0 = Vector3.Dot(lane[0] - centre, axis);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float d1 = Vector3.Dot(lane[j] - centre, axis);
                if ((d0 <= 0f && d1 > 0f) || (d0 >= 0f && d1 < 0f))
                {
                    float u = d0 / (d0 - d1);
                    Vector3 p = Vector3.Lerp(lane[i], lane[j], u);
                    float r2 = (p - centre).sqrMagnitude;
                    if (r2 <= best) { best = r2; segment = i; point = p; }
                }
                d0 = d1;
            }
            return segment >= 0;
        }

        /// <summary>The aim point for a ring: on the lane's crossing, lifted
        /// <see cref="AimClearanceAboveShell"/> clear of the shell on the rail's outer face.</summary>
        Vector3 ResolveAim(Transform gate)
        {
            Vector3 axis = _ring ? _ring.Axis : gate.forward;
            float mouth = _ring ? _ring.Radius : 40f;
            if (!_haveLane || !TryLaneCrossing(_lanePoints, gate.position, axis, mouth, out int seg, out Vector3 p))
                return gate.position;
            Vector3 up = _laneUps[seg];
            return p + up * (_laneShellUp[seg] + AimClearanceAboveShell);
        }

        /// <summary>The lane laid in <paramref name="domain"/>'s colour, else the first lane.</summary>
        static List<Prism> FindLane(List<Trail> trails, Domains domain)
        {
            if (trails == null) return null;
            List<Prism> first = null;
            for (int t = 0; t < trails.Count; t++)
            {
                var list = trails[t]?.TrailList;
                if (list == null || list.Count == 0) continue;
                first ??= list;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null) continue;
                    if (list[i].Domain == domain) return list;
                    break;   // a lane is one colour: its first live prism says which
                }
            }
            return first;
        }

        public override bool TryGetTarget(IVesselStatus status, Vector3 position, out SkimRaceTarget target)
        {
            target = default;
            if (_race == null) _race = Object.FindAnyObjectByType<GateRaceController>();
            var player = status.Player;
            if (_race == null || player?.RoundStats == null) return false;
            if (!_race.TryGetNextGate(player, out var gate) || gate == null) return false;

            if (gate != _ringTransform)
            {
                _ringTransform = gate;
                _ring = gate.GetComponent<RaceGateRing>();
                _aimResolved = false;
            }
            if (!_aimResolved)
            {
                _aim = ResolveAim(gate);
                _aimResolved = _haveLane;
            }

            target.Position = _aim;
            target.Radius = (_ring ? _ring.Radius : 40f) * CaptureFractionOfMouth;
            // The RAW count, not the ring: arriving at the same ring on the next lap is a new leg.
            target.Id = player.RoundStats.SwitchesThreaded + 1;
            return true;
        }

        public override int OwnProgress(IVesselStatus status) =>
            status.Player?.RoundStats?.SwitchesThreaded ?? 0;

        public override int SharedProgress(IVesselStatus status) => OwnProgress(status);

        public override int Remaining(IVesselStatus status) =>
            Mathf.Max(0, GameData.SwitchTargetCount - OwnProgress(status));

        public override void Reset()
        {
            _ringTransform = null;
            _ring = null;
            // The lane stays: the pilot keeps its built course across a race reset, and the lane is that course.
            _aimResolved = false;
        }
    }
}
