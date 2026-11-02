using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot - the Stoat's TIME race (<c>Arcade/SLINGSHOT.md</c>; Yash, 2026-10-27: "Stoat will use Slingshot and
    /// Meerkat will use Warpline"; Warpline was the Stoat's until then). A closed loop of
    /// switch rings is cut through the cell and every pilot flies LAPS of it in order; the first
    /// domain whose lead runner threads the last gate of the last lap wins, scored on finish TIME
    /// (golf).
    ///
    /// <para><b>The mode is a question about where to fall.</b> The Stoat cruises at 60 u/s and
    /// the barren race cell has nothing to skim, so the only speed past cruise is the pull of the
    /// attractor–repulsor wormhole pair it slings on its triggers (LT: attractor left, RT:
    /// attractor right; hold = size): up to 90 u/s more, toward the attractor and off the
    /// repulsor. A sling is a THROW - it drags the hull off its line - so every ring asks where
    /// the pull will have put you when it arrives. The pull moves only the Stoat that slung it
    /// (a vessel may not move an opposing vessel). See <see cref="SlingshotCourse"/>.</para>
    ///
    /// <para><b>The Orrery</b> (2026-11-03, <see cref="SlingshotOrrery"/>): the course's environment - a moon at every corner,
    /// belts, comet tails and shepherd moonlets up the intensities - laid on every peer from the course every peer received,
    /// by Blast Run's spawnable and moved by Blast Run's clock-driven runtime. The AI flies it on its shipped sling rule: a
    /// moon-relative press was measured worse (<c>Tools/Build/stoat_orbit_sim.py --orrery</c>).</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no cell of its own, no new
    /// scoring metric and no circuit generator - Redline's shape on Headlong's solver, with
    /// <see cref="SlingshotCourse"/> supplying the Stoat's cut. The autopilot slings through the
    /// replicated press path (<c>StoatSlingExecutor.AutopilotSling</c>): to turn, and for the warp alone on a
    /// long leg, since the orbit is the Stoat's whole kit (2026-11-02, <c>Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md</c>).</para>
    /// </summary>
    public class SlingshotController : GateRaceController
    {
        [Header("Slingshot circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = SlingshotCourseSource.DefaultLaps;

        [Header("The Orrery")]
        [Tooltip("Lays the Orrery's prisms (moons, belts, comet tails, shepherds): the same spawnable Switchback's Blast Run uses.")]
        [SerializeField] SpawnableSwitchbackFeatures featuresPrefab;

        // u the Orrery keeps outside the nucleus (Switchback's margin).
        const float NucleusMargin = 30f;
        static readonly Unity.Profiling.ProfilerMarker s_orrerySpawnMarker = new("Slingshot.Orrery.Spawn");

        readonly List<OrreryMoon> _moons = new();
        GameObject _orreryFeatures;
        GameObject _orreryPrisms;
        SwitchbackBlastRunRuntime _orreryClock;

        protected override string ModeName => "Slingshot";

        /// <summary>The corners' moons of the course in play (empty before a course is raised).</summary>
        public IReadOnlyList<OrreryMoon> Moons => _moons;

        /// <summary>
        /// The Orrery, hung on the course on every peer: the layout, its prisms (Blast Run's spawnable) and the clock that turns
        /// the shepherds (Blast Run's runtime).
        /// </summary>
        protected override void OnCourseRaised()
        {
            ClearOrrery();
            int intensity = Intensity;
            Vector3 centre = ResolveCellCentre();
            ResolveShell(out float inner, out _);
            float clearRadius = inner / innerRadiusNucleusFactor + NucleusMargin;
            var layout = SlingshotOrrery.Layout(_course, intensity, centre, clearRadius, _moons);

            Transform container = null;
            if (layout.Prisms.Count > 0)
            {
                if (!featuresPrefab)
                    CSDebug.LogError("[Slingshot] No features prefab assigned - intensity " + intensity +
                                     " is missing its Orrery. Assign SpawnableSwitchbackFeatures on the SlingshotController.");
                else
                {
                    var features = Instantiate(featuresPrefab, transform);
                    features.name = "SlingshotOrrery";
                    features.SetLayout(_course, intensity, layout, centre, clearRadius);
                    _orreryFeatures = features.gameObject;
                    using (s_orrerySpawnMarker.Auto()) _orreryPrisms = features.Spawn(intensity);
                    if (_orreryPrisms)
                    {
                        _orreryPrisms.name = "SlingshotOrrery (prisms)";
                        container = _orreryPrisms.transform;
                    }
                }
            }
            if (layout.Movers.Count > 0)
            {
                _orreryClock = new GameObject("SlingshotOrreryClock").AddComponent<SwitchbackBlastRunRuntime>();
                _orreryClock.transform.SetParent(transform, false);
                _orreryClock.Init(gameData, intensity, 0, _course.Count, () => RaceLength, layout, container, runTag: "Slingshot");
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Slingshot] Orrery I{intensity}: {_moons.Count} moons, {layout.Prisms.Count} prisms, {layout.Movers.Count} shepherds.");
        }

        void ClearOrrery()
        {
            _moons.Clear();
            if (_orreryClock) Destroy(_orreryClock.gameObject);
            if (_orreryPrisms) Destroy(_orreryPrisms);
            if (_orreryFeatures) Destroy(_orreryFeatures);
            _orreryClock = null;
            _orreryPrisms = null;
            _orreryFeatures = null;
        }

        public override void OnNetworkDespawn()
        {
            ClearOrrery();
            base.OnNetworkDespawn();
        }

        protected override RaceCourseSource CreateCourseSource() =>
            new SlingshotCourseSource { Laps = laps };

        /// <summary>
        /// Regatta's gate steering (<see cref="GateRaceController.AICommitFromApproachPoint"/>), as Warpline flew it for the
        /// Stoat: the scene's 180 u approach point lay outside its 160 u commit radius, Warpline's measured defect (no AI
        /// Stoat finished any of Yash's 11 Warpline races on the scene steering). The Stoat's Warpline-learned pair and sling
        /// numbers start its Slingshot variants (<c>Resources/AIBoostPolicies/StoatDipoleConfig_Slingshot</c>,
        /// <c>StoatSlingConfig_Slingshot</c>, the warp let go 250 u out) until its own Slingshot races replace them.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;
    }
}
