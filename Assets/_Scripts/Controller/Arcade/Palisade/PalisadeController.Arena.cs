using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Palisade's arena in the world (PALISADE.md section 9): every peer lays <see cref="PalisadeArena"/>'s prisms for
    /// the rung, round the cell's centre, when the countdown ends (the intensity is synced by then, and the crystal
    /// scatter is laid at the same moment). They are scenery with the game's own prism rules: laid in
    /// <see cref="Domains.Blue"/>, the platform's no-team colour, so every blade cuts them, nobody's standing volume
    /// counts them (<c>StatsManager.PrismCreated</c> credits a roster name and these carry none), and their colour is
    /// the worn colour set's own. Nothing grows back. Nothing here reads or writes a vessel.
    ///
    /// <para>The building AI flies the lanes (<see cref="TryLaneTarget"/>): beside a row of posts, in blade reach, and
    /// out through its frame. That is steering only; the sword is the vessel's own hand, which lights near hostile
    /// prisms, so a builder now has something to light on and lays afterimages at the frame.</para>
    /// </summary>
    public partial class PalisadeController
    {
        const string ArenaOwnerId = "PalisadeArena";

        /// <summary>Where one AI is on its lane run: which lane, and which leg of it.</summary>
        sealed class LaneRun
        {
            public int Lane;
            public int Leg;          // 0 to the run's start, 1 beside the row, 2 out through the frame
            public float LegEnds;    // a leg not finished by then is given up (a rival's wall may stand in it)
        }

        readonly List<PalisadeArenaPart> _arenaParts = new();
        readonly List<PalisadeLane> _arenaLanes = new();
        readonly List<Prism> _arenaPrisms = new();
        readonly Dictionary<IPlayer, LaneRun> _laneRuns = new();
        Transform _arenaRoot;

        Vector3 ArenaCentre => arenaCell ? arenaCell.transform.position : Vector3.zero;

        /// <summary>Peer-local and identical everywhere: the layout is a pure function of the rung.</summary>
        void LayArena()
        {
            ClearArena();
            var prefab = settings ? settings.arenaPrism : null;
            if (!prefab)
            {
                CSDebug.LogWarning("[PalisadeController] PalisadeSettings has no arena prism; the arena is not laid.");
                return;
            }

            int intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;
            PalisadeArena.Lay(PalisadeArena.ForIntensity(intensity), _arenaParts, _arenaLanes);
            if (_arenaParts.Count == 0) return;

            _arenaRoot = new GameObject("PalisadeArena").transform;
            _arenaRoot.position = ArenaCentre;
            var trail = new Trail();
            for (int i = 0; i < _arenaParts.Count; i++)
            {
                var part = _arenaParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                // Laid after the arena-ready gate has cleared, so it must not join the reveal watch.
                var prism = PrismTrailBuilder.LayOne(prefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), Domains.Blue, part.Kind),
                    _arenaRoot, trail, ArenaOwnerId, admitAuthoredScale: true, watchForReveal: false);
                if (prism) prism.CompletesAsLiveRibbon = true;
                _arenaPrisms.Add(prism);
            }
        }

        void ClearArena()
        {
            for (int i = 0; i < _arenaPrisms.Count; i++)
            {
                var prism = _arenaPrisms[i];
                // A prism the pool has handed to someone else since is no longer ours to return.
                if (!prism || prism.ownerID != ArenaOwnerId) continue;
                if (!EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _arenaPrisms.Clear();
            _arenaParts.Clear();
            _arenaLanes.Clear();
            _laneRuns.Clear();
            if (_arenaRoot) Destroy(_arenaRoot.gameObject);
            _arenaRoot = null;
        }

        /// <summary>
        /// The point a building AI flies for on its lane run, or false when the rung has no lanes. Pilot
        /// <paramref name="seat"/> starts on lane <c>seat % lanes</c>, so builders spread over the lanes, and takes the
        /// next lane each time it comes out of a frame.
        /// </summary>
        bool TryLaneTarget(IPlayer pilot, int seat, Vector3 position, out Vector3 target)
        {
            target = default;
            if (_arenaLanes.Count == 0) return false;

            if (!_laneRuns.TryGetValue(pilot, out var run))
            {
                run = new LaneRun { Lane = seat % _arenaLanes.Count, LegEnds = Time.time + settings.aiLaneLegSeconds };
                _laneRuns[pilot] = run;
            }

            var centre = ArenaCentre;
            var lane = _arenaLanes[run.Lane];
            Vector3 legTarget = LegTarget(lane, run.Leg) + centre;

            bool reached = run.Leg == 2
                ? Vector3.Dot(position - (lane.FrameCentre + centre), lane.Axis) > 0f   // out through the frame's plane
                : (position - legTarget).sqrMagnitude <= settings.aiLaneArriveDistance * settings.aiLaneArriveDistance;
            if (reached || Time.time >= run.LegEnds)
            {
                // A leg given up on time abandons the lane, like one finished: the next lane, from its start.
                bool lastLeg = run.Leg == 2 || !reached;
                run.Leg = lastLeg ? 0 : run.Leg + 1;
                if (lastLeg) run.Lane = (run.Lane + 1) % _arenaLanes.Count;
                run.LegEnds = Time.time + settings.aiLaneLegSeconds;
                lane = _arenaLanes[run.Lane];
                legTarget = LegTarget(lane, run.Leg) + centre;
            }

            target = legTarget;
            return true;
        }

        /// <summary>A lane run's three targets, relative to the cell's centre: the run's start, the row's far end
        /// (still beside it), and a point past the frame so the hull flies through the mouth, not at it.</summary>
        static Vector3 LegTarget(PalisadeLane lane, int leg) => leg switch
        {
            0 => lane.RunStart,
            1 => lane.RunEnd,
            _ => lane.FrameCentre + lane.Axis * PalisadeArena.FrameGap,
        };
    }
}
