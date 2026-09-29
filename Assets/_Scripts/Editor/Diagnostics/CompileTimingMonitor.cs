using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Measures what an edit COSTS: how long the editor spends compiling, and how long the domain
    /// reload after it takes. This is the number the assembly split is being paid for, so it needs
    /// to be measured the same way before and after (Docs/ASSEMBLY_SPLIT.md § Measuring).
    ///
    /// <para><b>Why a tool rather than a stopwatch.</b> The interesting quantity is the wall clock
    /// from "script saved" to "editor usable again", and a chunk of that — the domain reload — is
    /// exactly the window in which a normal script's state is destroyed. So the start is recorded
    /// in <see cref="SessionState"/> (which survives a reload, unlike a static field) and the end
    /// is read on the far side in <see cref="AssemblyReloadEvents.afterAssemblyReload"/>.</para>
    ///
    /// <para><b>What is recorded.</b> One line per compile-and-reload cycle, appended to
    /// <c>Logs/CompileTiming/compile-timing.csv</c>: timestamp, compile seconds, reload seconds,
    /// total, the number of assemblies Unity rebuilt, and their names. The assembly count is the
    /// half that the split moves — a one-line edit inside Assembly-CSharp rebuilds Assembly-CSharp
    /// no matter how the project is arranged, but an edit in an extracted leaf assembly should
    /// rebuild only that leaf and its dependents.</para>
    ///
    /// <para><b>READER tool</b> per Docs/TOOLING.md: writes only machine-local files under
    /// <c>Logs/CompileTiming/</c> (gitignored), never assets — no change ledger, no ship panel.
    /// Off by default; enable it from FrogletTools ▸ Diagnostics ▸ Compile Timing (a tab of
    /// <see cref="DiagnosticsWindow"/>), take the measurements, and switch it back off.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class CompileTimingMonitor
    {
        const string EnabledKey = "CosmicShore.CompileTiming.Enabled";
        const string CompileStartKey = "CosmicShore.CompileTiming.CompileStartUtc";
        const string CompileEndKey = "CosmicShore.CompileTiming.CompileEndUtc";
        const string ReloadStartKey = "CosmicShore.CompileTiming.ReloadStartUtc";
        const string AssembliesKey = "CosmicShore.CompileTiming.Assemblies";

        const string LogDirectory = "Logs/CompileTiming";
        const string LogFileName = "compile-timing.csv";
        const string CsvHeader =
            "utc,compile_seconds,reload_seconds,total_seconds,assemblies_rebuilt,assembly_names";

        /// <summary>
        /// Whether cycles are being recorded. Persisted per machine in <see cref="EditorPrefs"/>,
        /// so it survives a restart and never travels in the repo.
        /// </summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        /// <summary>Absolute path of the CSV this session appends to.</summary>
        public static string LogPath =>
            Path.Combine(ProjectRoot, LogDirectory, LogFileName).Replace('\\', '/');

        static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static CompileTimingMonitor()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        static void OnCompilationStarted(object _)
        {
            if (!Enabled) return;

            SessionState.SetString(CompileStartKey, NowStamp());
            SessionState.EraseString(CompileEndKey);
            SessionState.EraseString(ReloadStartKey);
            SessionState.SetString(AssembliesKey, string.Empty);
        }

        // One callback per assembly Unity actually rebuilt. Assemblies it skipped never fire, so
        // this list IS the rebuild set — the quantity the split is trying to shrink.
        static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] _)
        {
            if (!Enabled) return;

            var name = Path.GetFileNameWithoutExtension(assemblyPath);
            var seen = SessionState.GetString(AssembliesKey, string.Empty);
            var names = seen.Length == 0
                ? new List<string>()
                : seen.Split('|').ToList();

            if (names.Contains(name)) return;

            names.Add(name);
            SessionState.SetString(AssembliesKey, string.Join("|", names));
        }

        static void OnCompilationFinished(object _)
        {
            if (!Enabled) return;

            SessionState.SetString(CompileEndKey, NowStamp());
        }

        static void OnBeforeAssemblyReload()
        {
            if (!Enabled) return;

            SessionState.SetString(ReloadStartKey, NowStamp());
        }

        // The far side of the domain reload: every static field in the project was just wiped,
        // which is why each timestamp above went through SessionState rather than a field here.
        static void OnAfterAssemblyReload()
        {
            if (!Enabled) return;

            var compileStart = ReadStamp(CompileStartKey);
            var compileEnd = ReadStamp(CompileEndKey);
            var reloadStart = ReadStamp(ReloadStartKey);

            // A reload with no compile in front of it (entering play mode, a manual reload) is not
            // a measurement of an edit and is dropped rather than logged as a zero-compile cycle.
            if (compileStart == null || compileEnd == null || reloadStart == null)
            {
                ClearCycle();
                return;
            }

            var assemblies = SessionState.GetString(AssembliesKey, string.Empty)
                .Split('|')
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            var now = DateTime.UtcNow;
            var compileSeconds = (compileEnd.Value - compileStart.Value).TotalSeconds;
            var reloadSeconds = (now - reloadStart.Value).TotalSeconds;
            var totalSeconds = (now - compileStart.Value).TotalSeconds;

            ClearCycle();
            Append(now, compileSeconds, reloadSeconds, totalSeconds, assemblies);

            Debug.Log(
                $"[CompileTiming] compile {compileSeconds:F2}s + reload {reloadSeconds:F2}s = " +
                $"{totalSeconds:F2}s, {assemblies.Length} assembl{(assemblies.Length == 1 ? "y" : "ies")} rebuilt " +
                $"({string.Join(", ", assemblies)})");
        }

        static void Append(
            DateTime utc, double compileSeconds, double reloadSeconds, double totalSeconds,
            IReadOnlyList<string> assemblies)
        {
            try
            {
                var directory = Path.Combine(ProjectRoot, LogDirectory);
                Directory.CreateDirectory(directory);

                var path = Path.Combine(directory, LogFileName);
                var builder = new StringBuilder();

                if (!File.Exists(path))
                    builder.AppendLine(CsvHeader);

                builder.Append(utc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                    .Append(compileSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(reloadSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(totalSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(assemblies.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(string.Join(" ", assemblies))
                    .AppendLine();

                File.AppendAllText(path, builder.ToString());
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[CompileTiming] could not append to {LogPath}: {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[CompileTiming] could not append to {LogPath}: {e.Message}");
            }
        }

        static void ClearCycle()
        {
            SessionState.EraseString(CompileStartKey);
            SessionState.EraseString(CompileEndKey);
            SessionState.EraseString(ReloadStartKey);
            SessionState.EraseString(AssembliesKey);
        }

        static string NowStamp() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        static DateTime? ReadStamp(string key)
        {
            var raw = SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw)) return null;

            return DateTime.TryParse(
                raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : (DateTime?)null;
        }
    }
}
