using System.Collections.Generic;
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
        [SerializeField, Min(1f)] float startLineStandoff = 220f;

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

        protected override string ModeName => "Skein";

        public override int AuthoredGateTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSkeinRingTarget()
                : EndConditionOverridesSO.DefaultSkeinRingTarget;
        }

        /// <summary>
        /// The cable's rings, in cell-local coordinates.
        ///
        /// <para><paramref name="seed"/>, <paramref name="inner"/> and <paramref name="outer"/>
        /// are IGNORED, and that is the design rather than an oversight: this mode's course is a
        /// property of the arena, which is authored on the cell config, so a seed or a shell
        /// invented here could only disagree with it. <paramref name="gateCount"/> IS honoured -
        /// it is the authored race length, and the platform hands the same number to the
        /// monitor. The retry mirrors
        /// <c>SpawnableSkein.BuildEnvironment</c> exactly - same base seed, same 7919 stride,
        /// same six attempts - because the arena takes the first seed that lays a full course and
        /// the controller has to land on the same one.</para>
        /// </summary>
        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            if (!TryResolveArena(out var arena, out string why))
            {
                // Reported, not logged: the platform retries this every frame, so a LogError
                // here is a per-frame path. Until the window closes this is simply "not yet".
                CourseFailureDetail = why;
                return null;
            }

            // The AUTHORED target drives the course, so the finish line and the number of rings
            // laid cannot drift - the platform reads the same number for the monitor's target.
            var settings = arena.CourseSettings;
            settings.GateCount = Mathf.Max(3, gateCount);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                var build = SkeinCourse.BuildAll(unchecked(arena.CableSeed + attempt * 7919), settings);
                if (build == null) continue;

                // The generator works about the ORIGIN and Cell parents the environment container
                // at localPosition zero, so the cable's frame is the CELL's - not the prefab's,
                // which is an asset and never moves.
                var course = new List<RaceGate>(build.Gates.Count);
                for (int i = 0; i < build.Gates.Count; i++)
                {
                    var g = build.Gates[i];
                    course.Add(new RaceGate(g.Position, g.Axis, g.Radius));
                }
                return course;
            }

            CourseFailureDetail =
                $"Could not lay a {settings.GateCount}-ring cable at N={settings.StrandCount} " +
                "in six seeds - this one will not fix itself by waiting. Check " +
                "Tools/Build/skein_budget.py against these settings.";
            return null;
        }

        /// <summary>
        /// The cable's authored settings, off the cell config.
        ///
        /// <para>ExpectedConfig, never Config: both callers run during the first second, and a
        /// cell does not latch its config until <c>Initialize</c>. See the class remarks.</para>
        /// </summary>
        bool TryResolveArena(out SpawnableSkein arena, out string why)
        {
            arena = null;
            var cell = arenaCell != null ? arenaCell : FindAnyObjectByType<Cell>();
            var config = cell != null ? cell.ExpectedConfig : null;

            if (config == null)
            {
                why = "No Cell whose config is knowable - the cable's settings live on the cell " +
                      "config. A Skein cell must be IntensityWise (its choice is derivable from " +
                      "the intensity the server already holds); a Random multi-config cell " +
                      "cannot answer before it rolls, by design.";
                return false;
            }

            arena = config.EnvironmentPrefab as SpawnableSkein;
            if (arena == null)
            {
                why = $"The cell config '{config.name}' authors no SpawnableSkein " +
                      "EnvironmentPrefab, so there is no cable to hang rings on.";
                return false;
            }

            why = null;
            return true;
        }

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
        /// While ATTACHED, aim down the pilot's own rail rather than at the ring.
        ///
        /// <para>The ride constrains POSITION and never attitude, so where an attached AI looks
        /// is also where it LAUNCHES when the ribbon runs out - and every break in this arena is
        /// aimed by construction, so a competent AI is one that holds the throttle and lets the
        /// geometry throw it. Off-rail this returns false and the platform's ordinary gate
        /// aiming takes over, which is exactly right: then the pilot IS flying.</para>
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var status = pilot?.Vessel?.VesselStatus;
            var tf = pilot?.Vessel?.Transform;
            if (status == null || tf == null || !status.IsAttached || status.AttachedPrism == null)
                return false;

            Vector3 along = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : tf.forward;
            target = tf.position + along * aiRailLeadDistance;
            return true;
        }
    }
}
