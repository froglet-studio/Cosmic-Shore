using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A <see cref="Crystal"/> drawn and collected as a RACE RING - the same
    /// <see cref="RaceGateRing"/> every gate race (Switchback, Redline, Headlong...) is flown
    /// through, standing square across the track, threaded by its mouth instead of touched.
    /// Skim Race's crystals are these.
    ///
    /// <para><b>The crystal stays the crystal; only its size and its trigger change.</b> Placement
    /// (one per pilot, an authored anchor plus a random jitter, the next anchor on every pickup),
    /// replication, the pickup's credit and effects, the AI's target and the objective arrow all
    /// belong to the crystal and its manager, untouched. The crystal itself still draws - shrunk
    /// to a small omni crystal hanging in the middle of the ring
    /// (<see cref="crystalWorldScale"/>), which is what says threading this ring pays what an
    /// omni crystal pays - but its collider is off: this component draws the ring around it and
    /// turns a threading of the mouth into the same impactor pair a physics contact would have
    /// dispatched. A second course system beside the crystal one would have re-derived every one
    /// of those pieces.</para>
    ///
    /// <para><b>Domain is the crystal's rule, not a new one.</b> The ring is painted the crystal's
    /// domain (<see cref="RaceGateRing.Owner"/>), and a pilot the crystal's impactor would refuse
    /// (<see cref="OmniCrystalImpactor.AcceptsDomain"/> - strict on Skim Race's
    /// <see cref="TeamCrystalImpactor"/>) flies straight through it with nothing happening on
    /// either side.</para>
    ///
    /// <para><b>Square across the track.</b> The mouth faces the tangent of the laid track at the
    /// ring's nearest point (<see cref="SkimRaceCourse"/>, built from the prisms the game actually
    /// laid), so a pilot flying straight down the track looks into the open ring, wherever the
    /// jitter put it and however the track bends. The crystals can spawn before the track is laid,
    /// so a ring raised early faces a placeholder axis and turns once the track exists - before the
    /// connecting panel lifts, because the track is laid behind it.</para>
    ///
    /// <para><b>Detection: owner detects, server records</b> - the gate races' split, for their
    /// reason: a remote vessel's replicated position is interpolated and would miss or invent
    /// crossings of a mouth this size. Each machine tests only the pilots it simulates
    /// (<see cref="IPlayer.IsNetworkOwner"/>: the host's human and every AI, a client's own human),
    /// runs the vessel's half of the pickup locally, and reports the crystal's half to the server
    /// (<see cref="CrystalManager.ReportRingThreaded"/>), which refuses a ring that has already
    /// moved. The crossing is a swept segment test, so a boosted hull cannot fly through between
    /// two samples.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Crystal))]
    public class CrystalRaceRing : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("The crystal this ring draws. Defaults to the Crystal on this GameObject.")]
        [SerializeField] Crystal crystal;

        [Tooltip("The crystal's impactor. Its domain rule decides who may thread the ring, and its " +
                 "pickup path credits the threading. Defaults to the one on this GameObject.")]
        [SerializeField] OmniCrystalImpactor impactor;

        [Header("Ring")]
        [Tooltip("Mouth radius at INTENSITY 1, in world units - the drawn ring and the crossing test " +
                 "share it. Matches the pickup sphere of the omni crystal Skim Race used to fly " +
                 "(BigCrystalVariant: SphereCollider radius 1.2 x root scale 20 = 24 u), so at " +
                 "intensity 1 the target a pilot has to hit is the size it was. " +
                 "SkimRaceRingCrystalTests holds the two together.")]
        [SerializeField, Min(1f)] float intensityOneMouthRadius = 24f;

        [Tooltip("Fraction of the intensity-1 diameter the ring loses per intensity above 1. " +
                 "0.1 = intensity 2 is 90%, 3 is 80%, 4 is 70% of intensity 1.")]
        [SerializeField, Range(0f, 0.3f)] float mouthShrinkPerIntensity = 0.1f;

        [Header("Crystal")]
        [Tooltip("World scale of the omni crystal hanging in the middle of the ring. 1.42 renders it " +
                 "the size of a LEVEL-1 elemental crystal: one element level is a crystal at world " +
                 "scale 1 (ElementalCrystalEjector.PetalCrystalWorldScale), and the omni model is " +
                 "0.72 per unit of scale against the Charge model's 1.02 (the reference the other " +
                 "elements are corrected to), so 1.02 / 0.72 = 1.42. Purely visual - the omni pickup " +
                 "reads nothing from the crystal's size. SkimRaceRingCrystalTests measures both meshes.")]
        [SerializeField, Min(0.01f)] float crystalWorldScale = 1.42f;

        [Tooltip("Seconds a ring takes to bloom in at its new anchor. Detection is live at the full " +
                 "mouth from the first frame; only the drawing grows into it.")]
        [SerializeField, Min(0f)] float bloomSeconds = 0.9f;

        [Tooltip("Seconds the threaded ring takes to wither away.")]
        [SerializeField, Min(0.01f)] float retireSeconds = 0.4f;

        [Header("Detection")]
        [Tooltip("Ignore a single frame's motion longer than the fastest hull could fly plus a " +
                 "margin - a respawn or an eject must never read as having threaded the ring.")]
        [SerializeField, Min(1f)] float maxPlausibleSpeed = 400f;

        [Tooltip("Seconds after reporting a threading before this ring may report again. Normally " +
                 "the ring moves long before this (the pickup respawns the crystal); the window only " +
                 "matters when the server refused the report, e.g. a teammate took the crystal first.")]
        [SerializeField, Min(0.1f)] float reportRetrySeconds = 1.5f;

        /// <summary>Mouth radius in world units - what a pilot has to thread, at this match's
        /// intensity once the ring stands.</summary>
        public float MouthRadius => _ring ? _ring.Radius : intensityOneMouthRadius;

        /// <summary>World scale of the omni crystal in the ring's middle.</summary>
        public float CrystalWorldScale => crystalWorldScale;

        /// <summary>
        /// The mouth radius at <paramref name="intensity"/>: the intensity-1 radius, less
        /// <paramref name="shrinkPerIntensity"/> of it for every level above 1 - a diameter
        /// reduction, which for a circle is the same fraction of the radius. Floored at a tenth
        /// so a large step cannot close the ring.
        /// </summary>
        public static float MouthRadiusFor(float intensityOneRadius, float shrinkPerIntensity, int intensity) =>
            intensityOneRadius * Mathf.Max(0.1f, 1f - shrinkPerIntensity * (Mathf.Max(1, intensity) - 1));

        /// <summary>The ring currently standing for this crystal, or null before it is raised.</summary>
        public RaceGateRing Ring => _ring;

        /// <summary>
        /// How far the crystal may sit from where its ring was raised before the ring follows it.
        /// The crystal only ever moves by respawning to another anchor (hundreds of units), so
        /// this is float noise, not a tuning value.
        /// </summary>
        const float MovedSqr = 0.01f;

        /// <summary>Seconds between scene scans for the track while it has not been laid yet.</summary>
        const float CourseRetrySeconds = 0.25f;

        RaceGateRing _ring;
        Vector3 _ringPosition;
        bool _axisFromTrack;
        int _courseHint = -1;
        float _reportedAt = float.NegativeInfinity;

        readonly Dictionary<IPlayer, PilotSample> _samples = new();
        readonly List<IPlayer> _stalePilots = new();

        /// <summary>Per-pilot detection state, on the machine that simulates that pilot.</summary>
        class PilotSample
        {
            public IVessel Vessel;
            public VesselImpactor Collector;
            public Vector3 LastPosition;
            public bool HasLastPosition;

            /// <summary><c>VesselTransformer.TeleportCount</c> at the last sample: a change means
            /// the step is a jump, and a jump threads nothing (GateRaceController records why).</summary>
            public int TeleportCount;
        }

        // ── The track, shared by every ring ───────────────────────────────
        //
        // One course for the whole race, built once from the laid prisms. Keyed on the track it
        // was read from so a replay - a scene reload, possibly at another intensity - rebuilds it
        // instead of orienting the new rings against the last match's track.
        static SkimRaceCourse s_course;
        static SpawnableWaypointTrack s_courseTrack;
        static float s_nextCourseAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_course = null;
            s_courseTrack = null;
            s_nextCourseAttempt = 0f;
        }

        void Awake()
        {
            if (!crystal) TryGetComponent(out crystal);
            if (!impactor) TryGetComponent(out impactor);

            // World, not local: the manager the crystal is minted under could be scaled. Set in
            // Awake, which Instantiate runs before the crystal's Start reads its scale.
            if (crystal) LifeFormCrystal.SetWorldScale(crystal, crystalWorldScale);
        }

        void OnDestroy() => RetireRing();

        void LateUpdate()
        {
            // A crystal not raised by a manager (a prefab preview, a test fixture) is never in a
            // race and has nowhere to report a pickup.
            var manager = crystal ? crystal.CrystalManager : null;
            if (manager == null) return;

            SyncRing(manager);
            if (_ring) DetectThreading(manager);
        }

        // ── The ring follows the crystal ──────────────────────────────────

        /// <summary>
        /// Keep one ring standing where the crystal is. The crystal moves only by respawning to its
        /// next anchor, so a move IS a pickup: the old ring withers where it was threaded and a new
        /// one blooms at the next anchor - the continuity law's arrival and departure, rather than
        /// a ring sliding across the arena.
        /// </summary>
        void SyncRing(CrystalManager manager)
        {
            Vector3 position = crystal.transform.position;
            float radius = MouthRadiusFor(intensityOneMouthRadius, mouthShrinkPerIntensity, manager.Intensity);

            // A changed radius re-raises too: a client's intensity arrives over the network, and
            // a ring raised before it landed would otherwise keep the wrong size all race.
            if (_ring == null || (position - _ringPosition).sqrMagnitude > MovedSqr
                || !Mathf.Approximately(radius, _ring.Radius))
            {
                RetireRing();
                RaiseRing(manager, position, radius);
                return;
            }

            if (!_axisFromTrack && TryResolveTrackAxis(position, out var axis))
            {
                _ring.SetAxis(axis);
                _axisFromTrack = true;
            }

            if (_ring.Owner != crystal.ownDomain) _ring.SetOwner(crystal.ownDomain);
        }

        void RaiseRing(CrystalManager manager, Vector3 position, float radius)
        {
            _axisFromTrack = TryResolveTrackAxis(position, out var axis);

            var go = new GameObject($"RaceRing_{crystal.Id:00}");
            go.transform.SetParent(manager.transform, false);
            _ring = go.AddComponent<RaceGateRing>();

            var gameData = manager.GameData;
            var theme = gameData ? gameData.ThemeManagerData : null;
            _ring.Build(crystal.Id, new RaceGate(position, axis, radius), theme, bloomSeconds,
                        owner: crystal.ownDomain);

            _ringPosition = position;
            _reportedAt = float.NegativeInfinity;
        }

        void RetireRing()
        {
            if (_ring) _ring.Retire(retireSeconds);
            _ring = null;
        }

        bool TryResolveTrackAxis(Vector3 position, out Vector3 axis)
        {
            axis = Vector3.forward;
            if (!TryGetCourse(out var course)) return false;
            axis = TrackAxisAt(course, position, ref _courseHint);
            return true;
        }

        /// <summary>
        /// The direction the track flows at its point nearest <paramref name="position"/> - the axis
        /// a ring standing there must face for a pilot flying the track to look into its mouth.
        /// Pure, so the edit-mode tests can hold it on a synthetic course.
        /// </summary>
        public static Vector3 TrackAxisAt(SkimRaceCourse course, Vector3 position, ref int hint)
        {
            float s = course.Project(position, ref hint, out _, out _);
            course.Sample(s, out var tangent, out _);
            return tangent;
        }

        /// <summary>
        /// The laid track as a course, or false while it is not laid yet. Never read mid-lay: a
        /// half-laid track would hand a ring near its missing stretch the tangent of whichever
        /// laid stretch happened to be nearest.
        /// </summary>
        static bool TryGetCourse(out SkimRaceCourse course)
        {
            course = null;
            if (s_course != null && s_courseTrack) { course = s_course; return true; }

            s_course = null;
            s_courseTrack = null;

            // Throttled and shared: one scene scan per window however many rings are waiting.
            if (Time.unscaledTime < s_nextCourseAttempt) return false;
            s_nextCourseAttempt = Time.unscaledTime + CourseRetrySeconds;

            if (PrismTrailBuilder.IsLayingInProgress) return false;
            if (!SkimRaceCourseSource.TryBuildFromScene(out course, out var track)) return false;

            s_course = course;
            s_courseTrack = track;
            return true;
        }

        // ── Threading ─────────────────────────────────────────────────────

        void DetectThreading(CrystalManager manager)
        {
            var gameData = manager.GameData;
            if (gameData == null || gameData.Players == null) return;

            // Samples keep updating through the report latch, so the first step tested after it
            // is one frame long rather than the whole latch window.
            bool latched = Time.time - _reportedAt < reportRetrySeconds;

            float maxStep = maxPlausibleSpeed * Time.deltaTime * 2f + 5f;
            float maxStepSqr = maxStep * maxStep;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];

                // Only a vessel THIS machine simulates - IsNetworkOwner, not IsLocalUser, because
                // the host owns every AI.
                if (p == null || !p.IsNetworkOwner) continue;

                var vessel = p.Vessel;
                if (vessel == null || vessel is Object uo && !uo) { Forget(p); continue; }
                var t = vessel.Transform;
                if (!t) { Forget(p); continue; }

                if (!_samples.TryGetValue(p, out var sample))
                {
                    sample = new PilotSample();
                    _samples[p] = sample;
                }

                if (!ReferenceEquals(sample.Vessel, vessel))
                {
                    // A swapped or respawned hull is a new body: new impactor, no previous sample.
                    sample.Vessel = vessel;
                    sample.Collector = t.GetComponentInChildren<VesselImpactor>(true);
                    sample.HasLastPosition = false;
                }

                Vector3 cur = t.position;
                int teleports = TeleportCountOf(vessel);
                if (!sample.HasLastPosition || teleports != sample.TeleportCount)
                {
                    sample.LastPosition = cur;
                    sample.TeleportCount = teleports;
                    sample.HasLastPosition = true;
                    continue;
                }

                Vector3 prev = sample.LastPosition;
                sample.LastPosition = cur;

                if (latched) continue;
                if ((cur - prev).sqrMagnitude > maxStepSqr) continue;   // a jump, not a flight

                var collector = sample.Collector;
                if (!collector) continue;

                // The crystal's own domain rule. A pilot it would refuse threads nothing.
                if (impactor && !impactor.AcceptsDomain(collector.OwnDomain)) continue;

                if (!_ring.CrossedMouth(prev, cur)) continue;

                _reportedAt = Time.time;
                latched = true;
                Thread(manager, collector);
            }

            PruneDepartedPilots(players);
        }

        /// <summary>
        /// The two halves a physics contact would have dispatched, through the same sweep entry
        /// point a projectile uses for a contact found off its trigger. The vessel's half runs here,
        /// on the machine that owns the vessel - which is where its effects are decided and
        /// broadcast from. The crystal's half goes to the authority.
        /// </summary>
        void Thread(CrystalManager manager, VesselImpactor collector)
        {
            if (impactor) collector.AcceptImpacteeFromSweep(impactor);
            manager.ReportRingThreaded(crystal.Id, _ringPosition, collector);
        }

        static int TeleportCountOf(IVessel vessel)
        {
            var transformer = vessel?.VesselStatus?.VesselTransformer;
            return transformer ? transformer.TeleportCount : 0;
        }

        void Forget(IPlayer p)
        {
            if (_samples.TryGetValue(p, out var sample)) sample.HasLastPosition = false;
        }

        void PruneDepartedPilots(List<IPlayer> players)
        {
            if (_samples.Count <= players.Count) return;

            _stalePilots.Clear();
            foreach (var key in _samples.Keys)
                if (!players.Contains(key)) _stalePilots.Add(key);
            for (int i = 0; i < _stalePilots.Count; i++) _samples.Remove(_stalePilots[i]);
        }
    }
}
