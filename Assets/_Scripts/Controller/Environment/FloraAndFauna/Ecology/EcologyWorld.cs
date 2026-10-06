// Hierarchical ecology (round 11f, Docs/ECOLOGY_LOD.md): the cell as regions x voxels, the food field both LOD levels
// share, and every tunable number. Port of Tools/Ecology/hierarchy/{params,world}.py (research branch
// cece/eco-hierarchy). Pure C#: compiled and RUN headless by Tools/Build/ecology_lod_harness.
//
// Flora is the WORLD and is never LOD'd: a voxel field of grazeable volume F plus the skeletons K that starving fauna
// leave, over a soil nutrient pool N per region. Only FAUNA change representation. Mass is CLOSED:
//     F + K + N + macro bodies/stomachs (+ migrants in the inbox) + agent bodies/stomachs = constant
// Metabolism moves stomach -> N; flora growth moves N -> F (no flora is minted from nothing); grazing F/K -> stomach;
// predation prey body+stomach -> predator stomach; birth stomach -> offspring body+stomach; starvation body -> K.
// No flow runs on a clock.
using System;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>One fauna species' energetics (research params.Species). Units: world units, seconds, prism VOLUME.</summary>
    public sealed class EcologySpecies
    {
        public string Name = "";
        /// <summary>Body volume: the skeleton it leaves when it starves; what a predator eats.</summary>
        public double Body;
        /// <summary>Stomach an offspring is born with (paid by the parent).</summary>
        public double E0;
        /// <summary>Stomach at which an individual splits off one offspring (costs Body + E0).</summary>
        public double EBirth;
        /// <summary>Satiation scale: intake is scaled by (1 - e / EMax).</summary>
        public double EMax;
        /// <summary>Volume per second burned to the nutrient pool, always (death is e &lt;= 0, never a clock).</summary>
        public double Metab;
        public double Speed, Sprint, SprintMetab;
        /// <summary>Phases are read off the stomach as a fraction of EBirth: sated above Hi, hungry below Lo.</summary>
        public double PhaseLo = 0.3, PhaseHi = 0.8;
        /// <summary>Per-face-neighbour hop rate (1/s) by phase: sated, foraging, hungry (fitted to micro).</summary>
        public double[] HopRate = new double[3];

        /// <summary>0 sated, 1 foraging, 2 hungry.</summary>
        public int PhaseOf(double e)
        {
            double f = e / EBirth;
            return f < PhaseLo ? 2 : f > PhaseHi ? 0 : 1;
        }

        /// <summary>The research herbivore (params.HERB), hop rates from params.Params.hop_rate["herb"].</summary>
        public static EcologySpecies Herbivore() => new()
        {
            Name = "herb", Body = 8.0, E0 = 6.0, EBirth = 20.0, EMax = 32.0, Metab = 0.04,
            Speed = 18.0, Sprint = 32.0, SprintMetab = 0.04, HopRate = new[] { 0.00144, 0.00482, 0.0112 },
        };

        /// <summary>The research predator (params.PRED), hop rates from params.Params.hop_rate["pred"].</summary>
        public static EcologySpecies Predator() => new()
        {
            Name = "pred", Body = 40.0, E0 = 30.0, EBirth = 100.0, EMax = 120.0, Metab = 0.02,
            Speed = 20.0, Sprint = 36.0, SprintMetab = 0.06, HopRate = new[] { 0.0028, 0.0107, 0.0144 },
        };
    }

    /// <summary>A planted bug a gate must catch (research params.Params.bug). Never set outside the harness.</summary>
    public enum EcologyBug
    {
        None = 0,
        AbsorbDropStomach = 1,      // conservation: an absorbed agent loses 1% of its stomach
        BirthFreeBody = 2,          // conservation: a micro birth does not pay the offspring's body
        ExpandLosePool = 3,         // conservation: an expanded cohort loses 5% of its stomach
        AbsorbIgnoresVisibility = 4, // continuity: agents are absorbed in plain sight
        ExpandNoEmerge = 5,         // continuity: expanded agents are drawn at their sampled spot from frame 0
        MacroGrazeX15 = 6,          // consistency: macro grazing 50% too strong
        ExpandMeanField = 7,        // consistency: expansion hands every agent the region's mean stomach
        MacroAttackX2 = 8,          // consistency: macro predation attack rate doubled
    }

    /// <summary>Every tunable number (research params.Params), quoted verbatim; defaults ARE the research's.</summary>
    public sealed class EcologyParams
    {
        // ---- world
        public double R = 1200.0;          // cell (membrane) radius
        public double L = 200.0;           // region edge
        public int VoxPerAxis = 4;         // flora voxels per region axis (50 u voxels)
        // ---- flora (regional prism mass)
        public double FloraR = 0.006;      // logistic growth rate /s
        public double FloraCap = 240.0;    // volume a voxel can carry (calibrated regime: 120; enriched/cycles: 240)
        public double NutrientHalf = 4000.0;
        public double SeedRain = 0.02;     // growth uses F + SeedRain * mean(region flora): no flora from nothing
        public double NutrientDiffuse = 0.002;
        // ---- herbivore grazing (Holling II on voxel flora)
        public double HIntake = 0.25;      // max vol/s
        public double HHalf = 90.0;        // voxel grazeable volume at half intake
        public double OccTheta = 0.5;      // macro occupancy relaxes toward food^theta (fitted: 4.3% vs 18.6% uniform)
        public double OccTau = 1.0;
        // ---- predation (Holling II on region herbivore count; FITTED to micro, calibrate.py)
        public double PAttack = 5.0e-5;    // kills / (predator * herbivore-in-region * s) at low density
        public double PHandle = 357.0;     // s per kill
        public double PHuntBelow = 0.8;    // predators hunt only while stomach < this * EMax
        // ---- micro predation geometry (what PAttack / PHandle are fitted FROM)
        public double PSense = 25.0, PCatch = 4.0, HFlee = 40.0;
        public double SprintKappaHerb = 0.36, SprintKappaPred = 0.019;   // measured sprint time / encounter odds
        // ---- macro movement
        public double FoodBias = 0.3;      // hop weight (neighbour food / own food)^bias
        public double PreyBias = 0.0, FleeBias = 0.0;
        // ---- macro discretisation
        public int Cohorts = 10;
        public double MergeTol = 0.06;     // an arrival merges into a cohort whose mean is within this x EBirth
        public double EDiffuseHerb = 0.0009, EDiffusePred = 0.002;   // stomach diffusion within a cohort (vol^2/s)
        public double DtMacro = 1.0, DtMicro = 0.1;
        // ---- LOD (params.py; DISCOVERIES' prose quotes 400 ahead / 450 collapse from an earlier run - the gates ran on these)
        public double ExpandRadius = 280.0;
        public double ExpandAhead = 450.0;
        public double CollapseRadius = 400.0;
        public double VisibleRadius = 330.0;
        public double NearSeeRadius = 60.0;  // seen regardless of heading
        public int AgentBudget = 4500;
        public double ConeCos = 0.5;
        public int RepsPerRegion = 6;
        public double BloomS = 1.5;
        // ---- negative controls
        public EcologyBug Bug = EcologyBug.None;

        public EcologySpecies[] Species = { EcologySpecies.Herbivore(), EcologySpecies.Predator() };

        public EcologyParams Clone()
        {
            var c = (EcologyParams)MemberwiseClone();
            c.Species = new[] { Species[0], Species[1] };
            return c;
        }
    }

    /// <summary>Regions (cubes of L) inside the membrane, their voxels, and the shared food/soil field.</summary>
    public sealed class EcologyWorld
    {
        public readonly EcologyParams P;
        public readonly double R;
        public readonly int GridN, NReg, NVox;
        public readonly double Origin, VoxH;
        public readonly int[] LinToReg;
        public readonly Vector3[] Centers;    // float is enough for geometry; mass stays double
        public readonly double[] Cx, Cy, Cz;
        /// <summary>Face neighbours [r*6 + k] (-1 = none / outside the membrane), k: +x -x +y -y +z -z.</summary>
        public readonly int[] Nb;
        public readonly double[] VoxOffX, VoxOffY, VoxOffZ;
        /// <summary>1 where the voxel centre lies inside the membrane (only those carry flora).</summary>
        public readonly double[] VoxOk;
        public readonly double[] VoxOkSum;
        /// <summary>Flora, skeleton (per region*voxel) and soil nutrient (per region).</summary>
        public readonly double[] F, K, N;

        public EcologyWorld(EcologyParams p, double? radius = null)
        {
            P = p;
            R = radius ?? p.R;
            double L = p.L;
            GridN = (int)Math.Ceiling(2 * R / L);
            int n = GridN;
            Origin = -n * L / 2;
            LinToReg = new int[n * n * n];
            var cen = new System.Collections.Generic.List<(double, double, double, int, int, int)>();
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) for (int k = 0; k < n; k++)
            {
                double x = (i + 0.5) * L - n * L / 2, y = (j + 0.5) * L - n * L / 2, z = (k + 0.5) * L - n * L / 2;
                int lin = (i * n + j) * n + k;
                if (Math.Sqrt(x * x + y * y + z * z) <= R) { LinToReg[lin] = cen.Count; cen.Add((x, y, z, i, j, k)); }
                else LinToReg[lin] = -1;
            }
            NReg = cen.Count;
            Centers = new Vector3[NReg]; Cx = new double[NReg]; Cy = new double[NReg]; Cz = new double[NReg];
            Nb = new int[NReg * 6];
            for (int r = 0; r < NReg; r++)
            {
                var (x, y, z, i, j, k) = cen[r];
                Cx[r] = x; Cy[r] = y; Cz[r] = z; Centers[r] = new Vector3((float)x, (float)y, (float)z);
                for (int d = 0; d < 6; d++)
                {
                    int di = d == 0 ? 1 : d == 1 ? -1 : 0, dj = d == 2 ? 1 : d == 3 ? -1 : 0, dk = d == 4 ? 1 : d == 5 ? -1 : 0;
                    int a = i + di, b = j + dj, c = k + dk;
                    Nb[r * 6 + d] = a >= 0 && a < n && b >= 0 && b < n && c >= 0 && c < n ? LinToReg[(a * n + b) * n + c] : -1;
                }
            }
            int V = p.VoxPerAxis;
            NVox = V * V * V;
            VoxH = L / V;
            VoxOffX = new double[NVox]; VoxOffY = new double[NVox]; VoxOffZ = new double[NVox];
            for (int a = 0; a < V; a++) for (int b = 0; b < V; b++) for (int c = 0; c < V; c++)
            {
                int v = (a * V + b) * V + c;
                VoxOffX[v] = (a + 0.5) * VoxH - L / 2; VoxOffY[v] = (b + 0.5) * VoxH - L / 2; VoxOffZ[v] = (c + 0.5) * VoxH - L / 2;
            }
            VoxOk = new double[NReg * NVox]; VoxOkSum = new double[NReg];
            for (int r = 0; r < NReg; r++)
                for (int v = 0; v < NVox; v++)
                {
                    double x = Cx[r] + VoxOffX[v], y = Cy[r] + VoxOffY[v], z = Cz[r] + VoxOffZ[v];
                    double ok = Math.Sqrt(x * x + y * y + z * z) <= R ? 1.0 : 0.0;
                    VoxOk[r * NVox + v] = ok; VoxOkSum[r] += ok;
                }
            F = new double[NReg * NVox]; K = new double[NReg * NVox]; N = new double[NReg];
        }

        // ---- geometry

        /// <summary>The region holding a point, or -1 outside every region.</summary>
        public int RegionOf(double x, double y, double z)
        {
            int n = GridN;
            int i = Math.Clamp((int)Math.Floor((x - Origin) / P.L), 0, n - 1);
            int j = Math.Clamp((int)Math.Floor((y - Origin) / P.L), 0, n - 1);
            int k = Math.Clamp((int)Math.Floor((z - Origin) / P.L), 0, n - 1);
            return LinToReg[(i * n + j) * n + k];
        }

        /// <summary>The region holding a point, nudged 2% toward the centre when it falls between the cube grid and the
        /// sphere (research sim._absorb: region_of(pos * 0.98)); never -1 for a point inside the membrane.</summary>
        public int RegionOfInside(double x, double y, double z)
        {
            int r = RegionOf(x, y, z);
            if (r >= 0) return r;
            r = RegionOf(x * 0.98, y * 0.98, z * 0.98);
            if (r >= 0) return r;
            // pathological corner: the nearest centre
            int best = 0; double bd = double.MaxValue;
            for (int q = 0; q < NReg; q++)
            {
                double dx = x - Cx[q], dy = y - Cy[q], dz = z - Cz[q], d = dx * dx + dy * dy + dz * dz;
                if (d < bd) { bd = d; best = q; }
            }
            return best;
        }

        public int VoxelOf(double x, double y, double z, int reg)
        {
            int V = P.VoxPerAxis;
            int a = Math.Clamp((int)Math.Floor((x - Cx[reg] + P.L / 2) / VoxH), 0, V - 1);
            int b = Math.Clamp((int)Math.Floor((y - Cy[reg] + P.L / 2) / VoxH), 0, V - 1);
            int c = Math.Clamp((int)Math.Floor((z - Cz[reg] + P.L / 2) / VoxH), 0, V - 1);
            return (a * V + b) * V + c;
        }

        /// <summary>Plant flora at <paramref name="fill"/> x cap on average, patchy (lognormal sigma
        /// <paramref name="patchiness"/>), plus a nutrient pool per region (research world.seed_flora).</summary>
        public void SeedFlora(EcologyRng rng, double fill, double nutrientPerRegion, double patchiness = 1.0)
        {
            int n = NReg * NVox;
            var w = new double[n];
            double mean = 0;
            for (int i = 0; i < n; i++) { w[i] = Math.Exp(patchiness * rng.Normal()) * VoxOk[i]; mean += w[i]; }
            mean /= n;
            for (int i = 0; i < n; i++) F[i] = Math.Min(fill * P.FloraCap * w[i] / Math.Max(mean, 1e-9), P.FloraCap) * VoxOk[i];
            for (int r = 0; r < NReg; r++) N[r] = nutrientPerRegion;
        }

        /// <summary>Flora + soil, every region at the macro rate (both levels share it). Exactly conservative: growth is
        /// paid from N (scaled down where N cannot cover it) and nutrient diffuses pairwise.</summary>
        public void StepFlora(double dt)
        {
            int nv = NVox;
            for (int r = 0; r < NReg; r++)
            {
                double meanF = 0;
                for (int v = 0; v < nv; v++) meanF += F[r * nv + v];
                meanF /= nv;
                double lim = N[r] / (N[r] + P.NutrientHalf), tot = 0;
                Span<double> want = stackalloc double[nv];
                for (int v = 0; v < nv; v++)
                {
                    int i = r * nv + v;
                    double room = Math.Clamp(1.0 - (F[i] + K[i]) / P.FloraCap, 0.0, 1.0);
                    want[v] = P.FloraR * (F[i] + P.SeedRain * meanF) * room * lim * dt * VoxOk[i];
                    tot += want[v];
                }
                double scale = tot > N[r] ? N[r] / Math.Max(tot, 1e-12) : 1.0, paid = 0;
                for (int v = 0; v < nv; v++) { double g = want[v] * scale; F[r * nv + v] += g; paid += g; }
                N[r] -= paid;
            }
            if (P.NutrientDiffuse > 0)
                for (int k = 0; k <= 4; k += 2)
                {
                    // research: all flows of one axis computed from the same snapshot, then applied
                    Span<double> flow = NReg <= 4096 ? stackalloc double[NReg] : new double[NReg];
                    for (int i = 0; i < NReg; i++)
                    {
                        int j = Nb[i * 6 + k];
                        flow[i] = j >= 0 ? P.NutrientDiffuse * dt * (N[i] - N[j]) : 0.0;
                    }
                    for (int i = 0; i < NReg; i++)
                    {
                        int j = Nb[i * 6 + k];
                        if (j < 0) continue;
                        N[i] -= flow[i]; N[j] += flow[i];
                    }
                }
        }

        public double FloraTotal()
        {
            double s = 0;
            for (int i = 0; i < F.Length; i++) s += F[i] + K[i];
            return s;
        }

        public double FloraIn(int r)
        {
            double s = 0;
            for (int v = 0; v < NVox; v++) s += F[r * NVox + v] + K[r * NVox + v];
            return s;
        }

        /// <summary>F + K + N: the world's share of the closed ledger.</summary>
        public double Mass()
        {
            double s = 0;
            for (int i = 0; i < F.Length; i++) s += F[i] + K[i];
            for (int r = 0; r < NReg; r++) s += N[r];
            return s;
        }
    }
}
