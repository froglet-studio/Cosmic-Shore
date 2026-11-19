// A model of the Brittle Star flying a Brittle Star Time lap, for the harness's --race sweep: the hull's speed law
// (BrittleStarConfig modeBlendSeconds 0.35 ramp of the Speed Mode multiplier, then VesselTransformer.LERP_AMOUNT 1.5/s),
// its turn law (0.1 x speed + 80 deg/s, Speed Roll +90 at Time 5), the gate race's plane test (either direction, inside
// the mouth), and a steering law shared by every speed policy so only the SPEED decision differs between them.
// A model, never the game: what it measures is how much reading the course is worth, policy against policy.
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

public static class FlightSim
{
    public const float Dt = 1f / 60f;
    public const float ModeBlendSeconds = 0.35f;   // BrittleStarConfig.asset modeBlendSeconds
    public const float CreationMultiplier = 0.5f;  // BrittleStarConfig.asset creationSpeedMultiplier

    /// <summary>A speed decision: given the pilot's state and the course, hold Speed Mode (true) or not.</summary>
    public enum Steering { Approach, GateRaceAI, Centre, Line }

    public delegate bool SpeedPolicy(in State s, IReadOnlyList<RaceGate> lap, int ring);

    public struct State
    {
        public Vector3 Pos, Fwd;
        public float Speed;        // u/s
        public float Speed01;      // the mode's eased share (0 cruise .. 1 Speed Mode)
        public float Creation01;
        public bool SpeedOn;
        public int Threaded;       // rings threaded so far
        public float T;
        public int Misses;
        public float AimAngle;     // degrees between the nose and the steering point, this frame
    }

    public sealed class Options
    {
        public float TopMultiplier = BrittleStarTimeCourse.SpeedModeMultiplier;
        public bool Rolls;              // a human at Time 5 rolls and pulls (Speed Roll's +90 deg/s) while turning hard
        public bool NoseLag;            // an AI's nose closes on its command (VesselTransformer.RotationFollowRate 1.5/s)
        public float NoseFollow = BrittleStarTimeCourse.SpeedEaseRate;   // ...at this rate per second (AINoseFollowPerSecond)
        public bool OffIsCreation;      // the AI's autopilot picks Creation (x0.5) whenever it does not want Speed
        public int Laps = 2;
        public float MaxSeconds = 400f;
        public Vector3 StartPos = new Vector3(0f, 0f, 542f);   // an equatorial spawn pad (the recorded flights)
        public Steering Steer = Steering.Approach;
        /// <summary>A pilot's reaction: the steering point it acts on is this many seconds old (a human at the controls).</summary>
        public float ReactSeconds;
    }

    public struct Result
    {
        public float Seconds;
        public bool Finished;
        public int Misses;
        public int Threaded;
        public int Carried;   // threads graded FlatOut or CleanShift (BrittleStarTimeHeat)
    }

    /// <summary>Fly <paramref name="laps"/> laps of <paramref name="lap"/> from the spawn pad under <paramref name="policy"/>.</summary>
    public static Result Fly(IReadOnlyList<RaceGate> lap, SpeedPolicy policy, Options o)
    {
        int target = lap.Count * o.Laps;
        var s = new State { Pos = o.StartPos, Fwd = (lap[0].Position - o.StartPos).normalized, Speed = 0f };
        Vector3 nose = s.Fwd;
        int steps = (int)(o.MaxSeconds / Dt);
        _aimLag.Clear();
        int carried = 0;
        for (int k = 0; k < steps && s.Threaded < target; k++)
        {
            int ring = s.Threaded % lap.Count;
            var g = lap[ring];
            // The steering point (every policy shares the steering; only the speed decision differs).
            Vector3 aim = o.Steer switch
            {
                Steering.GateRaceAI => GateRaceAIPoint(s.Pos, s.Speed, lap, ring),
                Steering.Line => BrittleStarTimeLine.SteerPoint(lap, ring, s.Pos),
                Steering.Centre => (g.Position - s.Pos).magnitude < g.Radius ? g.Position + s.Fwd * 200f : g.Position,
                _ => SteerPoint(s.Pos, g, lap[(ring + 1) % lap.Count]),
            };
            if (o.ReactSeconds > 0f)
            {
                _aimLag.Enqueue(aim);
                aim = _aimLag.Count > (int)(o.ReactSeconds / Dt) ? _aimLag.Dequeue() : _aimLag.Peek();
            }
            s.AimAngle = Vector3.Angle(s.Fwd, aim - s.Pos);
            s.SpeedOn = policy(s, lap, ring);

            // Speed law: the mode's share ramps over modeBlendSeconds, then the hull eases toward its target.
            float step = Dt / ModeBlendSeconds;
            s.Speed01 = MoveTowards(s.Speed01, s.SpeedOn ? 1f : 0f, step);
            s.Creation01 = MoveTowards(s.Creation01, !s.SpeedOn && o.OffIsCreation ? 1f : 0f, step);
            float mult = Mathf.Lerp(1f, o.TopMultiplier, s.Speed01) * Mathf.Lerp(1f, CreationMultiplier, s.Creation01);
            float target01 = BrittleStarTimeCourse.CruiseSpeed * mult;
            s.Speed += (target01 - s.Speed) * Mathf.Min(1f, BrittleStarTimeCourse.SpeedEaseRate * Dt);

            // Turn law, toward the steering point.
            Vector3 want = (aim - s.Pos).normalized;
            float omega = BrittleStarTimeCourse.TurnRateDegPerSec(s.Speed);
            float off = Vector3.Angle(nose, want);
            if (o.Rolls && off > 20f) omega += BrittleStarTimeCourse.SpeedRollPullDegreesPerSecond;
            nose = RotateTowards(nose, want, omega * Dt);
            s.Fwd = o.NoseLag ? Vector3.Slerp(s.Fwd, nose, Mathf.Min(1f, o.NoseFollow * Dt)).normalized : nose;

            Vector3 prev = s.Pos;
            s.Pos += s.Fwd * (s.Speed * Dt);
            s.T += Dt;

            float da = Vector3.Dot(prev - g.Position, g.Axis), db = Vector3.Dot(s.Pos - g.Position, g.Axis);
            if (da == 0f || Math.Sign(da) == Math.Sign(db)) continue;
            Vector3 cross = Vector3.Lerp(prev, s.Pos, da / (da - db));
            if ((cross - g.Position).magnitude <= g.Radius)
            {
                var grade = BrittleStarTimeHeat.Grade(lap, ring, (cross - g.Position).magnitude, s.Speed,
                                                      BrittleStarTimeCourse.CruiseSpeed * o.TopMultiplier);
                if (grade >= BrittleStarThreadGrade.FlatOut) carried++;
                s.Threaded++;
            }
            else s.Misses++;
        }
        return new Result { Seconds = s.T, Finished = s.Threaded >= target, Misses = s.Misses, Threaded = s.Threaded, Carried = carried };
    }

    static readonly Queue<Vector3> _aimLag = new();

    /// <summary>The gate race's own AI steering (GateRaceController: approach lead 360 capped at 40% of the leg and 80% of
    /// commit, commit 320 capped at lead + half the turn circle, through 250 - the scene's numbers).</summary>
    public static Vector3 GateRaceAIPoint(Vector3 pos, float speed, IReadOnlyList<RaceGate> lap, int ring)
    {
        var g = lap[ring];
        var prev = lap[(ring - 1 + lap.Count) % lap.Count];
        float leg = (g.Position - prev.Position).magnitude;
        float commit0 = 320f, lead0 = 360f, through = 250f;
        float lead = GateRaceLead(lead0, leg, commit0);
        float r = BrittleStarTimeCourse.CornerRadiusAt(Mathf.Max(speed, 1f));
        float commit = Mathf.Min(commit0, lead + 0.5f * r);
        Vector3 axis = Vector3.Dot(pos - g.Position, g.Axis) <= 0f ? g.Axis : -g.Axis;
        return (g.Position - pos).magnitude > commit ? g.Position - axis * lead : g.Position + axis * through;
    }

    static float GateRaceLead(float lead, float leg, float commit) => Mathf.Min(Mathf.Min(lead, 0.4f * leg), 0.8f * commit);

    /// <summary>The steering law every policy shares: fly to a point back along the ring's axis on the side the pilot is
    /// on (the gate race's approach point, a lead of half the range capped at 300 u), and through the centre once close.</summary>
    public static Vector3 SteerPoint(Vector3 pos, in RaceGate g, in RaceGate next)
    {
        Vector3 axis = Vector3.Dot(pos - g.Position, g.Axis) <= 0f ? g.Axis : -g.Axis;
        float d = (g.Position - pos).magnitude;
        float lead = Mathf.Min(0.5f * d, 300f);
        if (d < 1.2f * g.Radius) return g.Position + axis * 200f;
        return g.Position - axis * lead;
    }

    public static float MoveTowards(float a, float b, float step) =>
        Mathf.Abs(b - a) <= step ? b : a + Mathf.Sign(b - a) * step;

    public static Vector3 RotateTowards(Vector3 from, Vector3 to, float maxDegrees)
    {
        float ang = Vector3.Angle(from, to);
        if (ang <= maxDegrees || ang < 1e-4f) return to.normalized;
        return Vector3.Slerp(from, to, maxDegrees / ang).normalized;
    }
}
