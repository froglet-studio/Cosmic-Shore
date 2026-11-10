using Unity.Collections;
using Unity.Mathematics;

namespace CosmicShore.Game
{
    /// <summary>
    /// The ghost pilots' motion rules, as plain Burst-safe math: one pilot, one fixed step, no neighbours
    /// (Docs/GENOME_GHOST_PILOTS.md). A line-for-line port of the genome lab's mover step
    /// (amoebius-genome <c>agenome/sim.py</c>, <c>World._step_movers</c>) for the ops the tuned genomes use:
    /// <c>ag.wander</c>, <c>ag.attract</c>, <c>ag.bound</c>, and the turn cap and cruise relax of the integrate step.
    ///
    /// <para>Two deliberate differences from the lab, both documented:
    /// <list type="bullet">
    /// <item><b>No boids forces.</b> At the tuned densities a pilot averages ~0.1 neighbours inside the boids radius,
    /// so cohesion, alignment and separation are near zero; only the boids op's <c>turn</c> survives, as the turn cap.
    /// That is what makes the step O(n) and neighbour-free.</item>
    /// <item><b>An exact turn cap.</b> The lab caps the turn by normalised lerp, which overshoots the cap by up to a few
    /// percent when the wanted turn is just above it. Here the heading is rotated by exactly the cap.</item>
    /// </list></para>
    ///
    /// <para>Everything here uses <c>Unity.Mathematics.math</c>, never <c>System.MathF</c> (a MathF call turns Burst off
    /// for every job in the assembly: Docs/claude/ANTI_PATTERNS.md).</para>
    /// </summary>
    public struct GhostPilotMotion
    {
        /// <summary>The lab's simulation step: 60 Hz, the game's step (render.py, FPS 15 x 4 substeps).</summary>
        public const float StepSeconds = 1f / 60f;

        /// <summary>The lab's wander scale: <c>acc += amp * wave * speed * 0.3</c>.</summary>
        public const float WanderSpeedGain = 0.3f;

        /// <summary>The lab's orbit scale: <c>acc += orbit * tangent * speed * 2</c>.</summary>
        public const float OrbitSpeedGain = 2f;

        public float CruiseSpeed;
        public float TurnCap;            // rad/s; <= 0 means uncapped
        public float SpeedRelax;         // 1/s, the lab's 2.0
        public float WanderOmega;        // 2 pi f
        public float WanderAccel;        // amp * max(speed, 1e-3) * 0.3
        public float BoundRadius;
        public float BoundStiffness;
        public bool Attract;
        public float3 AttractCentre;
        public float AttractStrength;
        public float AttractRadius;
        public float AttractOrbitAccel;  // orbit * speed * 2

        /// <summary>The motion a profile describes.</summary>
        public static GhostPilotMotion From(GhostPilotFlockProfileSO p)
        {
            float speed = p.CruiseSpeed;
            return new GhostPilotMotion
            {
                CruiseSpeed = speed,
                TurnCap = p.TurnCap,
                SpeedRelax = p.SpeedRelax,
                WanderOmega = p.WanderFrequency * 2f * math.PI,
                WanderAccel = p.WanderAmplitude * math.max(speed, 1e-3f) * WanderSpeedGain,
                BoundRadius = p.BoundRadius,
                BoundStiffness = p.BoundStiffness,
                Attract = p.AttractEnabled,
                AttractCentre = p.AttractCentre,
                AttractStrength = p.AttractStrength,
                AttractRadius = p.AttractRadius,
                AttractOrbitAccel = p.AttractOrbit * speed * OrbitSpeedGain,
            };
        }

        /// <summary>
        /// The steering acceleration on a pilot at <paramref name="pos"/> (relative to the flock centre) at sim time
        /// <paramref name="t"/>: wander (a sum of sines, six phases per pilot) + attract/orbit + the soft spherical bound.
        /// </summary>
        public static float3 Acceleration(float3 pos, float3 phaseA, float3 phaseB, float t, in GhostPilotMotion m)
        {
            float fq = m.WanderOmega;
            float3 wave = new float3(
                math.sin(fq * t + phaseA.x) + 0.5f * math.sin(2.3f * fq * t + phaseB.x),
                math.sin(fq * t * 0.8f + phaseA.y) + 0.5f * math.sin(1.7f * fq * t + phaseB.y),
                math.sin(fq * t * 1.1f + phaseA.z) + 0.5f * math.sin(2.9f * fq * t + phaseB.z));
            float3 acc = m.WanderAccel * wave;

            if (m.Attract)
            {
                float3 rel = pos - m.AttractCentre;
                float r = math.length(rel) + 1e-6f;
                float3 u = rel / r;
                acc += -m.AttractStrength * (r - m.AttractRadius) * u;
                if (math.abs(m.AttractOrbitAccel) > 1e-6f)
                    acc += m.AttractOrbitAccel * math.cross(new float3(0f, 1f, 0f), u);
            }

            float rb = math.length(pos) + 1e-6f;
            float over = math.max(rb - m.BoundRadius, 0f);
            acc += -m.BoundStiffness * over * pos / rb;
            return acc;
        }

        /// <summary>
        /// <paramref name="wanted"/> with its direction turned at most <paramref name="maxAngle"/> radians away from
        /// <paramref name="current"/>'s, keeping its own length. Rotates in the plane of the two directions; an exactly
        /// reversed wish turns about an arbitrary perpendicular.
        /// </summary>
        public static float3 CapTurn(float3 current, float3 wanted, float maxAngle)
        {
            float lenW = math.length(wanted);
            float lenC = math.length(current);
            if (lenW < 1e-9f || lenC < 1e-9f) return wanted;
            float3 u0 = current / lenC;
            float3 u1 = wanted / lenW;
            float c = math.clamp(math.dot(u0, u1), -1f, 1f);
            float ang = math.acos(c);
            if (ang <= maxAngle) return wanted;
            float3 w = u1 - u0 * c;
            float wl = math.length(w);
            if (wl < 1e-6f)
            {
                float3 axis = math.abs(u0.y) < 0.95f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                w = math.normalize(math.cross(axis, u0));
            }
            else w /= wl;
            float s, cs;
            math.sincos(maxAngle, out s, out cs);
            return (u0 * cs + w * s) * lenW;
        }

        /// <summary>
        /// One step of one pilot, the lab's integrate: <c>v += acc dt</c>, cap the turn, relax the speed toward cruise
        /// (<c>sp += (cruise - sp) * min(1, relax dt)</c>, direction unchanged), then <c>pos += v dt</c>.
        /// </summary>
        public static void Step(ref float3 pos, ref float3 vel, float3 phaseA, float3 phaseB, float t, float dt,
                                in GhostPilotMotion m)
        {
            float3 acc = Acceleration(pos, phaseA, phaseB, t, m);
            float3 nv = vel + acc * dt;
            if (m.TurnCap > 0f) nv = CapTurn(vel, nv, m.TurnCap * dt);
            float sp = math.length(nv) + 1e-6f;
            float sp2 = sp + (m.CruiseSpeed - sp) * math.min(1f, m.SpeedRelax * dt);
            vel = nv / sp * sp2;
            pos += vel * dt;
        }

        /// <summary>
        /// The lab's frame for a heading (<c>sim._basis</c>): z along <paramref name="fwd"/>, up = world y unless the
        /// heading is near vertical (then world x), as a rotation.
        /// </summary>
        public static quaternion Frame(float3 fwd)
        {
            float3 z = math.normalizesafe(fwd, new float3(0f, 0f, 1f));
            float3 up = math.abs(z.y) > 0.95f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 x = math.normalize(math.cross(up, z));
            float3 y = math.cross(z, x);
            return new quaternion(new float3x3(x, y, z));
        }

        /// <summary>
        /// Seeds pilots the way the lab's <c>ag.spawn</c> does: positions in a ball / shell / ring / line of
        /// <paramref name="radius"/>, a random heading at cruise speed, six random wander phases. Same seed, same flock.
        /// </summary>
        public static void Seed(uint seed, GhostPilotSpawnShape shape, float radius, float speed,
                                NativeArray<float3> pos, NativeArray<float3> vel,
                                NativeArray<float3> phaseA, NativeArray<float3> phaseB)
        {
            var rng = Random.CreateFromIndex(seed);
            int n = pos.Length;
            for (int i = 0; i < n; i++)
            {
                float3 p;
                switch (shape)
                {
                    case GhostPilotSpawnShape.Shell:
                        p = rng.NextFloat3Direction() * radius;
                        break;
                    case GhostPilotSpawnShape.Ring:
                    {
                        float a = rng.NextFloat(0f, 2f * math.PI);
                        float r = radius * math.sqrt(rng.NextFloat());
                        p = new float3(math.cos(a) * r, Gaussian(ref rng) * radius * 0.03f, math.sin(a) * r);
                        break;
                    }
                    case GhostPilotSpawnShape.Line:
                        p = new float3((rng.NextFloat() - 0.5f) * 2f * radius,
                                       Gaussian(ref rng) * radius * 0.02f, Gaussian(ref rng) * radius * 0.02f);
                        break;
                    default:
                        p = rng.NextFloat3Direction() * math.pow(rng.NextFloat(), 1f / 3f) * radius;
                        break;
                }
                pos[i] = p;
                vel[i] = rng.NextFloat3Direction() * speed;
                phaseA[i] = rng.NextFloat3(new float3(2f * math.PI));
                phaseB[i] = rng.NextFloat3(new float3(2f * math.PI));
            }
        }

        static float Gaussian(ref Random rng)
        {
            float u1 = math.max(rng.NextFloat(), 1e-7f);
            float u2 = rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
        }
    }

    /// <summary>Where pilots start (the lab's <c>ag.spawn</c> shape). Values are fixed for serialization.</summary>
    public enum GhostPilotSpawnShape
    {
        Ball = 0,
        Shell = 1,
        Ring = 2,
        Line = 3,
    }

    /// <summary>How a body or trail colour is chosen (the lab's <c>colormode</c>). Values fixed for serialization.</summary>
    public enum GhostPilotColorMode
    {
        /// <summary>The pilot's team hue mixed with the part's own colour (lab colormode 0).</summary>
        TeamMix = 0,
        /// <summary>The part's own colour only (lab colormode 1).</summary>
        Flat = 1,
    }
}
