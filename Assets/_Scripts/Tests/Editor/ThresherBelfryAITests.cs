using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

namespace CosmicShore.Tests
{
    /// <summary>
    /// How an AI Thresher reads the Belfry's clock (ThresherBelfryAI, 2026-11-07; plan step time-arcade). The flier
    /// tests' thresholds were MEASURED first, offline, by a twin of these fliers (32 phases a rung):
    /// <list type="bullet">
    /// <item>A pilot whose nose lags (turn rate = min(120 deg/s, 1.5/s x the angle off)) and leads the ring threads 32/32 at
    /// every intensity, median miss 0.2 / 0.6 / 1.7 / 4.9 u (I1-I4). Chasing the live pose it threads 12/32 at I4.
    /// Leading with the turn cost measured to the REST point (the first build) missed by a median 24 u at I4: the
    /// 12 u median bound below catches that.</item>
    /// <item>An aimed launch (orbit 300 u/s, launched at 4.8x, flown straight, the nose 120 deg off while it spins) led for
    /// the launch threads 32/32. Led for the orbit's speed with the spinning nose's turn (the first build) it threads
    /// 6/32 at I4.</item>
    /// </list>
    /// A nose with NO lag chases the live pose perfectly - which is why the fliers here lag: the real pilot's does.
    /// </summary>
    public class ThresherBelfryAITests
    {
        static readonly float[] Mouths = { 96f, 72f, 58f, 46f };
        const float Dt = 1f / 60f;
        static float[] s_sink;

        static List<RaceGate> Course(int intensity)
        {
            var gates = new List<RaceGate>();
            const int n = 8;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n, b = (i + 1) * Mathf.PI * 2f / n;
                var p = new Vector3(Mathf.Cos(a) * 800f, Mathf.Sin(a * 2f) * 120f, Mathf.Sin(a) * 800f);
                var q = new Vector3(Mathf.Cos(b) * 800f, Mathf.Sin(b * 2f) * 120f, Mathf.Sin(b) * 800f);
                gates.Add(new RaceGate(p, (q - p).normalized, Mouths[intensity - 1]));
            }
            return gates;
        }

        static ThresherBelfry.Layout Build(int intensity)
        {
            var layout = new ThresherBelfry.Layout();
            ThresherBelfry.Build(Course(intensity), intensity, layout);
            return layout;
        }

        /// <summary>The ring of gate 0 with its swing phase replaced (so a test can sweep every phase).</summary>
        static ThresherBelfry.Pendulum WithPhase(in ThresherBelfry.Pendulum r, float phase) =>
            new(r.Gate, r.Pivot, r.Up, r.Side, r.Forward, r.Length, r.Swing, r.Omega, phase);

        // ── The arrival solve ────────────────────────────────────────────

        [Test]
        public void TheArrivalSolveMeetsTheRing([Values(1, 2, 3, 4)] int intensity)
        {
            var layout = Build(intensity);
            var ring0 = layout.Rings[0];
            Vector3 p = ThresherBelfryAI.Rest(ring0) - ring0.Forward * 600f + ring0.Side * 40f;
            for (int k = 0; k < 64; k++)
            {
                var ring = WithPhase(ring0, k * Mathf.PI * 2f / 64f);
                const float v = 125f, extra = 30f;
                float tau = ThresherBelfryAI.ArrivalSeconds(ring, p, v, extra, 3f);
                float gap = (ring.BobAt(3f + tau) - p).magnitude + extra - v * tau;
                Assert.AreEqual(0f, gap, 0.5f, $"phase {k}: the solve is {gap:0.00} u off the ring");
                float hi = ((ring.BobAt(3f) - p).magnitude + extra + ThresherBelfryAI.MaxBobTravel(ring)) / v;
                Assert.LessOrEqual((ring.BobAt(3f + hi) - p).magnitude + extra - v * hi, 0f, "the bracket lost its root");
            }
        }

        [Test]
        public void FarOutTheAimIsTheGate_CloseInItIsTheRing()
        {
            var ring = Build(4).Rings[0];
            float period = 2f * Mathf.PI / ring.Omega;
            Vector3 far = ThresherBelfryAI.LeadCentre(ring, 10f + period, period, Dt, out float wFar);
            Assert.AreEqual(0f, wFar);
            Assert.AreEqual(0f, (far - ThresherBelfryAI.Rest(ring)).magnitude, 1e-3f);
            Vector3 near = ThresherBelfryAI.LeadCentre(ring, 10f, 0f, Dt, out float wNear);
            Assert.AreEqual(1f, wNear);
            Assert.AreEqual(0f, (near - ring.BobAt(10f - Dt)).magnitude, 1e-3f, "read one frame back, as the race scores it");
        }

        [Test]
        public void TheLatchHoldsTheAimStill()
        {
            var layout = Build(4);
            var ring = layout.Rings[0];
            var pilot = new ThresherBelfryAI.Pilot();
            Vector3 p = ThresherBelfryAI.Rest(ring) - ring.Forward * 100f;
            Vector3 a = ThresherBelfryAI.Aim(pilot, layout, 0, p, ring.Forward, 125f, 7f, Dt, 135f, 0.3f);
            Vector3 nose = (a - p).normalized;   // the pilot's nose has come onto the aim it was given
            Vector3 b = ThresherBelfryAI.Aim(pilot, layout, 0, p, nose, 125f, 7.05f, Dt, 135f, 0.3f);
            Assert.IsTrue(pilot.Diag.Latched);
            Assert.Less((a - b).magnitude, 2f, "the aim jittered with the clock");
        }

        // ── Flying it ────────────────────────────────────────────────────

        /// <summary>A lagging nose at 125 u/s flown 700 u at the ring; scored on the ring's live mouth (one frame back).</summary>
        static bool FlyLagged(ThresherBelfry.Layout layout, in ThresherBelfry.Pendulum ring, float radius, bool lead,
                              out float off)
        {
            const float v = 125f, turn = 120f * Mathf.Deg2Rad, lag = 1.5f;
            var probe = new ThresherBelfry.Layout();
            probe.Gates.Add(layout.Gates[0]);
            probe.Rings.Add(ring);
            var pilot = new ThresherBelfryAI.Pilot();
            Vector3 axis = ring.Forward, rest = ThresherBelfryAI.Rest(ring);
            Vector3 p = rest - axis * 700f, f = axis;
            float t = 0f;
            for (int step = 0; step < 30 * 60; step++)
            {
                Vector3 aim = lead
                    ? ThresherBelfryAI.Aim(pilot, probe, 0, p, f, v, t, Dt, 135f, 0.3f)
                    : ring.BobAt(t);
                Vector3 want = (aim - p).normalized;
                float angle = Vector3.Angle(f, want) * Mathf.Deg2Rad;
                f = Vector3.RotateTowards(f, want, Mathf.Min(turn, lag * angle) * Dt, 0f).normalized;
                Vector3 prev = p;
                p += f * v * Dt;
                t += Dt;
                float d0 = Vector3.Dot(prev - rest, axis), d1 = Vector3.Dot(p - rest, axis);
                if (d0 < 0f && d1 >= 0f)
                {
                    Vector3 hit = Vector3.Lerp(prev, p, -d0 / (d1 - d0));
                    Vector3 rel = hit - ring.BobAt(t - Dt);
                    off = (rel - Vector3.Dot(rel, axis) * axis).magnitude;
                    return off <= radius;
                }
            }
            off = float.MaxValue;
            return false;
        }

        static (int hits, float median) Sweep(int intensity, bool lead)
        {
            var layout = Build(intensity);
            float radius = layout.Gates[0].Radius;
            var offs = new List<float>();
            int hits = 0;
            for (int k = 0; k < 32; k++)
            {
                var ring = WithPhase(layout.Rings[0], k * Mathf.PI * 2f / 32f);
                if (FlyLagged(layout, ring, radius, lead, out float off)) hits++;
                offs.Add(off);
            }
            offs.Sort();
            return (hits, offs[16]);
        }

        [Test]
        public void ALeadingPilotThreadsTheSwingingRing([Values(1, 2, 3, 4)] int intensity)
        {
            var (hits, median) = Sweep(intensity, lead: true);
            Assert.GreaterOrEqual(hits, 31, $"I{intensity}: the lead threaded {hits}/32");
            Assert.LessOrEqual(median, 12f, $"I{intensity}: the lead's median miss is {median:0.0} u from the ring's centre");
        }

        [Test]
        public void NegativeControl_ChasingTheLivePoseMissesAtI4()
        {
            var lead = Sweep(4, lead: true);
            var live = Sweep(4, lead: false);
            Assert.LessOrEqual(live.hits, 20, $"chasing the live pose threaded {live.hits}/32 - the test has no teeth");
            Assert.GreaterOrEqual(live.median, 2f * lead.median, "the lead is no better than the chase");
        }

        const string ThresherConfigPath = "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset";

        /// <summary>Seconds a launch at <paramref name="v0"/> takes to cover <paramref name="path"/> when its speed eases back
        /// toward cruise at the throttle's tracking rate (THRESHERTIME.md: 1.5/s) - the hull's own profile, NOT the lead's
        /// constant-speed assumption, so the test can disagree with the lead.</summary>
        static float FlightSeconds(float path, float v0)
        {
            const float cruise = 125f, rate = 1.5f;
            float lo = 0f, hi = path / cruise;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                float s = cruise * mid + (v0 - cruise) * (1f - Mathf.Exp(-rate * mid)) / rate;
                if (s < path) lo = mid; else hi = mid;
            }
            return hi;
        }

        /// <summary>An aimed launch the way the game feeds it: planted, orbiting at <paramref name="orbit"/> u/s with the nose
        /// 120 deg off the ring (it spins), led through <see cref="ThresherBelfryAI.LeadInputs"/> with the shipped config's
        /// boost and exit buff, then flown straight on the hull's real profile (<see cref="FlightSeconds"/>). Scored on the
        /// ring's live mouth when the flight reaches its plane.</summary>
        static int Launches(int intensity, bool planted, out float median)
        {
            const float orbit = 300f;
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ThresherConfigSO>(ThresherConfigPath);
            Assert.IsNotNull(cfg, ThresherConfigPath + " moved - the launch test reads the shipped boost and exit buff");
            float factor = cfg.LaunchBoost * cfg.LaunchExitSpeedMultiplier;
            var layout = Build(intensity);
            float radius = layout.Gates[0].Radius;
            var offs = new List<float>();
            int hits = 0;
            for (int k = 0; k < 32; k++)
            {
                var ring = WithPhase(layout.Rings[0], k * Mathf.PI * 2f / 32f);
                var probe = new ThresherBelfry.Layout();
                probe.Gates.Add(layout.Gates[0]);
                probe.Rings.Add(ring);
                Vector3 axis = ring.Forward, rest = ThresherBelfryAI.Rest(ring);
                Vector3 p = rest - axis * 400f;
                Vector3 spinningNose = Quaternion.AngleAxis(120f, ring.Up) * axis;
                ThresherBelfryAI.LeadInputs(planted, orbit, spinningNose, factor, 125f, 5000f, 1f,
                                            out float v, out Vector3 nose);
                Vector3 aim = ThresherBelfryAI.Aim(new ThresherBelfryAI.Pilot(), probe, 0, p, nose, v, 0f, 0f, 0f, 0.3f);
                Vector3 d = (aim - p).normalized;
                float path = Vector3.Dot(rest - p, axis) / Vector3.Dot(d, axis);
                Vector3 rel = p + d * path - ring.BobAt(FlightSeconds(path, orbit * factor));
                float off = (rel - Vector3.Dot(rel, axis) * axis).magnitude;
                offs.Add(off);
                if (off <= radius) hits++;
            }
            offs.Sort();
            median = offs[16];
            return hits;
        }

        [Test]
        public void AnAimedLaunchIsLedForTheLaunch([Values(1, 2, 3, 4)] int intensity)
        {
            int hits = Launches(intensity, planted: true, out float median);
            Assert.GreaterOrEqual(hits, 31, $"I{intensity}: a launch led for the launch threaded {hits}/32");
            Assert.LessOrEqual(median, 5f, $"I{intensity}: median {median:0.0} u off");
        }

        [Test]
        public void NegativeControl_ALaunchLedForTheOrbitMisses()
        {
            int hits = Launches(4, planted: false, out _);
            Assert.LessOrEqual(hits, 16, $"led for the orbit's speed and the spinning nose, the launch threaded {hits}/32");
        }

        [Test]
        public void PlantedTheLeadAssumesAStraightLaunch_FlyingItsOwnSpeed()
        {
            ThresherBelfryAI.LeadInputs(true, 300f, Vector3.right, 4.8f, 125f, 5000f, 1f, out float v, out Vector3 nose);
            Assert.AreEqual(1440f, v, 1e-3f);
            Assert.AreEqual(Vector3.zero, nose, "a launch has no turn to make");
            ThresherBelfryAI.LeadInputs(false, 90f, Vector3.right, 4.8f, 125f, 5000f, 1f, out v, out nose);
            Assert.AreEqual(125f, v, 1e-3f, "the floor keeps the solve's one root");
            Assert.AreEqual(Vector3.right, nose);
            ThresherBelfryAI.LeadInputs(false, 300f, Vector3.right, 4.8f, 125f, 5000f, 2f, out v, out _);
            Assert.AreEqual(600f, v, 1e-3f, "at time scale 2 a hull covers twice the clock's distance");
        }

        // ── The maces ────────────────────────────────────────────────────

        [Test]
        public void TheDuckPointClearsEveryHeadAndChainAtEveryAngle()
        {
            var layout = Build(4);
            const float hull = 8f;
            float clear = ThresherBelfry.HeadReach + hull + 8f;
            foreach (var part in layout.Parts)
            {
                if (part.Role != ThresherBelfry.Role.MaceHead && part.Role != ThresherBelfry.Role.MaceChain) continue;
                var mace = layout.Maces[part.Group];
                Vector3 duck = ThresherBelfryAI.DuckPoint(mace, clear, -1000f, 20f);   // before the plane: AT the plane
                float period = 2f * Mathf.PI / mace.Omega;
                for (float t = 0f; t < period; t += period / 48f)
                {
                    ThresherBelfry.PoseAt(layout, part, t, out var at, out _);
                    Assert.Greater((at - duck).magnitude, hull + part.Size * 0.87f, "the duck point is inside the mace");
                }
            }
        }

        [Test]
        public void TheStraightPathToTheDuckPointPassesUnderTheHead()
        {
            // The review's case: a duck point PAST the plane is crossed higher than the clearance when the threat is seen
            // late. The duck point is at the plane until the pilot is a carrot short of it, so from any distance the
            // straight path crosses the plane at least the head's reach plus the hull below the hang point.
            var layout = Build(4);
            var mace = layout.Maces[0];
            Vector3 hang = ThresherBelfryAI.Hang(mace);
            const float hull = 8f, carrot = 20f;
            float clear = ThresherBelfry.HeadReach + hull + 8f;
            for (float d = carrot; d <= 220f; d += 10f)
            {
                Vector3 pos = hang - mace.Forward * d;
                Vector3 duck = ThresherBelfryAI.DuckPoint(mace, clear, -d, carrot);
                Vector3 dir = duck - pos;
                float k = d / Vector3.Dot(dir, mace.Forward);
                float depth = -Vector3.Dot(pos + dir * k - hang, mace.Up);
                Assert.GreaterOrEqual(depth, ThresherBelfry.HeadReach + hull,
                    $"seen {d} u out, the path crosses the mace's plane only {depth:0.0} u down");
            }
            Vector3 past = ThresherBelfryAI.DuckPoint(mace, clear, 5f, carrot);
            Assert.Greater(Vector3.Dot(past - hang, mace.Forward), 5f, "past the plane the duck point stays ahead of the hull");
        }

        [Test]
        public void AMaceOnThePathIsAThreat_AQuarterSwingLaterItIsNot()
        {
            var layout = Build(4);
            var mace = layout.Maces[0];
            Vector3 hang = ThresherBelfryAI.Hang(mace);
            Vector3 pos = hang - mace.Forward * 150f, vel = mace.Forward * 125f;
            float tau = 150f / 125f;
            float onLine = (Mathf.PI - mace.Phase) / mace.Omega - tau;   // the head crosses the line as the pilot does
            float band = ThresherBelfry.HeadReach + 16f;
            Assert.IsTrue(ThresherBelfryAI.MaceThreat(layout, 0, 1, pos, vel, onLine, 1.6f, 400f, 0f, band, band, out int m));
            Assert.AreEqual(0, m);
            float quarter = 0.25f * 2f * Mathf.PI / mace.Omega;
            Assert.IsFalse(ThresherBelfryAI.MaceThreat(layout, 0, 1, pos, vel, onLine + quarter, 1.6f, 400f, 0f, band, band, out _),
                "negative control: at the top of its swing the head is clear of the line");
            Assert.IsFalse(ThresherBelfryAI.MaceThreat(layout, 0, 1, pos - mace.Up * (band + 5f), vel, onLine, 1.6f, 400f, 0f,
                band, band, out _), "a pilot already passing under it is not threatened");
        }

        [Test]
        public void ShuttersLeaveAGap_OnlyAtI4()
        {
            for (int i = 1; i <= 3; i++)
            {
                var layout = Build(i);
                Assert.AreEqual(layout.Gates[0].Radius, ThresherBelfryAI.ShutterClearHalfWidth(layout, 0, 3f));
            }
            // At I4 the AI's gap must be the gap the BARS leave: measured from the bars' own poses (ThresherBelfry.PoseAt),
            // the inner faces either side of the slot's centre.
            var top = Build(4);
            var side = top.Rings[0].Side;
            for (float t = 0f; t < 7f; t += 0.13f)
            {
                var gate = ThresherBelfry.GateAt(top, 0, t);
                float inner = float.MaxValue;
                foreach (var part in top.Parts)
                {
                    if (part.Role != ThresherBelfry.Role.Shutter || part.Group != 0) continue;
                    ThresherBelfry.PoseAt(top, part, t, out var at, out _);
                    inner = Mathf.Min(inner, Mathf.Abs(Vector3.Dot(at - gate.Position, side)) - part.Size * 0.5f);
                }
                Assert.AreEqual(inner, ThresherBelfryAI.ShutterClearHalfWidth(top, 0, t), 1e-2f, $"t = {t:0.00} s");
                Assert.Greater(inner, 0f, "the bars met: the mouth shut");
            }
        }

        [Test]
        public void AHarderPilotReadsTheBeatBetter()
        {
            Assert.Greater(ThresherBelfryAI.TimingErrorSeconds(AIDifficulty.Easy), ThresherBelfryAI.TimingErrorSeconds(AIDifficulty.Medium));
            Assert.Greater(ThresherBelfryAI.TimingErrorSeconds(AIDifficulty.Medium), ThresherBelfryAI.TimingErrorSeconds(AIDifficulty.Hard));
            Assert.Greater(ThresherBelfryAI.TimingErrorSeconds(AIDifficulty.Hard), ThresherBelfryAI.TimingErrorSeconds(AIDifficulty.Legendary));
        }

        [Test]
        public void AimingAndDuckingAllocateNothing()
        {
            var layout = Build(4);
            var pilot = new ThresherBelfryAI.Pilot();
            var ring = layout.Rings[0];
            Vector3 p = ThresherBelfryAI.Rest(ring) - ring.Forward * 300f;
            ThresherBelfryAI.Aim(pilot, layout, 0, p, ring.Forward, 125f, 1f, Dt, 135f, 0.3f);   // warm up
            ThresherBelfryAI.MaceThreat(layout, 0, 2, p, ring.Forward * 125f, 1f, 1.6f, 400f, 0f, 30f, 30f, out _);
            // Unity's GC.Alloc recorder (System.GC.GetAllocatedBytesForCurrentThread reads 0 under the editor's Boehm GC,
            // so a test on it cannot fail).
            Assert.That(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    ThresherBelfryAI.Aim(pilot, layout, 0, p, ring.Forward, 125f, 1f + i * Dt, Dt, 135f, 0.3f);
                    ThresherBelfryAI.MaceThreat(layout, 0, 2, p, ring.Forward * 125f, 1f + i * Dt, 1.6f, 400f, 0f, 30f, 30f, out _);
                }
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(), "the per-frame AI allocated");
            // Negative control: the recorder sees an allocation when there is one.
            Assert.That(() => { s_sink = new float[16]; }, UnityEngine.TestTools.Constraints.Is.AllocatingGCMemory());
        }
    }
}
