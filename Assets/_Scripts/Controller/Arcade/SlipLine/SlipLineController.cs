using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slip Line - the Honeyguide-only race, and the Honeyguide's TIME card (SLIP_LINE.md). The Honeyguide cruises at 60
    /// u/s, and a SLIP down a prism trail of a colour its battery holds zips it along that trail at 480 u/s for one
    /// charge. The Ratel seat makes the charges (about one every 15 s, in a colour it cannot choose), so the race is
    /// catching rails and spending charges well: three rails a leg at I1 (any charge rides), one coloured rail a leg at
    /// I2 (half of them stubs not worth a charge), rails that lean off the line at I3 (turn out to meet them), and
    /// chains at I4 (two rails, two colours, one ride for two rings) with the smallest rings.
    ///
    /// <para><b>What the mode adds:</b> the rings, the RAILS (one super-shielded prism trail per rail, in the Dynamo's Ruby,
    /// Jade and Gold) and the AI's coach. <b>What it does not:</b> nothing about the vessel. The slip (its contact, its aim
    /// cone, its reach, its zip and its exit boost), the battery and the Ratel are the shipped kit. The one Honeyguide
    /// change that came with this race is <see cref="HoneyguideCrew.RaceCoach"/>, which lets a race tell the AI's slip
    /// guard where the AI is going.</para>
    ///
    /// <para><b>Every peer lays the same rails</b> from the broadcast rings (<see cref="SlipLineCourse.RailsFor"/> hashes
    /// each ring's exact axis, which the broadcast carries bit for bit), so nothing new is replicated.</para>
    /// </summary>
    public class SlipLineController : GateRaceController, IPlayerSpawnLine, IHoneyguideRaceCoach
    {
        [Header("Slip Line")]
        [Tooltip("The prism every rail is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_slip_line_assets.py. Unset = no rails, and a warning.")]
        [SerializeField] Prism railPrefab;

        [Tooltip("How far along a rail, past the hull's foot on its line, the AI steers: it closes onto the line already " +
                 "running along it, inside the slip's aim cone.")]
        [SerializeField, Min(5f)] float aiRailLead = 40f;

        const string RailOwnerId = "SlipLine";

        readonly List<Rail> _rails = new();
        readonly List<Prism> _railPrisms = new();
        readonly List<Vector3> _points = new();
        readonly Dictionary<HoneyguideCrew, IPlayer> _coached = new();
        readonly Dictionary<IPlayer, HoneyguideCrew> _crews = new();
        readonly Dictionary<HoneyguideCrew, (int ring, int rail)> _plans = new();
        Transform _railRoot;

        protected override string ModeName => "Slip Line";

        protected override RaceCourseSource CreateCourseSource() => new SlipLineCourseSource();

        public override void OnNetworkDespawn()
        {
            foreach (var crew in _coached.Keys)
                if (crew && ReferenceEquals(crew.RaceCoach, this)) crew.RaceCoach = null;
            _coached.Clear();
            _crews.Clear();
            _plans.Clear();
            ClearRails();
            base.OnNetworkDespawn();
        }

        // ── The start line: abreast, the start standoff behind ring 0 ────

        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            // The course works about the ORIGIN; the cell places it (the same offset the course broadcast applies).
            poses = SlipLineCourse.StartPoses(count);
            Vector3 centre = ResolveCellCentre();
            for (int i = 0; i < poses.Length; i++) poses[i].position += centre;
            return true;
        }

        // ── The rails (every peer, from the broadcast rings) ─────────────

        protected override void OnCourseRaised()
        {
            ClearRails();
            _plans.Clear();
            var settings = SlipLineCourse.ForIntensity(Intensity);
            settings.RingCount = _course.Count;
            SlipLineCourse.RailsFor(_course, settings, _rails);
            if (_rails.Count == 0) return;
            if (!railPrefab)
            {
                CSDebug.LogWarning("[SlipLine] no rail prefab on the controller - the rails are not laid.");
                return;
            }
            _railRoot = new GameObject("SlipLineRails").transform;
            var scale = new Vector3(SlipLineCourse.PrismWidth, SlipLineCourse.PrismWidth, SlipLineCourse.PrismLength);
            for (int i = 0; i < _rails.Count; i++)
            {
                var rail = _rails[i];
                // one trail per rail: the slip reads its prisms in order, start to end
                var trail = new Trail();
                var rotation = Quaternion.LookRotation(rail.Direction);
                var domain = HoneyguideConfigSO.DomainOf(rail.Colour);
                SlipLineCourse.PrismsOf(rail, _points);
                for (int p = 0; p < _points.Count; p++)
                {
                    var prism = PrismTrailBuilder.LayOne(railPrefab,
                        new PrismLay(new SpawnPoint(_points[p], rotation, scale), domain, PrismKind.SuperShielded),
                        _railRoot, trail, RailOwnerId, admitAuthoredScale: true, watchForReveal: true);
                    if (prism) _railPrisms.Add(prism);
                }
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[SlipLine] intensity {Intensity}: {_course.Count} rings, {_rails.Count} rails, {_railPrisms.Count} prisms.");
        }

        void ClearRails()
        {
            for (int i = 0; i < _railPrisms.Count; i++)
            {
                var prism = _railPrisms[i];
                if (prism && !prism.destroyed && prism.ownerID == RailOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _railPrisms.Clear();
            _rails.Clear();
            if (_railRoot) Destroy(_railRoot.gameObject);
            _railRoot = null;
        }

        // ── The AI (server): the coach ───────────────────────────────────

        protected override void OnRacerArmed(IPlayer pilot)
        {
            var tf = pilot?.Vessel?.Transform;
            var crew = tf ? tf.GetComponentInChildren<HoneyguideCrew>(true) : null;
            if (!crew) return;
            _coached[crew] = pilot;
            _crews[pilot] = crew;
            crew.RaceCoach = this;
        }

        /// <summary>
        /// The AI's steering while it means to ride: a lead point down the rail it chose for this leg
        /// (<see cref="SlipLineCourse.ChooseRail"/>, decided once per leg from where the hull is when the leg begins), so
        /// the hull meets the rail already running along it. No rail (no charge of a colour worth spending here): the
        /// gate race's own steering.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            if (pilot == null || !_crews.TryGetValue(pilot, out var crew) || !crew) return false;
            if (!TryPlannedRail(crew, pilot, out var rail)) return false;
            var hull = crew.RaceHull;
            target = SlipLineCourse.RailPursuit(hull.position, rail, aiRailLead);
            return true;
        }

        protected override bool OverrideAimIsLead(IPlayer pilot) => true;

        /// <summary>Where the AI is going while it rides: past the ring its rail threads, so the slip guard sees the ride
        /// carry it nearer. No planned rail: no answer (the guard measures against the gate the race steers for).</summary>
        public bool TryRaceGoal(HoneyguideCrew crew, out Vector3 goal)
        {
            goal = default;
            if (!crew || !_coached.TryGetValue(crew, out var pilot) || pilot == null) return false;
            if (!TryPlannedRail(crew, pilot, out var rail)) return false;
            goal = rail.Ring + 1 < _course.Count
                ? _course[rail.Ring + 1].Position
                : _course[rail.Ring].Position + _course[rail.Ring].Axis * 200f;
            return true;
        }

        bool TryPlannedRail(HoneyguideCrew crew, IPlayer pilot, out Rail rail)
        {
            rail = default;
            var hull = crew.RaceHull;
            if (!hull || _course.Count == 0 || _rails.Count == 0) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            int next = RingIndexFor(threaded);
            if (next < 0 || next >= _course.Count) return false;

            if (!_plans.TryGetValue(crew, out var plan) || plan.ring != next)
            {
                plan = (next, SlipLineCourse.ChooseRail(hull.position, _course, _rails, next, crew.Battery, crew.BatteryCap));
                _plans[crew] = plan;
            }
            if (plan.rail < 0 || plan.rail >= _rails.Count) return false;
            rail = _rails[plan.rail];

            // the charge went (a Ratel card spent it), or the hull ran past the rail without catching it: fly the leg
            bool stillHeld = false;
            var battery = crew.Battery;
            for (int i = 0; i < battery.Count; i++) if (battery[i] == rail.Colour) { stillHeld = true; break; }
            float along = Vector3.Dot(hull.position - rail.Start, rail.Direction);
            if (!stillHeld || along > rail.Length - SlipLineCourse.RailPast)
            {
                _plans[crew] = (next, -1);
                return false;
            }
            return true;
        }
    }
}
