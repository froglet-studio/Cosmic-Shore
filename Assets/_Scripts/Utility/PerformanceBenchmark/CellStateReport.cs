#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The DiagnosticsHUD <c>cells [label]</c> console command: a snapshot of every active
    /// cell's ecology state, written as <c>cells_&lt;scene&gt;[_&lt;label&gt;]_&lt;stamp&gt;.json</c>
    /// beside the <c>diag</c> and <c>prof</c> reports.
    ///
    /// <para>Why it exists: "this cell has no flora" has at least five causes that look
    /// identical on screen. The spawner never started (the bootstrap is still deferred, or the
    /// cell is a satellite). The phase is Frenzy, which freezes planting. A diagnostic hold is
    /// on. Every species is at its cap. Or the plants spawned and something removed or hid
    /// them. Each one leaves a different fingerprint in the fields below, so one command
    /// separates them where reading code could not.</para>
    ///
    /// <para>A READER: it changes nothing, and it walks the scene only when asked.</para>
    /// </summary>
    public static class CellStateReport
    {
        public const string CommandName = "cells";

        [Serializable]
        public class FloraSpecies
        {
            public string config;
            public string prefab;
            public int live;
            /// <summary>The initial batch this cell asks for, after its population scale.</summary>
            public int initialBatch;
            /// <summary>The seed floor the seeder tops back up to (0 = legacy unbounded planting).</summary>
            public int seedFloor;
            /// <summary>The live cap after the population scale (0 = uncapped).</summary>
            public int cap;
            public bool atCap;
            public float spawnProbability;
        }

        [Serializable]
        public class FaunaSpecies
        {
            public string config;
            public string prefab;
            public int live;
            public int initialBatch;
            public int cap;
            public bool atCap;
            public float spawnProbability;
            public int releaseTier;
        }

        [Serializable]
        public class CellState
        {
            public int id;
            public string gameObject;
            public string config;
            public string spawnProfile;
            public string configChoice;
            public bool isSatellite;
            public bool satelliteEcologyEnabled;

            /// <summary>The first-crystal bootstrap has run: cytoplasm spawned, spawner started.</summary>
            public bool postInitialized;
            /// <summary>The bootstrap is waiting on a config this peer could not choose yet.</summary>
            public bool postInitDeferred;
            /// <summary>The running life spawner, or empty when none runs.</summary>
            public string activeSpawner;

            public string phase;
            public float liveVolume;
            /// <summary>Trail + flora volume. Fauna bodies excluded. The IntensityWise fauna spawn floor.</summary>
            public float liveEnvironmentVolume;
            public int livePrismCount;
            public bool floraPlantingEnabled;
            public bool faunaSpawningEnabled;
            public bool diagnosticHold;

            public float restlessEnterVolume, frenzyEnterVolume, frenzyExitVolume;
            public int frenzyEnterCount, frenzyExitCount;

            public float membraneRadius;
            public float expectedNucleusRadius;
            public bool nucleusIsControlZone;
            public int faunaReleaseTier;
            public string controllingDomain;

            public float floraPopulationScale, floraPrismScale, faunaPopulationScale;

            public int floraLiveTotal, faunaLiveTotal;
            public List<FloraSpecies> flora = new();
            public List<FaunaSpecies> fauna = new();
        }

        [Serializable]
        public class Report
        {
            public string scene;
            public string timestamp;
            public string label;
            public float secondsSinceSceneLoad;

            /// <summary>
            /// Every <c>Flora</c> / <c>Fauna</c> object in the scene, registered to a cell or not.
            /// A gap between these and the cells' own live counts means lifeforms exist that no
            /// cell is counting; a zero here with a started spawner means none were ever made.
            /// </summary>
            public int floraObjectsInScene, faunaObjectsInScene;

            public List<CellState> cells = new();
            public List<string> notes = new();
        }

        /// <summary>The console handler: <c>cells [label]</c>.</summary>
        public static string Handle(string[] args)
        {
            string label = args is { Length: > 0 } ? string.Join("_", args) : "";
            var report = Take(label);
            string path = Save(report, DiagnosticsHUD.OutputDirectory);
            string summary = Summarize(report);
            Debug.Log($"[DiagnosticsHUD] {summary}\n  saved {path}");
            return $"{summary} | saved {Path.GetFileName(path)}";
        }

        public static Report Take(string label)
        {
            var report = new Report
            {
                scene = SceneManager.GetActiveScene().name,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                label = label ?? "",
                secondsSinceSceneLoad = Time.timeSinceLevelLoad,
                floraObjectsInScene = UnityEngine.Object.FindObjectsByType<Flora>(FindObjectsSortMode.None).Length,
                faunaObjectsInScene = UnityEngine.Object.FindObjectsByType<Fauna>(FindObjectsSortMode.None).Length,
            };

            var cells = Cell.ActiveCellsSnapshot;
            if (cells == null || cells.Count == 0)
            {
                report.notes.Add("No active Cell in the scene.");
                return report;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!cell) continue;
                report.cells.Add(Describe(cell, report.notes));
            }
            return report;
        }

        static CellState Describe(Cell cell, List<string> notes)
        {
            var config = cell.Config;
            var profile = config ? config.SpawnProfile : null;
            var t = cell.ResolvedThresholds;

            var s = new CellState
            {
                id = cell.ID,
                gameObject = cell.gameObject.name,
                config = config ? config.name : "",
                spawnProfile = profile ? profile.name : "",
                configChoice = cell.ConfigChoiceName,
                isSatellite = cell.IsSatellite,
                satelliteEcologyEnabled = cell.SatelliteEcologyEnabled,
                postInitialized = cell.IsPostInitialized,
                postInitDeferred = cell.IsPostInitDeferred,
                activeSpawner = cell.ActiveSpawnerName ?? "",
                phase = cell.Phase.ToString(),
                liveVolume = cell.LiveVolume,
                liveEnvironmentVolume = cell.LiveEnvironmentVolume,
                livePrismCount = cell.LiveBlockCount,
                floraPlantingEnabled = cell.FloraPlantingEnabled,
                faunaSpawningEnabled = cell.FaunaSpawningEnabled,
                diagnosticHold = Cell.DiagnosticProductionHold,
                restlessEnterVolume = t.RestlessEnterVolume,
                frenzyEnterVolume = t.FrenzyEnterVolume,
                frenzyExitVolume = t.FrenzyExitVolume,
                frenzyEnterCount = t.FrenzyEnter,
                frenzyExitCount = t.FrenzyExit,
                membraneRadius = cell.MembraneRadius,
                expectedNucleusRadius = cell.ExpectedNucleusWorldRadius,
                nucleusIsControlZone = cell.NucleusIsControlZone,
                faunaReleaseTier = cell.FaunaReleaseTier,
                controllingDomain = cell.ControllingDomain.ToString(),
                floraPopulationScale = profile ? profile.FloraPopulationScale : 1f,
                floraPrismScale = profile ? profile.FloraPrismScale : 1f,
                faunaPopulationScale = profile ? profile.FaunaPopulationScale : 1f,
            };

            if (!config)
            {
                notes.Add($"Cell {s.id}: no config assigned. The cell never chose one, so it has no spawner.");
                return s;
            }
            if (!profile)
            {
                notes.Add($"Cell {s.id}: config '{s.config}' has no SpawnProfile, so nothing is seeded.");
                return s;
            }

            if (profile.SupportedFloras != null)
                foreach (var cfg in profile.SupportedFloras)
                {
                    if (!cfg) { notes.Add($"Cell {s.id}: a null entry in SupportedFloras."); continue; }
                    int live = cell.GetLiveFloraCount(cfg);
                    s.floraLiveTotal += live;
                    s.flora.Add(new FloraSpecies
                    {
                        config = cfg.name,
                        prefab = cfg.FloraPrefab ? cfg.FloraPrefab.name : "(none)",
                        live = live,
                        initialBatch = cell.ResolveFloraPopulation(Mathf.Max(0, cfg.InitialSpawnCount)),
                        seedFloor = cell.ResolveFloraPopulation(cfg.PopulationSize),
                        cap = cell.ResolveFloraCap(cfg),
                        atCap = cell.IsFloraAtCap(cfg),
                        spawnProbability = cfg.SpawnProbability,
                    });
                }

            if (profile.SupportedFaunas != null)
                foreach (var cfg in profile.SupportedFaunas)
                {
                    if (!cfg) { notes.Add($"Cell {s.id}: a null entry in SupportedFaunas."); continue; }
                    int live = cell.GetLiveFaunaCount(cfg);
                    s.faunaLiveTotal += live;
                    s.fauna.Add(new FaunaSpecies
                    {
                        config = cfg.name,
                        prefab = cfg.FaunaPrefab ? cfg.FaunaPrefab.name : "(none)",
                        live = live,
                        initialBatch = cell.ResolveFaunaPopulation(cfg.InitialSpawnCount),
                        cap = cell.ResolveFaunaCap(cfg),
                        atCap = cell.IsFaunaAtCap(cfg),
                        spawnProbability = cfg.SpawnProbability,
                        releaseTier = cfg.ReleaseTier,
                    });
                }

            // The fingerprints, in the order the spawner meets them.
            if (s.isSatellite && !s.satelliteEcologyEnabled)
                notes.Add($"Cell {s.id}: a satellite cell runs no life spawner by design.");
            else if (s.postInitDeferred)
                notes.Add($"Cell {s.id}: bootstrap DEFERRED - this peer could not choose its config yet.");
            else if (!s.postInitialized)
                notes.Add($"Cell {s.id}: bootstrap has not run - no crystal has registered with this cell yet.");
            else if (string.IsNullOrEmpty(s.activeSpawner))
                notes.Add($"Cell {s.id}: bootstrap ran but no life spawner is running.");

            if (s.diagnosticHold)
                notes.Add("The 'freeze' diagnostic hold is ON: nothing is planted or seeded.");
            else if (!s.floraPlantingEnabled)
                notes.Add($"Cell {s.id}: planting is OFF because the phase is {s.phase}.");

            if (s.floraLiveTotal == 0 && s.flora.Count > 0 && s.postInitialized && s.floraPlantingEnabled)
                notes.Add($"Cell {s.id}: the spawner runs and planting is on, yet no plant is alive. " +
                          "Look in the console for an exception from a flora spawn.");

            return s;
        }

        public static string Summarize(Report r)
        {
            var sb = new StringBuilder(256);
            sb.Append($"cells: {r.cells.Count} | flora objects {r.floraObjectsInScene}, fauna objects {r.faunaObjectsInScene}");
            foreach (var c in r.cells)
                sb.Append($" | cell {c.id} '{c.config}' {c.phase} spawner={(string.IsNullOrEmpty(c.activeSpawner) ? "NONE" : c.activeSpawner)} " +
                          $"flora {c.floraLiveTotal} fauna {c.faunaLiveTotal} " +
                          $"planting {(c.floraPlantingEnabled ? "on" : "OFF")}");
            return sb.ToString();
        }

        public static string Save(Report report, string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                // Invariant culture: a file name is a KEY, and a device calendar that is not
                // Gregorian would otherwise stamp it in another year.
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string label = string.IsNullOrEmpty(report.label) ? "" : "_" + Sanitize(report.label);
                string path = Path.Combine(directory, $"cells_{Sanitize(report.scene)}{label}_{stamp}.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiagnosticsHUD] Could not save the cell report: {e.Message}");
                return "(save failed: " + e.Message + ")";
            }
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "scene";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
#endif
