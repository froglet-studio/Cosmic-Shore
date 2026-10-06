// THREAT FLORA 2 - the PHYSARUM GROVE (a slime mould that cables the grove between food and your trails),
// ported from the research model Tools/Ecology/flora/physarum.py (branch cece/eco-flora, DISCOVERIES.md
// "Threat flora", "Top 2 to port" #2) with its searched best (results/search_physarum_best.json).
// Jones (2010) "Characteristics of pattern formation and evolution in approximations of Physarum transport
// networks", Artificial Life 16(2), lifted to 3D. Plain C# over struct-of-arrays: no UnityEngine, so this
// file compiles and RUNS headless in Tools/Build/threat_flora_harness (asserted), and the glue
// (ThreatGrove + PhysarumSclerotium) converts at the boundary. Docs/THREAT_FLORA.md §3 is the design record.
//
// Two layers, both trivially data-parallel (Burst / compute shaped):
//   agents  N particles (position, heading, sensor phase, owner heart). Each step it samples the TRAIL field
//           SensorOffset ahead and at four sensors tilted SensorAngle off it, turns toward the best if that beats
//           forward, moves StepSpeed u/s, and deposits into its voxel.
//   trail   a scalar grid over the grove: 3-tap separable diffusion + evaporation - a CHEMICAL, not mass. Food
//           deposits every step; vessels deposit WakeDeposit (the searched best is NEGATIVE: a wake repels).
// TUBES are the body. Every MaterializeEvery seconds (a sparse threshold pass, never per frame) a voxel whose slow
// EMA passes On becomes a tube prism LAID FROM THE RESERVE, and one that falls under Off is RESORBED into it.
// A tube voxel holding food DIGESTS it into the reserve. So the cables are made of what the network ate, and
// every volume is in the ledger (Audit()).
// DANGER is PERISTALSIS: Greenberg-Hastings on tube voxels, one voxel per tick at WaveSpeed. Each heart (a
// SCLEROTIUM - the organism's crystal) is a pacemaker firing every Period s and BEATS danger for BeatOn s after
// each fire, glowing BeatGlow s before it; it climbs the slow trail gradient at HeartSpeed u/s.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    public enum PhysarumEventKind
    {
        /// <summary>Lay a tube prism at Voxel (the reserve is already debited; call CancelLay if it fails).</summary>
        Lay = 0,
        /// <summary>Resorb the tube prism at Voxel into the network (the reserve is already credited).</summary>
        Resorb = 1,
        /// <summary>The tube at Voxel turned DANGER (a pulse arrived).</summary>
        DangerOn = 2,
        /// <summary>The tube at Voxel went back to plain (refractory).</summary>
        DangerOff = 3,
        /// <summary>Food Food sits in a tube voxel and is digested: consume it, then CreditDigest its volume.</summary>
        Digest = 4,
        /// <summary>Heart Heart's beat starts (its shell turns danger).</summary>
        BeatOn = 5,
        /// <summary>Heart Heart's beat ends.</summary>
        BeatOff = 6,
        /// <summary>Heart Heart starts glowing ahead of its next beat (the beat's telegraph).</summary>
        BeatGlow = 7,
        /// <summary>Heart Heart moved: its new position is HeartPosition[Heart] (one event per materialize pass).</summary>
        HeartMoved = 8,
    }

    public struct PhysarumEvent
    {
        public PhysarumEventKind Kind;
        public int Voxel;
        public int Heart;
        public int Food;
    }

    /// <summary>
    /// Every number the network runs on. Defaults are the research's searched best
    /// (results/search_physarum_best.json) over its DEFAULTS (physarum.py) - quoted per field.
    /// </summary>
    public sealed class PhysarumParams
    {
        public float VoxelSize = 900f / 56f;    // DEFAULTS G=56 over the 900 u grove: 16.07 u voxels
        public float AgentsPerVoxel = 16000f / 91952f; // DEFAULTS n_agents=16000 over the 56^3 ball's inside voxels
        public int AgentCount = 0;              // > 0 overrides AgentsPerVoxel
        public float SensorOffset = 28f;        // DEFAULTS so
        public float SensorAngle = 30f;         // DEFAULTS sa (deg)
        public float RotationAngle = 40.4291f;  // best: ra (deg)
        public float StepSpeed = 44.3024f;      // best: ss (u/s)
        public float Deposit = 1f;              // DEFAULTS dep (per step)
        public float Diffuse = 0.5f;            // DEFAULTS diffuse (per step)
        public float Evaporate = 0.08f;         // DEFAULTS evap (per step)
        public float FoodDeposit = 1.5f;        // DEFAULTS food_dep (per step)
        public float On = 6f;                   // DEFAULTS on
        public float Off = 4.1165f;             // best: off
        public float Ema = 0.05f;               // DEFAULTS ema (per step)
        public float TubeVolume = 15.2827f;     // best: prism_vol (the glue reads it off the tube leaf)
        public float PlantVolume = 12000f;      // DEFAULTS plant_vol (the whole network's planted reserve)
        public float Digest = 0.3f;             // DEFAULTS digest (volume per s)
        public float Period = 3f;               // DEFAULTS period (s) - each heart's pacemaker
        public float WaveSpeed = 64.711f;       // best: wave_speed (u/s)
        public int ExciteTicks = 2;             // DEFAULTS ex_ticks - a tube is DANGER for this many wave ticks
        public int RefractoryTicks = 4;         // DEFAULTS refr - then refractory for this many
        public float WakeDeposit = -0.2435f;    // best: wake_dep (a vessel's wake REPELS the plasmodium)
        public int Warmup = 250;                // DEFAULTS warmup steps (the grove before you arrive)
        public float Jitter = 0.15f;            // DEFAULTS jitter
        public float HeartSpeed = 7.7737f;      // best: heart_speed (u/s) - sclerotia climb the slow trail
        public float BeatOn = 0.6f;             // DEFAULTS beat_on (s) - the beat stings this long after each fire
        public float BeatGlow = 0.8f;           // DEFAULTS beat_glow (s) - and glows this long before it
        public float HeartGuard = 50f;          // best: heart_guard (u) - the beat's sting radius
        public float StepSeconds = 0.1f;        // research dt: the Jones rule is PER STEP and was scored at 10 Hz
        public float MaterializeEvery = 0.5f;   // GAME: the sparse tube pass (research: every step)
        public int MaxTubes = 0;                // GAME: collider-budget cap on live tubes (0 = none, the research)
        public float ShellVolume = 0f;          // GAME: a sclerotium's beat shell, laid from the reserve

        public PhysarumParams Clone() => (PhysarumParams)MemberwiseClone();

        /// <summary>
        /// The research's element variants (elements.py, physarum rows): TIME = wave, step and digest x1.25,
        /// period /1.25; MASS = authored as THICKER TUBES ONLY (TubeVolume x1.5) with the SAME wave speed - the
        /// research's mass variant (slower waves, longer pulses) collapsed R to 0.72 because slow waves are
        /// dodged, and DISCOVERIES.md says to author it this way; SPACE = sensor offset x1.35, step x1.15;
        /// CHARGE = armour (the platform's shield, nothing here). 1 Charge, 2 Mass, 3 Space, 4 Time.
        /// </summary>
        public PhysarumParams ForElement(int element)
        {
            var p = Clone();
            switch (element)
            {
                case 4: p.WaveSpeed *= 1.25f; p.StepSpeed *= 1.25f; p.Digest *= 1.25f; p.Period /= 1.25f; break;
                case 2: p.TubeVolume *= 1.5f; break;
                case 3: p.SensorOffset *= 1.35f; p.StepSpeed *= 1.15f; break;
            }
            return p;
        }
    }

    public sealed class PhysarumCore
    {
        public readonly PhysarumParams P;
        public readonly ThreatGroveShape Shape;
        public readonly int NX, NY, NZ, VoxelCount;
        public readonly float H;
        public readonly Vector3 Origin;
        public readonly bool[] Inside;
        public readonly int InsideCount;

        // fields
        public float[] T, S;
        float[] _tmp;
        public readonly sbyte[] E;          // GH state: 0 rest, 1..ExciteTicks excited, then refractory
        public readonly int[] Wave;         // wave id per voxel (telegraph bookkeeping)
        public readonly bool[] Tube;
        public readonly bool[] Danger;
        sbyte[] _newE;
        int[] _newW;

        // agents (SoA)
        public readonly int AgentCount;
        public readonly Vector3[] AgentPos, AgentDir;
        readonly float[] _phase;
        public readonly int[] AgentOwner;

        // hearts (sclerotia)
        public readonly List<Vector3> HeartPosition = new List<Vector3>();
        public readonly List<bool> HeartAlive = new List<bool>();
        public readonly List<float> NextBeat = new List<float>();
        readonly List<bool> _beatOnPrev = new List<bool>();
        readonly List<bool> _glowPrev = new List<bool>();
        readonly List<Vector3> _heartPosted = new List<Vector3>();

        // food (refreshed by the caller; the core never owns food mass until it is digested)
        public int FoodCount;
        public Vector3[] FoodPos = new Vector3[64];
        public float[] FoodVol = new float[64];
        public bool[] FoodAlive = new bool[64];

        // vessels
        ThreatVessel[] _vessels = Array.Empty<ThreatVessel>();
        int _vesselCount;

        ThreatRng _rng;
        readonly int[] _depositVox;
        float _waveAcc, _matAcc, _stepAcc;
        public int WaveId;
        public float Time { get; private set; }
        public int TubeCount { get; private set; }
        public readonly List<PhysarumEvent> Events = new List<PhysarumEvent>(512);
        public bool WarmedUp { get; private set; }

        // ledger
        public double Reserve, Planted, Digested, Lost, SkeletonOut, HeartBodies;

        public PhysarumCore(PhysarumParams p, ThreatGroveShape shape, ulong seed)
        {
            P = p ?? new PhysarumParams();
            Shape = shape;
            _rng = new ThreatRng(seed);
            H = P.VoxelSize;
            if (shape.Kind == ThreatGroveShape.Ball)
            {
                int g = Math.Max(4, (int)MathF.Round(2f * shape.OuterRadius / H));
                NX = NY = NZ = g;
                H = 2f * shape.OuterRadius / g;
                Origin = shape.Centre - new Vector3(shape.OuterRadius);
            }
            else
            {
                shape.Bounds(out Vector3 lo, out Vector3 hi);
                NX = Math.Max(3, (int)MathF.Ceiling((hi.X - lo.X) / H));
                NY = Math.Max(3, (int)MathF.Ceiling((hi.Y - lo.Y) / H));
                NZ = Math.Max(3, (int)MathF.Ceiling((hi.Z - lo.Z) / H));
                Origin = lo;
            }
            VoxelCount = NX * NY * NZ;
            T = new float[VoxelCount]; S = new float[VoxelCount]; _tmp = new float[VoxelCount];
            E = new sbyte[VoxelCount]; _newE = new sbyte[VoxelCount];
            Wave = new int[VoxelCount]; _newW = new int[VoxelCount];
            Tube = new bool[VoxelCount]; Danger = new bool[VoxelCount];
            Inside = new bool[VoxelCount];
            int inside = 0;
            for (int v = 0; v < VoxelCount; v++)
            {
                Inside[v] = Shape.Contains(Centre(v));
                if (Inside[v]) inside++;
            }
            InsideCount = inside;

            AgentCount = P.AgentCount > 0 ? P.AgentCount : Math.Max(64, (int)MathF.Round(P.AgentsPerVoxel * inside));
            AgentPos = new Vector3[AgentCount]; AgentDir = new Vector3[AgentCount];
            _phase = new float[AgentCount]; AgentOwner = new int[AgentCount]; _depositVox = new int[AgentCount];
            for (int k = 0; k < AgentCount; k++)
            {
                // Jones: the plasmodium starts as a sheet over the whole domain (spread_init = 1)
                AgentPos[k] = Shape.Sample(ref _rng, H);
                AgentDir[k] = _rng.OnUnitSphere();
                _phase[k] = _rng.Range(0f, 2f * MathF.PI);
                AgentOwner[k] = -1;
            }
        }

        // ── grid helpers ──────────────────────────────────────────────────────────────────────────────────
        public Vector3 Centre(int v)
        {
            int iz = v % NZ, iy = (v / NZ) % NY, ix = v / (NY * NZ);
            return Origin + new Vector3((ix + 0.5f) * H, (iy + 0.5f) * H, (iz + 0.5f) * H);
        }

        public int Vox(Vector3 p)
        {
            int ix = Math.Clamp((int)MathF.Floor((p.X - Origin.X) / H), 0, NX - 1);
            int iy = Math.Clamp((int)MathF.Floor((p.Y - Origin.Y) / H), 0, NY - 1);
            int iz = Math.Clamp((int)MathF.Floor((p.Z - Origin.Z) / H), 0, NZ - 1);
            return (ix * NY + iy) * NZ + iz;
        }

        // ── hearts ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A sclerotium joins the network, bringing its planted share of the network's mass (the research plants
        /// the whole network with PlantVolume across its n hearts). Agents are shared round-robin among the
        /// living hearts (the research draws each agent's owner uniformly), so a heart that dies freezes its
        /// share and a heart that joins later revives the frozen ones.
        /// </summary>
        public int AddHeart(Vector3 position, float plantedVolume)
        {
            int k = HeartPosition.Count;
            HeartPosition.Add(Shape.Clamp(position, H));
            HeartAlive.Add(true);
            NextBeat.Add(Time + _rng.Range(0f, P.Period));
            _beatOnPrev.Add(false); _glowPrev.Add(false); _heartPosted.Add(HeartPosition[k]);
            Reserve += plantedVolume;
            Planted += plantedVolume;
            Reassign();
            return k;
        }

        /// <summary>The heart was taken: it stops beating, stops climbing, and its agents freeze (physarum.py cut).</summary>
        public void KillHeart(int k)
        {
            if (k < 0 || k >= HeartAlive.Count || !HeartAlive[k]) return;
            HeartAlive[k] = false;
        }

        public int LiveHearts
        {
            get { int n = 0; for (int k = 0; k < HeartAlive.Count; k++) if (HeartAlive[k]) n++; return n; }
        }

        /// <summary>
        /// When a heart JOINS, every agent is re-dealt round-robin among the living hearts - the research draws
        /// each agent's owner uniformly over its hearts, which is what a full deal gives. A heart that DIES only
        /// freezes its share (the research's `live = heart_alive[owner]`); a later sclerotium (the research never
        /// has one, the game's seeder does) revives them in the next deal.
        /// </summary>
        void Reassign()
        {
            var live = new List<int>();
            for (int k = 0; k < HeartAlive.Count; k++) if (HeartAlive[k]) live.Add(k);
            if (live.Count == 0) return;
            for (int a = 0; a < AgentCount; a++) AgentOwner[a] = live[a % live.Count];
        }

        /// <summary>The beat state at time t (physarum.py beating): stinging, and glowing ahead of the next fire.</summary>
        public bool Beating(int k)
        {
            if (!HeartAlive[k]) return false;
            float last = NextBeat[k] - P.Period;
            return last >= 0f && Time - last < P.BeatOn;
        }

        public bool Glowing(int k) => HeartAlive[k] && NextBeat[k] - Time < P.BeatGlow;

        // ── food & vessels (set by the caller) ────────────────────────────────────────────────────────────
        public void EnsureFood(int n)
        {
            if (FoodPos.Length >= n) return;
            int c = Math.Max(n, FoodPos.Length * 2);
            Array.Resize(ref FoodPos, c); Array.Resize(ref FoodVol, c); Array.Resize(ref FoodAlive, c);
        }

        public void SetVessels(ThreatVessel[] vessels, int count)
        {
            _vessels = vessels ?? Array.Empty<ThreatVessel>();
            _vesselCount = Math.Min(count, _vessels.Length);
        }

        // ── ledger hooks ──────────────────────────────────────────────────────────────────────────────────
        public void CreditDigest(float volume)
        {
            if (volume <= 0f) return;
            Reserve += volume;
            Digested += volume;
        }

        /// <summary>The glue could not lay a tube it was asked to: refund it.</summary>
        public void CancelLay(int v)
        {
            if (!Tube[v]) return;
            Tube[v] = false; Danger[v] = false; TubeCount--;
            Reserve += P.TubeVolume;
        }

        /// <summary>
        /// An active force removed the tube at v (rammed, shot, eaten, a vessel's cut): its volume leaves the
        /// network, and the trail there is wiped so the agents must re-find the gap (physarum.py _remove).
        /// </summary>
        public void TubeLost(int v)
        {
            if (v < 0 || v >= VoxelCount || !Tube[v]) return;
            Tube[v] = false; Danger[v] = false; TubeCount--;
            Lost += P.TubeVolume;
            T[v] = 0f; S[v] = 0f; E[v] = 0;
        }

        /// <summary>The tube at v left the network as a skeleton (its owning heart died): cell mass now.</summary>
        public void TubeReleased(int v)
        {
            if (v < 0 || v >= VoxelCount || !Tube[v]) return;
            Tube[v] = false; Danger[v] = false; TubeCount--;
            SkeletonOut += P.TubeVolume;
        }

        /// <summary>Lay a tube at v directly from the reserve (authoring a cable by hand; the harness's wave tests).</summary>
        public bool LayTubeAt(int v)
        {
            if (v < 0 || v >= VoxelCount || Tube[v] || !Inside[v] || Reserve < P.TubeVolume) return false;
            Reserve -= P.TubeVolume;
            Tube[v] = true; TubeCount++;
            Events.Add(new PhysarumEvent { Kind = PhysarumEventKind.Lay, Voxel = v });
            return true;
        }

        /// <summary>
        /// Cut everything inside a ball (physarum.py remove_ball / _remove): its tubes leave as cut mass and the
        /// trail and excitation there are wiped, so the agents have to re-find the gap.
        /// </summary>
        public int RemoveBall(Vector3 centre, float radius)
        {
            int removed = 0;
            float r2 = radius * radius;
            for (int v = 0; v < VoxelCount; v++)
            {
                if ((Centre(v) - centre).LengthSquared() >= r2) continue;
                if (Tube[v]) { TubeLost(v); removed++; }
                T[v] = 0f; S[v] = 0f; E[v] = 0;
            }
            return removed;
        }

        /// <summary>A heart lays its beat shell from the network's reserve. False = cannot pay.</summary>
        public bool SpendOnHeartBody(float volume)
        {
            if (Reserve < volume) return false;
            Reserve -= volume;
            HeartBodies += volume;
            return true;
        }

        public void HeartBodyRefund(float volume) { Reserve += volume; HeartBodies -= volume; }
        public void HeartBodyLost(float volume) { HeartBodies -= volume; Lost += volume; }
        public void HeartBodySkeleton(float volume) { HeartBodies -= volume; SkeletonOut += volume; }

        public double TubeVolumeTotal => TubeCount * (double)P.TubeVolume;

        /// <summary>planted + digested - lost - skeleton == reserve + tubes + heart bodies. 0 to rounding.</summary>
        public double Audit() => (Planted + Digested - Lost - SkeletonOut) - (Reserve + TubeVolumeTotal + HeartBodies);

        // ── the step ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Run the research's warm-up (the grove before you arrive): agents, field, tubes - no waves.</summary>
        public void RunWarmup()
        {
            while (!WarmupSteps(P.Warmup)) { }
        }

        int _warmDone;

        /// <summary>Run up to <paramref name="steps"/> more warm-up steps (the glue spreads the warm-up over frames
        /// rather than paying ~0.3 s in one). Events accumulate across calls until the caller clears them.
        /// Returns true once the whole warm-up has run.</summary>
        public bool WarmupSteps(int steps)
        {
            for (int i = 0; i < steps && _warmDone < P.Warmup; i++, _warmDone++)
                Sim(P.StepSeconds, warm: true, materialize: true);
            if (_warmDone >= P.Warmup) WarmedUp = true;
            return WarmedUp;
        }

        /// <summary>Advance by dt: sim steps at StepSeconds, the tube pass every MaterializeEvery, GH waves at
        /// VoxelSize / WaveSpeed. Events accumulate in <see cref="Events"/> (cleared here).</summary>
        public void Advance(float dt)
        {
            Events.Clear();
            _stepAcc += dt;
            while (_stepAcc >= P.StepSeconds - 1e-6f)
            {
                _stepAcc -= P.StepSeconds;
                _matAcc += P.StepSeconds;
                bool mat = _matAcc >= P.MaterializeEvery - 1e-6f;
                if (mat) _matAcc = 0f;
                Sim(P.StepSeconds, warm: false, materialize: mat);
                Waves(P.StepSeconds);
                if (mat) PostHearts();
            }
        }

        void Sim(float dt, bool warm, bool materialize)
        {
            Time += warm ? 0f : dt;
            MoveAgents(dt);
            for (int f = 0; f < FoodCount; f++)
                if (FoodAlive[f]) T[Vox(FoodPos[f])] += P.FoodDeposit;
            if (P.WakeDeposit != 0f && !warm)
                for (int i = 0; i < _vesselCount; i++)
                    T[Vox(_vessels[i].Position)] += P.WakeDeposit;
            DiffuseEvaporate();
            if (materialize) Tubes(warm);
            // physarum.py digests inside _sim, warm-up included: the grove eats while it settles, and that
            // income is why the research's warmed network outgrows its planted reserve (1,103 tubes vs 785)
            if (materialize) DigestPass(warm ? dt : P.MaterializeEvery);
            ClimbHearts(dt);
        }

        void MoveAgents(float dt)
        {
            float sa = ThreatFloraMath.Deg2Rad(P.SensorAngle);
            float cosA = MathF.Cos(sa), sinA = MathF.Sin(sa);
            float turnK = Math.Min(1f, P.RotationAngle / P.SensorAngle);
            float so = P.SensorOffset, step = P.StepSpeed * dt;
            for (int k = 0; k < AgentCount; k++)
            {
                int o = AgentOwner[k];
                if (o < 0 || !HeartAlive[o]) continue;
                Vector3 A = AgentPos[k], D = AgentDir[k];
                Vector3 up = MathF.Abs(D.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 u = Vector3.Normalize(Vector3.Cross(D, up));
                Vector3 w = Vector3.Cross(D, u);
                float F = T[Vox(A + D * so)];
                float best = F;
                Vector3 bdir = D;
                for (int s = 0; s < 4; s++)
                {
                    float th = _phase[k] + s * MathF.PI * 0.5f;
                    Vector3 sd = cosA * D + sinA * (MathF.Cos(th) * u + MathF.Sin(th) * w);
                    float val = T[Vox(A + sd * so)];
                    if (val > best) { best = val; bdir = sd; }
                }
                if (best > F) D += (bdir - D) * turnK;
                D += new Vector3(_rng.Gaussian(), _rng.Gaussian(), _rng.Gaussian()) * P.Jitter;
                D = Vector3.Normalize(D);
                A += D * step;
                if (!Shape.Contains(A, H))
                {
                    D = -D + new Vector3(_rng.Gaussian(), _rng.Gaussian(), _rng.Gaussian()) * 0.3f;
                    D = Vector3.Normalize(D);
                    A = Shape.Clamp(A, 1.5f * H);
                }
                AgentPos[k] = A; AgentDir[k] = D;
                _depositVox[k] = Vox(A);
            }
            // deposit AFTER every agent has sensed (physarum.py np.add.at after the vectorised move): an agent
            // must not smell the trail its neighbours laid this same step, or the field over-concentrates
            for (int k = 0; k < AgentCount; k++)
            {
                int o = AgentOwner[k];
                if (o < 0 || !HeartAlive[o]) continue;
                T[_depositVox[k]] += P.Deposit;
            }
        }

        void DiffuseEvaporate()
        {
            // separable 3-tap box blur, edges replicated (physarum.py _blur), mixed by Diffuse
            Blur(T, _tmp);
            float d = P.Diffuse, keep = 1f - P.Evaporate, ema = P.Ema;
            for (int v = 0; v < VoxelCount; v++)
            {
                float t = ((1f - d) * T[v] + d * _tmp[v]) * keep;
                if (!Inside[v] || t < 0f) t = 0f;
                T[v] = t;
                S[v] += (t - S[v]) * ema;
            }
        }

        void Blur(float[] src, float[] dst)
        {
            // three passes through dst / a scratch view: x, then y, then z
            int nyz = NY * NZ;
            float[] a = src, b = dst;
            // pass x: b = blur_x(a)
            for (int ix = 0; ix < NX; ix++)
            {
                int xm = Math.Max(ix - 1, 0) * nyz, x0 = ix * nyz, xp = Math.Min(ix + 1, NX - 1) * nyz;
                for (int r = 0; r < nyz; r++) b[x0 + r] = (a[xm + r] + a[x0 + r] + a[xp + r]) * (1f / 3f);
            }
            // pass y: a' = blur_y(b) into _scratch
            EnsureScratch();
            float[] c = _scratch;
            for (int ix = 0; ix < NX; ix++)
            for (int iy = 0; iy < NY; iy++)
            {
                int ym = (ix * NY + Math.Max(iy - 1, 0)) * NZ, y0 = (ix * NY + iy) * NZ, yp = (ix * NY + Math.Min(iy + 1, NY - 1)) * NZ;
                for (int iz = 0; iz < NZ; iz++) c[y0 + iz] = (b[ym + iz] + b[y0 + iz] + b[yp + iz]) * (1f / 3f);
            }
            // pass z: dst = blur_z(c)
            for (int row = 0; row < NX * NY; row++)
            {
                int o = row * NZ;
                for (int iz = 0; iz < NZ; iz++)
                {
                    int zm = Math.Max(iz - 1, 0), zp = Math.Min(iz + 1, NZ - 1);
                    b[o + iz] = (c[o + zm] + c[o + iz] + c[o + zp]) * (1f / 3f);
                }
            }
        }

        float[] _scratch;
        void EnsureScratch() { if (_scratch == null || _scratch.Length != VoxelCount) _scratch = new float[VoxelCount]; }

        readonly List<int> _cand = new List<int>(1024);

        void Tubes(bool warm)
        {
            if (LiveHearts == 0) return;
            // resorb first (it funds growth), then lay where the trail is strong, strongest first
            for (int v = 0; v < VoxelCount; v++)
            {
                if (!Tube[v] || S[v] >= P.Off) continue;
                Tube[v] = false; TubeCount--;
                if (Danger[v]) { Danger[v] = false; }
                E[v] = 0;
                Reserve += P.TubeVolume;
                Events.Add(new PhysarumEvent { Kind = PhysarumEventKind.Resorb, Voxel = v });
            }
            _cand.Clear();
            for (int v = 0; v < VoxelCount; v++)
                if (!Tube[v] && Inside[v] && S[v] > P.On) _cand.Add(v);
            if (_cand.Count == 0) return;
            _cand.Sort((x, y) => S[y].CompareTo(S[x]));
            int k = (int)Math.Min(_cand.Count, Math.Floor(Reserve / P.TubeVolume));
            if (P.MaxTubes > 0) k = Math.Min(k, P.MaxTubes - TubeCount);
            for (int i = 0; i < k; i++)
            {
                int v = _cand[i];
                Reserve -= P.TubeVolume;
                Tube[v] = true; TubeCount++;
                Events.Add(new PhysarumEvent { Kind = PhysarumEventKind.Lay, Voxel = v });
            }
        }

        void DigestPass(float dt)
        {
            if (LiveHearts == 0) return;
            for (int f = 0; f < FoodCount; f++)
            {
                if (!FoodAlive[f] || !Tube[Vox(FoodPos[f])]) continue;
                float pr = P.Digest * dt / Math.Max(FoodVol[f], 1e-6f);
                if (_rng.NextFloat() >= pr) continue;
                FoodAlive[f] = false; // requested: the caller consumes it and credits CreditDigest
                Events.Add(new PhysarumEvent { Kind = PhysarumEventKind.Digest, Food = f });
            }
        }

        static readonly int[] Nb6X = { 1, -1, 0, 0, 0, 0 };
        static readonly int[] Nb6Y = { 0, 0, 1, -1, 0, 0 };
        static readonly int[] Nb6Z = { 0, 0, 0, 0, 1, -1 };

        void ClimbHearts(float dt)
        {
            if (P.HeartSpeed <= 0f) return;
            for (int k = 0; k < HeartPosition.Count; k++)
            {
                if (!HeartAlive[k]) continue;
                Vector3 hp = HeartPosition[k];
                float best = S[Vox(hp)];
                int bd = -1;
                for (int d = 0; d < 6; d++)
                {
                    Vector3 q = hp + new Vector3(Nb6X[d], Nb6Y[d], Nb6Z[d]) * H;
                    if (!Shape.Contains(q, H)) continue;
                    float s = S[Vox(q)];
                    if (s > best) { best = s; bd = d; }
                }
                if (bd >= 0) HeartPosition[k] = hp + new Vector3(Nb6X[bd], Nb6Y[bd], Nb6Z[bd]) * (P.HeartSpeed * dt);
            }
        }

        void PostHearts()
        {
            for (int k = 0; k < HeartPosition.Count; k++)
            {
                if (!HeartAlive[k] || (HeartPosition[k] - _heartPosted[k]).LengthSquared() < 1e-6f) continue;
                _heartPosted[k] = HeartPosition[k];
                Events.Add(new PhysarumEvent { Kind = PhysarumEventKind.HeartMoved, Heart = k });
            }
        }

        /// <summary>Greenberg-Hastings on tube voxels, advanced one voxel per VoxelSize / WaveSpeed seconds.</summary>
        void Waves(float dt)
        {
            _waveAcc += dt;
            float tick = H / P.WaveSpeed;
            int exMax = P.ExciteTicks, total = P.ExciteTicks + P.RefractoryTicks;
            while (_waveAcc >= tick)
            {
                _waveAcc -= tick;
                for (int v = 0; v < VoxelCount; v++)
                {
                    sbyte e = E[v];
                    _newW[v] = Wave[v];
                    if (!Tube[v]) { _newE[v] = 0; continue; }
                    if (e >= 1) { int ne = e + 1; _newE[v] = (sbyte)(ne > total ? 0 : ne); continue; }
                    // resting tube: excited by any 6-neighbour that is in state 1
                    int iz = v % NZ, iy = (v / NZ) % NY, ix = v / (NY * NZ);
                    int wid = 0;
                    bool nb = false;
                    for (int d = 0; d < 6; d++)
                    {
                        int jx = ix + Nb6X[d], jy = iy + Nb6Y[d], jz = iz + Nb6Z[d];
                        if (jx < 0 || jy < 0 || jz < 0 || jx >= NX || jy >= NY || jz >= NZ) continue;
                        int u = (jx * NY + jy) * NZ + jz;
                        if (E[u] == 1) { nb = true; if (Wave[u] > wid) wid = Wave[u]; }
                    }
                    if (nb) { _newE[v] = 1; _newW[v] = wid; }
                    else _newE[v] = 0;
                }
                // pacemakers: each living heart fires its nearest tube voxel
                for (int k = 0; k < HeartPosition.Count; k++)
                {
                    if (!HeartAlive[k] || Time < NextBeat[k]) continue;
                    NextBeat[k] = Time + P.Period;
                    int v = NearestTube(HeartPosition[k]);
                    if (v >= 0 && _newE[v] == 0)
                    {
                        WaveId++;
                        _newE[v] = 1; _newW[v] = WaveId;
                    }
                }
                Array.Copy(_newE, E, VoxelCount);
                Array.Copy(_newW, Wave, VoxelCount);
                for (int v = 0; v < VoxelCount; v++)
                {
                    bool d = Tube[v] && E[v] >= 1 && E[v] <= exMax;
                    if (d == Danger[v]) continue;
                    Danger[v] = d;
                    Events.Add(new PhysarumEvent { Kind = d ? PhysarumEventKind.DangerOn : PhysarumEventKind.DangerOff, Voxel = v });
                }
            }
            // the sclerotium beat (its own sting, physarum.py beating)
            for (int k = 0; k < HeartPosition.Count; k++)
            {
                bool on = Beating(k), glow = Glowing(k);
                if (on != _beatOnPrev[k])
                {
                    _beatOnPrev[k] = on;
                    Events.Add(new PhysarumEvent { Kind = on ? PhysarumEventKind.BeatOn : PhysarumEventKind.BeatOff, Heart = k });
                }
                if (glow && !_glowPrev[k]) Events.Add(new PhysarumEvent { Kind = PhysarumEventKind.BeatGlow, Heart = k });
                _glowPrev[k] = glow;
            }
        }

        readonly List<int> _tubeList = new List<int>(1024);

        public int NearestTube(Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int v = 0; v < VoxelCount; v++)
            {
                if (!Tube[v]) continue;
                float d = (Centre(v) - p).LengthSquared();
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        /// <summary>Every tube voxel's centre (the research's threat_elements: every tube carries the pulse).</summary>
        public List<int> TubeVoxels()
        {
            _tubeList.Clear();
            for (int v = 0; v < VoxelCount; v++) if (Tube[v]) _tubeList.Add(v);
            return _tubeList;
        }

        /// <summary>
        /// The tube's long axis for the glue: the 13 voxel-neighbour directions, scored by how many tube voxels
        /// sit on BOTH sides of it (a cable runs through), else the strongest trail neighbour.
        /// </summary>
        public Vector3 TubeDirection(int v)
        {
            int iz = v % NZ, iy = (v / NZ) % NY, ix = v / (NY * NZ);
            int bestScore = -1;
            Vector3 best = Vector3.UnitZ;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx < 0 || (dx == 0 && dy < 0) || (dx == 0 && dy == 0 && dz <= 0)) continue; // 13 directions
                int score = (IsTube(ix + dx, iy + dy, iz + dz) ? 1 : 0) + (IsTube(ix - dx, iy - dy, iz - dz) ? 1 : 0);
                float sum = Field(ix + dx, iy + dy, iz + dz) + Field(ix - dx, iy - dy, iz - dz);
                int s2 = score * 100000 + (int)Math.Min(99999f, sum * 100f);
                if (s2 > bestScore) { bestScore = s2; best = Vector3.Normalize(new Vector3(dx, dy, dz)); }
            }
            return best;
        }

        bool IsTube(int x, int y, int z) =>
            x >= 0 && y >= 0 && z >= 0 && x < NX && y < NY && z < NZ && Tube[(x * NY + y) * NZ + z];

        float Field(int x, int y, int z) =>
            x >= 0 && y >= 0 && z >= 0 && x < NX && y < NY && z < NZ ? S[(x * NY + y) * NZ + z] : 0f;
    }
}
