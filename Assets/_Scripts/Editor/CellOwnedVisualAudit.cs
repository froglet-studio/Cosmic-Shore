using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Guards the "the Cell owns the environment" law from the two ways it silently rots.
    ///
    /// 1. A HAND-PLACED membrane / nucleus / cytoplasm in a scene whose Cell already spawns that
    ///    same prefab. The Cell instantiates its own in <c>SpawnVisuals</c> and tracks ONLY that
    ///    instance - every consumer (<c>NucleusWorldRadius</c>, <c>RefreshNucleusControlRadius</c>,
    ///    <c>IsInsideNucleus</c>, the cleanup + swap paths) reads the Cell's private field - so a
    ///    scene copy renders on top of the real one and no bookkeeping can see it. Three scenes
    ///    shipped a coincident <c>Nucleus.prefab</c> this way.
    ///
    ///    The check is deliberately narrow: it fires only when the scene's Cell is wired to a
    ///    config that supplies THE SAME prefab. A scene-placed object that merely looks like a
    ///    membrane is fine and is reported as OK - the <c>SkyboxModel</c> backdrop
    ///    (<c>MembraneBase</c>/<c>BigMembraneVariant</c>) is a different asset from the
    ///    <c>CapsuleMembrane</c> the configs actually spawn, and in the Recording Studio tool
    ///    scenes, which wire no configs at all, it is the only geometry that renders.
    ///
    /// 2. DEAD prefab-instance overrides on a Cell: a propertyPath naming a field that no longer
    ///    exists on <see cref="Cell"/>. Unity never prunes an unresolvable modification until the
    ///    object is touched and re-saved, so retired fields (<c>CellTypes</c>, <c>fauna1/2</c>,
    ///    <c>flora1/2</c>, <c>randomSpawnProfile</c>, <c>Crystal</c>) linger for years, often
    ///    pointing at guids no asset carries, and mislead anyone reading the scene for how the
    ///    Cell is wired.
    ///
    /// Read-only: reports, never writes. See Docs/TOOLING.md and CLAUDE.md's
    /// "The Cell owns the environment" bullet.
    /// </summary>
    public static class CellOwnedVisualAudit
    {
        static readonly string[] SceneFolders = { "Assets/_Scenes", "Assets/FTUE" };

        [MenuItem("FrogletTools/Ecology/Audit Cell-Owned Visuals")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 3,
            Description = "Find scene-placed duplicates of a Cell's membrane/nucleus/cytoplasm, " +
                          "and dead Cell overrides naming retired fields.")]
        public static void Audit()
        {
            // Caches are per-run so a re-run after an edit sees the new state.
            s_cellPrefabGuids = null;
            s_fileIdCache.Clear();

            var configs = LoadCellConfigs();
            if (configs.Count == 0)
            {
                Debug.LogWarning("[CellOwnedVisualAudit] No CellConfigDataSO assets found - nothing to audit.");
                return;
            }

            // Every prefab any config spawns, mapped back to the configs that spawn it.
            var ownedVisuals = new Dictionary<string, List<CellConfigDataSO>>();
            foreach (var cfg in configs)
            {
                foreach (var go in OwnedVisualsOf(cfg))
                {
                    string guid = GuidOf(go);
                    if (guid == null) continue;
                    if (!ownedVisuals.TryGetValue(guid, out var list))
                        ownedVisuals[guid] = list = new List<CellConfigDataSO>();
                    list.Add(cfg);
                }
            }

            var cellFields = SerializedFieldNames(typeof(Cell));
            var scenes = PrefabInstanceSceneScanner.FindScenes(SceneFolders);

            var duplicates = new List<string>();
            var benign = new List<string>();
            var deadOverrides = new List<string>();

            foreach (string scene in scenes)
            {
                // One pass for the visuals, one for the Cell instances themselves.
                var visualInstances = PrefabInstanceSceneScanner.ScanScene(
                    scene, new HashSet<string>(ownedVisuals.Keys));
                var sceneConfigs = ConfigsWiredInScene(scene, configs);

                foreach (var inst in visualInstances)
                {
                    if (!ownedVisuals.TryGetValue(inst.SourcePrefabGuid, out var owners)) continue;

                    string name = inst.RootNameOverride ?? AssetName(inst.SourcePrefabGuid);
                    var spawners = owners.Where(c => sceneConfigs.Contains(c)).ToList();

                    if (spawners.Count > 0)
                        duplicates.Add(
                            $"  {inst.SceneName}: '{name}' ({AssetPath(inst.SourcePrefabGuid)}) is ALSO spawned by " +
                            $"{string.Join(", ", spawners.Select(c => c.name))} - the Cell's copy is the tracked one.");
                    else
                        benign.Add(
                            $"  {inst.SceneName}: '{name}' ({AssetName(inst.SourcePrefabGuid)}) - no Cell in this " +
                            "scene spawns it; standalone scene geometry, left alone.");
                }

                foreach (string line in DeadCellOverrides(scene, cellFields))
                    deadOverrides.Add(line);
            }

            Report(duplicates, benign, deadOverrides, scenes.Count);
        }

        static void Report(List<string> duplicates, List<string> benign, List<string> deadOverrides, int sceneCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[CellOwnedVisualAudit] {sceneCount} scenes scanned.");

            sb.AppendLine();
            sb.AppendLine(duplicates.Count == 0
                ? "SCENE-PLACED DUPLICATES: none. Every Cell-owned visual is spawned by its Cell."
                : $"SCENE-PLACED DUPLICATES ({duplicates.Count}) - delete these from the scene; the Cell already " +
                  "spawns them, and only the Cell's instance is tracked:");
            foreach (string d in duplicates) sb.AppendLine(d);

            sb.AppendLine();
            sb.AppendLine(deadOverrides.Count == 0
                ? "DEAD CELL OVERRIDES: none."
                : $"DEAD CELL OVERRIDES ({deadOverrides.Count}) - propertyPaths naming fields that no longer exist " +
                  "on Cell. Inert at runtime, but they read as real wiring:");
            foreach (string o in deadOverrides) sb.AppendLine(o);

            if (benign.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"OK - scene geometry that is NOT a Cell duplicate ({benign.Count}):");
                foreach (string b in benign) sb.AppendLine(b);
            }

            if (duplicates.Count > 0 || deadOverrides.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        // ── config -> the visuals it owns ────────────────────────────────────

        static IEnumerable<GameObject> OwnedVisualsOf(CellConfigDataSO cfg)
        {
            if (cfg.MembranePrefab) yield return cfg.MembranePrefab;
            if (cfg.NucleusPrefab) yield return cfg.NucleusPrefab;
            if (cfg.CytoplasmPrefab) yield return cfg.CytoplasmPrefab.gameObject;
        }

        static List<CellConfigDataSO> LoadCellConfigs() =>
            AssetDatabase.FindAssets($"t:{nameof(CellConfigDataSO)}")
                .Select(g => AssetDatabase.LoadAssetAtPath<CellConfigDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null)
                .ToList();

        /// <summary>
        /// The configs any Cell in this scene could assign. Read from the scene YAML rather than by
        /// opening the scene: a Cell's list is normally a prefab-instance override
        /// (<c>CellConfigs.Array.data[N]</c>), and a scene that overrides nothing wires no configs
        /// at all - which is exactly the Recording Studio case where the Cell never spawns anything.
        /// </summary>
        static HashSet<CellConfigDataSO> ConfigsWiredInScene(string scenePath, List<CellConfigDataSO> all)
        {
            var byGuid = new Dictionary<string, CellConfigDataSO>();
            foreach (var c in all)
            {
                string g = GuidOf(c);
                if (g != null) byGuid[g] = c;
            }
            var wired = new HashSet<CellConfigDataSO>();
            string text = System.IO.File.ReadAllText(scenePath);

            foreach (var m in System.Text.RegularExpressions.Regex.Matches(
                         text, @"propertyPath: '?CellConfigs\.Array\.data\[\d+\]'?\s*\n\s*value:.*\n\s*objectReference: \{fileID: \d+, guid: ([0-9a-f]{32})")
                     .Cast<System.Text.RegularExpressions.Match>())
            {
                if (byGuid.TryGetValue(m.Groups[1].Value, out var cfg)) wired.Add(cfg);
            }

            // A Cell prefab that ships configs in the asset itself contributes them to every scene
            // that instances it without overriding the list.
            return wired;
        }

        // ── dead overrides on the Cell component ────────────────────────────

        static HashSet<string> s_cellPrefabGuids;

        /// <summary>Prefabs that contain a Cell at all - only their instances are worth resolving.
        /// Computed once per audit run (it loads every prefab under _Prefabs).</summary>
        static HashSet<string> CellPrefabGuids => s_cellPrefabGuids ??= new HashSet<string>(
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Prefabs" })
                .Where(g =>
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                    return go != null && go.GetComponentInChildren<Cell>(true) != null;
                }));

        static IEnumerable<string> DeadCellOverrides(string scenePath, HashSet<string> cellFields)
        {
            if (CellPrefabGuids.Count == 0) yield break;

            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            foreach (var inst in PrefabInstanceSceneScanner.ScanScene(scenePath, CellPrefabGuids))
            {
                foreach (var o in inst.Overrides)
                {
                    if (!IsCellComponent(o.TargetGuid, o.TargetFileId)) continue;

                    string root = o.PropertyPath.Split('.')[0];
                    if (root.StartsWith("m_", StringComparison.Ordinal)) continue; // Unity built-in
                    if (cellFields.Contains(root)) continue;

                    yield return $"  {sceneName}: Cell override '{o.PropertyPath}' - no such field on Cell.";
                }
            }
        }

        static readonly Dictionary<string, Dictionary<long, UnityEngine.Object>> s_fileIdCache = new();

        /// <summary>True when (prefab guid, local fileID) resolves to a <see cref="Cell"/> component.
        /// Resolving through the AssetDatabase covers prefab VARIANTS too, whose component fileIDs
        /// are derived from the base's and cannot be matched by a literal id.</summary>
        static bool IsCellComponent(string prefabGuid, long fileId)
        {
            if (prefabGuid == null) return false;
            if (!s_fileIdCache.TryGetValue(prefabGuid, out var map))
            {
                map = new Dictionary<long, UnityEngine.Object>();
                string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (obj == null) continue;
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long id))
                            map[id] = obj;
                    }
                }
                s_fileIdCache[prefabGuid] = map;
            }
            return map.TryGetValue(fileId, out var o) && o is Cell;
        }

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>Every field name Unity would serialize on <paramref name="type"/>, walking the
        /// hierarchy: public instance fields plus private ones carrying [SerializeField].</summary>
        static HashSet<string> SerializedFieldNames(Type type)
        {
            var names = new HashSet<string>();
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                              BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsNotSerialized) continue;
                    if (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                        names.Add(f.Name);
                }
            }
            return names;
        }

        static string GuidOf(UnityEngine.Object o)
        {
            string path = AssetDatabase.GetAssetPath(o);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        static string AssetPath(string guid) => AssetDatabase.GUIDToAssetPath(guid);

        static string AssetName(string guid) =>
            System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
    }
}
