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
    /// off its replicated gate count, so lapped circuits wrap). The capture radius is a fraction
    /// of the mouth so the pilot aims through it rather than at its rim. A new leg is a new id.</item>
    /// <item><b>Progress</b> - the pilot's own gates threaded, against the race length the
    /// turn monitor published (<c>GameDataSO.SwitchTargetCount</c>). Gates are flown
    /// individually, so the pilot's next ring is its own, never its team's.</item>
    /// </list>
    /// </summary>
    public sealed class RegattaRingObjective : SkimRaceObjective
    {
        /// <summary>Fraction of the mouth radius the pilot treats as "hit". The crossing test is
        /// the full mouth; aiming inside it is the margin a hull at 300 u/s needs.</summary>
        const float CaptureFractionOfMouth = 0.7f;

        GateRaceController _race;
        Transform _ringTransform;
        RaceGateRing _ring;

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
                half.Add(tr.lossyScale * 1.5f);
            }

            if (pts.Count < 3) return false;
            course = new SkimRaceCourse(pts, nrm, rot, half);
            return true;
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
            }

            target.Position = gate.position;
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
        }
    }
}
