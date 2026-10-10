using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta - the ARENA race: every playable hull on the same closed circuit of switch rings,
    /// with three super-shielded rails (one per domain) braided along the racing line. Every
    /// pilot flies LAPS of it in order. The race ends when the first pilot threads the last gate
    /// of the last lap, and the TEAM with the most gates threaded - every pilot's gates, summed
    /// (<see cref="RegattaScoringRuleSO"/>) - wins. Opponent AI fly the card's pinned
    /// <c>OpponentAIVessel</c> (the Squirrel) until the racing AI can drive every hull; ally AI
    /// fly what their teammates pick on the launch panel.
    ///
    /// <para><b>The mode is a question about what your hull is FOR.</b> A Rhino ramps to 1200 u/s
    /// on a straight and pays five seconds for every corner it cannot hold; a Manta trades Soar
    /// for yaw one trigger at a time; a Scarab's ceiling is its Time level; an Urchin latches onto
    /// the rail in its colour and lets the cable drive at 300 u/s through corners that cost it
    /// nothing; a Squirrel skims the same rail for the boost energy that is its only speed. The
    /// rails are the platform's ordinary conserved mass, super-shielded so a rider's speed
    /// resource cannot be shot out from under them (<see cref="SpawnableRegattaRails"/>).</para>
    ///
    /// <para><b>The grid is balanced by the CARD, not by the mode.</b> A 35 u/s Sparrow and a
    /// 1200 u/s Rhino cannot be equalised by any course, so the card authors per-hull STARTING
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
        [SerializeField, Min(1f)] float startLineStandoff = RegattaCourseSource.SceneStartLineStandoff;

        [Tooltip("Radius of the start ring around gate 0's axis. Wider than the mouth so the " +
                 "grid is not stacked on the rails.")]
        [SerializeField, Min(0f)] float startLineRadius = 120f;

        [Tooltip("How far down its OWN rail an attached AI aims. Far enough that the range falls " +
                 "every frame (which resets OrbitDetector) and the bearing stays near the tangent.")]
        [SerializeField, Min(1f)] float aiRailLeadDistance = 260f;

        protected override string ModeName => "Regatta";

        protected override RaceCourseSource CreateCourseSource() => new RegattaCourseSource();

        /// <summary>Points, not golf: a pilot scores their own gates and a team the SUM of its
        /// pilots' (<see cref="RegattaScoringRuleSO"/>), highest total winning - so the end-game
        /// domain totals, which sum Score, are the team standings the rule decided on.</summary>
        protected override bool UseGolfRules => false;

        /// <summary>The team score is a SUM of pilots' gates (<see cref="RegattaScoringRuleSO"/>),
        /// so halfway / home stretch / final lap are measured against the leading team's
        /// combined courses - its pilot count times the course - not one pilot's 24 gates, which
        /// a two-pilot team "reached halfway" on with each pilot a quarter of the way round.</summary>
        protected override bool RaceToastsSumTeams => true;

        /// <summary>
        /// The arena prefab, off the cell's EXPECTED config - never <c>Config</c>, which is a latch
        /// that lands a second after the first build attempt (SKEIN.md 13.2). Requires an
        /// IntensityWise cell, whose choice is derivable from the intensity the server holds.
        /// </summary>
        protected override CellConfigDataSO ResolveCourseConfig()
        {
            var cell = arenaCell != null ? arenaCell : FindAnyObjectByType<Cell>();
            return cell != null ? cell.ExpectedConfig : null;
        }

        bool TryResolveArena(out SpawnableRegattaRails arena, out string why) =>
            RegattaCourseSource.TryResolveArena(ResolveCourseConfig(), out arena, out why);

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
        /// The Urchin's kit, for every AI Urchin on the circuit (<see cref="UrchinAutopilotDriver"/>,
        /// shared with Skein and Hijack). Created on first use rather than in a field initializer,
        /// so it loads its config on the main thread during play rather than while Unity
        /// deserialises the scene.
        /// </summary>
        UrchinAutopilotDriver _urchinAI;
        UrchinAutopilotDriver UrchinAI => _urchinAI ??= new UrchinAutopilotDriver();

        /// <summary>
        /// While ATTACHED, ride the rail toward this pilot's next ring; while FREE, fly at the
        /// ring the platform's way (and lay a Track Projector rail when the ring is a long,
        /// straight, lined-up shot). Skein's override, on Regatta's course.
        ///
        /// <para><b>Why riding is not just "aim down the rail".</b> All three rails thread every
        /// ring, so a pilot on its OWN rail need only hold the throttle and let the cable drive -
        /// and <see cref="UrchinAutopilotDriver.TryRideToward"/> answers exactly that (every rail
        /// threads the objective ahead, so it RIDES, aiming down the rail's own tangent). What the
        /// old aim-down-the-rail override could not do was CHOOSE. The braid lays the three lanes
        /// 22 u apart, so an Urchin latches whichever it touches first; on a rival's lane it
        /// crawled at 20 u/s for the rest of the race, and on its own lane facing the wrong way
        /// round the closed loop it rode most of a lap backwards to reach the ring it had just
        /// passed under. Now (the rings' capture radius - 0.8 of a 54-110 u mouth - always
        /// contains a lane 22 u off the spine, so every lane "threads" every ring):</para>
        /// <list type="bullet">
        /// <item><b>A rival's lane is left.</b> Regatta's rails are super-shielded, so no spike can
        /// convert them (<see cref="UrchinAutopilotDriver.IsConvertible"/>) and the crawl is
        /// always "dry": the pilot Slips off unless the ring is a short crawl away. It is never
        /// spiked - a volley at a super-shielded rail would only spend the meter. With the Time-5
        /// Slipstream the rival's lane rides at full pace, is not a crawl, and is ridden.</item>
        /// <item><b>The wrong way round is reversed.</b> The ring threads BEHIND, so the pilot
        /// swings its nose round - the ride's direction is the pilot's facing.</item>
        /// <item><b>A stall is Slipped.</b> A reversal caught in the throttle deadband parks the
        /// ride; past <c>ParkedSeconds</c> the pilot lets go.</item>
        /// <item><b>Spikes still fire at convertible hostile mass</b> - anything other than the
        /// rails a pilot latches in this arena - exactly as in Skein and Hijack.</item>
        /// </list>
        ///
        /// <para>The course logic stays Regatta's: the ring is the same <see cref="_course"/> entry,
        /// through the same lap-wrapping <see cref="GateRaceController.RingIndexFor(int)"/>, that the
        /// platform's own gate aiming flies at. Every aim the driver returns while riding is down
        /// the rail's tangent, which keeps the range falling (OrbitDetector stays reset) and the
        /// authored <c>ram</c> throttle engaged. Off-rail this returns false and the platform's
        /// gate aiming takes over, including the crystal detour.</para>
        ///
        /// <para>Every other hull is untouched: none of them attaches, and the driver finds every
        /// control by capability, so it presses nothing on a hull that binds none of them.</para>
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
                // Finished, or the course has not landed: ride on down the rail.
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

        public override void OnNetworkDespawn()
        {
            _urchinAI?.Clear();
            base.OnNetworkDespawn();
        }
    }
}
