// The evidence model (Docs/EVOLUTION.md §6): the SHIPPED fauna rules - feeds-counted births with a cooldown and a
// cap, a stomach that drains at Capacity / starvationSeconds and refills on a feed, predators that take one prey per
// hunt tick, a seeder that tops a species back up to its floor - run as a WELL-MIXED cell with no geometry, over the
// shipped genome core (LifeformGenome / GenomeExpression / GenomeMutation, compiled from Assets/_Scripts). Every
// authored number is read from the Blob cell's assets by run.sh and passed in as Params; every modelling assumption
// is a named Params field with its provenance in the comment beside it.
//
// It is NOT the game: there is no space, so "reach" is an encounter-rate multiplier rather than a radius, the
// predator picks prey uniformly rather than by distance, and the flora is a pool that regrows rather than plants
// that grow. What it IS: the economy the genome is selected by, written once here and ported EXACTLY to JavaScript
// (Tools/Evolution/sim.js) - the parity gate holds the two to the same trajectory, draw for draw, so the lab page
// flies the model this harness measured. Everything below is deterministic from the seed: doubles everywhere except
// where the core stores floats, index-order loops, stable compaction, one draw order.
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;

namespace EvolutionHarness
{
    /// <summary>Planted defects (Program.cs negative controls: a gate that cannot bite is a fake pass).</summary>
    public enum ArenaBug { None = 0, IgnoreHerbivoreCap = 1, FreeUpkeep = 2 }

    public sealed class ArenaParams
    {
        // ---- the shipped species (Blob Tadpole Fauna Config Data / TadPoleFauna.prefab / Blob Shark Fauna Config Data / MassSharkFauna.prefab) ----
        public int HerbFeedsPerOffspring = 20;      // FaunaConfigurationSO.FeedsPerOffspring (Blob Tadpole)
        public double HerbCooldown = 10;            // ReproductionCooldownSeconds (Blob Tadpole)
        public int HerbCap = 6;                     // MaxLivePopulation (Blob Tadpole) - scaled by PopulationScale
        public int HerbFloor = 4;                   // PopulationSize, the seed floor (Blob Tadpole) - scaled
        public double HerbStarvation = 90;          // starvationSeconds (TadPoleFauna.prefab / Variant.StarvationSeconds)
        public int PredFeedsPerOffspring = 6;       // FeedsPerOffspring (Blob Shark)
        public double PredCooldown = 30;            // ReproductionCooldownSeconds (Blob Shark)
        public int PredCap = 2;                     // MaxLivePopulation (Blob Shark) - scaled by PredatorScale
        public int PredFloor = 1;                   // PopulationSize (Blob Shark) - scaled
        public double PredStarvation = 45;          // starvationSeconds (MassSharkFauna.prefab)
        public double HuntInterval = 20;            // LightFaunaDataSO.huntIntervalSeconds (script default; the shark's asset carries none)
        public double PredationImmunity = 6;        // Fauna.predationImmunitySeconds
        public double StomachCapacity = 16;         // CellPhaseThresholds.NominalPrismVolume (stomachCapacity <= 0 on every shipped asset)
        public double SpawnWave = 15;               // SpawnProfileSO.BaseFaunaSpawnTime (Blob Cell Spawn Profile)
        public bool ConservedStomach = false;       // CellConfigDataSO.ConservedFaunaStomach (off everywhere but the Swarm cell)

        // ---- the shipped cell ----
        public double FoodCap = 750;                // SpawnProfileSO.FloraSpawnVolumeCeiling 12000 / nominal prism volume 16

        // ---- modelling assumptions (named so the page can show them as such) ----
        public double FloraGrowth = 20;             // prisms/s the flora add below the ceiling (Tools/ecosim FLORA_GROWTH_PER_S)
        public double GrazeRate = 1.15;             // prisms/s one authored herbivore removes at full food (Tools/ecosim GRAZE_PER_HERBIVORE_S)
        public double HalfSaturation = 150;         // food (prisms) at which the graze rate is half its full-food value
        public double PopulationScale = 1;          // SpawnProfileSO.FaunaPopulationScale: cap and floor x this (the lab's population lever)
        public double PredatorScale = 1;            // the same lever for the predator (a separate dial in the lab)
        public double Dt = 0.5;                     // seconds per step (the tadpole's behaviour tick is 1.5 s; half of it keeps rate x dt small)
        public bool Seeder = true;                  // the periodic top-up to the floor (off = a closed population)
        public bool InertPhenotype = false;         // the SELECTION-OFF control: genomes inherit and mutate but express nothing
        public ArenaBug Bug = ArenaBug.None;        // planted defects for the harness's negative controls - never set by a run

        public EvolutionSettings Evolution = EvolutionSettings.Default(true);

        public ArenaParams Clone()
        {
            var c = (ArenaParams)MemberwiseClone();
            c.Evolution = Evolution.Clone();
            return c;
        }
    }

    /// <summary>One census row of the arena (what the golden and the results carry).</summary>
    public struct ArenaRow
    {
        public double T;
        public int Herbivores, Predators;
        public double Food;
        public double[] Mean, Sd;         // per locus, over living herbivores
        public double MeanGeneration;
        public int MaxGeneration;
        public long Births, Founders, Starved, Eaten, PredBirths, PredStarved; // since the previous row
        public long Draws;                // RNG draws so far (the parity gate's cheapest check)
    }

    sealed class CountingRng : IGenomeRng
    {
        readonly GenomeRng _rng;
        public long Draws;
        public CountingRng(uint seed) => _rng = new GenomeRng(seed);
        public double NextUnit() { Draws++; return _rng.NextUnit(); }
    }

    public sealed class EvolutionArena
    {
        public readonly ArenaParams P;
        readonly CountingRng _rng;
        readonly int _herbCap, _herbFloor, _predCap, _predFloor;

        // Herbivores - struct of arrays, compacted stably at the end of each step.
        readonly List<double> hStomach = new List<double>();
        readonly List<int> hFeeds = new List<int>();
        readonly List<double> hLastBirth = new List<double>();
        readonly List<double> hBorn = new List<double>();
        readonly List<int> hGeneration = new List<int>();
        readonly List<LifeformGenome> hGenome = new List<LifeformGenome>();
        readonly List<LifeformPhenotype> hPhenotype = new List<LifeformPhenotype>();
        readonly List<bool> hAlive = new List<bool>();

        // Predators - no genome in this model (Docs/EVOLUTION.md §8).
        readonly List<double> pStomach = new List<double>();
        readonly List<int> pFeeds = new List<int>();
        readonly List<double> pLastBirth = new List<double>();
        readonly List<double> pNextHunt = new List<double>();
        readonly List<bool> pAlive = new List<bool>();

        public double Food;
        public int Step;
        public double T => Step * P.Dt;
        double _nextWave;

        long cBirths, cFounders, cStarved, cEaten, cPredBirths, cPredStarved;
        public long TotalBirths, TotalStarved, TotalEaten, TotalFounders;
        int _maxGeneration;

        public int Herbivores => hStomach.Count;
        public int Predators => pStomach.Count;
        public long Draws => _rng.Draws;

        public EvolutionArena(ArenaParams p, uint seed)
        {
            P = p;
            _rng = new CountingRng(seed);
            _herbCap = ScaleCount(p.HerbCap, p.PopulationScale);
            _herbFloor = ScaleCount(p.HerbFloor, p.PopulationScale);
            _predCap = ScaleCount(p.PredCap, p.PredatorScale);
            _predFloor = ScaleCount(p.PredFloor, p.PredatorScale);
            Food = p.FoodCap;
            _nextWave = 0;
            // The first wave seeds the floors at t = 0 (the spawner's bootstrap).
            SeedToFloor();
        }

        /// <summary>SpawnProfileSO.ScaleFaunaPopulation: round half away from zero, never below 1 for a positive count.</summary>
        static int ScaleCount(int authored, double scale)
        {
            if (authored <= 0) return 0;
            double v = Math.Round(authored * scale, MidpointRounding.AwayFromZero);
            return v < 1 ? 1 : (int)v;
        }

        LifeformPhenotype Express(in LifeformGenome g) =>
            P.InertPhenotype ? LifeformPhenotype.Neutral : GenomeExpression.Express(g, P.Evolution);

        void SpawnHerbivore(in LifeformGenome g, double stomach, int generation)
        {
            hStomach.Add(stomach);
            hFeeds.Add(0);
            hLastBirth.Add(double.NegativeInfinity);
            hBorn.Add(T);
            hGeneration.Add(generation);
            hGenome.Add(g);
            hPhenotype.Add(Express(g));
            hAlive.Add(true);
            if (generation > _maxGeneration) _maxGeneration = generation;
        }

        void SpawnPredator()
        {
            pStomach.Add(P.StomachCapacity);
            pFeeds.Add(0);
            pLastBirth.Add(double.NegativeInfinity);
            pNextHunt.Add(T + P.HuntInterval);
            pAlive.Add(true);
        }

        /// <summary>The seeder: tops each species back up to its floor (FaunaReproductionRules.SeedSpawnCount).</summary>
        void SeedToFloor()
        {
            int herbDeficit = _herbFloor - hStomach.Count;
            if (_herbCap > 0) herbDeficit = Math.Min(herbDeficit, _herbCap - hStomach.Count);
            for (int i = 0; i < herbDeficit; i++)
            {
                var g = GenomeMutation.Founder(P.Evolution, _rng);
                SpawnHerbivore(g, P.StomachCapacity, 0);
                cFounders++; TotalFounders++;
            }
            int predDeficit = _predFloor - pStomach.Count;
            if (_predCap > 0) predDeficit = Math.Min(predDeficit, _predCap - pStomach.Count);
            for (int i = 0; i < predDeficit; i++) SpawnPredator();
        }

        public void Advance(int steps)
        {
            for (int i = 0; i < steps; i++) StepOnce();
        }

        public void StepOnce()
        {
            double dt = P.Dt, t = T;

            // (1) The seeder's wave clock.
            if (P.Seeder && t >= _nextWave)
            {
                _nextWave += P.SpawnWave;
                SeedToFloor();
            }

            // (2) The flora regrow toward the ceiling (logistic: production slows as the cell fills).
            if (P.FoodCap > 0)
            {
                double room = 1.0 - Food / P.FoodCap;
                if (room > 0) Food += P.FloraGrowth * dt * room;
                if (Food > P.FoodCap) Food = P.FoodCap;
            }

            // (3) Herbivores: graze, maybe give birth, pay upkeep, maybe starve. Children are appended at the END.
            int nh = hStomach.Count;
            var children = new List<(LifeformGenome g, double stomach, int gen)>();
            double sat = Food > 0 ? Food / (Food + P.HalfSaturation) : 0.0;
            double drainPerSecond = P.HerbStarvation > 0 ? P.StomachCapacity / P.HerbStarvation : 0.0;
            for (int i = 0; i < nh; i++)
            {
                if (!hAlive[i]) continue;
                var ph = hPhenotype[i];

                // Encounter: pace sweeps more ground, reach senses a wider tube of it (area), food density saturates.
                double rate = P.GrazeRate * ph.Pace * ph.Reach * ph.Reach * sat;
                double pGraze = rate * dt;
                if (pGraze > 1.0) pGraze = 1.0;
                if (Food >= 1.0 && _rng.NextUnit() < pGraze)
                {
                    Food -= 1.0;
                    // The shipped refill rule, or the conserved one (Docs/ECOLOGY_LOD.md §2).
                    if (P.ConservedStomach)
                    {
                        double s = hStomach[i] + P.StomachCapacity;
                        hStomach[i] = s > P.StomachCapacity ? P.StomachCapacity : s;
                    }
                    else hStomach[i] = P.StomachCapacity;
                    hFeeds[i] = hFeeds[i] + 1;

                    // Fauna.TryReproduce: feeds against the phenotype's threshold, the cooldown, the CELL's cap.
                    int fpo = GenomeExpression.FeedsPerOffspring(P.HerbFeedsPerOffspring, ph);
                    bool capOk = _herbCap <= 0 || (nh + children.Count) < _herbCap || P.Bug == ArenaBug.IgnoreHerbivoreCap;
                    if (fpo > 0 && hFeeds[i] >= fpo && (t - hLastBirth[i]) >= P.HerbCooldown && capOk)
                    {
                        hLastBirth[i] = t;
                        hFeeds[i] = 0;
                        var childGenome = GenomeMutation.Mutate(hGenome[i], P.Evolution, _rng);
                        children.Add((childGenome, P.StomachCapacity * ph.Provision, hGeneration[i] + 1));
                    }
                }

                // Upkeep: the stomach drains at Capacity / starvationSeconds, times the phenotype's upkeep.
                if (drainPerSecond > 0 && P.Bug != ArenaBug.FreeUpkeep)
                {
                    hStomach[i] = hStomach[i] - drainPerSecond * ph.Upkeep * dt;
                    if (hStomach[i] <= 0)
                    {
                        hAlive[i] = false;
                        cStarved++; TotalStarved++;
                    }
                }
            }

            // (4) Predators: one hunt per interval, prey picked uniformly among the step's herbivores.
            int np = pStomach.Count;
            int predChildren = 0;
            double predDrain = P.PredStarvation > 0 ? P.StomachCapacity / P.PredStarvation : 0.0;
            for (int j = 0; j < np; j++)
            {
                if (!pAlive[j]) continue;
                if (t >= pNextHunt[j])
                {
                    pNextHunt[j] = pNextHunt[j] + P.HuntInterval;
                    if (nh > 0)
                    {
                        int target = (int)(_rng.NextUnit() * nh);
                        if (target >= nh) target = nh - 1;
                        bool immune = (t - hBorn[target]) < P.PredationImmunity;
                        if (hAlive[target] && !immune)
                        {
                            hAlive[target] = false;
                            cEaten++; TotalEaten++;
                            pStomach[j] = P.StomachCapacity;   // predation is a full refill under both rules
                            pFeeds[j] = pFeeds[j] + 1;
                            bool capOk = _predCap <= 0 || (np + predChildren) < _predCap;
                            if (P.PredFeedsPerOffspring > 0 && pFeeds[j] >= P.PredFeedsPerOffspring
                                && (t - pLastBirth[j]) >= P.PredCooldown && capOk)
                            {
                                pLastBirth[j] = t;
                                pFeeds[j] = 0;
                                predChildren++;
                            }
                        }
                    }
                }
                if (predDrain > 0)
                {
                    pStomach[j] = pStomach[j] - predDrain * dt;
                    if (pStomach[j] <= 0)
                    {
                        pAlive[j] = false;
                        cPredStarved++;
                    }
                }
            }

            // (5) Compaction (stable), then the newborns.
            Compact();
            for (int i = 0; i < children.Count; i++)
            {
                SpawnHerbivore(children[i].g, children[i].stomach, children[i].gen);
                cBirths++; TotalBirths++;
            }
            for (int j = 0; j < predChildren; j++)
            {
                SpawnPredator();
                cPredBirths++;
            }

            Step++;
        }

        void Compact()
        {
            int w = 0;
            for (int i = 0; i < hStomach.Count; i++)
            {
                if (!hAlive[i]) continue;
                if (w != i)
                {
                    hStomach[w] = hStomach[i]; hFeeds[w] = hFeeds[i]; hLastBirth[w] = hLastBirth[i];
                    hBorn[w] = hBorn[i]; hGeneration[w] = hGeneration[i]; hGenome[w] = hGenome[i];
                    hPhenotype[w] = hPhenotype[i]; hAlive[w] = true;
                }
                w++;
            }
            Truncate(hStomach, w); Truncate(hFeeds, w); Truncate(hLastBirth, w); Truncate(hBorn, w);
            Truncate(hGeneration, w); Truncate(hGenome, w); Truncate(hPhenotype, w); Truncate(hAlive, w);

            w = 0;
            for (int j = 0; j < pStomach.Count; j++)
            {
                if (!pAlive[j]) continue;
                if (w != j)
                {
                    pStomach[w] = pStomach[j]; pFeeds[w] = pFeeds[j]; pLastBirth[w] = pLastBirth[j];
                    pNextHunt[w] = pNextHunt[j]; pAlive[w] = true;
                }
                w++;
            }
            Truncate(pStomach, w); Truncate(pFeeds, w); Truncate(pLastBirth, w); Truncate(pNextHunt, w); Truncate(pAlive, w);
        }

        static void Truncate<T>(List<T> list, int count)
        {
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        /// <summary>A census row; resets the since-last-row counters. Two-pass mean / sd in index order (exact in JS).</summary>
        public ArenaRow Census()
        {
            int L = LifeformGenome.LocusCount, n = hStomach.Count;
            var row = new ArenaRow
            {
                T = T, Herbivores = n, Predators = pStomach.Count, Food = Food,
                Mean = new double[L], Sd = new double[L],
                Births = cBirths, Founders = cFounders, Starved = cStarved, Eaten = cEaten,
                PredBirths = cPredBirths, PredStarved = cPredStarved, Draws = _rng.Draws,
                MaxGeneration = _maxGeneration,
            };
            if (n > 0)
            {
                for (int l = 0; l < L; l++)
                {
                    double sum = 0;
                    for (int i = 0; i < n; i++) sum += hGenome[i][l];
                    double mean = sum / n;
                    double ss = 0;
                    for (int i = 0; i < n; i++) { double d = hGenome[i][l] - mean; ss += d * d; }
                    row.Mean[l] = mean;
                    row.Sd[l] = n > 1 ? Math.Sqrt(ss / (n - 1)) : 0;
                }
                double gsum = 0;
                for (int i = 0; i < n; i++) gsum += hGeneration[i];
                row.MeanGeneration = gsum / n;
            }
            cBirths = cFounders = cStarved = cEaten = cPredBirths = cPredStarved = 0;
            return row;
        }

        /// <summary>Every living herbivore's genome (for histograms).</summary>
        public void CopyGenomes(List<LifeformGenome> into)
        {
            for (int i = 0; i < hGenome.Count; i++) into.Add(hGenome[i]);
        }
    }
}
