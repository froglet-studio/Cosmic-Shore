using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CosmicShore.Player
{
    /// <summary>
    /// Eval mode: fly fixed genomes, many times, and report what each is worth — nothing
    /// evolves. Genomes come from GenomeJson files (what the Training window exports and
    /// what train mode writes). The population is the genomes interleaved, so every match
    /// races them against each other; a genome's number is its mean over all its flights,
    /// with the standard error that says whether two genomes are actually different.
    /// </summary>
    public sealed partial class TrainingHost
    {
        readonly List<string> _evalFiles = new();
        int _evalFlights = 12;
        readonly List<(string name, object genome)> _evalGenomes = new();

        public void ConfigureEval(IEnumerable<string> genomeFiles, int flights)
        {
            _evalFiles.AddRange(genomeFiles);
            _evalFlights = Math.Max(1, flights);
        }

        void InstallEval(object scenario)
        {
            var genomeJson = _game.GetType(Ns + "GenomeJson")!;
            var load = genomeJson.GetMethod("LoadFromFile")!;
            foreach (var file in _evalFiles)
            {
                var g = load.Invoke(null, new object[] { file })
                    ?? throw new InvalidOperationException($"Could not read a genome from {file}.");
                _evalGenomes.Add((Path.GetFileNameWithoutExtension(file), g));
            }
            if (_evalGenomes.Count == 0) throw new InvalidOperationException("eval needs at least one --genome FILE.");

            var genomeType = _game.GetType(Ns + "TrainingGenome")!;
            var clone = genomeType.GetMethod("Clone")!;
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(genomeType))!;
            int total = _evalGenomes.Count * _evalFlights;
            for (int k = 0; k < total; k++)
            {
                var g = clone.Invoke(_evalGenomes[k % _evalGenomes.Count].genome, null)!;
                Set(g, "EvaluationCount", 0);
                Set(g, "Fitness", 0f);
                list.Add(g);
            }
            var population = Get(_state, "Population");
            Set(population, "population", list);
            Set(population, "populationSize", total);
            Set(population, "evaluationsThisGen", 0);
            Set(population, "nextCheckoutIndex", 0);
            Set(population, "inflightCheckouts", 0);
            Set(_state, "ScenarioKey", Get(scenario, "Key")); // or the runner resets the population it was handed
            Console.WriteLine($"[train] evaluating {_evalGenomes.Count} genome(s) × {_evalFlights} flights: {string.Join(", ", _evalGenomes.Select(e => e.name))}");
        }

        void PollEval(int frame)
        {
            var population = Get(_state, "Population");
            int total = _evalGenomes.Count * _evalFlights;
            if ((int)Get(population, "evaluationsThisGen") < total || (int)Get(population, "inflightCheckouts") != 0) return;
            Done = true;
            var genomes = Genomes();
            var sb = new StringBuilder();
            double wall = Seconds - _wallStart;
            sb.AppendLine($"[train] eval done: {total} flights in {wall:0.0}s wall ({frame / 60.0 / Math.Max(wall, 1e-3):0.0}x realtime)");
            sb.AppendLine("[train] genome                         flights    mean     s.e.   crystals/flight");
            var csv = new StringBuilder("genome,flight,fitness\n");
            for (int e = 0; e < _evalGenomes.Count; e++)
            {
                var xs = new List<double>();
                for (int k = e; k < genomes.Count; k += _evalGenomes.Count) xs.Add((float)Get(genomes[k], "Fitness"));
                double mean = xs.Average();
                double se = xs.Count > 1 ? Math.Sqrt(xs.Sum(x => (x - mean) * (x - mean)) / (xs.Count - 1) / xs.Count) : 0;
                // Skim Race fitness is 100 per crystal less the episode clock (and a small score term).
                double crystals = xs.Average(x => Math.Max(0, Math.Floor((x + 140) / 100)));
                sb.AppendLine($"[train] {_evalGenomes[e].name,-30} {xs.Count,7} {mean,8:0.0} {se,8:0.0} {crystals,10:0.00}");
                for (int i = 0; i < xs.Count; i++)
                    csv.Append(_evalGenomes[e].name).Append(',').Append(i).Append(',').Append(xs[i].ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            if (!string.IsNullOrEmpty(_outDir))
            {
                Directory.CreateDirectory(_outDir);
                string path = Path.Combine(_outDir, "eval.csv");
                File.WriteAllText(path, csv.ToString());
                sb.AppendLine($"[train] wrote {path}");
            }
            Summary = sb.ToString();
            Console.Write(Summary);
        }
    }
}
