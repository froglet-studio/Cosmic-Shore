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
    /// <para><b>The Tiger Moth flies it now (2026-10-29).</b> The Butterfly and its Fold were retired; the Tiger Moth's
    /// Drop is the jump (<c>R_VesselActions/TIGER_MOTH.md</c>). It still has ONE degree of freedom - the line it falls
    /// along is the hull's DOWN at the press - so the course's logic stands with "the heading you leave on" read as "the
    /// belly you leave on": to jump the gap to the next cluster a pilot puts the cluster under the belly, drops, and lands
    /// flying toward it. The drop is TRAVEL, not a teleport, so a ring it passes through is threaded. Each cluster's exit
    /// gate FACES SQUARE to the next cluster (<see cref="WaystationCourseSettings.ExitTwistDegrees"/>, Garrett 2026-10-29:
    /// "keep your flow by dropping to the next cluster"): thread it, roll the cluster under you, drop. The paragraphs
    /// below describe the Fold-era design and keep its record; the autopilot drive aims the belly.</para>
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

        [Tooltip("Let go this many units SHORT of the ring's plane, so the hull flies through the mouth after the fold " +
                 "(a crossing inside the fold's own step is not counted - GateRaceController declines it).")]
        [SerializeField, Min(0f)] float aiFoldLandShort = 30f;

        [Tooltip("Seconds before an autopilot re-tries a fold it could not start. A press the " +
                 "ability refuses (still recharging) is harmless but costs an RPC, so it is not " +
                 "re-sent every frame.")]
        [SerializeField, Min(0.1f)] float aiFoldRetrySeconds = 1.5f;

        [Header("Autopilot belly aim (the Tiger Moth's drop)")]
        [Tooltip("The drop falls along the hull's BELLY, so before a long gap the autopilot turns its belly onto the next " +
                 "ring (Garrett, 2026-10-29: \"make the waystation AI aim its belly at the rings\"). It starts only when the " +
                 "ring is within this many degrees of the nose, where the turn is a pitch up of up to 90 degrees; a ring " +
                 "further round is flown at nose-first until it is in front.")]
        [SerializeField, Range(10f, 120f)] float aiBellyAimEnterDegrees = 75f;

        [Tooltip("How closely the NOSE must be on the ring before the autopilot starts the pitch up. The pitch is pure, so " +
                 "the belly ends exactly this far off the ring: the nose is lined up first (the autopilot's strength), then " +
                 "the hull pitches 90 degrees up.")]
        [SerializeField, Range(0.5f, 20f)] float aiBellyNoseDegrees = 3f;

        [Tooltip("How closely the BELLY must be on the ring for the autopilot to drop. Wider than a fold's line-up: the " +
                 "drop is travel, so it lands short of the ring (by twice the miss it carries) and flies the rest.")]
        [SerializeField, Range(1f, 30f)] float aiDropAimDegrees = 10f;

        [Tooltip("A ring further round than the pitch entry but within this many degrees of the nose is ABEAM - where " +
                 "every exit gate leaves the next cluster, since the gate faces square to it. There the autopilot holds " +
                 "the heading square to the ring and ROLLS its belly onto it (AIPilot.MarkBellyTarget), then drops. " +
                 "Further round still, it flies nose-first until the ring comes back into one of the two arcs.")]
        [SerializeField, Range(60f, 170f)] float aiBellyRollMaxDegrees = 125f;

        [Tooltip("Longest an autopilot spends lining up and pitching before it gives up and flies on (the retry wait then " +
                 "applies). A 90 degree pitch at the hull's 45 deg/s takes 2 s plus the settle.")]
        [SerializeField, Min(0.5f)] float aiBellyAimMaxSeconds = 6f;

        /// <summary>How far ahead of the hull the belly aim's steering point sits. Only its direction matters: it is a
        /// lead the pilot steers by (OverrideAimIsLead), never a place it reaches.</summary>
        const float BellyAimLead = 500f;

        readonly Dictionary<IPlayer, float> _bellyAimSince = new();
        readonly Dictionary<IPlayer, Vector3> _bellyPitchNose = new();   // the held pitch-up point, once the nose was on
        readonly HashSet<IPlayer> _bellyRolls = new();                    // lining up by ROLL (the ring is abeam)
        readonly Dictionary<Transform, TigerMothDropExecutor> _dropExecutors = new();

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
            _bellyAimSince.Clear();
            _bellyPitchNose.Clear();
            _bellyRolls.Clear();
            _dropExecutors.Clear();
        }

        /// <summary>
        /// The autopilot's belly aim before a gap worth a drop, one of two ways. A ring ABEAM (between the pitch entry and
        /// <see cref="aiBellyRollMaxDegrees"/>, where every exit gate leaves the next cluster) is lined up by ROLL: steer
        /// the heading square to the ring, and roll the belly onto it (<see cref="OverrideBellyTarget"/>). A ring nearer the
        /// nose is lined up by PITCH, in two phases. First the NOSE goes onto the next ring (the
        /// ring itself is the steering point); then, once it is within <see cref="aiBellyNoseDegrees"/>, the hull pitches
        /// straight up to a point captured at that moment (<see cref="TigerMothDropMath.BellyAimNose"/>), which carries the
        /// belly onto the ring, and <see cref="TickAutopilotFolds"/> drops. Inputs only, as every AI here: both phases are
        /// steering points AIPilot flies its sticks at. It answers only while the drop is ready and the gap is long, and
        /// gives up after <see cref="aiBellyAimMaxSeconds"/>, so a bot that never lines up flies the course as before.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var status = pilot?.Vessel?.VesselStatus;
            if (status == null || !BellyAimWanted(pilot, status, out var toGate))
            {
                if (pilot != null) ForgetBellyAim(pilot);
                return false;
            }
            Transform hull = status.Transform;
            if (_bellyRolls.Contains(pilot))
            {
                // Hold the heading square to the ring (re-derived: the roll stick is the mode's, so the yaw that keeps the
                // nose square no longer drags the bank) while the roll brings the belly round.
                Vector3 square = Vector3.ProjectOnPlane(hull.forward, toGate);
                target = hull.position + (square.sqrMagnitude > 1e-6f ? square.normalized : hull.forward) * BellyAimLead;
                return true;
            }
            if (!_bellyPitchNose.TryGetValue(pilot, out var nose))
            {
                if (Vector3.Angle(hull.forward, toGate) > aiBellyNoseDegrees)
                {
                    target = hull.position + toGate;   // phase 1: nose on the ring
                    return true;
                }
                nose = TigerMothDropMath.BellyAimNose(hull.rotation, toGate);
                _bellyPitchNose[pilot] = nose;
            }
            target = hull.position + nose * BellyAimLead;   // phase 2: pitch up, belly onto the ring
            return true;
        }

        protected override bool OverrideAimIsLead(IPlayer pilot) => true;

        protected override bool OverrideBellyTarget(IPlayer pilot, out Vector3 worldDirection)
        {
            worldDirection = default;
            var status = pilot?.Vessel?.VesselStatus;
            if (status == null || !_bellyRolls.Contains(pilot) || !TryGetNextGate(pilot, out var gate) || gate == null)
                return false;
            worldDirection = gate.position - status.Transform.position;
            return true;
        }

        void ForgetBellyAim(IPlayer pilot)
        {
            _bellyAimSince.Remove(pilot);
            _bellyPitchNose.Remove(pilot);
            _bellyRolls.Remove(pilot);
        }

        bool BellyAimWanted(IPlayer pilot, IVesselStatus status, out Vector3 toGate)
        {
            toGate = default;
            float now = Time.time;
            var handler = status.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<TigerMothDropActionSO>(out _, out _)) return false;
            var drop = DropExecutorOf(status.Transform);
            if (!drop || drop.IsDropping || !drop.IsReady) return false;
            if (_aiFoldReleaseAt.ContainsKey(pilot)) return false;
            if (_aiFoldRetryAt.TryGetValue(pilot, out float retryAt) && now < retryAt) return false;
            if (!TryGetNextGate(pilot, out var gate) || gate == null) return false;

            toGate = gate.position - status.Transform.position;
            if (toGate.magnitude < aiFoldMinDistance) return false;

            if (_bellyAimSince.TryGetValue(pilot, out float since))
            {
                if (now - since <= aiBellyAimMaxSeconds) return true;
                ForgetBellyAim(pilot);
                _aiFoldRetryAt[pilot] = now + aiFoldRetrySeconds;   // could not line up: fly on, try again later
                return false;
            }
            float off = Vector3.Angle(status.Transform.forward, toGate);
            if (off > aiBellyRollMaxDegrees) return false;
            if (off > aiBellyAimEnterDegrees) _bellyRolls.Add(pilot);   // abeam: line up by roll
            _bellyAimSince[pilot] = now;
            return true;
        }

        TigerMothDropExecutor DropExecutorOf(Transform hull)
        {
            if (!hull) return null;
            if (_dropExecutors.TryGetValue(hull, out var cached) && cached) return cached;
            var found = hull.GetComponentInChildren<TigerMothDropExecutor>(true);
            _dropExecutors[hull] = found;
            return found;
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
                // The Tiger Moth's Drop (the Butterfly and its Fold retired 2026-10-29, TIGER_MOTH.md): it falls along the
                // hull's DOWN, so the bot presses when its belly - not its nose - is on the next ring.
                if (!handler.TryGetBoundAction<TigerMothDropActionSO>(out var drop, out var input)) continue;
                if (!TryGetNextGate(pilot, out var gate) || gate == null) continue;

                Vector3 hull = status.Transform.position;
                Vector3 toGate = gate.position - hull;
                float distance = toGate.magnitude;

                if (distance < aiFoldMinDistance) continue;
                float aim = TigerMothDropMath.BellyAimError(status.Transform.rotation, toGate);
                if (aim > aiDropAimDegrees) continue;

                // The hold IS the distance (the drop's numbers read off its own asset). Land short of the ring's plane by the authored margin plus twice the miss the aim carries, so the hull
                // has room to turn into the mouth; the drop is travel, so a ring it crosses on the way is threaded anyway.
                float along = distance * Mathf.Cos(aim * Mathf.Deg2Rad)
                              - aiFoldLandShort - 2f * distance * Mathf.Sin(aim * Mathf.Deg2Rad);
                _aiFoldReleaseAt[pilot] = now + TigerMothDropMath.HoldSecondsFor(Mathf.Max(0f, along), drop.DropSpeed,
                    drop.DropEaseSeconds, drop.LandSeconds, status.Speed);
                _aiFoldRetryAt[pilot] = now + aiFoldRetrySeconds;
                ForgetBellyAim(pilot);
                handler.PerformShipControllerActionsReplicated(input);
            }
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
            if (handler.TryGetBoundAction<TigerMothDropActionSO>(out _, out var input))
                handler.StopShipControllerActionsReplicated(input);
        }
    }
}
