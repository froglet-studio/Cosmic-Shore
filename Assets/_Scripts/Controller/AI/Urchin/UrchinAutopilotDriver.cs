using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flies an AUTOPILOTED Urchin's kit - the three verbs <see cref="AIPilot"/> cannot reach on
    /// its own. Shared by every mode that seats an AI Urchin (Skein and Hijack today), the
    /// <see cref="ButterflyAutopilotModeDriver"/> shape: a plain object the mode controller owns,
    /// asked from inside the mode's own <c>SetExternalTargetProvider</c> closure, so it needs no
    /// scene wiring and nothing in <see cref="AIPilot"/> changes.
    ///
    /// <para><b>Why a mode needs this.</b> <see cref="AIPilot"/> writes a stick and a throttle.
    /// For the Urchin that covers exactly one of its four abilities - Trail Rider is passive, it
    /// latches on CONTACT - and leaves the other three on buttons nobody presses. The result is
    /// the pilot the fleet audit recorded: it cruises, it rides whatever it happens to touch to
    /// wherever that goes, and it crawls hostile rail at 20 u/s forever because it never spikes
    /// the road ahead. The kit, as an AI uses it:</para>
    /// <list type="bullet">
    /// <item><b>Trail Rider - choosing the rail.</b> <see cref="TryRideToward"/> walks the rail
    /// both ways from the pilot (<see cref="UrchinRailAssessment"/>) and decides RIDE (it goes
    /// there), REVERSE (it goes there the other way - the ride's direction is the pilot's FACING,
    /// <c>GunVesselTransformer.Slide</c>, so the AI turns its nose round) or LEAVE.</item>
    /// <item><b>Slip</b> is how it leaves: the ride constrains position, never attitude, so no
    /// amount of steering gets an Urchin off a rail. Also the stall escape for a ride that has
    /// genuinely parked.</item>
    /// <item><b>Chain Spikes</b> are tapped while the prism underfoot is HOSTILE and the meter can
    /// pay: the volley converts the rail ahead, and the crawl becomes a grind again.</item>
    /// <item><b>Track Projector</b> is fired on a long, straight, lined-up shot at the objective
    /// (<see cref="TryProjectTrackToward"/>) - the track is laid along the NOSE and launched off
    /// at 1.2x grind speed, so it is only worth it when that line goes where the pilot is
    /// going.</item>
    /// </list>
    ///
    /// <para><b>Server-side, and authoritative state only.</b> The host owns and simulates every
    /// AI vessel, so everything read here - attachment, the ridden prism, course, speed, the ammo
    /// meter, the Track cooldown - is the server's own simulation of that vessel, the same state
    /// its ride is computed from. Every press goes through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, so each peer
    /// runs the same ability, and every control is found by CAPABILITY
    /// (<see cref="R_VesselActionHandler.TryGetBoundAction{T}"/>), never by a named trigger: a
    /// re-bound Urchin, or a hull that binds none of these, is driven correctly or left alone.
    /// The one read that is NOT replicated platform-wide is a prism's domain
    /// (<c>PrismTeamManager</c> is local - HIJACK.md §10); on the server it is the same view the
    /// server's ride speed and steal credit are computed from, so the AI is consistent with its
    /// own simulation even where a client's view differs.</para>
    ///
    /// <para><b>Throttle is left to <see cref="AIPilot"/>.</b> The Urchin prefab's AIPilot
    /// authors <c>ram: 1</c>, so whenever the aim point is on the course the pilot is at full
    /// throttle - which is why every aim this driver returns while riding is a point down the
    /// rail's own tangent. That field is fleet-wide and was audited for this driver (HIJACK.md
    /// §6): every context an AI Urchin flies in wants it.</para>
    /// </summary>
    public sealed class UrchinAutopilotDriver
    {
        readonly UrchinAutopilotConfigSO _config;
        readonly Dictionary<IPlayer, PilotState> _states = new();

        /// <param name="config">The tunables. Null loads <c>Resources/UrchinAutopilotConfig</c>,
        /// falling back to the class defaults.</param>
        public UrchinAutopilotDriver(UrchinAutopilotConfigSO config = null)
        {
            _config = config != null ? config : UrchinAutopilotConfigSO.LoadDefault();
        }

        public UrchinAutopilotConfigSO Config => _config;

        sealed class PilotState
        {
            public float MovingSince = -1f;
            public float NextAssess;
            public Trail AssessedTrail;
            public UrchinRailVerdict Verdict = UrchinRailVerdict.Ride;
            public float NextSpike;
            public float NextSlip;
            public float NextTrack;
            public TrailFollower Follower;
            public UrchinTrackActionExecutor TrackExecutor;
            public bool HadFollower;
            public bool HadTrackExecutor;
            public Transform ResolvedFor;
        }

        // ── Riding ───────────────────────────────────────────────────────────

        /// <summary>
        /// SERVER. While <paramref name="pilot"/> is riding a RAIL, decide what to do with it and
        /// return where to aim. Returns false when the pilot is not riding a rail - including the
        /// frame it Slips off one - and the caller then flies at its objective the ordinary way.
        ///
        /// <para>Also runs the two ride-time verbs every caller wants: a spike tap when the mass
        /// underfoot is hostile, and the parked-ride escape. Safe to call every frame; the walk is
        /// throttled by <see cref="UrchinAutopilotConfigSO.AssessIntervalSeconds"/>.</para>
        /// </summary>
        /// <param name="objective">Where the pilot is trying to get to.</param>
        /// <param name="objectiveRadius">The objective's own capture radius (a ring's mouth). A rail
        /// passing inside <c>CaptureFraction</c> of it counts as going there.</param>
        /// <param name="railLead">How far down the rail to aim while riding; null uses the
        /// config. A mode that already authors this distance passes its own.</param>
        public bool TryRideToward(IPlayer pilot, Vector3 objective, float objectiveRadius, out Vector3 aim,
                                  float? railLead = null)
        {
            aim = default;
            var status = pilot?.Vessel?.VesselStatus;
            var tf = pilot?.Vessel?.Transform;
            if (status == null || tf == null || !status.IsAttached) return false;

            var prism = status.AttachedPrism;
            var trail = prism ? prism.Trail : null;
            if (trail == null) return false;

            var s = State(pilot, status);
            float now = Time.time;

            // Spike the road ahead. Asked on every attached frame, rail or not: rolling a hostile
            // burr is the same question as grinding a hostile rail.
            bool hostile = prism.Domain != pilot.Domain;
            if (hostile) TrySpike(pilot);

            // Only a 1D ribbon has a direction to choose. A shell or a solid (a burr) is ridden
            // across its surface by the nose, so the caller's own aim is already the right one.
            if (trail.Dimension != PrismscapeDimension.Trail) return false;

            // Parked - a reversal caught in the throttle deadband, or a ribbon taken out from
            // under the ride. NOT the hostile crawl, which is a raid in progress.
            if (s.MovingSince < 0f || status.Speed >= _config.ParkedSpeed) s.MovingSince = now;
            else if (now - s.MovingSince > _config.ParkedSeconds && TrySlip(pilot))
            {
                s.MovingSince = now;
                s.AssessedTrail = null;
                return false;
            }

            float forwardLead = Mathf.Max(1f, railLead ?? _config.RailLeadDistance);
            Vector3 pos = tf.position;
            Vector3 course = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : tf.forward;

            if (now >= s.NextAssess || s.AssessedTrail != trail)
            {
                s.NextAssess = now + _config.AssessIntervalSeconds;
                s.AssessedTrail = trail;
                s.Verdict = Assess(s, status, trail, prism, course, pos, objective, objectiveRadius,
                                   crawlingDry: hostile && !CanAffordSpike(status, _config.SpikeMinAmmo));
            }

            switch (s.Verdict)
            {
                case UrchinRailVerdict.Leave:
                    if (TrySlip(pilot))
                    {
                        s.AssessedTrail = null;
                        return false;
                    }
                    // Slip unbound or still inside its own interval: keep riding meanwhile.
                    goto default;

                case UrchinRailVerdict.Reverse:
                {
                    // Behind AND to one side. Dead astern the cross product AIPilot steers by
                    // vanishes, and the nose would not know which way to swing. Far enough out
                    // (a multiple of the live turn radius) that the turning-circle test reads it
                    // as reachable rather than breaking off.
                    float radius = status.VesselTransformer ? status.VesselTransformer.MinTurnRadius : 0f;
                    float lead = Mathf.Max(forwardLead, radius * _config.ReverseLeadTurnRadii);
                    aim = pos - course * lead + tf.up * (0.6f * lead);
                    return true;
                }

                default:
                    aim = pos + course * forwardLead;
                    return true;
            }
        }

        UrchinRailVerdict Assess(PilotState s, IVesselStatus status, Trail trail, Prism prism,
                                 Vector3 course, Vector3 pos, Vector3 objective, float objectiveRadius,
                                 bool crawlingDry)
        {
            int index = trail.GetBlockIndex(prism);
            if (index < 0) return UrchinRailVerdict.Ride;

            int step = TravelStep(s, trail, index, course);
            var points = new TrailPoints(trail);
            var ahead = UrchinRailAssessment.Scan(ref points, index, step, objective, _config.ScanArc);
            var behind = UrchinRailAssessment.Scan(ref points, index, -step, objective, _config.ScanArc);

            var rules = new UrchinRailRules
            {
                CaptureRadius = Mathf.Max(1f, objectiveRadius * _config.CaptureFraction),
                ClosingFraction = _config.ClosingFraction,
                DryCrawlArc = _config.DryCrawlArc,
                LaunchPreferArc = _config.LaunchPreferArc,
            };
            return UrchinRailAssessment.Decide(ahead, behind, Vector3.Distance(pos, objective), rules, crawlingDry);
        }

        /// <summary>+1 when the ride is travelling toward the rail's head (index order), -1 toward
        /// its tail. Read off the follower when the hull carries one; otherwise from the course,
        /// which the ride writes as the travel tangent every frame.</summary>
        static int TravelStep(PilotState s, Trail trail, int index, Vector3 course)
        {
            if (s.Follower != null && s.Follower.AttachedTrail == trail)
                return s.Follower.Direction == TrailFollowerDirection.Backward ? -1 : 1;

            Vector3 heading = trail.HeadingAt(index);
            return Vector3.Dot(course, heading) < 0f ? -1 : 1;
        }

        /// <summary>A live trail as <see cref="IUrchinRailPoints"/>. Ridable means what
        /// <c>Trail.IsRidable</c> means: present, active, and still a member of THIS trail (a
        /// pooled prism reused elsewhere is somewhere else entirely).</summary>
        readonly struct TrailPoints : IUrchinRailPoints
        {
            readonly Trail _trail;
            public TrailPoints(Trail trail) => _trail = trail;

            public int Count => _trail.TrailList.Count;

            public bool TryGetPoint(int index, out Vector3 point)
            {
                var p = _trail.TrailList[index];
                if (!p || !p.gameObject.activeInHierarchy || p.Trail != _trail)
                {
                    point = default;
                    return false;
                }
                point = _trail.RidePoint(p);
                return true;
            }
        }

        // ── Track Projector ──────────────────────────────────────────────────

        /// <summary>
        /// SERVER. In free flight, lay a Track Projector rail at <paramref name="objective"/> when
        /// it is a long, straight shot the nose is already lined up on. Returns true on the frame
        /// it fires.
        ///
        /// <para>The track is laid along the NOSE and is the pilot's own colour, so the pilot
        /// grinds it at friendly speed and launches off its end at 1.2x that - a straight line at
        /// several times cruise. Three conditions keep it from being a liability: the nose must
        /// point at the objective (<c>TrackAimDegrees</c>); the objective must be several track
        /// lengths away, because the carried launch speed bleeds off over seconds and overshoots a
        /// near target; and, when the objective has a flow axis (a ring), the flight line must run
        /// along it so the launch goes THROUGH the mouth rather than past it.</para>
        /// </summary>
        /// <param name="flowAxis">The objective's flow direction, or zero for a point objective.</param>
        public bool TryProjectTrackToward(IPlayer pilot, Vector3 objective, Vector3 flowAxis)
        {
            var status = pilot?.Vessel?.VesselStatus;
            var tf = pilot?.Vessel?.Transform;
            var handler = status?.ActionHandler;
            if (status == null || tf == null || handler == null || status.IsAttached) return false;

            var s = State(pilot, status);
            float now = Time.time;
            if (now < s.NextTrack) return false;

            if (!handler.TryGetBoundAction<UrchinTrackActionSO>(out var so, out var input) || so == null)
                return false;
            if (s.TrackExecutor != null && !s.TrackExecutor.TrackReady) return false;

            Vector3 to = objective - tf.position;
            float range = to.magnitude;
            if (range < 1e-3f) return false;
            Vector3 dir = to / range;

            if (range < so.ResolveLength(status) * _config.TrackMinRangeInLengths) return false;
            if (Vector3.Angle(tf.forward, dir) > _config.TrackAimDegrees) return false;
            if (flowAxis.sqrMagnitude > 1e-6f &&
                Mathf.Abs(Vector3.Dot(flowAxis.normalized, dir)) < _config.TrackAxisAlignment)
                return false;

            // The executor owns the real cooldown; this clock only stops a press per frame while
            // the replicated press is in flight, and covers a hull whose executor was not found.
            s.NextTrack = now + Mathf.Max(1f, so.Cooldown);
            handler.PerformShipControllerActionsReplicated(input);
            handler.StopShipControllerActionsReplicated(input);
            return true;
        }

        // ── Chain spikes ─────────────────────────────────────────────────────

        /// <summary>
        /// SERVER. Tap the chain-spike trigger if the meter can pay and the tap interval has run.
        /// Press and release in the same call: the trigger is tap-for-shotgun / hold-for-burst,
        /// and an AI that held it would charge a burst it never released.
        /// </summary>
        /// <param name="intervalSeconds">Seconds between taps; null uses the config.</param>
        /// <param name="minAmmo01">Minimum meter fraction; null uses the config.</param>
        public bool TrySpike(IPlayer pilot, float? intervalSeconds = null, float? minAmmo01 = null)
        {
            var status = pilot?.Vessel?.VesselStatus;
            var handler = status?.ActionHandler;
            if (handler == null) return false;

            var s = State(pilot, status);
            float now = Time.time;
            if (now < s.NextSpike) return false;

            if (!handler.TryGetBoundAction<UrchinSpikeActionSO>(out _, out var input)) return false;
            if (!CanAffordSpike(status, minAmmo01 ?? _config.SpikeMinAmmo)) return false;

            s.NextSpike = now + Mathf.Max(0.1f, intervalSeconds ?? _config.SpikeIntervalSeconds);
            handler.PerformShipControllerActionsReplicated(input);
            handler.StopShipControllerActionsReplicated(input);
            return true;
        }

        /// <summary>
        /// True while the meter covers a tap's authored cost AND sits at or above
        /// <paramref name="minAmmo01"/> of its maximum. The meter and the cost are read off the
        /// bound spike ability (<see cref="UrchinSpikeActionSO.AmmoIndex"/> /
        /// <see cref="UrchinSpikeActionSO.AmmoCost"/>) - the same two numbers the executor's own
        /// <c>CanPay</c> checks - so a retune cannot leave the AI pressing a trigger that no-ops.
        /// </summary>
        public bool CanAffordSpike(IVesselStatus status, float minAmmo01)
        {
            var handler = status?.ActionHandler;
            if (handler == null || !handler.TryGetBoundAction<UrchinSpikeActionSO>(out var so, out _) || so == null)
                return false;

            var resources = status.ResourceSystem != null ? status.ResourceSystem.Resources : null;
            if (resources == null || resources.Count == 0) return true;   // no meter: never gate
            int index = so.AmmoIndex;
            if (index < 0 || index >= resources.Count || resources[index] == null) return true;

            var ammo = resources[index];
            if (ammo.CurrentAmount < so.AmmoCost) return false;
            return ammo.MaxAmount <= 0f || ammo.CurrentAmount / ammo.MaxAmount >= minAmmo01;
        }

        /// <summary>True when the prism under <paramref name="pilot"/>'s ride is not its own colour.</summary>
        public static bool IsHostileUnderfoot(IPlayer pilot)
        {
            var prism = pilot?.Vessel?.VesselStatus?.AttachedPrism;
            return prism && prism.Domain != pilot.Domain;
        }

        // ── Slip ─────────────────────────────────────────────────────────────

        /// <summary>
        /// SERVER. Let go of the rail and phase out for the Slip's ghost window. Rate-limited by
        /// <see cref="UrchinAutopilotConfigSO.MinSlipIntervalSeconds"/>, so a rail lying between
        /// the pilot and its objective cannot hold it in a slip / re-latch loop.
        /// </summary>
        public bool TrySlip(IPlayer pilot)
        {
            var status = pilot?.Vessel?.VesselStatus;
            var handler = status?.ActionHandler;
            if (handler == null) return false;

            var s = State(pilot, status);
            float now = Time.time;
            if (now < s.NextSlip) return false;

            if (!handler.TryGetBoundAction<UrchinSlipActionSO>(out _, out var input)) return false;

            s.NextSlip = now + _config.MinSlipIntervalSeconds;
            handler.PerformShipControllerActionsReplicated(input);
            handler.StopShipControllerActionsReplicated(input);
            return true;
        }

        // ── Bookkeeping ──────────────────────────────────────────────────────

        /// <summary>Forget one pilot (a pilot who left the match).</summary>
        public void Forget(IPlayer pilot)
        {
            if (pilot != null) _states.Remove(pilot);
        }

        /// <summary>Forget every pilot. Call at teardown and on a replay.</summary>
        public void Clear() => _states.Clear();

        PilotState State(IPlayer pilot, IVesselStatus status)
        {
            if (!_states.TryGetValue(pilot, out var s))
            {
                s = new PilotState();
                _states[pilot] = s;
            }

            // Re-resolved when the pilot's hull changes (a pilot swap hands a bot a different
            // vessel) or a component found earlier has since been destroyed - Unity's == is what
            // catches that. Never per frame for a hull that simply has no such component.
            var root = status?.Transform;
            if (root != null && (s.ResolvedFor != root ||
                                 (s.HadFollower && s.Follower == null) ||
                                 (s.HadTrackExecutor && s.TrackExecutor == null)))
            {
                if (s.ResolvedFor != root)
                {
                    s.MovingSince = -1f;
                    s.AssessedTrail = null;
                }
                s.Follower = root.GetComponentInChildren<TrailFollower>(true);
                s.TrackExecutor = root.GetComponentInChildren<UrchinTrackActionExecutor>(true);
                s.HadFollower = s.Follower != null;
                s.HadTrackExecutor = s.TrackExecutor != null;
                s.ResolvedFor = root;
            }
            return s;
        }
    }
}
