using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Short-horizon model-predictive planner for the Skim Race pilot.
    ///
    /// Each decision it tries a grid of stick/throttle commands, holds each for a short first
    /// segment, then lets a simple pursuit continuation fly the rest of the horizon, and rolls the
    /// Squirrel forward with the SAME dynamics <c>VesselTransformer</c> runs:
    /// <list type="bullet">
    /// <item>the stick turns the COMMANDED orientation at the hull's turn rate about the hull's
    /// current axes, and the hull slerps toward it at the follow rate (1.5/s);</item>
    /// <item>speed eases toward throttle x cruise x boost at the same rate; boost decays at
    /// 0.3/s and gains 0.1 per track prism that enters skim reach;</item>
    /// <item>a crystal is taken when the hull comes within its capture radius, which also lays
    /// the 8-prism pickup ring (+0.8 boost).</item>
    /// </list>
    /// The rollout is scored on time to the crystal, boost banked, and track clearance — a
    /// predicted hull contact is ruinous, because in the game it resets the boost to 1x. The
    /// chosen command is only the FIRST segment; the plan is redone every decision.
    ///
    /// Everything it predicts from is something the pilot can see: its own pose and speed, the
    /// commanded rotation, the visible track prisms and the crystal in front of it.
    /// </summary>
    public sealed class SkimRacePlanner
    {
        public struct Result
        {
            public float Yaw, Pitch, Throttle;
            public float Cost;
            public bool Captures;
            public float CaptureTime;
            public float PredictedBoost;
            public bool HullRisk;
        }

        readonly SkimRaceAIConfigSO _cfg;
        readonly HashSet<int> _skimSeen = new();
        static readonly float[] StickLevels = { -1f, -0.55f, -0.25f, -0.1f, 0f, 0.1f, 0.25f, 0.55f, 1f };

        public SkimRacePlanner(SkimRaceAIConfigSO cfg) { _cfg = cfg; }

        public Result Plan(in SkimRaceObservation o, SkimRaceCourse course, Vector3 passPoint, int courseHint,
            float nominalYaw, float nominalPitch, float nominalThrottle)
        {
            var best = new Result { Cost = float.MaxValue };
            float[] throttles = { 1f, _cfg.PlannerSlowThrottle };

            // Nominal (the pursuit controller's own answer) first, so ties keep the smooth choice.
            Evaluate(o, course, passPoint, courseHint, nominalYaw, nominalPitch, nominalThrottle, ref best);
            for (int ti = 0; ti < throttles.Length; ti++)
            for (int yi = 0; yi < StickLevels.Length; yi++)
            for (int pi = 0; pi < StickLevels.Length; pi++)
                Evaluate(o, course, passPoint, courseHint, StickLevels[yi], StickLevels[pi], throttles[ti], ref best);
            return best;
        }

        void Evaluate(in SkimRaceObservation o, SkimRaceCourse course, Vector3 pass, int hint,
            float yaw, float pitch, float throttle, ref Result best)
        {
            float H = _cfg.PlannerHorizon, T1 = _cfg.PlannerSegment, dt = _cfg.PlannerStep;
            float turn = o.TurnRateDegrees, follow = o.FollowRate;
            float cruise = o.ThrottleScaler, maxBoost = o.MaxBoost;
            float capture = Mathf.Max(4f, (o.TargetRadius > 0f ? o.TargetRadius : _cfg.DefaultCaptureRadius) - _cfg.PlannerCaptureMargin);

            Quaternion rot = o.Rotation;
            Quaternion cmd = o.CommandedRotation;
            Vector3 pos = o.Position;
            float speed = o.Speed, boost = Mathf.Max(1f, o.BoostMultiplier);
            _skimSeen.Clear();
            int skims = 0;
            bool hull = false, captured = false;
            float tCapture = 0f;
            float minClear = float.PositiveInfinity;

            for (float t = 0f; t < H; t += dt)
            {
                float y = yaw, p = pitch, thr = throttle;
                if (t >= T1)
                {
                    // Continuation: pursue the pass point off the commanded heading.
                    Vector3 cf = cmd * Vector3.forward;
                    Vector3 to = (o.HasTarget && !captured ? pass : pos + (rot * Vector3.forward) * 100f) - pos;
                    Vector3 axis = Vector3.Cross(cf, to.normalized);
                    float ang = Vector3.Angle(cf, to);
                    float stick = Mathf.Clamp01(ang * _cfg.StickGainPerDegree);
                    Vector3 up = rot * Vector3.up, right = rot * Vector3.right;
                    float u = Vector3.Dot(axis, up), r = Vector3.Dot(axis, right);
                    float m = MathfNoAlloc.Max(Mathf.Abs(u), Mathf.Abs(r), 1e-4f);
                    y = stick * u / m; p = stick * r / m; thr = 1f;
                }

                Vector3 hu = rot * Vector3.up, hr = rot * Vector3.right;
                cmd = Quaternion.AngleAxis(y * turn * dt, hu) * cmd;
                cmd = Quaternion.AngleAxis(p * turn * dt, hr) * cmd;
                rot = Quaternion.Slerp(rot, cmd, follow * dt);

                boost = boost > 1f ? boost - 0.3f * dt : Mathf.Min(1f, boost + 0.3f * dt);
                float target = thr * cruise * boost;
                speed = Mathf.Lerp(speed, target, follow * dt);
                pos += (rot * Vector3.forward) * speed * dt;

                if (course != null && course.HasShells)
                {
                    float c = course.ShellClearance(pos, hint, 10, out int idx);
                    minClear = Mathf.Min(minClear, c);
                    if (c < _cfg.HullMargin) { hull = true; break; }
                    if (c < _cfg.PlannerSkimReach && idx >= 0 && _skimSeen.Add(idx))
                    {
                        skims++;
                        boost = Mathf.Min(maxBoost, boost + 0.1f);
                    }
                }

                if (!captured && o.HasTarget && (pos - o.TargetPosition).sqrMagnitude <= capture * capture)
                {
                    captured = true;
                    tCapture = t;
                    boost = Mathf.Min(maxBoost, boost + 0.8f);
                    break; // the next crystal is not known yet; score the capture itself
                }
            }

            // ── score (seconds-equivalent; lower is better) ──
            float cost;
            if (hull)
                cost = 1000f;
            else if (captured)
            {
                cost = tCapture - _cfg.PlannerBoostValue * (boost - o.BoostMultiplier);
                // The next crystal will be further along the track: leave the pickup heading that way.
                if (course != null && _cfg.PlannerExitWeight > 0f)
                {
                    int h2 = hint;
                    float sc = course.Project(pos, ref h2, out _, out _);
                    course.Sample(sc + 60f, out Vector3 tan, out _);
                    cost += _cfg.PlannerExitWeight * (1f - Vector3.Dot(rot * Vector3.forward, tan)) * 0.5f;
                }
            }
            else
            {
                float remaining = o.HasTarget ? Vector3.Distance(pos, pass) : 0f;
                cost = H + remaining / Mathf.Max(speed, 60f) - _cfg.PlannerBoostValue * (boost - o.BoostMultiplier);
            }
            if (minClear < _cfg.HullMargin + _cfg.PlannerClearanceBuffer)
                cost += _cfg.PlannerClearancePenalty * (1f - (minClear - _cfg.HullMargin) / Mathf.Max(_cfg.PlannerClearanceBuffer, 0.01f));
            // Small preference for gentle sticks (smoothness).
            cost += _cfg.PlannerEffortCost * (Mathf.Abs(yaw) + Mathf.Abs(pitch));

            if (cost < best.Cost)
                best = new Result
                {
                    Yaw = yaw, Pitch = pitch, Throttle = throttle, Cost = cost,
                    Captures = captured, CaptureTime = tCapture, PredictedBoost = boost, HullRisk = hull,
                };
        }
    }
}
