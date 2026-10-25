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

        protected override RaceCourseSource CreateCourseSource() => new WaystationCourseSource();

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

        [Tooltip("The fold lands along the heading it leaves on, so an aim error of θ lands d·sin θ off the ring's centre. " +
                 "The aim is also held under the angle that lands within this share of the ring's mouth (2026-10-17: at " +
                 "the authored 12 degrees alone a 420 u fold landed 87 u off a 40-56 u mouth).")]
        [SerializeField, Range(0.1f, 1f)] float aiFoldMouthShare = 0.5f;

        [Tooltip("Let go this many units SHORT of the ring's plane, so the hull flies through the mouth after the fold " +
                 "(a crossing inside the fold's own step is not counted - GateRaceController declines it).")]
        [SerializeField, Min(0f)] float aiFoldLandShort = 30f;

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
                // TryGetComponent, never VesselStatus.AIPilot: that getter ADDS an autopilot, and this loop
                // reads every player, humans included, every server tick (vessel-ai skill §1.7).
                if (!(status is Component statusComponent) || !statusComponent.TryGetComponent(out AIPilot autopilot)
                    || !autopilot.AutoPilotEnabled)
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
                float aim = Vector3.Angle(status.Transform.forward, toGate);
                if (aim > FoldAimLimitDegrees(distance, RingRadiusFor(pilot), aiFoldMouthShare, aiFoldAimDegrees)) continue;

                // The hold IS the distance: the executor reaches hold x ReachSpeed along the
                // heading. Let go short of the ring's plane (FoldHoldSeconds), so the hull then
                // flies through the mouth. ReachSpeed is read off the ability's own asset.
                _aiFoldReleaseAt[pilot] = now + FoldHoldSeconds(distance, aim, aiFoldLandShort, fold.ReachSpeed);
                _aiFoldRetryAt[pilot] = now + aiFoldRetrySeconds;
                handler.PerformShipControllerActionsReplicated(input);
            }
        }

        /// <summary>
        /// The largest aim error a fold over <paramref name="distance"/> may leave on and still land within
        /// <paramref name="mouthShare"/> of a ring of <paramref name="ringRadius"/>: asin(share·R/d), never over the
        /// authored <paramref name="maxDegrees"/>. The fold lands along its heading, so an error of θ lands d·sin θ off.
        /// </summary>
        public static float FoldAimLimitDegrees(float distance, float ringRadius, float mouthShare, float maxDegrees)
        {
            if (distance <= 1e-3f || ringRadius <= 0f) return maxDegrees;
            float limit = Mathf.Asin(Mathf.Clamp01(mouthShare * ringRadius / distance)) * Mathf.Rad2Deg;
            return Mathf.Min(maxDegrees, limit);
        }

        /// <summary>
        /// Seconds to hold a fold aimed <paramref name="aimDegrees"/> off a ring <paramref name="distance"/> away so it
        /// lands <paramref name="landShort"/> before the ring's plane: (d·cos θ - short) / ReachSpeed, never negative.
        /// </summary>
        public static float FoldHoldSeconds(float distance, float aimDegrees, float landShort, float reachSpeed) =>
            Mathf.Max(0f, distance * Mathf.Cos(aimDegrees * Mathf.Deg2Rad) - landShort) / Mathf.Max(1f, reachSpeed);

        /// <summary>The mouth radius of <paramref name="pilot"/>'s next ring, or 0 when the course has not landed.</summary>
        float RingRadiusFor(IPlayer pilot)
        {
            int index = pilot?.RoundStats?.SwitchesThreaded ?? -1;
            if (index < 0 || index >= RaceLength || _course.Count == 0) return 0f;
            return _course[RingIndexFor(index)].Radius;
        }

        /// <summary>A race: the AI flies at full throttle, as a person does (WAYSTATION.md expects the bot at 67 u/s; at
        /// the Butterfly's authored 0.6 skill throttle it cruised at about 45).</summary>
        protected override float AIThrottleFloor => 1f;

        /// <summary>
        /// Regatta's gate steering (<see cref="GateRaceController.AICommitFromApproachPoint"/>, 2026-10-26): commit no earlier
        /// than the approach point, which then lies inside the commit radius; aim through the ring's CENTRE; re-attack after
        /// a missed mouth. With the shipped steering the AI Butterfly threaded 0 of 24 rings in both of Caleb's races
        /// (2026-10-20/21): its approach point sat 300 u out against a 260 u commit, and it orbited the first ring 150-300 u
        /// off for the whole race on its 85 u turning circle (45 deg/s at 67 u/s). Offline on those two courses
        /// (<c>Tools/Build/switchback_dolphin_sim.py --mode Waystation</c>, no fold): 0 and 5 rings before, 24 of 24 on both
        /// after, in 205 s and 213 s against Caleb's 245 s and 205 s.
        /// </summary>
        protected override bool AICommitFromApproachPoint => true;

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
