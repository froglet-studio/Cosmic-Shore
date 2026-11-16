using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How an AI Thresher reads the Belfry's clock (<see cref="ThresherBelfry"/>, 2026-11-07; plan step
    /// <c>time-arcade</c>, Docs/AI_SYSTEM/AI_PLAN.md). Pure and allocation-free: the mode
    /// (<see cref="ThresherTimeController"/>) asks it where to aim and when to duck, and the pilot flies that with its own
    /// inputs. Nothing here touches a vessel (the input-only rule, <c>Tools/Build/check_ai_no_state_writes.py</c>).
    ///
    /// <para><b>Lead the ring.</b> A swinging ring is met, not chased: solve for the time <c>tau</c> the pilot reaches it,
    /// <c>|Bob(now + tau) - p| + turnCost = v * tau</c>, and aim at where it will be then. The ring moves slower than the
    /// pilot (its top speed is under cruise, ThresherBelfryTests), so the left side falls slower than the right climbs:
    /// one root, and bisection on a bracket that provably holds it finds it. Far out the aim fades to the swing's rest
    /// point (the gate), so a long leg is flown straight; close in the solved arrival is LATCHED so the aim does not
    /// jitter as the estimate refines. The ring's pose is read one frame back (<c>-dt</c>) because the race credits a
    /// crossing in Update before the Belfry poses the ring in LateUpdate.</para>
    ///
    /// <para><b>Duck the maces.</b> A mace swings in the plane square to its straight, and its head never comes below
    /// its hang point by more than <see cref="ThresherBelfry.HeadReach"/> (the bob's height above the hang point is
    /// <c>L(1 - cos a)</c>, never negative). So a pilot whose path meets a head on its way through steers to a point
    /// <c>clear</c> below the hang point, which is clear of the head and chain at every angle and every time - one duck
    /// clears both of an I4 straight's opposed maces.</para>
    /// </summary>
    public static class ThresherBelfryAI
    {
        /// <summary>The Thresher's fixed turn rate (THRESHER.md: 120 deg/s at every speed).</summary>
        public const float TurnRateRad = 120f * Mathf.Deg2Rad;
        /// <summary>How fast the nose follows the stick (the gate-race AI's lag): a full right-angle reaim costs about
        /// <c>v / NoseLagRate</c> of extra path.</summary>
        public const float NoseLagRate = 1.5f;

        /// <summary>Per-pilot state the mode keeps (one per armed AI, allocated once).</summary>
        public sealed class Pilot
        {
            public int LatchedRing = -1;
            public float LatchedArrival;
            public int DuckMace = -1;
            public int BiasMace = -1;
            public float Bias;
            public int Ducks;
            public System.Random Rng;
            public Diag Diag;
            /// <summary>The hull's Thresher executor (read only: is an aimed launch coming?). Looked up again while it is
            /// missing, so a lookup made before the hull's actions were wired is not kept as a miss for the match.</summary>
            public ThresherExecutor Thresher;
        }

        /// <summary>
        /// What the arrival solve should assume. With an AIMED launch coming (planted, launch view up), the pilot's next
        /// move is a straight line at <paramref name="launchBoost"/> x the orbit's speed (the launch's boost times its exit
        /// buff), with no turn to make (the plane is laid onto the aim, the nose's spin is not a turn) - so the speed is the
        /// launch's and the turn cost is none. Otherwise (flying, or a plain let-go on the tangent at the orbit's speed)
        /// it is the hull's own speed (clamped) and nose. Speeds are converted to the Belfry clock's seconds by
        /// <paramref name="clockScale"/> (game seconds per clock second: <c>Time.timeScale</c>, because the clock is the
        /// server's real time and the hull moves in game time).
        /// </summary>
        public static void LeadInputs(bool planted, float speed, Vector3 forward, float launchBoost, float minSpeed,
                                      float maxSpeed, float clockScale, out float v, out Vector3 noseForTurn)
        {
            if (planted)
            {
                v = Mathf.Max(minSpeed, speed * launchBoost);
                noseForTurn = Vector3.zero;   // TurnCost answers 0 for no nose: the launch leaves straight
            }
            else
            {
                v = Mathf.Clamp(speed, minSpeed, Mathf.Max(minSpeed, maxSpeed));
                noseForTurn = forward;
            }
            v *= clockScale > 1e-3f ? clockScale : 1f;
        }

        /// <summary>What the AI is doing this frame, for a viewer or the soak.</summary>
        public struct Diag
        {
            public Vector3 Aim;
            public float Tau, Weight;
            public int Ring, DuckMace;
            public bool Latched, Ducking;
        }

        /// <summary>Extra path the turn onto <paramref name="target"/> costs: half the arc of the turn circle plus the
        /// nose's lag, both growing with the angle off the nose (a right angle or more is the full lag).</summary>
        public static float TurnCost(Vector3 p, Vector3 forward, Vector3 target, float v)
        {
            Vector3 to = target - p;
            if (to.sqrMagnitude < 1e-4f || forward.sqrMagnitude < 1e-6f) return 0f;
            float theta = Vector3.Angle(forward, to) * Mathf.Deg2Rad;
            float turnRadius = v / TurnRateRad;
            return 0.5f * turnRadius * theta + v / NoseLagRate * Mathf.Min(1f, theta / (Mathf.PI * 0.5f));
        }

        /// <summary>The widest a pendulum's bob can move between any two times: the chord across its whole swing, plus
        /// its rise, an upper bound that keeps the bisection's bracket honest.</summary>
        public static float MaxBobTravel(in ThresherBelfry.Pendulum ring) =>
            2f * ring.Length * Mathf.Sin(ring.Swing) + ring.Length * (1f - Mathf.Cos(ring.Swing));

        public const int BisectionSteps = 14;

        /// <summary>
        /// Seconds from <paramref name="now"/> until a pilot at <paramref name="p"/> flying <paramref name="v"/> u/s
        /// (with <paramref name="extraPath"/> of turning to do first) meets the swinging ring: the root of
        /// <c>f(tau) = |Bob(now + tau) - p| + extraPath - v * tau</c>. f(0) &gt;= 0, and at
        /// <c>tauMax = (|Bob(now) - p| + extraPath + MaxBobTravel) / v</c> the triangle inequality makes f &lt;= 0, and f
        /// only falls while the ring is slower than <paramref name="v"/>: one root, inside the bracket.
        /// </summary>
        public static float ArrivalSeconds(in ThresherBelfry.Pendulum ring, Vector3 p, float v, float extraPath, float now)
        {
            v = Mathf.Max(1f, v);
            float lo = 0f;
            float hi = ((ring.BobAt(now) - p).magnitude + extraPath + MaxBobTravel(ring)) / v;
            for (int i = 0; i < BisectionSteps; i++)
            {
                float mid = 0.5f * (lo + hi);
                float f = (ring.BobAt(now + mid) - p).magnitude + extraPath - v * mid;
                if (f > 0f) lo = mid;
                else hi = mid;
            }
            return hi;
        }

        /// <summary>How much of the lead to take: none beyond three quarters of a swing out (fly the gate), all of it
        /// inside a quarter swing (meet the ring).</summary>
        public static float LeadWeight(float tau, float period) =>
            Mathf.Clamp01((0.75f * period - tau) / (0.5f * period));

        /// <summary>The swing's rest point: where the ring hangs at the middle of its swing (the gate).</summary>
        public static Vector3 Rest(in ThresherBelfry.Pendulum ring) => ring.Pivot - ring.Up * ring.Length;

        /// <summary>Where to aim: between the rest point and the ring's pose at the arrival time, read one frame back.</summary>
        public static Vector3 LeadCentre(in ThresherBelfry.Pendulum ring, float arrivalTime, float tau, float dt,
                                         out float weight)
        {
            float period = 2f * Mathf.PI / Mathf.Max(1e-3f, ring.Omega);
            weight = LeadWeight(tau, period);
            return Vector3.Lerp(Rest(ring), ring.BobAt(arrivalTime - dt), weight);
        }

        /// <summary>
        /// The aim for <paramref name="pilot"/> at ring <paramref name="ring"/>: solve, latch inside
        /// <paramref name="latchRadius"/> (keep the arrival unless a fresh solve moves it by more than
        /// <paramref name="relatchSeconds"/>), weight, and return the point.
        /// </summary>
        public static Vector3 Aim(Pilot pilot, ThresherBelfry.Layout layout, int ring, Vector3 p, Vector3 forward, float v,
                                  float now, float dt, float latchRadius, float relatchSeconds)
        {
            var pendulum = layout.Rings[ring];
            Vector3 rest = Rest(pendulum);
            // The turn is to where the pilot is actually heading - last frame's aim at this ring - not to the rest point:
            // measured to the rest point it grows as the nose swings onto the lead and pushes the arrival late (offline,
            // an I4 median miss of 24 u against 5 u).
            Vector3 turnTo = pilot != null && pilot.LatchedRing == ring ? pilot.Diag.Aim : rest;
            float tau = ArrivalSeconds(pendulum, p, v, TurnCost(p, forward, turnTo, v), now);
            float arrival = now + tau;
            bool latched = false;
            if (pilot != null)
            {
                bool close = (rest - p).sqrMagnitude < latchRadius * latchRadius;
                if (close && pilot.LatchedRing == ring && Mathf.Abs(arrival - pilot.LatchedArrival) <= relatchSeconds)
                {
                    arrival = pilot.LatchedArrival;
                    latched = true;
                }
                else
                {
                    pilot.LatchedRing = ring;
                    pilot.LatchedArrival = arrival;
                }
            }
            float tauEff = Mathf.Max(0f, arrival - now);
            Vector3 aim = LeadCentre(pendulum, arrival, tauEff, dt, out float weight);
            if (pilot != null)
                pilot.Diag = new Diag { Aim = aim, Tau = tauEff, Weight = weight, Ring = ring, DuckMace = pilot.DuckMace,
                                        Latched = latched, Ducking = pilot.DuckMace >= 0 };
            return aim;
        }

        /// <summary>A mace's hang point: where its head crosses the straight at the middle of its swing.</summary>
        public static Vector3 Hang(in ThresherBelfry.Pendulum mace) => mace.Pivot - mace.Up * mace.Length;

        /// <summary>
        /// The point to steer at to pass under <paramref name="mace"/>, on the DUCK LINE (<paramref name="clear"/> below its
        /// hang point, inward away from the pivot, along the straight). Before the mace's plane it is the line's point AT
        /// the plane, so the straight path to it crosses the plane exactly <paramref name="clear"/> down (a point past the
        /// plane would cross it higher - above the clearance when the threat is seen late). From <paramref name="carrot"/>
        /// short of the plane on, it runs <paramref name="carrot"/> ahead of the pilot (<paramref name="along"/> is the
        /// pilot's distance past the plane), so the target is never behind the hull.
        /// </summary>
        public static Vector3 DuckPoint(in ThresherBelfry.Pendulum mace, float clear, float along, float carrot) =>
            Hang(mace) - mace.Up * clear + mace.Forward * Mathf.Max(0f, along + carrot);

        /// <summary>
        /// Does a pilot at <paramref name="pos"/> flying <paramref name="vel"/> meet a mace head among
        /// <paramref name="count"/> maces from <paramref name="first"/> within <paramref name="horizon"/> seconds? The pilot
        /// crosses a mace's plane at <c>tau</c>; the head is tested a slab either side of that (it is one head-reach
        /// thick along the straight), with the pilot's timing belief <paramref name="bias"/> added.
        /// </summary>
        public static bool MaceThreat(ThresherBelfry.Layout layout, int first, int count, Vector3 pos, Vector3 vel,
                                      float now, float horizon, float maxReach, float bias, float band, float clear,
                                      out int mace)
        {
            mace = -1;
            for (int m = first; m < first + count && m < layout.Maces.Count; m++)
            {
                var pendulum = layout.Maces[m];
                Vector3 forward = pendulum.Forward;
                float vf = Vector3.Dot(vel, forward);
                if (vf <= 1f) continue;
                Vector3 hang = Hang(pendulum);
                float tau = Vector3.Dot(hang - pos, forward) / vf;
                if (tau <= 0f || tau > Mathf.Min(horizon, maxReach / vf)) continue;
                Vector3 q = pos + vel * tau;
                if (Vector3.Dot(q - hang, pendulum.Up) < -clear) continue;   // already passing under it
                float slab = ThresherBelfry.HeadReach / vf;
                for (int k = -1; k <= 1; k++)
                {
                    Vector3 head = pendulum.BobAt(now + tau + k * slab + bias);
                    if ((q - head).sqrMagnitude < band * band)
                    {
                        mace = m;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>How wrong a pilot of this level reads a mace's beat (s, either way): the handicap covers the rings,
        /// this covers the maces, so nothing is counted twice.</summary>
        public static float TimingErrorSeconds(AIDifficulty difficulty) => difficulty switch
        {
            AIDifficulty.Easy => 0.60f,
            AIDifficulty.Medium => 0.35f,
            AIDifficulty.Hard => 0.15f,
            _ => 0.03f,
        };

        /// <summary>The half-width of the gap the I4 shutters leave at <paramref name="t"/> (the full radius when there
        /// are no shutters): the bars' inner faces either side of the slot's centre.</summary>
        public static float ShutterClearHalfWidth(ThresherBelfry.Layout layout, int gate, float t)
        {
            float radius = layout.Gates[gate].Radius;
            if (!layout.Spec.HasShutters) return radius;
            float open = ThresherBelfry.ShutterOpen(layout, gate, t);
            return radius * Mathf.Lerp(ThresherBelfry.ShutterMinOpen, 1f, open) - ThresherBelfry.ShutterCube * 0.5f;
        }
    }
}
