using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta - the ARENA race: every playable hull on the same closed circuit of switch rings,
    /// with three super-shielded rails (one per domain) braided along the racing line. Every
    /// pilot flies LAPS of it in order; the first DOMAIN whose LEAD RUNNER threads the last gate
    /// of the last lap wins.
    ///
    /// <para><b>The mode is a question about what your hull is FOR.</b> A Rhino ramps to 1210 u/s
    /// on a straight and pays five seconds for every corner it cannot hold; a Manta trades Soar
    /// for yaw one trigger at a time; a Scarab's ceiling is its Time level; an Urchin latches onto
    /// the rail in its colour and lets the cable drive at 300 u/s through corners that cost it
    /// nothing; a Squirrel skims the same rail for the boost energy that is its only speed. The
    /// rails are the platform's ordinary conserved mass, super-shielded so a rider's speed
    /// resource cannot be shot out from under them (<see cref="SpawnableRegattaRails"/>).</para>
    ///
    /// <para><b>The grid is balanced by the CARD, not by the mode.</b> A 35 u/s Sparrow and a
    /// 1210 u/s Rhino cannot be equalised by any course, so the card authors per-hull STARTING
    /// ELEMENT levels (<c>SO_ArcadeGame.StartingElements</c>, a platform capability this mode
    /// introduced) from an offline lap-time model (<c>Tools/Build/regatta_balance.py</c>), and
    /// the comeback system does the rest during the race. Nothing here reads a hull, scales a
    /// speed, or knows which vessel is which - the controller is the gate-race platform plus
    /// the arena's rings.</para>
    ///
    /// <para><b>Everything about running the race is the platform's</b> (<see cref="GateRaceController"/>):
    /// the broadcast, the rings, the crossing test, the owner-detects/server-records round trip,
    /// the lead-runner fold, the AI steering. This class supplies the course (read off the arena
    /// prefab, Skein's pattern, so rails and rings cannot disagree), the lap count (derived from
    /// the authored target and the arena's ring count - one authority), a start line behind gate
    /// 0, and an attached AI's aim down its own rail.</para>
    /// </summary>
    public class RegattaController : GateRaceController, IPlayerSpawnLine
    {
        [Header("Regatta")]
        [Tooltip("The scene's Cell. Its EXPECTED config carries the arena prefab whose seed and " +
                 "intensity the rings are derived from. Optional - found if empty.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("How far behind gate 0, along its axis, the start line sits. Every hull spawns " +
                 "here pointed through the first ring, which is fair by symmetry.")]
        [SerializeField, Min(1f)] float startLineStandoff = 260f;

        [Tooltip("Radius of the start ring around gate 0's axis. Wider than the mouth so the " +
                 "grid is not stacked on the rails.")]
        [SerializeField, Min(0f)] float startLineRadius = 120f;

        [Tooltip("How far down its OWN rail an attached AI aims. Far enough that the range falls " +
                 "every frame (which resets OrbitDetector) and the bearing stays near the tangent.")]
        [SerializeField, Min(1f)] float aiRailLeadDistance = 260f;

        protected override string ModeName => "Regatta";

        /// <summary>Laps = authored gate threadings / the arena's rings per lap. ONE authority:
        /// the arena lays exactly RingsPerLap rings, the target is authored once in the end-game
        /// conditions, and the generator asserts the target is a whole number of laps.</summary>
        protected override int LapsPerRace =>
            Mathf.Max(1, AuthoredGateTarget() / RegattaCourse.RingsPerLap);

        public override int AuthoredGateTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetRegattaGateTarget()
                : EndConditionOverridesSO.DefaultRegattaGateTarget;
        }

        /// <summary>
        /// The arena's rings, cell-local. <paramref name="seed"/>, <paramref name="inner"/> and
        /// <paramref name="outer"/> are IGNORED by design: the circuit is a property of the ARENA
        /// (the rails are laid through it), authored on the cell config, so a seed or a shell
        /// invented here could only disagree with it. <paramref name="gateCount"/> is honoured
        /// through <see cref="LapsPerRace"/>.
        /// </summary>
        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            if (!TryResolveArena(out var arena, out string why))
            {
                // Reported, not logged: the platform retries this every frame until its window
                // closes, and until then this is simply "not yet".
                CourseFailureDetail = why;
                return null;
            }

            if (gateCount % RegattaCourse.RingsPerLap != 0)
                CSDebug.LogWarning($"[Regatta] Authored target {gateCount} is not a whole number of " +
                                   $"{RegattaCourse.RingsPerLap}-ring laps; racing " +
                                   $"{LapsPerRace} lap(s). Author a multiple of {RegattaCourse.RingsPerLap}.");

            return arena.BuildGatesNow();
        }

        /// <summary>
        /// The arena prefab, off the cell's EXPECTED config - never <c>Config</c>, which is a latch
        /// that lands a second after this runs (SKEIN.md 13.2). Requires an IntensityWise cell,
        /// whose choice is derivable from the intensity the server already holds.
        /// </summary>
        bool TryResolveArena(out SpawnableRegattaRails arena, out string why)
        {
            arena = null;
            var cell = arenaCell != null ? arenaCell : FindAnyObjectByType<Cell>();
            var config = cell != null ? cell.ExpectedConfig : null;

            if (config == null)
            {
                why = "No Cell whose config is knowable - the circuit's seed lives on the cell " +
                      "config's arena prefab. A Regatta cell must be IntensityWise.";
                return false;
            }

            arena = config.EnvironmentPrefab as SpawnableRegattaRails;
            if (arena == null)
            {
                why = $"The cell config '{config.name}' authors no SpawnableRegattaRails " +
                      "EnvironmentPrefab, so there are no rails to hang rings on.";
                return false;
            }

            why = null;
            return true;
        }

        /// <summary>
        /// Everyone starts on a ring behind gate 0, pointed through it. The platform's cell ring
        /// would put pilots 1120 u out facing the CENTRE with gate 0 on the pole above them,
        /// which for a grid of hulls whose cruise speeds span 35x is a first corner nobody asked
        /// for. Answerable during the spawn chain because the rings are a pure function of the
        /// prefab asset's seed and intensity; the fallback (no cell resolved yet) uses the
        /// default seed at the selected intensity, which is the shipped arena.
        /// </summary>
        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            var gates = TryResolveArena(out var arena, out _)
                ? arena.BuildGatesNow()
                : RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, Intensity);

            if (gates == null || gates.Count == 0)
            {
                poses = null;
                return false;
            }

            var first = gates[0];
            Vector3 centre = ResolveCellCentre();
            poses = CellSpawnFormation.BuildFacingRing(count, first.Position + centre, first.Axis,
                                                       startLineStandoff, startLineRadius);
            return true;
        }

        /// <summary>
        /// While ATTACHED, aim down the pilot's own rail rather than at the ring. The ride
        /// constrains position and the rail passes through every ring, so a competent riding AI
        /// is one that holds the throttle and lets the cable drive; aiming at the ring instead
        /// would have the pilot fighting the rail's own curve. Off-rail the platform's ordinary
        /// gate aiming takes over.
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
