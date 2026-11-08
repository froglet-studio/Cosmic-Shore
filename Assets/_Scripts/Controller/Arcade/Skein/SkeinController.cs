using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skein - the Urchin-only cable race, and the first gate race flown by RIDING rather than
    /// by flying.
    ///
    /// <para>Everything about running a gate race - the broadcast, the rings, the crossing
    /// detection, the optimistic report/reconcile round trip, the final scores - lives in
    /// <see cref="GateRaceController"/>, shared with Switchback and Headlong. This class is what
    /// makes it SKEIN, and it is deliberately three things: where the course comes from, that it
    /// is flown once, and how an AI aims while it is attached to a rail.</para>
    ///
    /// <para><b>The course is the ARENA'S, not this controller's.</b> The rings sit on the cable
    /// the cell built, so re-deriving them from a seed of our own would be two derivations of one
    /// course - which is exactly the drift that produces rings floating beside the rails they are
    /// supposed to be on. Instead this reads the cable's own authored settings off the cell
    /// config and re-runs the identical deterministic generation, including the same seed
    /// retry: both sides are pure functions of the same authored inputs, so they agree with no
    /// communication and nothing to wait for.</para>
    ///
    /// <para>That last point is why this does NOT poll for the arena. An earlier cut searched the
    /// scene for the <see cref="SpawnableSkein"/> component and found nothing, ever - because
    /// <c>SpawnableBase.Spawn()</c> does not instantiate itself, it lays prisms into a plain
    /// container and stays a prefab asset. The rings and the objective arrow never appeared in a
    /// single match.</para>
    ///
    /// <para>Its replacement had the same shape and shipped the same symptom: <c>Cell.Config</c>
    /// is not "this cell's configuration", it is "the configuration this cell has LATCHED", and
    /// it latches a full second after this runs. The platform calls <see cref="BuildCourse"/>
    /// exactly ONCE, so a null read is not a slow read, it is a race that never runs again -
    /// no rings, no scoring, no turn end, and one error on the host console. What answers this
    /// early, on the server, before a prism is laid, is <c>Cell.ExpectedConfig</c>: the config
    /// this cell WILL choose, derived from the same intensity the server already holds.</para>
    /// </summary>
    public class SkeinController : GateRaceController, IPlayerSpawnLine
    {
        [Header("Skein arena")]
        [Tooltip("The scene Cell whose config carries the cable. Left empty this falls back to a " +
                 "scene search; the reference is preferred because a satellite Cell (the arcade " +
                 "card's preview) is also a Cell and a search could find the wrong one.")]
        [SerializeField] Cell arenaCell;

        [Header("Skein start line")]
        [Tooltip("How far BEHIND the start collar the pilots line up. Far enough that the ring " +
                 "reads as something to fly at rather than something they are already inside.")]
        [SerializeField, Min(1f)] float startLineStandoff = RaceCourseSource.DefaultStartLineStandoff;

        [Tooltip("Radius of the ring the pilots stand on, about the collar's axis. Must stay " +
                 "well inside the collar's own 150 u mouth: everyone threads the first gate by " +
                 "flying straight forward, so the start asks nothing and the race begins at the " +
                 "first real decision instead of at a steering test.")]
        [SerializeField, Min(0f)] float startLineRadius = 90f;

        [Header("Skein AI")]
        [Tooltip("How far down its OWN rail an attached AI aims. Far enough that the range falls " +
                 "every frame (which resets OrbitDetector) and the bearing stays near the tangent " +
                 "(which holds LookingAtCrystal, so the authored ram keeps the grind at 150 u/s).")]
        [SerializeField, Min(1f)] float aiRailLeadDistance = 260f;

        [Header("Skein AI: the rail seek")]
        [Tooltip("A FREE AI Urchin flies to catch a rail that threads its next ring when the flight to the rail plus " +
                 "the ride from it beats this share of the straight flight to the ring. Off (the shipped steering " +
                 "before 2026-11-08) the Urchin only rides what it happens to touch and never finishes I3/I4 in the " +
                 "model (Docs/Reports/SKEIN_URCHIN_AI.md).")]
        [SerializeField] bool aiSeekRails = true;

        [Tooltip("The seek must beat this fraction of the straight flight's time to the ring.")]
        [SerializeField, Range(0.1f, 1f)] float aiSeekGain = 0.9f;

        [Tooltip("Rail arc (units) a seek candidate may ride to the ring.")]
        [SerializeField, Min(50f)] float aiSeekArc = 1600f;

        [Tooltip("Fraction of the ring's mouth inside which a rail counts as threading it.")]
        [SerializeField, Range(0.1f, 1f)] float aiSeekCaptureFraction = 0.8f;

        [Tooltip("The pilot flies to the rail no more than acos(this) off the rail's own heading, so it latches it " +
                 "riding the race's way.")]
        [SerializeField, Range(-1f, 1f)] float aiSeekApproachCos = 0.3f;

        [Tooltip("Prisms past the chosen rail point the pilot aims at, so it joins the rail going its way.")]
        [SerializeField, Min(0)] int aiSeekAheadPrisms = 3;

        [Tooltip("Seconds between seek decisions (and at once on a new ring).")]
        [SerializeField, Min(0.05f)] float aiSeekIntervalSeconds = 0.25f;

        [Tooltip("The free speed floor (u/s) the seek prices the flight at: the hull's cruise.")]
        [SerializeField, Min(1f)] float aiSeekCruiseSpeed = 65f;

        [Tooltip("The own-colour grind speed (u/s) the seek prices the ride at.")]
        [SerializeField, Min(1f)] float aiSeekRideSpeed = 300f;

        [Tooltip("Seconds after leaving a rail during which the seek does not pick it again (the game's re-latch " +
                 "grace).")]
        [SerializeField, Min(0f)] float aiSeekReattachGraceSeconds = 0.35f;

        protected override string ModeName => "Skein";

        /// <summary>
        /// Skein flies Regatta's gate aim (commit from the approach point, through the centre, re-attack at once after a
        /// missed mouth). With the shipped aim an AI Urchin orbits the approach point 300 u out and crosses the pinned
        /// rings' 27-40 u mouths off-centre; with this and the rail seek every modelled race finishes (hold-out
        /// composite 19.13 -> 8.77, Docs/Reports/SKEIN_URCHIN_AI.md). Garrett, 2026-11-08.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() => new SkeinCourseSource();

        /// <summary>
        /// The cable's authored settings live on the cell config - the EXPECTED one, never
        /// <c>Config</c>: both callers run during the first second, and a cell does not latch its
        /// config until <c>Initialize</c>.
        /// </summary>
        protected override CellConfigDataSO ResolveCourseConfig()
        {
            var cell = arenaCell != null ? arenaCell : FindAnyObjectByType<Cell>();
            return cell != null ? cell.ExpectedConfig : null;
        }

        bool TryResolveArena(out SpawnableSkein arena, out string why) =>
            SkeinCourseSource.TryResolveArena(ResolveCourseConfig(), out arena, out why);

        /// <summary>
        /// Everyone starts just behind the start collar, aimed through it.
        ///
        /// <para>The platform's cell ring puts pilots on a great circle 1120 u out facing the
        /// CENTRE, which for a cable arena means facing a knot rather than facing the thing they
        /// are supposed to fly through first. Switchback fixes the same problem by moving its
        /// first gate onto the spawn ring's pole, and Headlong by rotating its whole circuit
        /// there; neither is available here, because Skein's rings sit on rails the arena builds
        /// and the two would have to rotate together. So the pilots move instead - which is also
        /// the only arrangement that can be CLOSE as well as fair.</para>
        ///
        /// <para>Fair by symmetry rather than by tuning: every slot is
        /// <c>sqrt(standoff^2 + radius^2)</c> from the collar and pointed at it, and the ring's
        /// phase is derived from the collar's own axis rather than authored.</para>
        ///
        /// <para>Answerable during the SPAWN CHAIN, which is the requirement that decides the
        /// implementation: <see cref="SkeinCourse.StartPose"/> reads the spine's closed form at
        /// arc 0 and needs no seed, no course and no cable. The fallback settings are exact
        /// rather than approximate - only <c>StrandCount</c> varies per intensity and the collar
        /// is on the SPINE - and exist so a cell that has not resolved yet still gets a start
        /// line rather than the cell ring.</para>
        /// </summary>
        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            // The intensity is deliberately NOT consulted in the fallback: only StrandCount
            // varies per intensity and the collar is on the SPINE, whose two radii are the same
            // at every setting - so any intensity's settings give the identical collar. Reading
            // one would add an injected-gameData dependency to the earliest call in the scene
            // for a value that cannot change the answer.
            var settings = TryResolveArena(out var arena, out _)
                ? arena.CourseSettings
                : SkeinCourseSettings.ForIntensity(1);

            SkeinCourse.StartPose(settings, out var collar, out var axis);

            // The generator works about the ORIGIN; the cell places it. Same offset the course
            // broadcast applies, for the same reason.
            collar += ResolveCellCentre();

            poses = CellSpawnFormation.BuildFacingRing(count, collar, axis,
                                                       startLineStandoff, startLineRadius);
            return true;
        }

        /// <summary>
        /// The Urchin's kit, for every AI on the cable (<see cref="UrchinAutopilotDriver"/>).
        /// Created on first use rather than in a field initializer, so it loads its config on the
        /// main thread during play rather than while Unity deserialises the scene.
        /// </summary>
        UrchinAutopilotDriver _urchinAI;
        UrchinAutopilotDriver UrchinAI =>
            _urchinAI ??= new UrchinAutopilotDriver(UrchinAutopilotConfigSO.LoadForMode(GameModes.Skein));

        /// <summary>
        /// While ATTACHED, ride the cable toward this pilot's next ring; while FREE, fly at it the
        /// platform's way (and lay a Track Projector rail when it is a long straight shot).
        ///
        /// <para>The ride constrains POSITION and never attitude, so where an attached AI looks
        /// is also where it LAUNCHES when the ribbon runs out - and every break in this arena is
        /// aimed by construction. What the AI could not do before is CHOOSE: above intensity 1
        /// most rings are pinned to one strand, and a pilot that only aims down whatever it
        /// touched threads the rings its strands happen to pass and no others.
        /// <see cref="UrchinAutopilotDriver.TryRideToward"/> walks the strand both ways from the
        /// pilot and rides it (it goes through the ring, or closes most of the way at grind
        /// speed), REVERSES onto it (the ring is back the way it came - the ride's direction is
        /// the pilot's facing), or SLIPS off it; and it taps the chain spikes while the strand
        /// underfoot is a rival's colour, so a hostile lane is converted rather than crawled.
        /// Every aim it returns while riding is down the strand's own tangent, which keeps the
        /// range falling (OrbitDetector stays reset) and <c>LookingAtCrystal</c> - and with it the
        /// authored <c>ram</c> throttle - engaged.</para>
        ///
        /// <para>Off-rail this returns false and the platform's ordinary gate aiming takes over,
        /// which is exactly right: then the pilot IS flying.</para>
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var status = pilot?.Vessel?.VesselStatus;
            var tf = pilot?.Vessel?.Transform;
            if (status == null || tf == null) return false;

            bool hasGate = TryGetAIGate(pilot, out var gate);

            if (status.IsAttached && status.AttachedPrism != null)
            {
                SeekStateFor(pilot).LastAttachedAt = status.AttachedPrism.transform.position;
                SeekStateFor(pilot).WasAttached = true;
                // Finished, or the course has not landed: ride on down the strand.
                if (!hasGate)
                {
                    Vector3 along = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : tf.forward;
                    target = tf.position + along * aiRailLeadDistance;
                    return true;
                }

                // False the frame it Slips off: fall through to flying at the ring.
                return UrchinAI.TryRideToward(pilot, gate.Position, gate.Radius, out target, aiRailLeadDistance);
            }

            if (hasGate) UrchinAI.TryProjectTrackToward(pilot, gate.Position, gate.Axis);
            return false;
        }

        /// <summary>The ring <paramref name="pilot"/> must thread next, from the same course and
        /// the same progress counter the platform's own gate aiming reads.</summary>
        bool TryGetAIGate(IPlayer pilot, out RaceGate gate)
        {
            gate = default;
            if (_course.Count == 0) return false;
            int index = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (index < 0 || index >= RaceLength) return false;
            gate = _course[RingIndexFor(index)];
            return true;
        }

        // ── the rail seek ────────────────────────────────────────────────

        sealed class SeekState
        {
            public bool WasAttached;
            public Vector3 LastAttachedAt;
            public int LeftRail = -1;
            public float ReattachUntil = -1f;
            public int Ring = -1;
            public float NextDecision;
            public bool HasAim;
            public Vector3 Aim;
        }

        readonly Dictionary<IPlayer, SeekState> _seek = new();
        List<Vector3[]> _seekRails;
        List<float[]> _seekArcs;
        bool _seekRailsTried;
        int _candidatesRing = -1;
        readonly List<SkeinRailSeek.Candidate> _candidates = new();

        SeekState SeekStateFor(IPlayer pilot)
        {
            if (!_seek.TryGetValue(pilot, out var state)) _seek[pilot] = state = new SeekState();
            return state;
        }

        /// <summary>
        /// A free AI Urchin flies to catch a rail that threads its next ring when that beats the straight flight
        /// (<see cref="SkeinRailSeek"/>); the gate aim otherwise. The model's seek line for line, re-decided every
        /// <see cref="aiSeekIntervalSeconds"/> and at once on a new ring.
        /// </summary>
        protected override bool TrySeekAim(IPlayer pilot, int ringNo, in RaceGate gate, Vector3 self, out Vector3 aim)
        {
            aim = default;
            if (!aiSeekRails) return false;
            var status = pilot?.Vessel?.VesselStatus;
            if (status == null || status.VesselType != VesselClassType.Urchin || status.IsAttached) return false;
            if (!TryResolveSeekRails()) return false;

            var state = SeekStateFor(pilot);
            float now = Time.time;
            if (state.WasAttached)
            {
                // Just left a rail: the game will not re-latch it for a moment, so neither does the seek.
                state.WasAttached = false;
                state.LeftRail = SkeinRailSeek.RailAt(_seekRails, state.LastAttachedAt, 2f);
                state.ReattachUntil = now + aiSeekReattachGraceSeconds;
            }

            if (now >= state.NextDecision || state.Ring != ringNo)
            {
                state.NextDecision = now + aiSeekIntervalSeconds;
                state.Ring = ringNo;
                if (_candidatesRing != ringNo)
                {
                    _candidatesRing = ringNo;
                    SkeinRailSeek.Candidates(_seekRails, _seekArcs, gate.Position,
                        gate.Radius * aiSeekCaptureFraction, aiSeekArc, 2, _candidates);
                }
                int exclude = now < state.ReattachUntil ? state.LeftRail : -1;
                state.HasAim = SkeinRailSeek.TryPick(_candidates, _seekRails, self, gate.Position,
                    Mathf.Max(aiSeekCruiseSpeed, status.Speed), aiSeekRideSpeed, aiSeekGain, aiSeekApproachCos,
                    aiSeekAheadPrisms, exclude, out state.Aim);
            }

            aim = state.Aim;
            return state.HasAim;
        }

        /// <summary>The cable's rails in world space, rebuilt once from the arena's own seed and settings (the arena
        /// is a prefab that lays prisms, never an instance to read them from) - the retry
        /// <c>SpawnableSkein.BuildEnvironment</c> and <see cref="SkeinCourseSource"/> both run.</summary>
        bool TryResolveSeekRails()
        {
            if (_seekRails != null) return true;
            if (_seekRailsTried) return false;
            _seekRailsTried = true;
            if (!TryResolveArena(out var arena, out _)) return false;

            var settings = arena.CourseSettings;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var build = SkeinCourse.BuildAll(unchecked(arena.CableSeed + attempt * 7919), settings);
                if (build == null) continue;
                Vector3 centre = ResolveCellCentre();
                _seekRails = new List<Vector3[]>(build.Positions.Count);
                _seekArcs = new List<float[]>(build.Positions.Count);
                foreach (var local in build.Positions)
                {
                    var world = new Vector3[local.Length];
                    for (int k = 0; k < local.Length; k++) world[k] = local[k] + centre;
                    _seekRails.Add(world);
                    _seekArcs.Add(SkeinRailSeek.ArcLengths(world));
                }
                return true;
            }
            return false;
        }

        public override void OnNetworkDespawn()
        {
            _seek.Clear();
            _seekRails = null;
            _seekArcs = null;
            _seekRailsTried = false;
            _candidatesRing = -1;
            _urchinAI?.Clear();
            base.OnNetworkDespawn();
        }
    }
}
