using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Skim Race pilot's decision core: observation in, stick/throttle/drift out. No Unity
    /// object references, no scene access, no side effects beyond its own small state — which is
    /// what lets the same class run in the game and in an offline harness.
    ///
    /// Three layers, in order:
    ///
    /// <b>1. Where to go (the racing line).</b> Between crystals the pilot follows the visible
    /// track a fixed height above the ribbon — that is where the skim boost is — looking ahead a
    /// fixed TIME of travel, so the line is speed-aware. As the next crystal comes up along the
    /// track the aim blends onto it, and inside <see cref="SkimRaceAIConfigSO.CrystalDirectDistance"/>
    /// it is flown at directly. A crystal BEHIND the pilot on the course (overshot) is chased
    /// directly rather than followed a lap round.
    ///
    /// <b>2. How to steer (lag compensation).</b> The hull does not turn when the stick turns: the
    /// stick rotates a COMMANDED orientation at up to the hull's turn rate, and the visible hull
    /// slerps toward it at <see cref="SkimRaceObservation.FollowRate"/> per second (~0.67 s time
    /// constant). Steering off the hull alone overshoots every corner by up to ~80°. So the
    /// controller decides where the hull should point, places the commanded heading a bounded
    /// LEAD past the hull toward it, and drives the stick to put the commanded heading there.
    ///
    /// <b>3. How fast (throttle and recovery).</b> Full throttle, eased only when the crystal sits
    /// inside the turning circle the current speed allows (the Dubins test the platform's
    /// <see cref="PursuitReachability"/> also uses) and during recovery. Recovery engages when the
    /// distance to the crystal has not closed for <see cref="SkimRaceAIConfigSO.StallSeconds"/>
    /// (orbiting, wedged, lost) and flies a slower direct pursuit for a bounded time.
    /// </summary>
    public sealed class SkimRaceDriver
    {
        public enum Mode { Idle = 0, Racing = 1, Recovering = 2 }

        public struct Diagnostics
        {
            public Mode Mode;
            public Vector3 AimPoint;
            public Vector3 DesiredDirection;
            public float HeadingErrorDegrees;
            public float CommandErrorDegrees;
            public bool CrystalPull;
            public bool Unreachable;
            public int Recoveries;
            public string LastRecoveryReason;
        }

        readonly SkimRaceAIConfigSO _cfg;

        Mode _mode = Mode.Idle;
        float _recoveryUntil;
        int _recoveries;
        string _lastRecoveryReason = "";

        // progress tracking against the current target
        Vector3 _trackedTargetPos;
        bool _hasTrackedTarget;
        float _bestDistance;
        float _sinceProgress;
        float _sinceTarget;
        bool _drifting;

        Diagnostics _diag;

        // The current crystal's pass, in the ribbon frame at the crystal's own arc length.
        bool _bumpActive;
        bool _bumpCrossing;
        bool _centreInSlab;
        float _side = 1f;          // which face of the ribbon the line skims: +1 normal side, -1 underside
        float _pendingSide = 1f;   // the face the current crystal's pass leaves the line on
        float _crossLateralSign = 1f;
        float _bumpCentre;
        float _passLateral;
        float _passHeight;
        Vector3 _passWorld;

        /// <summary>Signed arc distance from <paramref name="b"/> to <paramref name="a"/>, in (-L/2, L/2].</summary>
        static float SignedDelta(SkimRaceCourse c, float a, float b)
        {
            float d = c.Wrap(a - b);
            return d > c.Length * 0.5f ? d - c.Length : d;
        }

        static float Bump(float ds, float halfWidth)
        {
            halfWidth = Mathf.Max(1f, halfWidth);
            return Mathf.Abs(ds) >= halfWidth ? 0f : 0.5f * (1f + Mathf.Cos(Mathf.PI * ds / halfWidth));
        }

        static void Frame(SkimRaceCourse c, float s, out Vector3 point, out Vector3 tangent, out Vector3 normal, out Vector3 lateral)
        {
            point = c.Sample(s, out tangent, out normal);
            lateral = Vector3.Cross(normal, tangent);
            if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.Cross(Vector3.up, tangent);
            lateral.Normalize();
            normal = Vector3.Cross(tangent, lateral).normalized;
        }

        /// <summary>
        /// Where to pass the crystal. The crystal is a sphere the hull only has to TOUCH, so the
        /// pass point is the point within (capture radius - margin) of it that deviates least from
        /// the skim line — usually nowhere near the crystal's centre.
        ///
        /// The ribbon is a thin slab the hull must never enter (a strike resets the boost), and it
        /// skims equally well from either face, so the line keeps to ONE face (<c>_side</c>) and
        /// changes face only when a crystal sits too deep on the other side to be reached from
        /// this one. That change is flown round the ribbon's EDGE: a lateral swing past the slab's
        /// half-width that is complete before the height crosses the ribbon plane.
        /// </summary>
        void PlanPass(in SkimRaceObservation o, SkimRaceCourse course)
        {
            float sTarget = course.Wrap(o.CourseProgress + o.TargetAheadOnCourse);
            Frame(course, sTarget, out Vector3 cp, out Vector3 t, out Vector3 n, out Vector3 l);
            Vector3 rel = o.TargetPosition - cp;
            float cl = Vector3.Dot(rel, l), ch = Vector3.Dot(rel, n), ct = Vector3.Dot(rel, t);
            float H = Mathf.Abs(_cfg.SkimHeight);
            float R = Mathf.Max(2f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.PassMargin);
            float clearH = _cfg.RibbonClearHeight;

            float side = _side;
            ClosestOnDisk(cl, ch, R, 0f, side * H, out float ql, out float qh);
            bool crossing = false;
            if (qh * side < clearH)
            {
                if ((ch + side * R) * side >= clearH)
                {
                    // Reachable from this face: touch the disk at the slab's surface.
                    qh = side * clearH;
                    float span = Mathf.Sqrt(Mathf.Max(0f, R * R - (qh - ch) * (qh - ch)));
                    ql = Mathf.Clamp(0f, cl - span, cl + span);
                }
                else
                {
                    // Too deep on the far face: change face for this crystal and stay there.
                    side = -side;
                    ClosestOnDisk(cl, ch, R, 0f, side * H, out ql, out qh);
                    if (qh * side < clearH) qh = side * clearH;
                    crossing = true;
                }
            }

            _centreInSlab = Mathf.Abs(ch) < clearH + 1f && Mathf.Abs(cl) < _cfg.RibbonClearLateral;
            _bumpActive = true;
            _bumpCrossing = crossing;
            _bumpCentre = course.Wrap(sTarget + ct);
            _passLateral = ql;
            _passHeight = qh;
            _pendingSide = side;
            _crossLateralSign = cl >= 0f ? 1f : -1f;
            _passWorld = cp + t * ct + l * ql + n * qh;
        }

        /// <summary>
        /// The safety layer under the racing line. Pure pursuit cuts corners, so a line that is
        /// correct (the face change swings round the edge first) can still be flown through the
        /// ribbon. If, within <see cref="SkimRaceAIConfigSO.SlabGuardSeconds"/>, the hull would be
        /// inside the slab's edge AND within its half-height, the aim is replaced: hold this face's
        /// height, and if the line wants the other face, swing out past the edge first.
        /// </summary>
        bool GuardSlab(in SkimRaceObservation o, SkimRaceCourse course, ref Vector3 aim)
        {
            Frame(course, o.CourseProgress, out Vector3 p, out Vector3 t, out Vector3 n, out Vector3 l);
            Vector3 rel = o.Position - p;
            float vl = Vector3.Dot(rel, l), vh = Vector3.Dot(rel, n);
            float tau = _cfg.SlabGuardSeconds;
            float pl = vl + Vector3.Dot(o.Velocity, l) * tau;
            float ph = vh + Vector3.Dot(o.Velocity, n) * tau;
            float clearL = _cfg.RibbonClearLateral, clearH = _cfg.RibbonClearHeight;
            float face = Mathf.Abs(vh) > 0.5f ? Mathf.Sign(vh) : _side;

            bool insideEdgeNow = Mathf.Abs(vl) < clearL;
            bool insideEdgeSoon = Mathf.Abs(pl) < clearL;
            bool nearPlane = Mathf.Abs(ph) < clearH + 1.5f || Mathf.Sign(ph) != face;
            if (!(insideEdgeNow || insideEdgeSoon) || !nearPlane) return false;

            // Where the line itself wants to be, relative to the ribbon here.
            Vector3 aimRel = aim - p;
            float al = Vector3.Dot(aimRel, l), ah = Vector3.Dot(aimRel, n);
            bool wantsOtherFace = Mathf.Sign(ah) != face && Mathf.Abs(ah) > clearH;

            float ahead = Mathf.Max(60f, o.Speed * 0.5f);
            float holdH = face * (clearH + 3f);
            float latTarget = wantsOtherFace
                ? (Mathf.Abs(al) > 1f ? Mathf.Sign(al) : (vl >= 0f ? 1f : -1f)) * (clearL + 6f)
                : vl;
            aim = p + t * ahead + n * holdH + l * latTarget;
            return true;
        }

        int _guardHint = -1;

        /// <summary>
        /// The hull veto. Predicts where the hull will be over the next
        /// <see cref="SkimRaceAIConfigSO.HullGuardSeconds"/> — the nose easing toward the heading
        /// already COMMANDED at the hull's follow rate, which is how the transformer will actually
        /// move it — and tests each predicted point against the nearby track prisms' contact
        /// shells. If any comes within <see cref="SkimRaceAIConfigSO.HullMargin"/>, the aim is
        /// replaced by an escape along that prism's normal on the side the hull is already on.
        /// Nothing in the race is worth a strike: it resets the boost to 1x.
        /// </summary>
        bool GuardHull(in SkimRaceObservation o, SkimRaceCourse course, ref Vector3 aim)
        {
            if (!course.HasShells) return false;
            course.Project(o.Position, ref _guardHint, out _, out _);
            const int steps = 6;
            float T = _cfg.HullGuardSeconds;
            Vector3 p = o.Position;
            float speed = Mathf.Max(o.Speed, 1f);
            int threat = -1;
            float prevT = 0f;
            for (int k = 1; k <= steps; k++)
            {
                float t = T * k / steps;
                float w = 1f - Mathf.Exp(-o.FollowRate * t);
                Vector3 fwd = Vector3.Slerp(o.Forward, o.CommandedForward, w);
                p += fwd * speed * (t - prevT);
                prevT = t;
                if (course.ShellClearance(p, _guardHint, 8, out int idx) < _cfg.HullMargin) { threat = idx; break; }
            }
            if (threat < 0) return false;

            Vector3 up = course.PrismUp(threat);
            float h = Vector3.Dot(o.Position - course.PointAtIndex(threat), up);
            float side = Mathf.Abs(h) > 0.3f ? Mathf.Sign(h) : _side;
            aim = o.Position + o.Forward * Mathf.Max(40f, speed * 0.35f) + up * (side * _cfg.HullEscapeLift);
            _hullVetoes++;
            return true;
        }

        int _hullVetoes;
        int _massVetoes;
        float _approachW = 300f;

        /// <summary>
        /// Laid mass near the pilot this decision (trail rails, pickup rings), filled by the caller
        /// from what it can see. Empty = no laid-mass guard this decision.
        /// </summary>
        public readonly List<SkimRaceObstacle> Obstacles = new();
        public int MassVetoes => _massVetoes;

        static readonly float[] GuardSticks = { -1f, -0.5f, 0f, 0.5f, 1f };

        /// <summary>
        /// Rolls the Squirrel forward (the transformer's own dynamics, as in
        /// <see cref="SkimRacePlanner"/>) holding the stick for <see cref="SkimRaceAIConfigSO.MassGuardSegment"/>
        /// and then pursuing <paramref name="aim"/>, and returns the hull's closest approach to any
        /// laid-mass box, sampling the hull's centre, wingtips and nose/tail.
        /// </summary>
        float RolloutClearance(in SkimRaceObservation o, float yaw, float pitch, float throttle, Vector3 aim,
            float stopBelow, out Vector3 endPos)
        {
            float H = _cfg.MassGuardSeconds, T1 = _cfg.MassGuardSegment, dt = _cfg.MassGuardStep;
            Quaternion rot = o.Rotation, cmd = o.CommandedRotation;
            Vector3 pos = o.Position;
            float speed = o.Speed;
            float boost = Mathf.Max(1f, o.BoostMultiplier);
            float minC = float.PositiveInfinity;
            float w = _cfg.HullHalfWidth, l = _cfg.HullHalfLength;
            for (float t = 0f; t < H; t += dt)
            {
                float y = yaw, p = pitch;
                if (t >= T1)
                {
                    Vector3 cf = cmd * Vector3.forward;
                    Vector3 to = aim - pos;
                    Vector3 axis = Vector3.Cross(cf, to.normalized);
                    float stick = Mathf.Clamp01(Vector3.Angle(cf, to) * _cfg.StickGainPerDegree);
                    Vector3 hu = rot * Vector3.up, hr = rot * Vector3.right;
                    float u = Vector3.Dot(axis, hu), r = Vector3.Dot(axis, hr);
                    float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(r), 1e-4f);
                    y = stick * u / m; p = stick * r / m;
                }
                cmd = Quaternion.AngleAxis(y * o.TurnRateDegrees * dt, rot * Vector3.up) * cmd;
                cmd = Quaternion.AngleAxis(p * o.TurnRateDegrees * dt, rot * Vector3.right) * cmd;
                rot = Quaternion.Slerp(rot, cmd, o.FollowRate * dt);
                boost = boost > 1f ? boost - 0.3f * dt : 1f;
                speed = Mathf.Lerp(speed, throttle * o.ThrottleScaler * boost, o.FollowRate * dt);
                pos += (rot * Vector3.forward) * speed * dt;

                Vector3 right = rot * (Vector3.right * w), fwd = rot * (Vector3.forward * l);
                for (int i = 0; i < Obstacles.Count; i++)
                {
                    var ob = Obstacles[i];
                    float reach = ob.Half.magnitude + w + l + _cfg.MassGuardMargin;
                    if ((ob.Center - pos).sqrMagnitude > reach * reach) continue;
                    float c = Mathf.Min(ob.Distance(pos), Mathf.Min(ob.Distance(pos + right), ob.Distance(pos - right)));
                    c = Mathf.Min(c, Mathf.Min(ob.Distance(pos + fwd), ob.Distance(pos - fwd)));
                    if (c < minC) minC = c;
                }
                if (minC < stopBelow) break;
            }
            endPos = pos;
            return minC;
        }

        /// <summary>
        /// The laid-mass guard. Returns true when the commanded stick was replaced because its
        /// rollout touches a trail rail or ring prism (a hull contact resets the skim boost).
        /// </summary>
        bool GuardMass(in SkimRaceObservation o, Vector3 aim, ref float yaw, ref float pitch, float throttle)
        {
            float margin = _cfg.MassGuardMargin;
            float nominal = RolloutClearance(o, yaw, pitch, throttle, aim, margin, out Vector3 nominalEnd);
            if (nominal >= margin) return false;

            float bestCost = float.MaxValue, bestYaw = yaw, bestPitch = pitch;
            for (int a = 0; a < GuardSticks.Length; a++)
            for (int b = 0; b < GuardSticks.Length; b++)
            {
                float cy = GuardSticks[a], cp = GuardSticks[b];
                float c = RolloutClearance(o, cy, cp, throttle, aim, margin, out Vector3 end);
                // Safe candidates first, then the one that stays closest to the intended path.
                float cost = (c >= margin ? 0f : 1000f + 100f * (margin - Mathf.Min(c, margin)))
                             + Vector3.Distance(end, nominalEnd) * 0.05f
                             + 0.1f * (Mathf.Abs(cy - yaw) + Mathf.Abs(cp - pitch));
                if (cost < bestCost) { bestCost = cost; bestYaw = cy; bestPitch = cp; }
            }
            yaw = bestYaw;
            pitch = bestPitch;
            _massVetoes++;
            return true;
        }

        SkimRacePlanner _planner;
        SkimRacePlanner.Result _plan;
        float _nextPlan;
        int _planHint = -1;
        float _ringReadyAt;
        float _ringHoldUntil;
        int _rings;
        public int RingsLaid => _rings;
        public int HullVetoes => _hullVetoes;

        static void ClosestOnDisk(float cl, float ch, float r, float nl, float nh, out float ql, out float qh)
        {
            float dl = nl - cl, dh = nh - ch;
            float dm = Mathf.Sqrt(dl * dl + dh * dh);
            float k = dm > r ? r / dm : 1f;
            ql = cl + dl * k;
            qh = ch + dh * k;
        }

        /// <summary>The racing line at arc length <paramref name="s"/>: ribbon + face height + crystal pass.</summary>
        Vector3 LinePoint(SkimRaceCourse c, float s)
        {
            Frame(c, s, out Vector3 p, out _, out Vector3 n, out Vector3 l);
            float H = Mathf.Abs(_cfg.SkimHeight);
            if (!_bumpActive) return p + n * (_side * H);

            float ds = SignedDelta(c, s, _bumpCentre);
            float W = _approachW;
            if (!_bumpCrossing)
            {
                float w = Bump(ds, W);
                float baseH = _side * H;
                return p + n * (baseH + (_passHeight - baseH) * w) + l * (_passLateral * w);
            }

            // Face change: height ramps from this face to the pass height over the second half of
            // the approach, while a wider lateral swing holds the line outside the slab's edge.
            float rampStart = -W, rampEnd = -W * (1f - _cfg.CrossingHeightFraction);
            float u = Mathf.Clamp01((ds - rampStart) / Mathf.Max(1f, rampEnd - rampStart));
            u = u * u * (3f - 2f * u);
            float hgt = Mathf.Lerp(_side * H, _passHeight, ds >= rampEnd ? 1f : u);
            float swingCentre = (rampStart + rampEnd) * 0.5f;
            float swing = _crossLateralSign * (_cfg.RibbonClearLateral + 4f) * Bump(ds - swingCentre, W * 0.9f);
            float lat = Mathf.Lerp(swing, _passLateral, Mathf.Clamp01((ds - rampEnd) / Mathf.Max(1f, -rampEnd)));
            return p + n * hgt + l * lat;
        }

        public SkimRaceDriver(SkimRaceAIConfigSO config)
        {
            _cfg = config;
        }

        public Mode CurrentMode => _mode;
        public int Recoveries => _recoveries;
        public float TimeSinceProgress => _sinceProgress;
        public Diagnostics LastDiagnostics => _diag;

        /// <summary>Forget everything about the previous race. Call on race start and reset.</summary>
        public void Reset()
        {
            _mode = Mode.Idle;
            _recoveryUntil = 0f;
            _recoveries = 0;
            _lastRecoveryReason = "";
            _hasTrackedTarget = false;
            _bestDistance = float.MaxValue;
            _sinceProgress = 0f;
            _sinceTarget = 0f;
            _drifting = false;
            _diag = default;
            _side = Mathf.Sign(_cfg.SkimHeight) == 0f ? 1f : Mathf.Sign(_cfg.SkimHeight);
            _guardHint = -1;
            _hullVetoes = 0;
            _massVetoes = 0;
            Obstacles.Clear();
            _ringReadyAt = 0f;
            _ringHoldUntil = 0f;
            _nextPlan = 0f;
            _plan = default;
            _planHint = -1;
            _rings = 0;
            _pendingSide = _side;
            _bumpActive = false;
        }

        /// <summary>
        /// One decision. <paramref name="course"/> may be null (track not laid yet): the pilot
        /// then flies straight at the crystal. <paramref name="now"/> is race time in seconds.
        /// </summary>
        public SkimRaceAction Decide(in SkimRaceObservation o, SkimRaceCourse course, float now, float dt)
        {
            if (_mode == Mode.Idle) _mode = Mode.Racing;
            dt = Mathf.Max(dt, 1e-4f);

            UpdateProgress(o, now, dt);

            // ── 1. Aim point ────────────────────────────────────────────────
            bool haveCourse = course != null && o.HasCourse;
            bool pull = false;
            Vector3 aim;
            _bumpActive = false;

            // Approach length follows the boost bank: with boost to spend the line can leave skim
            // range early; with none, it stays on the ribbon and rebuilds it before rising.
            float approach = Mathf.Lerp(_cfg.LowBoostApproachScale, 1f,
                Mathf.Clamp01((o.BoostMultiplier - 1f) / Mathf.Max(0.01f, _cfg.LowBoostFull - 1f)));
            _approachW = _cfg.CrystalBumpHalfWidth * approach;
            float directDistance = _cfg.CrystalDirectDistance * approach;

            if (haveCourse && o.HasTarget)
                PlanPass(o, course);

            bool behind = haveCourse && o.HasTarget && o.TargetAheadOnCourse > o.CourseLength * 0.5f;
            bool inFront = o.HasTarget && o.TargetAlignment > 0.35f;
            Vector3 passPoint = _bumpActive ? _passWorld : o.TargetPosition;
            float passDist = Vector3.Distance(o.Position, passPoint);

            if (o.HasTarget && (!haveCourse || _mode == Mode.Recovering || behind))
            {
                aim = passPoint;
                pull = true;
            }
            else if (o.HasTarget && passDist <= directDistance && inFront)
            {
                // Terminal: fly at the pass point itself, which pure pursuit would otherwise cut -
                // biased toward the crystal's centre when that does not take the hull into the
                // ribbon's slab, so steering error near the pickup eats margin, not the pickup.
                aim = passPoint;
                if (_bumpActive && !_centreInSlab)
                    aim = Vector3.Lerp(passPoint, o.TargetPosition, _cfg.TerminalCentreBias);
                pull = true;
            }
            else if (haveCourse)
            {
                float look = Mathf.Clamp(o.Speed * _cfg.LookaheadSeconds, _cfg.LookaheadMin, _cfg.LookaheadMax);
                aim = LinePoint(course, o.CourseProgress + look);
                pull = _bumpActive && Mathf.Abs(SignedDelta(course, o.CourseProgress + look, _bumpCentre)) < _approachW;
            }
            else
            {
                aim = o.Position + o.Forward * 100f;
            }

            // ── 1b. Slab guard: never let the hull approach the ribbon plane inside its edge ─
            bool guarded = false;
            if (haveCourse && _cfg.SlabGuardSeconds > 0f)
                guarded = GuardSlab(o, course, ref aim);
            if (haveCourse && _cfg.HullGuardSeconds > 0f && GuardHull(o, course, ref aim))
                guarded = true;

            Vector3 toAim = aim - o.Position;
            Vector3 desired = toAim.sqrMagnitude > 1e-4f ? toAim.normalized : o.Forward;

            // ── 2. Lag-compensated steering ────────────────────────────────
            float headingErr = Vector3.Angle(o.Forward, desired);
            Vector3 cmdTarget = desired;
            if (headingErr > 0.05f)
            {
                Vector3 axis = Vector3.Cross(o.Forward, desired);
                if (axis.sqrMagnitude < 1e-8f) axis = o.Up; // anti-parallel: pick a side
                float lead = Mathf.Min(headingErr * _cfg.LeadGain, headingErr + _cfg.MaxLeadDegrees, 179f);
                cmdTarget = Quaternion.AngleAxis(lead, axis.normalized) * o.Forward;
            }

            float cmdErr = Vector3.Angle(o.CommandedForward, cmdTarget);
            float yaw = 0f, pitch = 0f;
            if (cmdErr > 0.01f)
            {
                Vector3 axis = Vector3.Cross(o.CommandedForward, cmdTarget);
                if (axis.sqrMagnitude < 1e-8f) axis = o.Up;
                axis.Normalize();
                float stick = Mathf.Clamp01(cmdErr * _cfg.StickGainPerDegree);
                // Pitch rotates about the hull's right axis, yaw about its up axis (see
                // VesselTransformer.Pitch/Yaw), so the split is the rotation axis expressed in
                // hull space. A component along the hull's forward would be roll: ignored.
                float u = Vector3.Dot(axis, o.Up);
                float r = Vector3.Dot(axis, o.Right);
                float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(r), 1e-4f);
                yaw = stick * u / m;
                pitch = stick * r / m;
            }

            // Cosmetic levelling only: roll the wings toward the ribbon plane.
            float roll = 0f;
            if (_cfg.LevelingRoll > 0f)
            {
                Vector3 refUp = Vector3.up;
                if (haveCourse) course.Sample(o.CourseProgress, out _, out refUp);
                roll = Mathf.Clamp(-Vector3.Dot(o.Right, refUp) * _cfg.LevelingRoll * 2f, -_cfg.LevelingRoll, _cfg.LevelingRoll);
            }

            // ── 3. Throttle ─────────────────────────────────────────────────
            float throttle = _cfg.CruiseThrottle;
            bool unreachable = false;
            if (o.HasTarget && pull)
            {
                float omega = o.TurnRateDegrees * Mathf.Deg2Rad;
                float radius = o.Speed / Mathf.Max(omega, 1e-3f);
                // The hull lags the stick, so the circle it can actually fly is wider.
                radius += o.Speed / Mathf.Max(o.FollowRate, 0.1f) * 0.5f * Mathf.Clamp01(headingErr / 90f);
                float off = Vector3.Angle(o.Forward, o.ToTarget) * Mathf.Deg2Rad;
                float need = 2f * radius * Mathf.Sin(Mathf.Min(off, Mathf.PI * 0.5f)) * _cfg.ReachabilityMargin;
                if (off > Mathf.PI * 0.5f) need = 2f * radius * _cfg.ReachabilityMargin;
                if (o.TargetDistance < need)
                {
                    unreachable = true;
                    throttle = _cfg.MinThrottle;
                }
            }
            if (_mode == Mode.Recovering) throttle = Mathf.Min(throttle, _cfg.RecoveryThrottle);

            // ── Planner (optional): replaces the pursuit command with the best rolled-out one ──
            if (_cfg.UsePlanner && o.HasTarget && haveCourse)
            {
                if (now >= _nextPlan)
                {
                    _nextPlan = now + 1f / Mathf.Max(1f, _cfg.PlannerHz);
                    _planner ??= new SkimRacePlanner(_cfg);
                    course.Project(o.Position, ref _planHint, out _, out _);
                    _plan = _planner.Plan(o, course, passPoint, _planHint, yaw, pitch, throttle);
                }
                yaw = _plan.Yaw;
                pitch = _plan.Pitch;
                throttle = _plan.Throttle;
            }

            // ── Laid-mass guard: never fly the hull into trail rails or pickup-ring prisms ──
            if (_cfg.MassGuardSeconds > 0f && Obstacles.Count > 0 && GuardMass(o, aim, ref yaw, ref pitch, throttle))
                guarded = true;

            // ── Boost Ring launch (optional) ────────────────────────────────
            bool ring = false;
            if (_cfg.UseLaunchRing)
            {
                if (now < _ringHoldUntil)
                {
                    // Hold the line the ring was laid on: no new stick, so the commanded heading -
                    // and with it the hull - stays on the ring's axis until it is through.
                    yaw = pitch = 0f;
                    throttle = _cfg.CruiseThrottle;
                }
                else if (now >= _ringReadyAt
                         && o.BoostMultiplier < _cfg.RingBelowBoost
                         && headingErr < _cfg.RingAlignDegrees
                         && Vector3.Angle(o.Forward, o.CommandedForward) < _cfg.RingAlignDegrees
                         && !guarded
                         && (!o.HasTarget || o.TargetDistance > _cfg.RingForwardDistance + 60f))
                {
                    ring = true;
                    _ringReadyAt = now + _cfg.RingCooldownSeconds;
                    _ringHoldUntil = now + (_cfg.RingForwardDistance + 25f) / Mathf.Max(o.Speed, 30f);
                    _rings++;
                }
            }

            // ── Drift (optional) ────────────────────────────────────────────
            if (_cfg.UseDrift)
            {
                if (!_drifting && headingErr > _cfg.DriftEnterDegrees) _drifting = true;
                else if (_drifting && headingErr < _cfg.DriftExitDegrees) _drifting = false;
            }
            else _drifting = false;

            _diag = new Diagnostics
            {
                Mode = _mode,
                AimPoint = aim,
                DesiredDirection = desired,
                HeadingErrorDegrees = headingErr,
                CommandErrorDegrees = cmdErr,
                CrystalPull = pull,
                Unreachable = unreachable,
                Recoveries = _recoveries,
                LastRecoveryReason = _lastRecoveryReason,
            };

            return new SkimRaceAction
            {
                Yaw = yaw,
                Pitch = pitch,
                Roll = roll,
                Throttle = throttle,
                Drift = _drifting,
                Ring = ring,
            }.Clamped();
        }

        void UpdateProgress(in SkimRaceObservation o, float now, float dt)
        {
            if (!o.HasTarget)
            {
                _sinceTarget += dt;
                if (_sinceTarget > _cfg.MissingTargetGrace) _hasTrackedTarget = false;
                return;
            }
            _sinceTarget = 0f;

            // A new crystal (or the same crystal respawned elsewhere) restarts the progress clock.
            if (!_hasTrackedTarget || (o.TargetPosition - _trackedTargetPos).sqrMagnitude > 40f * 40f)
            {
                if (_hasTrackedTarget) _side = _pendingSide; // the previous crystal was taken: adopt its face
                _hasTrackedTarget = true;
                _bestDistance = o.TargetDistance;
                _sinceProgress = 0f;
                if (_mode == Mode.Recovering) _mode = Mode.Racing;
            }
            _trackedTargetPos = o.TargetPosition;

            if (o.TargetDistance < _bestDistance - _cfg.ProgressEpsilon)
            {
                _bestDistance = o.TargetDistance;
                _sinceProgress = 0f;
            }
            else
            {
                _sinceProgress += dt;
            }

            if (_mode == Mode.Recovering)
            {
                if (now >= _recoveryUntil)
                {
                    _mode = Mode.Racing;
                    _bestDistance = o.TargetDistance;
                    _sinceProgress = 0f;
                }
            }
            else if (_sinceProgress > _cfg.StallSeconds)
            {
                _mode = Mode.Recovering;
                _recoveryUntil = now + _cfg.RecoverySeconds;
                _recoveries++;
                _lastRecoveryReason = o.TargetDistance < 300f ? "orbit/overshoot near crystal" : "no closing progress";
                _bestDistance = o.TargetDistance;
                _sinceProgress = 0f;
            }
        }
    }
}
