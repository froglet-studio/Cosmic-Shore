using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// Train mode, generation by generation, over N processes.
    ///
    /// Each worker process runs the real game and the real runner, but serves only its own
    /// slice of the population: every genome in the slice is flown <c>K</c> times, and the
    /// per-flight fitness is recovered from the genome's running mean between matches. A
    /// worker then publishes its slice's results and waits for the others'.
    ///
    /// The merge is CANONICAL and every worker does it: restore the population as it stood
    /// at the start of the generation, then feed every result — in genome order, then flight
    /// order — through the game's own TrainingPopulation.ReturnFitness and
    /// TrainingSessionStateSO.RecordEpisode, then run TrainingPopulation.Evolve under a
    /// random state seeded from (run seed, generation). Novelty, the hall of fame and the
    /// next generation are therefore identical in every process without sending a
    /// population anywhere — only fitness numbers cross between processes.
    ///
    /// Nothing about the algorithm changes: the same selection, crossover, mutation and
    /// novelty code evolves the population; what changes is that N matches fly at once and
    /// that a genome can be flown more than once per generation (--evals), which the
    /// running mean ReturnFitness already keeps was built for.
    /// </summary>
    public sealed partial class TrainingHost
    {
        int _workers = 1, _worker, _evals = 1, _generations;
        int _evoSeed;
        string _dir;
        int _rangeStart, _rangeEnd;
        string _genStart;
        int _generationAtStart;
        int[] _lastN = Array.Empty<int>();
        float[] _lastF = Array.Empty<float>();
        List<float>[] _mine = Array.Empty<List<float>>();
        // Genome object → its index at the start of the generation (the index the merge uses).
        // The live list is re-ordered within this worker's slice between passes, so a genome is
        // tracked by identity, never by where it currently sits.
        readonly Dictionary<object, int> _originalIndex = new(ReferenceEqualityComparer.Instance);
        int _pass;
        bool _submitted;
        int _gensDone;
        double _genWallStart;

        /// <summary>Parallel train settings (call before Install).</summary>
        public void ConfigureParallel(int workers, int worker, int evals, int generations, int evoSeed, string dir)
        {
            _workers = Math.Max(1, workers);
            _worker = Math.Clamp(worker, 0, _workers - 1);
            _evals = Math.Max(1, evals);
            _generations = generations;
            _evoSeed = evoSeed;
            _dir = dir;
        }

        string ResultPath(int generation, int worker) => Path.Combine(_dir, $"g{generation:D5}_w{worker}.txt");
        string DonePath => Path.Combine(_dir, "done");

        void InstallTrain(object scenario)
        {
            if (string.IsNullOrEmpty(_dir)) _dir = Path.Combine(_outDir ?? ".", "run");
            Directory.CreateDirectory(_dir);
            _game.GetType(Ns + "PolicyBootstrap")?.GetMethod("EnsureInitialized")?.Invoke(null, null);

            // The runner resets a state that belongs to another scenario at StartSession; do it
            // here instead, under the shared seed, so every worker starts from the same population.
            if ((string)Get(_state, "ScenarioKey") != (string)Get(scenario, "Key")
                || (int)Get(Get(_state, "Population"), "PopulationSize") == 0)
            {
                Seeded(-1, () => _state.GetType().GetMethod("ResetForScenario")!.Invoke(_state, new[] { Get(scenario, "Key"), scenario }));
                Console.WriteLine($"[train] fresh population for {Get(scenario, "Key")}");
            }
            if (Get(_state, "HallOfFameBest") == null)
                Set(_state, "HallOfFameBest", _game.GetType(Ns + "TrainingGenome")!.GetMethod("FromRegistryDefaults")!.Invoke(null, null));

            // A generation already scored in full (Unity's, or an earlier port run) evolves first.
            var population = Get(_state, "Population");
            int count = (int)Get(population, "PopulationSize");
            if ((int)Get(population, "evaluationsThisGen") >= count)
            {
                int scored = (int)Get(population, "generation");
                Evolve();
                Console.WriteLine($"[train] generation {scored} was already scored; evolved to {Get(Get(_state, "Population"), "generation")}");
            }

            int racers = Math.Max(2, (int)Get(scenario, "OpponentCount"));
            int per = (int)Math.Ceiling(count / (double)_workers / racers) * racers;
            _rangeStart = Math.Min(count, _worker * per);
            _rangeEnd = Math.Min(count, _rangeStart + per);
            Console.WriteLine($"[train] worker {_worker + 1}/{_workers}: genomes [{_rangeStart}, {_rangeEnd}) × {_evals} flight(s) per generation · results in {_dir}");
            BeginGeneration();
        }

        /// <summary>Rewinds the cursor onto this worker's slice and records where every genome starts.</summary>
        void BeginGeneration()
        {
            var population = Get(_state, "Population");
            // Far below the population size, so the runner's own Checkout never decides the
            // generation is complete and evolves on its own; the merge decides that.
            Set(population, "evaluationsThisGen", -1_000_000);
            Set(population, "nextCheckoutIndex", _rangeStart);
            Set(population, "inflightCheckouts", 0);
            _genStart = JsonUtility.ToJson(_state);
            _generationAtStart = (int)Get(population, "generation");
            var genomes = Genomes();
            _lastN = genomes.Select(g => (int)Get(g, "EvaluationCount")).ToArray();
            _lastF = genomes.Select(g => (float)Get(g, "Fitness")).ToArray();
            _mine = genomes.Select(_ => new List<float>()).ToArray();
            _originalIndex.Clear();
            for (int i = 0; i < genomes.Count; i++) _originalIndex[genomes[i]] = i;
            _pass = 0;
            ShuffleSlice(); // after the snapshot: the merge replays in the original order
            _submitted = false;
            _genWallStart = Seconds;
        }

        List<object> Genomes() => ((System.Collections.IEnumerable)Get(Get(_state, "Population"), "population")).Cast<object>().ToList();

        void PollTrain(int frame)
        {
            if (File.Exists(DonePath) && _worker != 0) { Done = true; Summary = "[train] done (worker 0 finished the run)\n"; return; }
            var population = Get(_state, "Population");
            if ((int)Get(population, "inflightCheckouts") != 0) return; // mid-match

            Harvest();
            if ((int)Get(population, "nextCheckoutIndex") >= _rangeEnd)
            {
                // A pass over the slice is done. Serve it again in a new order: the runner hands
                // genome i the (i mod racers)-th seat of a match with slice neighbours as
                // opponents, and seats are NOT equal (measured: seat 2 flies ~half the crystals
                // of seats 0 and 1, in Unity and in the port alike), so a fixed order would score
                // the seat, not the genome.
                _pass++;
                ShuffleSlice();
                Set(population, "nextCheckoutIndex", _rangeStart);
            }

            if (!_submitted && Enumerable.Range(_rangeStart, _rangeEnd - _rangeStart).All(i => _mine[i].Count >= _evals))
            {
                var sb = new StringBuilder();
                for (int i = _rangeStart; i < _rangeEnd; i++)
                    foreach (var x in _mine[i].Take(_evals))
                        sb.Append(i).Append(' ').Append(x.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                string path = ResultPath(_generationAtStart, _worker);
                File.WriteAllText(path + ".tmp", sb.ToString());
                File.Move(path + ".tmp", path, overwrite: true);
                _submitted = true;
            }

            if (_submitted && Enumerable.Range(0, _workers).All(w => File.Exists(ResultPath(_generationAtStart, w))))
                MergeAndEvolve(frame);
        }

        /// <summary>
        /// Recovers each flight's fitness from the running mean the runner keeps: after the
        /// n-th flight the mean is F_n = F_{n-1} + (x - F_{n-1}) / n, so x = F_{n-1} + (F_n - F_{n-1}) n.
        /// </summary>
        void Harvest()
        {
            var genomes = Genomes();
            for (int slot = _rangeStart; slot < _rangeEnd && slot < genomes.Count; slot++)
            {
                if (!_originalIndex.TryGetValue(genomes[slot], out int i)) continue;
                int n = (int)Get(genomes[slot], "EvaluationCount");
                if (n <= _lastN[i]) continue;
                float f = (float)Get(genomes[slot], "Fitness");
                int added = n - _lastN[i];
                float perFlight = added == 1
                    ? _lastF[i] + (f - _lastF[i]) * n
                    : (f * n - _lastF[i] * _lastN[i]) / added;
                for (int k = 0; k < added; k++) _mine[i].Add(perFlight);
                _lastN[i] = n;
                _lastF[i] = f;
            }
        }

        /// <summary>Deterministic re-order of this worker's slice in the live list (a rotation plus a shuffle).</summary>
        void ShuffleSlice()
        {
            var list = (System.Collections.IList)Get(Get(_state, "Population"), "population");
            int len = _rangeEnd - _rangeStart;
            if (len < 2) return;
            var rng = new System.Random(unchecked(_evoSeed * 31 + _generationAtStart * 1009 + _worker * 97 + _pass));
            var slice = new object[len];
            for (int j = 0; j < len; j++) slice[j] = list[_rangeStart + j];
            for (int j = len - 1; j > 0; j--)
            {
                int k = rng.Next(j + 1);
                (slice[j], slice[k]) = (slice[k], slice[j]);
            }
            for (int j = 0; j < len; j++) list[_rangeStart + j] = slice[j];
        }

        void MergeAndEvolve(int frame)
        {
            var results = new List<float>[_mine.Length];
            for (int i = 0; i < results.Length; i++) results[i] = new List<float>();
            for (int w = 0; w < _workers; w++)
                foreach (var line in File.ReadAllLines(ResultPath(_generationAtStart, w)))
                {
                    var parts = line.Split(' ');
                    if (parts.Length != 2) continue;
                    int i = int.Parse(parts[0], CultureInfo.InvariantCulture);
                    if (i >= 0 && i < results.Length) results[i].Add(float.Parse(parts[1], CultureInfo.InvariantCulture));
                }

            // Canonical: the generation as it started, then every flight in (genome, flight) order.
            JsonUtility.FromJsonOverwrite(_genStart, _state);
            var population = Get(_state, "Population");
            Set(population, "inflightCheckouts", 0);
            var genomes = Genomes();
            var fitnessType = _game.GetType(Ns + "TrainingFitness")!;
            var returnFitness = population.GetType().GetMethod("ReturnFitness")!;
            var recordEpisode = _state.GetType().GetMethod("RecordEpisode")!;
            int flights = 0;
            double sum = 0; float best = float.NegativeInfinity;
            for (int i = 0; i < genomes.Count; i++)
                foreach (var x in results[i])
                {
                    var fitness = Activator.CreateInstance(fitnessType)!;
                    Set(fitness, "Total", x);
                    returnFitness.Invoke(population, new[] { (object)i, fitness, genomes[i] });
                    recordEpisode.Invoke(_state, new[] { fitness, genomes[i] });
                    flights++; sum += x; best = Math.Max(best, x);
                }

            int gen = _generationAtStart;
            double genWall = Seconds - _genWallStart;
            var means = genomes.Select(g => (float)Get(g, "Fitness")).ToArray();
            Console.WriteLine($"[train] generation {gen}: {flights} flights · mean flight {sum / Math.Max(1, flights):0.0} · best flight {best:0.0} · " +
                              $"best genome mean {means.Max():0.0} · hall of fame {Get(_state, "HallOfFameBestFitness"):0.0} · " +
                              $"{genWall:0.0}s wall · frame {frame}");
            if (_worker == 0)
            {
                AppendProgress(gen, flights, sum / Math.Max(1, flights), best, means.Max(), genWall);
                ExportTrained();
            }

            _gensDone++;
            if ((_generations > 0 && _gensDone >= _generations)
                || (_episodes > 0 && (int)Get(_state, "EpisodesCompleted") - _startEpisodes >= _episodes))
            {
                if (_worker == 0) File.WriteAllText(DonePath, gen.ToString(CultureInfo.InvariantCulture));
                Finish(frame);
                return;
            }
            Evolve();
            BeginGeneration();
        }

        /// <summary>TrainingPopulation.Evolve under a random state every worker shares, then restored.</summary>
        void Evolve()
        {
            var population = Get(_state, "Population");
            Seeded((int)Get(population, "generation"), () => population.GetType().GetMethod("Evolve")!.Invoke(population, null));
            Set(population, "evaluationsThisGen", 0);
            Set(population, "nextCheckoutIndex", 0);
        }

        void Seeded(int generation, Action body)
        {
            var saved = CosmicShore.Engine.Random.state;
            CosmicShore.Engine.Random.InitState(unchecked(_evoSeed * 486187739 + generation * 16777619 + 7));
            try { body(); }
            finally { CosmicShore.Engine.Random.state = saved; }
        }

        void AppendProgress(int gen, int flights, double meanFlight, float bestFlight, float bestMean, double wall)
        {
            if (string.IsNullOrEmpty(_outDir)) return;
            Directory.CreateDirectory(_outDir);
            string key = (string)Get(Get(_control, "Scenario"), "Key");
            string path = Path.Combine(_outDir, key + ".progress.csv");
            if (!File.Exists(path)) File.WriteAllText(path, "generation,flights,mean_flight,best_flight,best_genome_mean,hall_of_fame,generation_wall_s,total_wall_s\n");
            File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture,
                $"{gen},{flights},{meanFlight:0.###},{bestFlight:0.###},{bestMean:0.###},{Get(_state, "HallOfFameBestFitness"):0.###},{wall:0.#},{Seconds - _wallStart:0.#}\n"));
        }

        /// <summary>The deployable result after every generation: the archive entry Unity's AI flies, and its JSON sidecars.</summary>
        void ExportTrained()
        {
            if (string.IsNullOrEmpty(_outDir) || _archive == null) { if (!string.IsNullOrEmpty(_outDir)) Export(new StringBuilder()); return; }
            var scenario = Get(_control, "Scenario");
            var best = Get(_state, "HallOfFameBest");
            if (best != null)
            {
                int trained = (int)(_game.GetType(Ns + "ArchiveDeployment")?.GetField("TrainedIntensity")?.GetValue(null) ?? 4);
                _archive.GetType().GetMethod("Upsert")!.Invoke(_archive, new object[]
                {
                    Get(scenario, "Vessel"), Get(scenario, "GameMode"), trained, best,
                    (float)Get(_state, "HallOfFameBestFitness"), (int)Get(Get(_state, "Population"), "generation"),
                    $"Port headless training: {Get(_state, "EpisodesCompleted")} episodes",
                });
            }
            Export(new StringBuilder());
            ExportRobustBest();
        }

        /// <summary>
        /// The game's hall of fame keeps the best running mean at the moment of a flight, so a
        /// genome's FIRST lucky flight can hold it forever — and in Skim Race one genome's flights
        /// range from 0 to 12 crystals. The robust pick is the best mean among genomes flown at
        /// least <c>max(4, 2K)</c> times (elites accumulate flights across generations).
        /// Written beside the hall-of-fame file as KEY.robust.json (same GenomeJson format).
        /// </summary>
        void ExportRobustBest()
        {
            int minFlights = Math.Max(4, 2 * _evals);
            object best = null; float bestMean = float.NegativeInfinity; int bestN = 0;
            foreach (var g in Genomes())
            {
                int n = (int)Get(g, "EvaluationCount");
                float f = (float)Get(g, "Fitness");
                if (n >= minFlights && f > bestMean) { best = g; bestMean = f; bestN = n; }
            }
            if (best == null) return;
            string key = (string)Get(Get(_control, "Scenario"), "Key");
            string path = Path.Combine(_outDir, key + ".robust.json");
            _game.GetType(Ns + "GenomeJson")!.GetMethod("SaveToFile")!.Invoke(null, new[] { best, path });
            Console.WriteLine($"[train] robust best: mean {bestMean:0.0} over {bestN} flights -> {path}");
        }
    }
}
