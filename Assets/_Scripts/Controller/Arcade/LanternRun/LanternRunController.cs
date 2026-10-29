using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lantern Run - the Angler-only circuit race, and the Angler's TIME card. A closed loop of rings is flown in LAPS;
    /// the first domain whose lead runner threads the last gate of the last lap wins (golf: finish time).
    ///
    /// <para><b>You travel by your school.</b> The Angler cruises at 40 u/s; it RACES by leapfrogging its tamed creatures
    /// (ANGLER.md): paint a lantern past the next ring, throw a group at it, dash after them through the mouth, throw the
    /// other group, dash again. Every lantern ring hangs a cluster of neutral prisms past its mouth to paint
    /// (<see cref="LanternRunCourse.Lanterns"/>); omni crystals in the cell grow the school; LT locks a rival and spends a
    /// creature to bite them. See <see cref="LanternRunCourse"/> and LANTERNRUN.md.</para>
    ///
    /// <para><b>What the mode adds:</b> the lanterns (<see cref="SpawnableLanternRunLanterns"/>, laid on every peer from
    /// the broadcast course in <see cref="OnCourseRaised"/>, Switchback's features shape) and the AI's leapfrog
    /// (<see cref="AnglerLeapfrogAIPolicySO"/>, on the hull). Everything else is the gate-race platform.</para>
    /// </summary>
    public class LanternRunController : GateRaceController
    {
        [Header("Lantern Run circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE length, so the circuit " +
                 "is laid with target/laps rings.")]
        [SerializeField, Min(1)] int laps = LanternRunCourseSource.DefaultLaps;

        [Header("Lanterns")]
        [Tooltip("Lays the lanterns past the rings (LanternRunCourse.Lanterns).")]
        [SerializeField] SpawnableLanternRunLanterns lanternsPrefab;

        /// <summary>u a lantern keeps outside the nucleus.</summary>
        const float NucleusMargin = 30f;

        protected override string ModeName => "Lantern Run";

        /// <summary>Regatta's gate steering: commit from the approach point, aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() => new LanternRunCourseSource { Laps = laps };

        /// <summary>The lanterns, hung on every peer from the course every peer received.</summary>
        protected override void OnCourseRaised()
        {
            if (!lanternsPrefab)
            {
                CSDebug.LogError("[Lantern Run] No lanterns prefab assigned - the race has nothing to paint. " +
                                 "Assign SpawnableLanternRunLanterns on the controller.");
                return;
            }
            var lanterns = Instantiate(lanternsPrefab, transform);
            lanterns.name = "LanternRunLanterns";
            ResolveShell(out float inner, out _);
            lanterns.SetCourse(_course, Intensity, ResolveCellCentre(), inner / innerRadiusNucleusFactor + NucleusMargin);
            var container = lanterns.Spawn(Intensity);
            if (container) container.name = "LanternRunLanterns (prisms)";
        }
    }
}
