using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Waystation — the Butterfly-only migration race. The course is a chain of CLUSTERS: tight
    /// knots of switch rings, laid a FOLD apart. Inside a cluster you FLY; between clusters you
    /// FOLD. The first domain whose lead runner threads the last ring wins.
    ///
    /// <para>Everything about running a gate race — the broadcast, the rings, the crossing
    /// detection, the optimistic report/reconcile round trip, the AI steering and the final
    /// scores — lives in <see cref="GateRaceController"/>, shared with Switchback, Headlong,
    /// Breakwater, Skein and Redline. This class is what makes it WAYSTATION: an open chain
    /// dealt into clusters, and a course that cannot fail (<see cref="WaystationCourse"/>).</para>
    ///
    /// <para><b>The mode is the Fold's one degree of freedom.</b> Since the Fold collapsed to a
    /// single reach along the heading, it cannot be aimed once it has begun — the destination is
    /// the line the pilot was already flying. So the last ring of every cluster is also the
    /// aiming device for the next jump: thread it on the right line and the fold is free;
    /// thread it badly and you pay for the turn on a 45 deg/s hull before you can commit. That
    /// is the whole race, and it is built into the COURSE
    /// (<c>WaystationCourseSettings.MaxExitOffsetDegrees</c>) rather than into a rule.</para>
    ///
    /// <para><b>A teleport threads nothing.</b> A fold crosses hundreds of units along its own
    /// heading and the next cluster's rings are on that heading BY CONSTRUCTION, so without a
    /// rule a pilot would be credited for every ring their jump passed through. The rule is the
    /// platform's, not this mode's: <c>VesselTransformer.TeleportCount</c> states the fact and
    /// <see cref="GateRaceController"/> declines a step that contains one. It is stated as a
    /// COUNTER rather than a distance because the step guard already there fails the other way
    /// round — it rejects a LONG jump by accident and credits a SHORT one.</para>
    ///
    /// <para><b>An AI folds, and it had to be given the verb explicitly.</b> <c>AIPilot</c> writes
    /// a stick and a throttle and nothing else, so every held ability in the fleet is inert under
    /// autopilot — and here the held ability is the whole mode: a bot that only flew would cross
    /// each gap at 67 u/s against a human who crosses it instantly, which is not a competitor.
    /// The drive lives in the CONTROLLER rather than on the vessel (the Broadside / Tollway /
    /// Hijack shape), because the decision is mode knowledge — how far the next ring is — while
    /// the NUMBERS stay in the ability's own asset, asked through
    /// <c>R_VesselActionHandler.TryGetBoundAction</c> so a retune of the Fold moves the bot with
    /// it. It presses through <c>PerformShipControllerActionsReplicated</c>, never a local
    /// <c>StartAction</c>: an AI is simulated server-only, so a local press would teleport the
    /// vessel on one machine and play the wither and the bloom for nobody.</para>
    ///
    /// <para><b>The drive FAILS SAFE.</b> Every gate it applies — far enough to be worth folding,
    /// inside the fold's resolved reach, and lined up within a tight cone — refuses by simply not
    /// pressing, and a bot that never presses flies the course exactly as it did before the drive
    /// existed. That is deliberate: an autopilot fold that guessed would put a bot somewhere its
    /// own objective is not, and no amount of it is worth one wrong jump.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new metric (<c>SwitchesThreaded</c>, the gate
    /// race's own), no new turn monitor (<c>RaceGateTurnMonitor</c> asks the controller), no new
    /// scoring class (a second asset on <c>GateRaceScoringRuleSO</c>), no cell of its own, no
    /// weapon. The Butterfly's shipped kit supplies the racing: Soar-less slow cruise, the wing
    /// spread, and the Fold.</para>
    /// </summary>
    public class WaystationController : GateRaceController
    {
        // There is deliberately NO "first cluster distance" field. Every cluster centre sits on
        // one sphere - that is what makes the chain contain itself - so where the first one goes
        // is decided by that sphere and the spawn pole, and a serialized distance could only
        // disagree with the geometry. A config that cannot affect anything is worse than absent
        // (the Breakwater rule).

        protected override string ModeName => "Waystation";

        public override int AuthoredGateTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            int asked = overrides != null
                ? overrides.GetWaystationRingTarget()
                : EndConditionOverridesSO.DefaultWaystationRingTarget;

            // ROUNDED UP to whole clusters, and rounded up HERE rather than in the course: this
            // is the number the turn monitor publishes as the finish line before a course
            // exists, so the two have to agree from the first frame. A target that named a ring
            // the course never laid would be a match that cannot end.
            int per = WaystationCourseSettings.ForIntensity(Intensity).RingsPerCluster;
            return WaystationCourse.ClusterCount(asked, per) * Mathf.Max(1, per);
        }

        /// <summary>
        /// An open chain of clusters. Unlike Switchback's walk this CANNOT fail — a cluster is a
        /// capped-turn walk from its own centre and a hop is a deflected step clamped back into
        /// the shell, so there is nothing for a tight shell to make impossible. There is
        /// therefore no back-off here and no shortened course: what comes back is always the
        /// count <see cref="AuthoredGateTarget"/> already promised.
        /// </summary>
        protected override List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer)
        {
            var settings = WaystationCourseSettings.ForIntensity(Intensity);
            settings.InnerRadius = inner;
            settings.OuterRadius = outer;
            settings.RingTarget = Mathf.Max(settings.RingsPerCluster, gateCount);
            settings.FirstClusterDirection = Vector3.up;   // the equatorial spawn ring's pole

            var course = WaystationCourse.Generate(seed, settings);
            if (course != null && course.Count > 0) return course;

            CourseFailureDetail =
                $"WaystationCourse returned nothing for {settings.RingTarget} rings in shell " +
                $"{inner:F0}..{outer:F0} - it has no failure path, so this is a settings fault " +
                "(RingsPerCluster or RingTarget at zero).";
            return null;
        }

        // ── The autopilot's Fold ──────────────────────────────────────────

        [Header("Autopilot fold")]
        [Tooltip("Shortest gap an autopilot will spend a fold on. Under this it simply flies - a " +
                 "fold costs the whole hold plus the wither and the bloom, so folding a short " +
                 "gap is slower than crossing it.")]
        [SerializeField, Min(0f)] float aiFoldMinDistance = 420f;

        [Tooltip("How closely an autopilot must already be pointed at its next ring before it " +
                 "commits. The Fold has ONE degree of freedom - the heading you leave on - and " +
                 "pitch and yaw are dead for the duration, so a bot that presses off-line lands " +
                 "off-line with no way to correct.")]
        [SerializeField, Range(1f, 90f)] float aiFoldAimDegrees = 12f;

        [Tooltip("Seconds before an autopilot re-tries a fold it could not start. A press the " +
                 "ability refuses (still recharging) is harmless but costs an RPC, so it is not " +
                 "re-sent every frame.")]
        [SerializeField, Min(0.1f)] float aiFoldRetrySeconds = 1.5f;

        /// <summary>When each folding bot should let go, and when each waiting bot may try again.</summary>
        readonly Dictionary<IPlayer, float> _aiFoldReleaseAt = new();
        readonly Dictionary<IPlayer, float> _aiFoldRetryAt = new();

        /// <summary>
        /// The mode's server tick. Deliberately NOT a <c>void Update()</c> of its own:
        /// <c>Update</c> is a Unity message rather than a virtual, so declaring one here would
        /// HIDE <see cref="GateRaceController"/>'s and take the platform's crossing detection - the
        /// whole race - down with it, silently. <see cref="GateRaceController.OnServerTick"/> is
        /// the seam that exists so a driver cannot make that mistake.
        /// </summary>
        protected override void OnServerTick()
        {
            if (gameData == null) return;

            if (!gameData.IsTurnRunning)
            {
                // A held fold outlives the whistle otherwise: the executor would keep the vessel
                // stopped, its wings shut and its trail penned for the rest of the scene.
                if (_aiFoldReleaseAt.Count > 0) ReleaseEveryAutopilotFold();
                return;
            }

            TickAutopilotFolds();
        }

        void OnDisable() => ReleaseEveryAutopilotFold();

        void ReleaseEveryAutopilotFold()
        {
            foreach (var pilot in new List<IPlayer>(_aiFoldReleaseAt.Keys))
                ReleaseFold(pilot);
            _aiFoldReleaseAt.Clear();
            _aiFoldRetryAt.Clear();
        }

        void TickAutopilotFolds()
        {
            float now = Time.time;
            var players = gameData.Players;
            if (players == null) return;

            for (int i = 0; i < players.Count; i++)
            {
                var pilot = players[i];
                var status = pilot?.Vessel?.VesselStatus;
                var handler = status?.ActionHandler;
                if (handler == null) continue;

                // The pilot being an AUTOPILOT, not the player being an AI - the same gate the
                // Manta's boost drive uses, so a lava-lamp or companion Butterfly behaves alike.
                if (status.AIPilot == null || !status.AIPilot.AutoPilotEnabled)
                {
                    if (_aiFoldReleaseAt.ContainsKey(pilot)) ReleaseFold(pilot);
                    continue;
                }

                if (_aiFoldReleaseAt.TryGetValue(pilot, out float releaseAt))
                {
                    if (now >= releaseAt) ReleaseFold(pilot);
                    continue;
                }

                if (_aiFoldRetryAt.TryGetValue(pilot, out float retryAt) && now < retryAt) continue;
                if (!handler.TryGetBoundAction<FoldActionSO>(out var fold, out var input)) continue;
                if (!TryGetNextGate(pilot, out var gate) || gate == null) continue;

                Vector3 hull = status.Transform.position;
                Vector3 toGate = gate.position - hull;
                float distance = toGate.magnitude;

                if (distance < aiFoldMinDistance) continue;
                if (distance > fold.ResolveRange(status)) continue;
                if (Vector3.Angle(status.Transform.forward, toGate) > aiFoldAimDegrees) continue;

                // The hold IS the distance: the executor reaches hold x ReachSpeed along the
                // heading, so letting go after distance / ReachSpeed lands on the ring. Read off
                // the ability's own asset rather than copied here.
                float reachSpeed = Mathf.Max(1f, fold.ReachSpeed);
                _aiFoldReleaseAt[pilot] = now + distance / reachSpeed;
                _aiFoldRetryAt[pilot] = now + aiFoldRetrySeconds;
                handler.PerformShipControllerActionsReplicated(input);
            }
        }

        void ReleaseFold(IPlayer pilot)
        {
            _aiFoldReleaseAt.Remove(pilot);
            var handler = pilot?.Vessel?.VesselStatus?.ActionHandler;
            if (handler == null) return;
            if (handler.TryGetBoundAction<FoldActionSO>(out _, out var input))
                handler.StopShipControllerActionsReplicated(input);
        }
    }
}
