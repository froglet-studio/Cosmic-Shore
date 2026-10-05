using System.Collections.Generic;
using Unity.Profiling;
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

        // The thinking's cost, by part, in the Unity Profiler (and tallied by the offline simulator under
        // the same names). Each wraps a whole step - at most once per decision, and TrackMpc / Planner
        // only on the decisions that re-plan - so the markers cost nothing that shows; the parts a policy
        // switches off never appear.
        static readonly ProfilerMarker s_DecideMarker = new("SkimRaceDriver.Decide");
        static readonly ProfilerMarker s_PlanPassMarker = new("SkimRaceDriver.PlanPass");
        static readonly ProfilerMarker s_GuardsMarker = new("SkimRaceDriver.Guards");
        static readonly ProfilerMarker s_PlannerMarker = new("SkimRaceDriver.Planner");
        static readonly ProfilerMarker s_TrackMpcMarker = new("SkimRaceDriver.TrackMpc");
        static readonly ProfilerMarker s_LevelApproachMarker = new("SkimRaceDriver.LevelApproach");
        static readonly ProfilerMarker s_MpcMarker = new("SkimRaceDriver.Mpc");
        static readonly ProfilerMarker s_GuardMassMarker = new("SkimRaceDriver.GuardMass");

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

        /// <summary>
        /// The lobby difficulty's deliberate mistakes, or null for none (Hard). When set, every
        /// decision is made on <see cref="SkimRaceHandicap.View"/> of the observation - what the pilot
        /// BELIEVES - while progress is still judged against the real crystal. Null leaves the driver
        /// exactly the unhandicapped pilot.
        /// </summary>
        public SkimRaceHandicap Handicap { get; set; }

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
            float H = LaneSkimHeight;
            float R = Mathf.Max(2f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.PassMargin);
            float clearH = _cfg.RibbonClearHeight;

            float side = _side;
            ClosestOnDisk(cl, ch, R, 0f, side * H, out float ql, out float qh);
            bool crossing = false;
            // A pass point BESIDE the ribbon (outside its lateral reach) is reachable from this face
            // without a face change, whatever its height: the line swings out first (the band clamp
            // in Compose holds it off the plane until it is past the edge), then drops. Only a pass
            // point over/under the slab itself needs the face-change machinery.
            bool beside = _cfg.BesidePassNoCrossing && Mathf.Abs(ql) >= _cfg.RibbonClearLateral;
            if (!beside && qh * side < clearH)
            {
                if ((ch + side * R) * side >= clearH)
                {
                    // Reachable from this face: touch the disk at the slab's surface.
                    qh = side * clearH;
                    float span = Mathf.Sqrt(Mathf.Max(0f, R * R - (qh - ch) * (qh - ch)));
                    ql = Mathf.Clamp(0f, cl - span, cl + span);
                }
                else if (_cfg.SidePassOverCrossing && SidePass(cl, ch, R, side * H, out float sl, out float sh))
                {
                    // Too deep for this face, but reachable from BESIDE the ribbon: take it out past
                    // the plate edge at its own height and stay on this face - no face change.
                    ql = sl;
                    qh = sh;
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
            float centre = course.Wrap(sTarget + ct);
            // Remember where this face change began (once per crystal), so the swing starts there.
            if (crossing && (float.IsNaN(_crossForCentre) || Mathf.Abs(SignedDelta(course, centre, _crossForCentre)) > 60f))
            {
                _crossForCentre = centre;
                _crossOriginDs = SignedDelta(course, o.CourseProgress, centre);
            }
            if (!crossing) _crossForCentre = float.NaN;
            _bumpCentre = centre;
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
        bool _direct;
        public bool DirectFlight => _direct;

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
        int _viaHint = -1;
        public int ViaPoints { get; private set; }

        /// <summary>
        /// Walks the straight line from the hull to <paramref name="target"/> and, at the first
        /// point where it would enter the ribbon's contact shell, returns a via-point that keeps
        /// it out: lifted along the plate's normal when the hull and the crystal are on the same
        /// face, or round the ribbon's EDGE when they are on opposite faces (a strip in space can
        /// only be crossed beside it). Only visible track geometry is used.
        /// </summary>
        bool DirectVia(in SkimRaceObservation o, SkimRaceCourse course, Vector3 target, out Vector3 via)
            => DirectVia(o, course, target, _cfg.DirectViaClearance, out via);

        bool DirectVia(in SkimRaceObservation o, SkimRaceCourse course, Vector3 target, float clearance, out Vector3 via)
        {
            via = target;
            Vector3 a = o.Position;
            float L = Vector3.Distance(a, target);
            float stop = L - Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) * 0.8f);
            int hint = _viaHint;
            int blockIdx = -1;
            for (float u = 4f; u < stop; u += 4f)
            {
                Vector3 p = Vector3.Lerp(a, target, u / L);
                course.Project(p, ref hint, out _, out float dist);
                if (dist > 60f) continue;
                if (course.ShellClearance(p, hint, 6, out int idx) < clearance) { blockIdx = idx; break; }
            }
            course.Project(a, ref _viaHint, out _, out _);
            if (blockIdx < 0) return false;

            Vector3 c0 = course.PointAtIndex(blockIdx), n = course.PrismUp(blockIdx), l = course.PrismRight(blockIdx);
            float ha = Vector3.Dot(a - c0, n), hb = Vector3.Dot(target - c0, n);
            if (Mathf.Abs(ha) > 0.5f && Mathf.Sign(ha) == Mathf.Sign(hb))
                via = c0 + n * (Mathf.Sign(ha) * _cfg.DirectViaLift);
            else
            {
                float side = Mathf.Sign(Vector3.Dot(a - c0, l) + Vector3.Dot(target - c0, l));
                if (side == 0f) side = 1f;
                via = c0 + l * (side * _cfg.DirectViaLateral) + n * (0.5f * (ha + hb));
            }
            ViaPoints++;
            return true;
        }

        /// <summary>
        /// Would the STRAIGHT line from the hull to <paramref name="target"/> pass through the
        /// ribbon's contact shell? (Same walk as <see cref="DirectVia"/>, answer only.)
        /// </summary>
        bool ChordBlocked(in SkimRaceObservation o, SkimRaceCourse course, Vector3 target)
            => ChordBlocked(o, course, target, _cfg.ChordClearance);

        bool ChordBlocked(in SkimRaceObservation o, SkimRaceCourse course, Vector3 target, float clearance)
        {
            int saved = ViaPoints;
            bool blocked = DirectVia(o, course, target, clearance, out _);
            ViaPoints = saved;
            return blocked;
        }

        // Track shells the guard also respects this decision (null = laid mass only), and the
        // extra margin it keeps from them (expressed against MassGuardMargin).
        SkimRaceCourse _guardCourse;
        int _guardCourseHint = -1;
        int _guardBaseHint = -1;

        SkimRaceCourse _rollCourse;   // set while the nominal command is line pursuit
        int _rollHint = -1;

        /// <summary>The driver's steering law: lead the hull, then drive the commanded heading there.</summary>
        void Steer(Vector3 fwd, Vector3 up, Vector3 right, Vector3 cmdFwd, Vector3 desired,
            out float yaw, out float pitch, out float headingErr, out float cmdErr)
        {
            headingErr = Vector3.Angle(fwd, desired);
            Vector3 cmdTarget = desired;
            if (headingErr > 0.05f)
            {
                Vector3 axis = Vector3.Cross(fwd, desired);
                if (axis.sqrMagnitude < 1e-8f) axis = up;
                float lead = Mathf.Min(headingErr * _cfg.LeadGain, headingErr + _cfg.MaxLeadDegrees, 179f);
                cmdTarget = Quaternion.AngleAxis(lead, axis.normalized) * fwd;
            }
            cmdErr = Vector3.Angle(cmdFwd, cmdTarget);
            yaw = 0f; pitch = 0f;
            if (cmdErr > 0.01f)
            {
                Vector3 axis = Vector3.Cross(cmdFwd, cmdTarget);
                if (axis.sqrMagnitude < 1e-8f) axis = up;
                axis.Normalize();
                float stick = Mathf.Clamp01(cmdErr * _cfg.StickGainPerDegree);
                float u = Vector3.Dot(axis, up), r = Vector3.Dot(axis, right);
                float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(r), 1e-4f);
                yaw = stick * u / m;
                pitch = stick * r / m;
            }
        }

        /// <summary>Drive the stick so the COMMANDED heading reaches <paramref name="cmdTarget"/> (the lower half of <see cref="Steer"/>).</summary>
        void StickToward(Vector3 up, Vector3 right, Vector3 cmdFwd, Vector3 cmdTarget, out float yaw, out float pitch, out float cmdErr)
        {
            cmdErr = Vector3.Angle(cmdFwd, cmdTarget);
            yaw = 0f; pitch = 0f;
            if (cmdErr <= 0.01f) return;
            Vector3 axis = Vector3.Cross(cmdFwd, cmdTarget);
            if (axis.sqrMagnitude < 1e-8f) axis = up;
            axis.Normalize();
            float stick = Mathf.Clamp01(cmdErr * _cfg.StickGainPerDegree);
            float u = Vector3.Dot(axis, up), r = Vector3.Dot(axis, right);
            float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(r), 1e-4f);
            yaw = stick * u / m;
            pitch = stick * r / m;
        }

        /// <summary>Unit tangent of the racing line at arc length <paramref name="s"/> (central difference).</summary>
        Vector3 LineTangent(SkimRaceCourse c, float s, float h)
        {
            Vector3 d = LinePoint(c, s + h) - LinePoint(c, s - h);
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// The line tracker (<see cref="SkimRaceAIConfigSO.UseLineTracker"/>). The hull's heading is a
        /// first-order lag of the commanded heading with tau = 1/FollowRate, so to fly heading h_d(t) the
        /// command must be h_d + tau x dh_d/dt. Along a line of curvature k at speed v that lead is the
        /// angle k x v x tau, about the line's binormal. The desired heading is the line tangent bent toward
        /// the line by the cross-track error (rejoin within TrackerConvergeSeconds of travel).
        /// Returns the commanded target direction; <paramref name="headingErr"/> is hull vs desired.
        /// </summary>
        Vector3 TrackerCommand(in SkimRaceObservation o, SkimRaceCourse c, out float headingErr, out Vector3 desired)
        {
            float v = Mathf.Max(o.Speed, 30f);
            float tau = 1f / Mathf.Max(o.FollowRate, 0.1f);
            int h = _trackerHint;
            float s0 = c.Project(o.Position, ref h, out _, out _);
            _trackerHint = h;
            float span = _cfg.TrackerCurvatureSpan;
            // Closest line point near the hull's own course progress (the line is a smooth offset of the course).
            float best = float.MaxValue, sBest = s0;
            for (float ds = -24f; ds <= 24f; ds += 6f)
            {
                float d2 = (LinePoint(c, s0 + ds) - o.Position).sqrMagnitude;
                if (d2 < best) { best = d2; sBest = s0 + ds; }
            }
            Vector3 lp = LinePoint(c, sBest);
            Vector3 t0 = LineTangent(c, sBest, span * 0.5f);
            Vector3 e = o.Position - lp;
            e -= t0 * Vector3.Dot(e, t0);
            float D = Mathf.Max(v * _cfg.TrackerConvergeSeconds, _cfg.TrackerConvergeMin);
            desired = (t0 * D - e).normalized;

            // Lag feedforward: the turn the line makes over the next tau of travel, applied as a lead.
            float sAhead = sBest + v * tau * 0.5f;
            Vector3 ta = LineTangent(c, sAhead - span, span * 0.5f), tb = LineTangent(c, sAhead + span, span * 0.5f);
            Vector3 axis = Vector3.Cross(ta, tb);
            float turnPerUnit = Vector3.Angle(ta, tb) / (2f * span);          // degrees per world unit
            float ffDeg = turnPerUnit * v * tau * _cfg.TrackerFeedforwardGain;

            headingErr = Vector3.Angle(o.Forward, desired);
            Vector3 cmdTarget = desired;
            if (headingErr > 0.05f)
            {
                Vector3 ax = Vector3.Cross(o.Forward, desired);
                if (ax.sqrMagnitude > 1e-8f)
                    cmdTarget = Quaternion.AngleAxis(Mathf.Min(headingErr * _cfg.TrackerHeadingGain, _cfg.TrackerMaxLeadDegrees), ax.normalized) * desired;
            }
            if (ffDeg > 0.05f && axis.sqrMagnitude > 1e-10f)
                cmdTarget = Quaternion.AngleAxis(Mathf.Min(ffDeg, _cfg.TrackerMaxLeadDegrees), axis.normalized) * cmdTarget;
            return cmdTarget;
        }
        int _trackerHint = -1;

        static readonly float[] CaptureThrottles = { 1f, 0.8f, 0.6f, 0.45f, 0.3f };

        /// <summary>
        /// Would the hull MISS the crystal's capture sphere (closest approach reached outside it within
        /// <see cref="SkimRaceAIConfigSO.CaptureHorizon"/>) if it pursued <paramref name="aim"/> at
        /// <paramref name="throttle"/>? Returns true unless such a miss is predicted. Rolls the transformer's own
        /// dynamics (commanded rotation at the turn rate, hull slerp at the follow rate, speed lerp) with
        /// this driver's steering law re-applied every step.
        /// </summary>
        bool RolloutCaptures(in SkimRaceObservation o, Vector3 aim, float throttle)
        {
            const float dt = 0.04f;
            Quaternion rot = o.Rotation, cmd = o.CommandedRotation;
            Vector3 pos = o.Position;
            float speed = o.Speed, boost = Mathf.Max(1f, o.BoostMultiplier);
            float r = Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.CaptureMargin);
            float prevD = (pos - o.TargetPosition).magnitude;
            for (float t = 0f; t < _cfg.CaptureHorizon; t += dt)
            {
                Steer(rot * Vector3.forward, rot * Vector3.up, rot * Vector3.right, cmd * Vector3.forward,
                    (aim - pos).normalized, out float y, out float p, out _, out _);
                cmd = Quaternion.AngleAxis(y * o.TurnRateDegrees * dt, rot * Vector3.up) * cmd;
                cmd = Quaternion.AngleAxis(p * o.TurnRateDegrees * dt, rot * Vector3.right) * cmd;
                rot = Quaternion.Slerp(rot, cmd, o.FollowRate * dt);
                boost = boost > 1f ? boost - 0.3f * dt : 1f;
                speed = Mathf.Lerp(speed, throttle * o.ThrottleScaler * boost, o.FollowRate * dt);
                pos += (rot * Vector3.forward) * speed * dt;
                float d = (pos - o.TargetPosition).magnitude;
                if (d <= r) return true;
                // Closest approach passed outside the sphere: a MISS. Still closing at the horizon is not
                // a miss - the crystal is simply further away than the rollout looks.
                if (d > prevD + 0.01f) return false;
                prevD = d;
            }
            return true;
        }

        // ── Level approach ───────────────────────────────────────────────────
        float _nextLevel;
        float _levelYaw, _levelPitch;
        bool _levelValid;
        int _levelHint = -1;
        public int LevelOverrides { get; private set; }

        /// <summary>
        /// Cost (seconds-equivalent, lower is better) of holding (<paramref name="yaw"/>, <paramref name="pitch"/>)
        /// for LevelSegment, then pursuing <paramref name="passPoint"/> with this driver's steering law until
        /// the predicted capture, then re-following the racing line for LevelExitSeconds - rolled through the
        /// transformer's own dynamics and tested against the visible track shells (centre and wingtips).
        /// </summary>
        float LevelCost(in SkimRaceObservation o, SkimRaceCourse course, float yaw, float pitch, float throttle, Vector3 passPoint)
        {
            const float dt = 0.05f;
            Quaternion rot = o.Rotation, cmd = o.CommandedRotation;
            Vector3 pos = o.Position;
            float speed = o.Speed, boost = Mathf.Max(1f, o.BoostMultiplier);
            float r = Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.CaptureMargin);
            float approachH = _cfg.LevelApproachSeconds * 1.5f;
            float captureAt = -1f, minClear = float.PositiveInfinity, prevD = (pos - o.TargetPosition).magnitude;
            int hint = _levelHint;
            float w = _cfg.HullHalfWidth;
            for (float t = 0f; t < approachH + _cfg.LevelExitSeconds; t += dt)
            {
                if (captureAt < 0f && t > approachH) break;
                if (captureAt >= 0f && t > captureAt + _cfg.LevelExitSeconds) break;
                float y = yaw, p = pitch;
                if (t >= _cfg.LevelSegment)
                {
                    Vector3 a;
                    if (captureAt < 0f) a = passPoint;
                    else
                    {
                        float sr = course.Project(pos, ref hint, out _, out _);
                        a = LinePoint(course, sr + _lookDist);
                    }
                    Steer(rot * Vector3.forward, rot * Vector3.up, rot * Vector3.right, cmd * Vector3.forward,
                        (a - pos).normalized, out y, out p, out _, out _);
                }
                cmd = Quaternion.AngleAxis(y * o.TurnRateDegrees * dt, rot * Vector3.up) * cmd;
                cmd = Quaternion.AngleAxis(p * o.TurnRateDegrees * dt, rot * Vector3.right) * cmd;
                rot = Quaternion.Slerp(rot, cmd, o.FollowRate * dt);
                boost = boost > 1f ? boost - 0.3f * dt : 1f;
                speed = Mathf.Lerp(speed, throttle * o.ThrottleScaler * boost, o.FollowRate * dt);
                pos += (rot * Vector3.forward) * speed * dt;

                course.Project(pos, ref hint, out _, out _);
                Vector3 wing = rot * (Vector3.right * w);
                float c = Mathf.Min(course.ShellClearance(pos, hint, 4, out _),
                    Mathf.Min(course.ShellClearance(pos + wing, hint, 4, out _), course.ShellClearance(pos - wing, hint, 4, out _)));
                if (c < minClear) minClear = c;
                if (c < _cfg.LevelStrikeMargin)
                    return _cfg.LevelStrikeCost + (captureAt < 0f ? approachH - t : 0f);

                if (captureAt < 0f)
                {
                    float d = (pos - o.TargetPosition).magnitude;
                    if (d <= r) captureAt = t;
                    else if (d > prevD + 0.01f) return 100f + d;   // passed the crystal outside its sphere
                    prevD = d;
                }
            }
            float time = captureAt >= 0f ? captureAt : approachH + prevD / Mathf.Max(speed, 60f);
            return time - _cfg.LevelClearanceWeight * Mathf.Min(minClear, _cfg.LevelClearanceCap);
        }

        void LevelApproach(in SkimRaceObservation o, SkimRaceCourse course, Vector3 passPoint, float now,
            float throttle, ref float yaw, ref float pitch)
        {
            if (now >= _nextLevel)
            {
                _nextLevel = now + 1f / Mathf.Max(1f, _cfg.LevelHz);
                course.Project(o.Position, ref _levelHint, out _, out _);
                float best = LevelCost(o, course, yaw, pitch, throttle, passPoint) - _cfg.LevelNominalBias;
                float by = yaw, bp = pitch;
                for (int a = 0; a < MpcSticks.Length; a++)
                for (int b = 0; b < MpcSticks.Length; b++)
                {
                    float c = LevelCost(o, course, MpcSticks[a], MpcSticks[b], throttle, passPoint);
                    if (c < best) { best = c; by = MpcSticks[a]; bp = MpcSticks[b]; }
                }
                _levelValid = !(by == yaw && bp == pitch);
                _levelYaw = by; _levelPitch = bp;
                if (_levelValid) LevelOverrides++;
            }
            if (_levelValid) { yaw = _levelYaw; pitch = _levelPitch; }
        }

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
            float capture = o.HasTarget ? Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - 1f) : 0f;
            _guardCourseHint = _guardBaseHint;
            for (float t = 0f; t < H; t += dt)
            {
                float y = yaw, p = pitch;
                if (t >= T1)
                {
                    // Continuation = what this driver will actually do next: re-follow the racing
                    // line from wherever the rollout has got to (or keep the fixed aim in terminal
                    // flight), with the same lag-compensated steering law as Decide.
                    Vector3 a = aim;
                    if (_rollCourse != null)
                    {
                        float sr = _rollCourse.Project(pos, ref _rollHint, out _, out _);
                        a = LinePoint(_rollCourse, sr + _lookDist);
                    }
                    Steer(rot * Vector3.forward, rot * Vector3.up, rot * Vector3.right, cmd * Vector3.forward,
                        (a - pos).normalized, out y, out p, out _, out _);
                }
                cmd = Quaternion.AngleAxis(y * o.TurnRateDegrees * dt, rot * Vector3.up) * cmd;
                cmd = Quaternion.AngleAxis(p * o.TurnRateDegrees * dt, rot * Vector3.right) * cmd;
                rot = Quaternion.Slerp(rot, cmd, o.FollowRate * dt);
                boost = boost > 1f ? boost - 0.3f * dt : 1f;
                speed = Mathf.Lerp(speed, throttle * o.ThrottleScaler * boost, o.FollowRate * dt);
                pos += (rot * Vector3.forward) * speed * dt;
                // The pickup happens first: nothing after it on this path matters to this decision.
                if (o.HasTarget && (pos - o.TargetPosition).sqrMagnitude <= capture * capture) break;

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
                if (_guardCourse != null)
                {
                    _guardCourse.Project(pos, ref _guardCourseHint, out _, out _); // keep the shell window on the rollout
                    // The ribbon's contact shell, measured from the hull centre and wingtips, offset
                    // so TrackGuardMargin maps onto the same threshold as laid mass.
                    float offset = _cfg.MassGuardMargin - _cfg.TrackGuardMargin;
                    float tc = _guardCourse.ShellClearance(pos, _guardCourseHint, 6, out _);
                    tc = Mathf.Min(tc, _guardCourse.ShellClearance(pos + right, _guardCourseHint, 6, out _));
                    tc = Mathf.Min(tc, _guardCourse.ShellClearance(pos - right, _guardCourseHint, 6, out _));
                    if (tc + offset < minC) minC = tc + offset;
                }
                if (minC < stopBelow) break;
            }
            endPos = pos;
            return minC;
        }

        // ── Model-predictive control ──────────────────────────────────────────
        static readonly float[] MpcSticks = { -1f, -0.5f, 0f, 0.5f, 1f };
        readonly HashSet<int> _mpcSkimmed = new();
        float _nextMpc;
        float _mpcYaw, _mpcPitch, _mpcThrottle = 1f;
        bool _mpcValid;
        public int MpcOverrides { get; private set; }

        /// <summary>
        /// Scores one command: held for MpcSegment, then the driver's own continuation (re-following
        /// the racing line, or the fixed aim in terminal flight), rolled through the transformer's
        /// dynamics against the visible track shells and laid mass. Lower is better, in
        /// seconds-equivalent: a strike is ruinous (it resets the boost), a pickup ends the rollout,
        /// banked boost is worth MpcBoostValue seconds per 1x.
        /// </summary>
        float MpcCost(in SkimRaceObservation o, SkimRaceCourse course, float yaw, float pitch, float throttle, Vector3 aim)
        {
            float H = _cfg.MpcHorizon, T1 = _cfg.MpcSegment, dt = _cfg.MpcStep;
            Quaternion rot = o.Rotation, cmd = o.CommandedRotation;
            Vector3 pos = o.Position;
            float speed = o.Speed, boost = Mathf.Max(1f, o.BoostMultiplier), b0 = boost;
            float w = _cfg.HullHalfWidth, l = _cfg.HullHalfLength;
            float capture = o.HasTarget ? Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.MpcCaptureMargin) : 0f;
            _mpcSkimmed.Clear();
            int hint = _rollHint;
            for (float t = 0f; t < H; t += dt)
            {
                float y = yaw, p = pitch;
                if (t >= T1)
                {
                    Vector3 a = aim;
                    if (_rollCourse != null)
                    {
                        float sr = _rollCourse.Project(pos, ref hint, out _, out _);
                        a = LinePoint(_rollCourse, sr + _lookDist);
                    }
                    Steer(rot * Vector3.forward, rot * Vector3.up, rot * Vector3.right, cmd * Vector3.forward,
                        (a - pos).normalized, out y, out p, out _, out _);
                }
                cmd = Quaternion.AngleAxis(y * o.TurnRateDegrees * dt, rot * Vector3.up) * cmd;
                cmd = Quaternion.AngleAxis(p * o.TurnRateDegrees * dt, rot * Vector3.right) * cmd;
                rot = Quaternion.Slerp(rot, cmd, o.FollowRate * dt);
                boost = boost > 1f ? boost - 0.3f * dt : 1f;
                float thr = t >= T1 ? 1f : throttle;
                speed = Mathf.Lerp(speed, thr * o.ThrottleScaler * boost, o.FollowRate * dt);
                pos += (rot * Vector3.forward) * speed * dt;

                if (o.HasTarget && (pos - o.TargetPosition).sqrMagnitude <= capture * capture)
                    return t - _cfg.MpcBoostValue * (Mathf.Min(o.MaxBoost, boost + 0.8f) - b0);

                // contacts: track shells (centre + wingtips) and laid mass (centre, wingtips, nose, tail)
                Vector3 right = rot * (Vector3.right * w), fwd = rot * (Vector3.forward * l);
                float clear = float.PositiveInfinity;
                if (course != null && course.HasShells)
                {
                    course.Project(pos, ref hint, out _, out _); // keep the shell window on the rollout
                    float c0 = course.ShellClearance(pos, hint, 6, out int idx);
                    if (c0 < 7.5f && idx >= 0 && _mpcSkimmed.Add(idx)) boost = Mathf.Min(o.MaxBoost, boost + 0.1f);
                    clear = Mathf.Min(c0, Mathf.Min(course.ShellClearance(pos + right, hint, 6, out _),
                                                     course.ShellClearance(pos - right, hint, 6, out _)));
                }
                for (int i = 0; i < Obstacles.Count; i++)
                {
                    var ob = Obstacles[i];
                    float reach = ob.Half.magnitude + w + l + 1f;
                    if ((ob.Center - pos).sqrMagnitude > reach * reach) continue;
                    float c = Mathf.Min(ob.Distance(pos), Mathf.Min(ob.Distance(pos + right), ob.Distance(pos - right)));
                    clear = Mathf.Min(clear, Mathf.Min(c, Mathf.Min(ob.Distance(pos + fwd), ob.Distance(pos - fwd))));
                }
                if (clear < _cfg.MpcHullMargin)
                    return _cfg.MpcStrikeCost + (H - t) * 2f // an earlier strike is worse
                           + (_cfg.MpcStrikeUsesBoostLoss ? _cfg.MpcBoostValue * (boost - 1f) : 0f);
            }
            // Remaining distance measured ALONG the course (going the wrong way along the track wraps
            // to nearly a lap), never less than the straight line.
            float remaining = 0f;
            if (o.HasTarget)
            {
                remaining = Vector3.Distance(pos, o.TargetPosition);
                if (course != null && _mpcTargetS >= 0f)
                {
                    int h2 = hint;
                    float sEnd = course.Project(pos, ref h2, out _, out _);
                    remaining = Mathf.Max(remaining, course.Ahead(sEnd, _mpcTargetS) - capture);
                }
            }
            return H + remaining / Mathf.Max(speed, 60f) - _cfg.MpcBoostValue * (boost - b0);
        }

        float _mpcTargetS = -1f;

        void Mpc(in SkimRaceObservation o, SkimRaceCourse course, Vector3 aim, float now,
            ref float yaw, ref float pitch, ref float throttle)
        {
            if (now >= _nextMpc)
            {
                _mpcTargetS = course != null ? course.Wrap(o.CourseProgress + o.TargetAheadOnCourse) : -1f;
                _nextMpc = now + 1f / Mathf.Max(1f, _cfg.MpcHz);
                float best = MpcCost(o, course, yaw, pitch, throttle, aim) - _cfg.MpcNominalBias;
                float by = yaw, bp = pitch, bt = throttle;
                float[] throttles = { 1f, _cfg.MpcSlowThrottle };
                for (int ti = 0; ti < throttles.Length; ti++)
                for (int a = 0; a < MpcSticks.Length; a++)
                for (int b = 0; b < MpcSticks.Length; b++)
                {
                    float c = MpcCost(o, course, MpcSticks[a], MpcSticks[b], throttles[ti], aim);
                    if (c < best) { best = c; by = MpcSticks[a]; bp = MpcSticks[b]; bt = throttles[ti]; }
                }
                _mpcValid = !(by == yaw && bp == pitch && bt == throttle);
                _mpcYaw = by; _mpcPitch = bp; _mpcThrottle = bt;
                if (_mpcValid) MpcOverrides++;
            }
            if (_mpcValid) { yaw = _mpcYaw; pitch = _mpcPitch; throttle = _mpcThrottle; }
        }

        // ── Tracking MPC: follow the racing line itself, not a look-ahead point on it ──
        float _nextTrack;
        float _trackYaw, _trackPitch;
        bool _trackValid;

        /// <summary>
        /// Mean squared distance of a rolled-out path from the racing line (each predicted position
        /// against the line at that position's own course progress, plus a small lead), with a pickup
        /// inside the horizon rewarded. Steering by this is model-predictive path following: it
        /// accounts for the hull's lag instead of hoping a look-ahead point does.
        /// </summary>
        float TrackCost(in SkimRaceObservation o, SkimRaceCourse course, float yaw, float pitch, float throttle, Vector3 aim, bool lineMode)
        {
            float H = _cfg.TrackMpcHorizon, T1 = _cfg.TrackMpcSegment, dt = _cfg.TrackMpcStep;
            Quaternion rot = o.Rotation, cmd = o.CommandedRotation;
            Vector3 pos = o.Position;
            float speed = o.Speed, boost = Mathf.Max(1f, o.BoostMultiplier);
            float capture = o.HasTarget ? Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.PassMargin) : 0f;
            int hint = _trackHint;
            float sum = 0f; int n = 0;
            // The course position of the rollout's CURRENT point, projected once per step and reused by the
            // next step's continuation (it is the same point). This rollout runs ~26 times per decision and
            // its cost is paid in the game's frame time.
            bool haveS = false; float sPos = 0f;
            for (float t = 0f; t < H; t += dt)
            {
                float y = yaw, p = pitch;
                if (t >= T1)
                {
                    Vector3 a = aim;
                    if (lineMode)
                    {
                        float sr0 = haveS ? sPos : course.Project(pos, ref hint, out _, out _);
                        a = LinePoint(course, sr0 + _lookDist);
                    }
                    Steer(rot * Vector3.forward, rot * Vector3.up, rot * Vector3.right, cmd * Vector3.forward,
                        (a - pos).normalized, out y, out p, out _, out _);
                }
                cmd = Quaternion.AngleAxis(y * o.TurnRateDegrees * dt, rot * Vector3.up) * cmd;
                cmd = Quaternion.AngleAxis(p * o.TurnRateDegrees * dt, rot * Vector3.right) * cmd;
                rot = Quaternion.Slerp(rot, cmd, o.FollowRate * dt);
                boost = boost > 1f ? boost - 0.3f * dt : 1f;
                speed = Mathf.Lerp(speed, throttle * o.ThrottleScaler * boost, o.FollowRate * dt);
                pos += (rot * Vector3.forward) * speed * dt;
                if (o.HasTarget && (pos - o.TargetPosition).sqrMagnitude <= capture * capture)
                    return sum / Mathf.Max(1, n) - _cfg.TrackMpcCaptureReward * (H - t);
                haveS = false;
                if (_cfg.TrackMpcStrikeCost > 0f && course.HasShells)
                {
                    sPos = course.Project(pos, ref hint, out _, out _);
                    haveS = true;
                    float clear = course.ShellClearance(pos, hint, 6, out _);
                    // A wingtip is HullHalfWidth from the centre, so it can be at most that much closer to a
                    // shell: only when the centre is within reach of the margin can a wingtip decide it.
                    if (clear < _cfg.MpcHullMargin + _cfg.HullHalfWidth)
                    {
                        Vector3 wing = rot * (Vector3.right * _cfg.HullHalfWidth);
                        clear = Mathf.Min(clear, Mathf.Min(course.ShellClearance(pos + wing, hint, 6, out _),
                                                           course.ShellClearance(pos - wing, hint, 6, out _)));
                    }
                    if (clear < _cfg.MpcHullMargin)
                        return sum / Mathf.Max(1, n) + _cfg.TrackMpcStrikeCost * (1f + (H - t) / H);
                }
                Vector3 target;
                if (lineMode)
                {
                    float sr = haveS ? sPos : course.Project(pos, ref hint, out _, out _);
                    sPos = sr; haveS = true;
                    target = LinePoint(course, sr + _cfg.TrackMpcLead);
                    float d = Vector3.Distance(pos, target);
                    sum += d * d; n++;
                }
                else
                {
                    // Terminal flight: close on the aim point.
                    float d = Vector3.Distance(pos, aim);
                    sum += d; n++;
                }
            }
            return sum / Mathf.Max(1, n);
        }

        void TrackMpc(in SkimRaceObservation o, SkimRaceCourse course, Vector3 aim, bool lineMode, float now,
            ref float yaw, ref float pitch, float throttle)
        {
            if (now >= _nextTrack)
            {
                // Timed only when it re-plans (TrackMpcHz), so the Profiler shows the frames it lands on.
                using var replanScope = s_TrackMpcMarker.Auto();
                _nextTrack = now + 1f / Mathf.Max(1f, _cfg.TrackMpcHz);
                course.Project(o.Position, ref _trackHint, out _, out _);
                float best = TrackCost(o, course, yaw, pitch, throttle, aim, lineMode) * (1f - _cfg.TrackMpcNominalBias);
                float by = yaw, bp = pitch;
                for (int a = 0; a < MpcSticks.Length; a++)
                for (int b = 0; b < MpcSticks.Length; b++)
                {
                    float c = TrackCost(o, course, MpcSticks[a], MpcSticks[b], throttle, aim, lineMode);
                    if (c < best) { best = c; by = MpcSticks[a]; bp = MpcSticks[b]; }
                }
                _trackValid = !(by == yaw && bp == pitch);
                _trackYaw = by; _trackPitch = bp;
            }
            if (_trackValid) { yaw = _trackYaw; pitch = _trackPitch; }
        }
        int _trackHint = -1;

        /// <summary>
        /// The laid-mass guard. Returns true when the commanded stick was replaced because its
        /// rollout touches a trail rail or ring prism (a hull contact resets the skim boost).
        /// </summary>
        bool GuardMass(in SkimRaceObservation o, Vector3 aim, ref float yaw, ref float pitch, float throttle)
        {
            float margin = _cfg.MassGuardMargin;
            float nominal = RolloutClearance(o, yaw, pitch, throttle, aim, margin, out Vector3 nominalEnd);
            LastGuardNominal = nominal;
            LastGuardChosen = nominal;
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
                if (cost < bestCost) { bestCost = cost; bestYaw = cy; bestPitch = cp; LastGuardChosen = c; }
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

        /// <summary>
        /// The capture-disk point beside the ribbon (|lateral| = RibbonClearLateral, on the crystal's
        /// side) closest in height to <paramref name="faceH"/>; false when the disk does not reach it.
        /// </summary>
        bool SidePass(float cl, float ch, float R, float faceH, out float ql, out float qh)
        {
            float Ls = _cfg.RibbonClearLateral;
            float sgn = cl >= 0f ? 1f : -1f;
            float dl = sgn * Ls - cl;
            ql = sgn * Ls; qh = ch;
            if (Mathf.Abs(dl) > R) return false;
            float span = Mathf.Sqrt(Mathf.Max(0f, R * R - dl * dl));
            qh = Mathf.Clamp(faceH, ch - span, ch + span);
            return true;
        }

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
            float H = LaneSkimHeight;
            if (!_bumpActive) return p + n * (_side * H);

            float ds = SignedDelta(c, s, _bumpCentre);
            float W = _approachW;
            if (!_bumpCrossing)
            {
                float w = Bump(ds, W);
                float baseH = _side * H;
                return Compose(p, n, l, _passLateral * w, baseH + (_passHeight - baseH) * w);
            }

            if (_cfg.SequencedCrossing)
            {
                // Face change, SEQUENCED so the pursuit chord can never cut the ribbon: (1) swing out
                // past the slab's edge on this face, (2) only once the swing is complete one full
                // look-ahead earlier, change height, (3) swing back in to the pass. Pure pursuit aims
                // a look-ahead ahead, so the swing has to lead the height change by that distance.
                float lead = Mathf.Max(_lookDist, 1f);
                float swingIn = Mathf.Max(30f, 0.35f * lead);
                float latStart = Mathf.Max(-W, _crossOriginDs);
                float hStart = latStart + swingIn + lead;
                float hLen = Mathf.Max(40f, W * _cfg.CrossingHeightFraction);
                float hEnd = Mathf.Min(hStart + hLen, -Mathf.Max(10f, 0.25f * lead));
                if (hEnd < hStart + 20f) hStart = hEnd - 20f;
                float Lc = _crossLateralSign * (_cfg.RibbonClearLateral + 4f);
                float sLat = ds < hEnd
                    ? Lc * Smooth01((ds - latStart) / swingIn)
                    : Mathf.Lerp(Lc, _passLateral, Smooth01((ds - hEnd) / Mathf.Max(1f, -hEnd)));
                float sHgt = Mathf.Lerp(_side * H, _passHeight, Smooth01((ds - hStart) / Mathf.Max(1f, hEnd - hStart)));
                return Compose(p, n, l, sLat, sHgt);
            }

            // Face change: height ramps from this face to the pass height over the second half of
            // the approach, while a wider lateral swing holds the line outside the slab's edge.
            // CrossingLeadSeconds starts the whole profile earlier, by the travel the hull's lag eats.
            if (_cfg.CrossingLeadSeconds > 0f) ds += Mathf.Min(_speed * _cfg.CrossingLeadSeconds, 0.5f * W);
            float rampStart = -W, rampEnd = -W * (1f - _cfg.CrossingHeightFraction);
            float u = Mathf.Clamp01((ds - rampStart) / Mathf.Max(1f, rampEnd - rampStart));
            u = u * u * (3f - 2f * u);
            float hgt = Mathf.Lerp(_side * H, _passHeight, ds >= rampEnd ? 1f : u);
            float swingCentre = (rampStart + rampEnd) * 0.5f;
            float swing = _crossLateralSign * (_cfg.RibbonClearLateral + 4f) * Bump(ds - swingCentre, W * 0.9f);
            float lat = Mathf.Lerp(swing, _passLateral, Mathf.Clamp01((ds - rampEnd) / Mathf.Max(1f, -rampEnd)));
            return Compose(p, n, l, lat, hgt);
        }

        /// <summary>
        /// This seat's lane among the AI seats in the race (0, 1, ...). Each lane skims at its own
        /// height (<see cref="SkimRaceAIConfigSO.LaneHeightStep"/> apart), so one seat's trail
        /// rails - laid at its own height, 9.66 u to either side - are never at the height of
        /// another seat that strays sideways into them. Set by the pilot from public seat order.
        /// </summary>
        public int Lane { get; set; }
        public float LastTrackError { get; private set; } = -1f;

        /// <summary>Diagnostic: the closest the PLANNED line comes to a track shell over the next span.</summary>
        public float LineMinClearance(SkimRaceCourse course, float s0, float span)
        {
            float best = float.PositiveInfinity; int h = -1;
            for (float d = 0f; d <= span; d += 6f)
            {
                Vector3 p = LinePoint(course, s0 + d);
                course.Project(p, ref h, out _, out _);
                best = Mathf.Min(best, course.ShellClearance(p, h, 6, out _));
            }
            return best;
        }
        public float LastGuardNominal = float.PositiveInfinity, LastGuardChosen = float.PositiveInfinity;
        int _lastCollected = -1;
        float _pickupHoldUntil = -1f;
        public bool Crossing => _bumpActive && _bumpCrossing;

        /// <summary>A face change is planned for the current crystal and its centre is within
        /// <paramref name="within"/> of arc length ahead.</summary>
        bool ApproachingCrossing(SkimRaceCourse c, float s, float within)
        {
            if (!_bumpActive || !_bumpCrossing) return false;
            float ds = SignedDelta(c, s, _bumpCentre);
            return ds <= 0f && ds >= -within;
        }

        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        float _lookDist = 100f;        // current pursuit look-ahead (world units)
        float _speed;                  // the hull's speed this decision (sizes the crossing lead)
        float _crossOriginDs = -1e6f;  // where (signed, relative to the crystal) the current face change was decided
        float _crossForCentre = float.NaN;
        float LaneSkimHeight => Mathf.Abs(_cfg.SkimHeight) + Lane * _cfg.LaneHeightStep;

        /// <summary>
        /// A racing-line point from its frame offsets - never inside the slab band: within the
        /// ribbon's lateral reach the line keeps at least RibbonClearHeight off the plane, on
        /// whichever face it is on. (Without this, a pass beside-and-below the ribbon blended height
        /// and lateral together and the line dipped through the plates' edge.)
        /// </summary>
        Vector3 Compose(Vector3 p, Vector3 n, Vector3 l, float lat, float hgt)
        {
            if (_cfg.LineBandClamp && Mathf.Abs(lat) < _cfg.RibbonClearLateral)
            {
                float c = _cfg.RibbonClearHeight;
                if (Mathf.Abs(hgt) < c) hgt = (Mathf.Abs(hgt) > 1e-3f ? Mathf.Sign(hgt) : _side) * c;
            }
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
            _direct = false;
            _viaHint = -1;
            ViaPoints = 0;
            Obstacles.Clear();
            _ringReadyAt = 0f;
            Handicap?.Reset();
            _ringHoldUntil = 0f;
            _nextPlan = 0f;
            _plan = default;
            _planHint = -1;
            _rings = 0;
            _pendingSide = _side;
            _crossForCentre = float.NaN;
            _crossOriginDs = -1e6f;
            _lastCollected = -1;
            _pickupHoldUntil = -1f;
            _nextMpc = 0f; _mpcValid = false; MpcOverrides = 0;
            _nextTrack = 0f; _trackValid = false; _trackHint = -1;
            _lookDist = 100f;
            _trackerHint = -1;
            _nextLevel = 0f; _levelValid = false; _levelHint = -1; LevelOverrides = 0;
            _bumpActive = false;
        }

        /// <summary>
        /// One decision. <paramref name="course"/> may be null (track not laid yet): the pilot
        /// then flies straight at the crystal. <paramref name="now"/> is race time in seconds.
        /// </summary>
        public SkimRaceAction Decide(in SkimRaceObservation observed, SkimRaceCourse course, float now, float dt)
        {
            using (s_DecideMarker.Auto())
                return DecideCore(observed, course, now, dt);
        }

        SkimRaceAction DecideCore(in SkimRaceObservation observed, SkimRaceCourse course, float now, float dt)
        {
            if (_mode == Mode.Idle) _mode = Mode.Racing;
            dt = Mathf.Max(dt, 1e-4f);
            _speed = observed.Speed;

            // Progress (and so recovery) is judged against the REAL crystal; everything below decides on
            // what the pilot believes, which differs only for a handicapped (Easy / Medium) pilot.
            UpdateProgress(observed, now, dt);
            var o = Handicap != null ? Handicap.View(observed, course) : observed;

            // ── 1. Aim point ────────────────────────────────────────────────
            bool haveCourse = course != null && o.HasCourse;
            bool pull = false;
            bool linePursuit = false;
            Vector3 aim;
            _bumpActive = false;

            // Approach length follows the boost bank: with boost to spend the line can leave skim
            // range early; with none, it stays on the ribbon and rebuilds it before rising.
            float approach = Mathf.Lerp(_cfg.LowBoostApproachScale, 1f,
                Mathf.Clamp01((o.BoostMultiplier - 1f) / Mathf.Max(0.01f, _cfg.LowBoostFull - 1f)));
            _approachW = _cfg.CrystalBumpHalfWidth * approach;
            float directDistance = _cfg.CrystalDirectDistance * approach;

            if (haveCourse && o.HasTarget)
                using (s_PlanPassMarker.Auto())
                    PlanPass(o, course);

            bool behind = haveCourse && o.HasTarget && o.TargetAheadOnCourse > o.CourseLength * 0.5f;
            bool inFront = o.HasTarget && o.TargetAlignment > 0.35f;
            Vector3 passPoint = _bumpActive ? _passWorld : o.TargetPosition;
            float passDist = Vector3.Distance(o.Position, passPoint);

            if (_cfg.DirectBoost > 0f && o.HasTarget)
            {
                if (!_direct && o.BoostMultiplier >= _cfg.DirectBoost) _direct = true;
                else if (_direct && o.BoostMultiplier < _cfg.DirectBoost - _cfg.DirectBoostHysteresis) _direct = false;
            }
            else _direct = false;

            if (_direct && haveCourse && course.HasShells)
            {
                // Direct flight: straight at the crystal, routed round the ribbon only where the
                // straight line would enter its contact shell.
                aim = o.TargetPosition;
                if (DirectVia(o, course, o.TargetPosition, out Vector3 via)) aim = via;
                pull = true;
            }
            else if (o.HasTarget && (!haveCourse || _mode == Mode.Recovering || behind || _direct))
            {
                aim = passPoint;
                pull = true;
            }
            else if (o.HasTarget && passDist <= directDistance && inFront
                     && !(haveCourse && _cfg.TerminalNeedsClearChord && course.HasShells && ChordBlocked(o, course, passPoint, _cfg.TerminalChordClearance)))
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
                if (_cfg.CrossingLookaheadScale < 1f && ApproachingCrossing(course, o.CourseProgress, _approachW))
                    look = Mathf.Max(20f, look * _cfg.CrossingLookaheadScale);
                aim = LinePoint(course, o.CourseProgress + look);
                // Pursue only along a CLEAR chord: pure pursuit flies the straight line to its
                // look-ahead point, which can cut the ribbon even where the line itself is clear.
                for (int k = 0; k < 4 && _cfg.ChordClearance > 0f && course.HasShells && ChordBlocked(o, course, aim); k++)
                {
                    look *= 0.6f;
                    aim = LinePoint(course, o.CourseProgress + look);
                }
                _lookDist = look;
                linePursuit = true;
                pull = _bumpActive && Mathf.Abs(SignedDelta(course, o.CourseProgress + look, _bumpCentre)) < _approachW;
            }
            else
            {
                aim = o.Position + o.Forward * 100f;
            }

            LastTrackError = haveCourse ? Vector3.Distance(o.Position, LinePoint(course, o.CourseProgress)) : -1f;

            // ── 1b. Slab guard: never let the hull approach the ribbon plane inside its edge ─
            bool guarded = false;
            using (s_GuardsMarker.Auto())
            {
                if (haveCourse && _cfg.SlabGuardSeconds > 0f)
                    guarded = GuardSlab(o, course, ref aim);
                if (haveCourse && _cfg.HullGuardSeconds > 0f && GuardHull(o, course, ref aim))
                    guarded = true;
            }

            Vector3 toAim = aim - o.Position;
            Vector3 desired = toAim.sqrMagnitude > 1e-4f ? toAim.normalized : o.Forward;

            // ── 2. Lag-compensated steering ────────────────────────────────
            Steer(o.Forward, o.Up, o.Right, o.CommandedForward, desired, out float yaw, out float pitch,
                out float headingErr, out float cmdErr);
            if (_cfg.UseLineTracker && linePursuit && !guarded)
            {
                Vector3 cmdTarget = TrackerCommand(o, course, out headingErr, out desired);
                StickToward(o.Up, o.Right, o.CommandedForward, cmdTarget, out yaw, out pitch, out cmdErr);
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
            if (_cfg.CaptureThrottleSearch && o.HasTarget && pull
                && passDist <= Mathf.Max(o.Speed, 60f) * _cfg.CaptureWindowSeconds)
            {
                // Highest throttle that still captures; if none does, the lowest (tightest turn).
                float chosen = CaptureThrottles[CaptureThrottles.Length - 1];
                for (int k = 0; k < CaptureThrottles.Length; k++)
                {
                    float th = Mathf.Min(CaptureThrottles[k], _cfg.CruiseThrottle);
                    if (RolloutCaptures(o, aim, th)) { chosen = th; break; }
                }
                throttle = chosen;   // the rollout supersedes the Dubins estimate above
                unreachable = chosen < _cfg.CruiseThrottle;
            }
            if (_cfg.CrossingThrottle < 1f && haveCourse && ApproachingCrossing(course, o.CourseProgress, _cfg.CrossingSlowDistance))
                throttle = Mathf.Min(throttle, _cfg.CrossingThrottle);
            if (_mode == Mode.Recovering) throttle = Mathf.Min(throttle, _cfg.RecoveryThrottle);

            // ── Planner (optional): replaces the pursuit command with the best rolled-out one ──
            if (_cfg.UsePlanner && o.HasTarget && haveCourse)
            {
                if (now >= _nextPlan)
                {
                    _nextPlan = now + 1f / Mathf.Max(1f, _cfg.PlannerHz);
                    _planner ??= new SkimRacePlanner(_cfg);
                    course.Project(o.Position, ref _planHint, out _, out _);
                    using (s_PlannerMarker.Auto())
                        _plan = _planner.Plan(o, course, passPoint, _planHint, yaw, pitch, throttle);
                }
                yaw = _plan.Yaw;
                pitch = _plan.Pitch;
                throttle = _plan.Throttle;
            }

            // ── Laid-mass guard: never fly the hull into trail rails or pickup-ring prisms ──
            _guardCourse = null;
            LastGuardNominal = LastGuardChosen = float.PositiveInfinity;
            _rollCourse = (_cfg.RolloutFollowsLine && haveCourse && linePursuit) ? course : null;
            if (_rollCourse != null) _rollHint = -1;
            bool trackGuard = haveCourse && course.HasShells
                              && (_cfg.TrackGuard == 2 || (_cfg.TrackGuard == 1 && _direct));
            if (trackGuard)
            {
                _guardCourse = course;
                // Project from a point ahead too: a fast rollout can leave the 6-prism window
                // around the hull's own projection.
                course.Project(o.Position, ref _guardBaseHint, out _, out _);
            }
            if (_cfg.UseTrackMpc && haveCourse && _mode != Mode.Recovering)
                TrackMpc(o, course, aim, linePursuit, now, ref yaw, ref pitch, throttle);
            if (_cfg.UseLevelApproach && haveCourse && course.HasShells && o.HasTarget && _mode != Mode.Recovering
                && !behind && passDist <= Mathf.Max(o.Speed, 60f) * _cfg.LevelApproachSeconds)
            {
                using (s_LevelApproachMarker.Auto())
                    LevelApproach(o, course, passPoint, now, throttle, ref yaw, ref pitch);
            }
            else _levelValid = false;

            if (_cfg.UseMpc && o.HasTarget)
            {
                if (_rollCourse == null && _cfg.RolloutFollowsLine && haveCourse) { _rollCourse = course; _rollHint = -1; }
                if (haveCourse) course.Project(o.Position, ref _rollHint, out _, out _);
                using (s_MpcMarker.Auto())
                    Mpc(o, haveCourse ? course : null, aim, now, ref yaw, ref pitch, ref throttle);
            }
            else if (_cfg.MassGuardSeconds > 0f && (Obstacles.Count > 0 || trackGuard))
            {
                using (s_GuardMassMarker.Auto())
                    if (GuardMass(o, aim, ref yaw, ref pitch, throttle)) guarded = true;
            }

            // ── Pickup hold: fly straight through the pickup ring's hollow centre ──
            // A pickup lays 8 prisms on radius 8.2, centred 8 u ahead along the hull's heading
            // (AOEShieldedRingSpawner). Turning toward the next crystal at once sweeps a wingtip into
            // one of them (boost reset); holding the stick for the ~14 u it takes to clear the ring
            // passes through the hollow - and skims all eight (+0.8).
            if (o.Collected > _lastCollected && _lastCollected >= 0 && _cfg.PickupClearDistance > 0f)
                _pickupHoldUntil = now + _cfg.PickupClearDistance / Mathf.Max(o.Speed, 30f);
            _lastCollected = o.Collected;
            if (now < _pickupHoldUntil) { yaw = 0f; pitch = 0f; roll = 0f; }

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
