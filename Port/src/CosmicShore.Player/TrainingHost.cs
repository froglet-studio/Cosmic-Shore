using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Content;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// Runs the game's own AI genetic training (Assets/_Scripts/Utility/AITraining) headless.
    ///
    /// In Unity the editor's Learn button creates a DontDestroyOnLoad TrainingAutoLauncher,
    /// hands it the TrainingControl asset and presses Play; the launcher then drives the real
    /// boot → menu → all-AI match → replay loop. This host is that button: it loads the SAME
    /// control/scenario/state/archive assets through the content runtime, creates the SAME
    /// launcher, and lets the unmodified game code run. The engine tick is the only thing that
    /// differs — fixed 60 Hz virtual time, as fast as the CPU goes.
    ///
    /// Modes:
    ///   train   — continue the session asset's evolution (what Learn does).
    ///   replay  — re-score the generation the session asset already scored in Unity, genome for
    ///             genome, then report the port-vs-Unity fitness disparity. No evolution happens:
    ///             the run stops the moment every genome has one port score.
    ///
    /// The launcher's three editor-only lookups (UnityEditor.AssetDatabase.FindAssets) have no
    /// player-build equivalent, so the host fills those fields before the launcher's Start —
    /// with the exact instances the DI root registered (the content runtime caches every asset
    /// by guid, so the launcher and AppManager share one GameDataSO, as they do in Unity).
    ///
    /// Everything is by reflection: the AITraining types exist only when the port is compiled
    /// against an Assets/ tree that has them (-p:LiveAssetsDir=...).
    /// </summary>
    public sealed class TrainingHost
    {
        public enum Mode { Train, Replay }

        const string ControlPath = "Assets/_SO_Assets/AI Training/TrainingControl.asset";
        const string Ns = "CosmicShore.Utility.AITraining.";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        readonly Mode _mode;
        readonly int _episodes;
        readonly string _outDir;
        readonly string _scenarioName;
        readonly Assembly _game;

        object _control, _state, _archive;
        readonly List<(string label, float unity, int index)> _baseline = new();
        readonly List<float[]> _rounds = new();
        readonly int _repeats;
        int _startEpisodes;
        double _wallStart;
        int _lastReported = -1;

        public bool Done { get; private set; }
        public string Summary { get; private set; }

        public TrainingHost(Mode mode, int episodes, string outDir, string scenarioName, int repeats = 1)
        {
            _mode = mode;
            _repeats = Math.Max(1, repeats);
            _episodes = episodes;
            _outDir = outDir;
            _scenarioName = scenarioName;
            _game = PlayerBoot.GameAssembly;
        }

        static float Seconds => (float)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>After PlayerBoot.Start: load the control asset and create the launcher.</summary>
        public void Install(ContentRuntime runtime)
        {
            var launcherType = _game.GetType(Ns + "TrainingAutoLauncher")
                ?? throw new InvalidOperationException(
                    "This build has no AI training code. Compile the port against an Assets/ tree that has " +
                    "Assets/_Scripts/Utility/AITraining (dotnet build ... -p:LiveAssetsDir=<worktree>/Assets -p:LiveSrcDir=obj/live-src-be) " +
                    "and run it with COSMIC_SHORE_PROJECT=<worktree>.");

            _control = LoadAsset(runtime, ControlPath)
                ?? throw new InvalidOperationException($"{ControlPath} not found in {runtime.Db.ProjectRoot}.");

            if (!string.IsNullOrEmpty(_scenarioName))
            {
                var scenarioPath = runtime.Db.AllAssetPaths.FirstOrDefault(p =>
                    p.Replace('\\', '/').Contains("/AI Training/Scenarios/", StringComparison.Ordinal)
                    && Path.GetFileNameWithoutExtension(p).Equals(_scenarioName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"No scenario asset named '{_scenarioName}' under AI Training/Scenarios.");
                Set(_control, "Scenario", LoadAsset(runtime, runtime.Db.ProjectRelative(scenarioPath)));
            }

            // Operator limits the Learn window writes: the episode cap and a finite watchdog.
            Set(_control, "HumanPlaysThisLaunch", false);
            Set(_control, "Schedule", null);
            _state = Get(_control, "State");
            _archive = Get(_control, "Archive");
            var scenario = Get(_control, "Scenario");
            Console.WriteLine($"[train] scenario {Get(scenario, "Key")} · mode {_mode} · state '{Name(_state)}' · archive '{Name(_archive)}'");

            if (_mode == Mode.Replay) PrepareReplay(scenario);

            _startEpisodes = (int)Get(_state, "EpisodesCompleted");
            Set(_control, "TargetEpisodes", -1); // the host decides when to stop; -1 keeps the runner going

            var go = new GameObject("[Training AutoLauncher]");
            go.SetActive(false);
            var launcher = go.AddComponent(launcherType);
            Set(launcher, "Control", _control);
            Set(launcher, "_gameData", FindFirstAsset(runtime, "GameDataSO"));
            Set(launcher, "_cellData", FindFirstAsset(runtime, "CellRuntimeDataSO"));
            Set(launcher, "_appState", FindFirstAsset(runtime, "ApplicationStateDataVariable"));
            go.SetActive(true);
            _wallStart = Seconds;
        }

        /// <summary>
        /// Rewinds the scored generation so every genome is served again, keeping Unity's scores
        /// aside as the baseline. Nothing about the genomes themselves changes.
        /// </summary>
        void PrepareReplay(object scenario)
        {
            var population = Get(_state, "Population");
            var genomes = ((System.Collections.IEnumerable)Get(population, "population")).Cast<object>().ToList();
            if (genomes.Count == 0) throw new InvalidOperationException("Replay needs a session asset that already scored a generation in Unity.");
            if ((string)Get(_state, "ScenarioKey") != (string)Get(scenario, "Key"))
                throw new InvalidOperationException($"The session asset holds '{Get(_state, "ScenarioKey")}', not '{Get(scenario, "Key")}'.");
            for (int i = 0; i < genomes.Count; i++)
            {
                var g = genomes[i];
                if ((int)Get(g, "EvaluationCount") <= 0) continue;
                _baseline.Add(($"g{Get(g, "GenerationBorn")}#{i}", (float)Get(g, "Fitness"), i));
            }
            if (_baseline.Count != genomes.Count)
                throw new InvalidOperationException($"Replay needs a fully scored generation ({_baseline.Count}/{genomes.Count} genomes have a Unity score).");
            Rewind(population, genomes);
            Console.WriteLine($"[train] replaying {_baseline.Count} Unity-scored genomes (generation {Get(population, "generation")}) × {_repeats} round(s)");
        }

        /// <summary>Serves the same generation again: scores cleared, cursors at the start, nothing evolved.</summary>
        static void Rewind(object population, List<object> genomes)
        {
            foreach (var g in genomes)
            {
                Set(g, "EvaluationCount", 0);
                Set(g, "Fitness", 0f);
            }
            Set(population, "evaluationsThisGen", 0);
            Set(population, "nextCheckoutIndex", 0);
            Set(population, "inflightCheckouts", 0);
        }

        /// <summary>Once per frame: stop when the run has what it came for.</summary>
        public void Poll(int frame)
        {
            if (Done || _state == null) return;
            int completed = (int)Get(_state, "EpisodesCompleted") - _startEpisodes;
            if (completed != _lastReported)
            {
                _lastReported = completed;
                if (completed > 0)
                    Console.WriteLine($"[train] frame {frame}: {completed} episodes · best {Get(_state, "HallOfFameBestFitness"):0.##} · " +
                                      $"gen {Get(Get(_state, "Population"), "generation")} · {(Seconds - _wallStart):0.0}s wall");
            }

            if (_mode == Mode.Replay)
            {
                // A round is complete once every genome reported and nothing is checked out. The
                // runner's next Checkout would EVOLVE here, so the rewind must land first — it
                // does: the next checkout waits for the replay scene and the inter-episode delay.
                var population = Get(_state, "Population");
                if ((int)Get(population, "evaluationsThisGen") >= _baseline.Count && (int)Get(population, "inflightCheckouts") == 0)
                {
                    var genomes = ((System.Collections.IEnumerable)Get(population, "population")).Cast<object>().ToList();
                    _rounds.Add(_baseline.Select(b => (float)Get(genomes[b.index], "Fitness")).ToArray());
                    if (_rounds.Count >= _repeats) Finish(frame);
                    else
                    {
                        Console.WriteLine($"[train] round {_rounds.Count}/{_repeats} scored · rewinding the generation");
                        Rewind(population, genomes);
                    }
                }
            }
            else if (_episodes > 0 && completed >= _episodes) Finish(frame);
        }

        void Finish(int frame)
        {
            Done = true;
            double wall = Seconds - _wallStart;
            var sb = new StringBuilder();
            int episodes = (int)Get(_state, "EpisodesCompleted") - _startEpisodes;
            double sim = frame / 60.0;
            sb.AppendLine($"[train] done: {episodes} evaluations in {wall:0.0}s wall ({sim:0.0}s simulated, {sim / Math.Max(wall, 1e-3):0.0}x realtime)");

            if (_mode == Mode.Replay) sb.Append(ReplayReport());
            else sb.AppendLine($"[train] hall of fame {Get(_state, "HallOfFameBestFitness"):0.##} · generation {Get(Get(_state, "Population"), "generation")}");

            if (!string.IsNullOrEmpty(_outDir)) Export(sb);
            Summary = sb.ToString();
            Console.Write(Summary);
        }

        string ReplayReport()
        {
            var u = _baseline.Select(b => (double)b.unity).ToArray();
            var p = Enumerable.Range(0, _baseline.Count).Select(i => _rounds.Average(r => (double)r[i])).ToArray();
            var sb = new StringBuilder();
            sb.Append("[train] genome      unity");
            for (int r = 0; r < _rounds.Count; r++) sb.Append($"  port r{r + 1}");
            if (_rounds.Count > 1) sb.Append("  port mean");
            sb.AppendLine();
            for (int i = 0; i < _baseline.Count; i++)
            {
                sb.Append($"[train] {_baseline[i].label,-8} {u[i],9:0.00}");
                foreach (var r in _rounds) sb.Append($" {r[i],9:0.00}");
                if (_rounds.Count > 1) sb.Append($" {p[i],10:0.00}");
                sb.AppendLine();
            }
            sb.AppendLine($"[train] port vs unity: mean |delta| {u.Zip(p, (a, b) => Math.Abs(a - b)).Average():0.00} · " +
                          $"unity mean {u.Average():0.00} · port mean {p.Average():0.00} · rank correlation {Spearman(u, p):0.000} · " +
                          $"best genome unity #{ArgMax(u)} / port #{ArgMax(p)}");
            // The noise floor: two port rounds of the same genomes. Port-vs-Unity can be no
            // closer than port-vs-port, so this is the number the disparity is read against.
            if (_rounds.Count > 1)
            {
                var pairs = new List<double>();
                var maes = new List<double>();
                for (int a = 0; a < _rounds.Count; a++)
                    for (int b = a + 1; b < _rounds.Count; b++)
                    {
                        var ra = _rounds[a].Select(x => (double)x).ToArray();
                        var rb = _rounds[b].Select(x => (double)x).ToArray();
                        pairs.Add(Spearman(ra, rb));
                        maes.Add(ra.Zip(rb, (x, y) => Math.Abs(x - y)).Average());
                    }
                var single = Enumerable.Range(0, _rounds.Count).Select(r => Spearman(u, _rounds[r].Select(x => (double)x).ToArray())).Average();
                sb.AppendLine($"[train] noise floor (port round vs port round): mean |delta| {maes.Average():0.00} · rank correlation {pairs.Average():0.000}");
                sb.AppendLine($"[train] single round vs unity: rank correlation {single:0.000} (compare with the floor above)");
            }
            return sb.ToString();
        }

        static int ArgMax(double[] v) { int b = 0; for (int i = 1; i < v.Length; i++) if (v[i] > v[b]) b = i; return b; }

        static double Spearman(double[] a, double[] b)
        {
            double[] ra = Ranks(a), rb = Ranks(b);
            double ma = ra.Average(), mb = rb.Average(), num = 0, da = 0, db = 0;
            for (int i = 0; i < a.Length; i++) { num += (ra[i] - ma) * (rb[i] - mb); da += (ra[i] - ma) * (ra[i] - ma); db += (rb[i] - mb) * (rb[i] - mb); }
            return da <= 0 || db <= 0 ? 0 : num / Math.Sqrt(da * db);
        }

        static double[] Ranks(double[] v)
        {
            var idx = Enumerable.Range(0, v.Length).OrderBy(i => v[i]).ToArray();
            var r = new double[v.Length];
            for (int k = 0; k < idx.Length;)
            {
                int j = k; while (j + 1 < idx.Length && v[idx[j + 1]] == v[idx[k]]) j++;
                for (int t = k; t <= j; t++) r[idx[t]] = (k + j) / 2.0;
                k = j + 1;
            }
            return r;
        }

        /// <summary>
        /// Writes what Unity can take back: the hall-of-fame genome through the game's own
        /// GenomeJson (the file the Training window's "Import JSON" reads), plus the session
        /// state and the archive as JsonUtility JSON.
        /// </summary>
        void Export(StringBuilder sb)
        {
            Directory.CreateDirectory(_outDir);
            var scenario = Get(_control, "Scenario");
            string key = (string)Get(scenario, "Key");
            var genomeJson = _game.GetType(Ns + "GenomeJson");
            var best = Get(_state, "HallOfFameBest");
            if (best != null && genomeJson != null)
            {
                string path = Path.Combine(_outDir, key + ".json");
                genomeJson.GetMethod("SaveToFile")!.Invoke(null, new[] { best, path });
                sb.AppendLine($"[train] wrote {path}");
            }
            if (_mode == Mode.Replay)
            {
                var rows = new StringBuilder("{\n  \"scenario\": \"" + key + "\",\n  \"genomes\": [\n");
                for (int i = 0; i < _baseline.Count; i++)
                {
                    rows.Append("    { \"label\": \"").Append(_baseline[i].label).Append("\", \"unity\": ")
                        .Append(_baseline[i].unity.ToString("R", CultureInfo.InvariantCulture)).Append(", \"port\": [")
                        .Append(string.Join(", ", _rounds.Select(r => r[i].ToString("R", CultureInfo.InvariantCulture))))
                        .Append("] }").Append(i + 1 < _baseline.Count ? ",\n" : "\n");
                }
                rows.Append("  ]\n}\n");
                string replayPath = Path.Combine(_outDir, key + ".replay.json");
                File.WriteAllText(replayPath, rows.ToString());
                sb.AppendLine($"[train] wrote {replayPath}");
            }
            string statePath = Path.Combine(_outDir, key + ".state.json");
            File.WriteAllText(statePath, JsonUtility.ToJson(_state, true));
            sb.AppendLine($"[train] wrote {statePath}");
            if (_archive != null)
            {
                string archivePath = Path.Combine(_outDir, key + ".archive.json");
                File.WriteAllText(archivePath, JsonUtility.ToJson(_archive, true));
                sb.AppendLine($"[train] wrote {archivePath}");
            }
        }

        // ── Assets ─────────────────────────────────────
        static object LoadAsset(ContentRuntime runtime, string projectPath)
        {
            string guid = runtime.Db.GuidOf(projectPath);
            if (guid == null) return null;
            return runtime.Assets.Load(new ObjRef(11400000, guid, 2), typeof(ScriptableObject));
        }

        /// <summary>
        /// UnityEditor.AssetDatabase.FindAssets("t:T")[0] over the project's .asset files: the
        /// first asset (by path) whose script is T. Reads only the head of each file.
        /// </summary>
        static object FindFirstAsset(ContentRuntime runtime, string typeName)
        {
            string script = runtime.Db.AllAssetPaths.FirstOrDefault(p => p.Replace('\\', '/').EndsWith("/" + typeName + ".cs", StringComparison.Ordinal));
            if (script == null) return null;
            string scriptGuid = runtime.Db.GuidOf(runtime.Db.ProjectRelative(script));
            string needle = "guid: " + scriptGuid;
            foreach (var path in runtime.Db.AllAssetPaths.Where(p => p.EndsWith(".asset", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
            {
                using var reader = new StreamReader(path);
                var head = new char[4096];
                int n = reader.Read(head, 0, head.Length);
                if (new string(head, 0, n).Contains(needle, StringComparison.Ordinal))
                    return LoadAsset(runtime, runtime.Db.ProjectRelative(path));
            }
            return null;
        }

        static string Name(object o) => o is CosmicShore.Engine.Object u ? u.name : "null";

        // ── Reflection ─────────────────────────────────
        static MemberInfo Member(Type t, string name)
        {
            for (var k = t; k != null; k = k.BaseType)
            {
                var f = k.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                var p = k.GetProperty(name, Any | BindingFlags.DeclaredOnly);
                if (p != null) return p;
            }
            throw new MissingMemberException(t.FullName, name);
        }

        static object Get(object o, string name)
        {
            if (o == null) return null;
            return Member(o.GetType(), name) switch
            {
                FieldInfo f => f.GetValue(o),
                PropertyInfo p => p.GetValue(o),
                _ => null,
            };
        }

        static void Set(object o, string name, object value)
        {
            switch (Member(o.GetType(), name))
            {
                case FieldInfo f: f.SetValue(o, value); break;
                case PropertyInfo p: p.SetValue(o, value); break;
            }
        }
    }
}
