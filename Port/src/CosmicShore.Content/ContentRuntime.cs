using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.SceneManagement;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Content
{
    /// <summary>
    /// The port running the Unity project's own content, the way the player does:
    /// the build scene list from <c>ProjectSettings/EditorBuildSettings.asset</c>,
    /// real Single/Additive scene loads through <see cref="SceneManager"/>, Reflex-style
    /// dependency injection (root scopes from <c>Resources/ReflexSettings</c>, a child
    /// scope per loaded scene), <c>Resources.Load</c> over every <c>Resources/</c> folder,
    /// and prefab assets materialized as inactive templates for <c>Instantiate</c>.
    /// </summary>
    public sealed class ContentRuntime : ISceneContentBackend
    {
        public static ContentRuntime Current { get; private set; }

        public readonly AssetDatabase Db;
        public readonly ScriptTypeMap Scripts;
        public readonly AssetLoader Assets;
        public readonly InstantiateOptions Options;
        public readonly Textures.TextureImporter Textures;
        public readonly Fonts.TmpFontLibrary Fonts;
        public readonly List<(string path, string guid, bool enabled)> BuildScenes = new();

        /// <summary>The root DI container (Reflex project scope), once booted.</summary>
        public Container RootContainer { get; private set; }

        /// <summary>Every scene load's diagnostics, most recent last.</summary>
        public readonly List<(string scene, LoadedScene result)> Loads = new();

        readonly Dictionary<string, string> _resources = new(StringComparer.OrdinalIgnoreCase);
        GameObject _templatesRoot;
        readonly Dictionary<string, (PrefabGraph graph, LoadedScene loaded)> _prefabTemplates = new(StringComparer.Ordinal);

        public ContentRuntime(string projectRoot, IEnumerable<Assembly> assemblies, InstantiateOptions options = null)
        {
            Db = new AssetDatabase(projectRoot);
            Scripts = new ScriptTypeMap(Db, assemblies.Append(typeof(GameObject).Assembly));
            Assets = new AssetLoader(Db, Scripts);
            Options = options ?? new InstantiateOptions();
            ReadBuildSettings();
            IndexResources();
            Assets.Importers[typeof(GameObject)] = LoadPrefabObject;
            Textures = new Textures.TextureImporter(Db);
            CosmicShore.Content.Textures.TextureImporter.Register(Assets, Textures);
            Fonts = new Fonts.TmpFontLibrary(Db);
            Assets.Importers[typeof(CosmicShore.Engine.UI.TMP_FontAsset)] = LoadFontAsset;
            Audio.MixerImporter.Register(Assets, new Audio.MixerImporter(Db));
        }

        /// <summary>A TMP font asset reference (only a MonoBehaviour whose script IS TMP_FontAsset).</summary>
        EngineObject LoadFontAsset(ObjRef r)
        {
            var doc = Db.Load(r.Guid)?.Get(r.FileId);
            if (doc == null || doc.TypeName != "MonoBehaviour") return null;
            var script = ObjRef.From(doc.Body["m_Script"]);
            if (script.Guid != CosmicShore.Content.Fonts.TmpFontLibrary.FontAssetScriptGuid) return null;
            return Fonts.LoadRef(r);
        }

        /// <summary>Installs this runtime as the engine's scene + Resources backend.</summary>
        public void Install()
        {
            Current = this;
            Fonts.ApplyToGlobalSettings();
            SceneManager.Backend = this;
            Resources.ContentLoader = LoadResource;
        }

        void ReadBuildSettings()
        {
            var path = Path.Combine(Db.ProjectRoot, "ProjectSettings", "EditorBuildSettings.asset");
            if (!File.Exists(path)) return;
            var doc = UnityYaml.ParseDocuments(File.ReadAllText(path)).FirstOrDefault();
            foreach (var s in doc?.Body["m_Scenes"]?.Items ?? Array.Empty<YNode>())
                BuildScenes.Add((s.Str("path"), s.Str("guid"), s.Bool("enabled")));
        }

        void IndexResources()
        {
            const string marker = "/Resources/";
            foreach (var full in Db.AllAssetPaths)
            {
                var rel = Db.ProjectRelative(full);
                int i = rel.LastIndexOf(marker, StringComparison.Ordinal);
                if (i < 0 || Directory.Exists(full)) continue;
                var key = rel.Substring(i + marker.Length);
                var dot = key.LastIndexOf('.');
                if (dot > 0) key = key.Substring(0, dot);
                _resources.TryAdd(key, full);
            }
        }

        /// <summary>Scene asset path for a name, path or build index string.</summary>
        public string ResolveScenePath(string nameOrPath)
        {
            if (nameOrPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return nameOrPath;
            foreach (var (p, _, _) in BuildScenes)
                if (string.Equals(Path.GetFileNameWithoutExtension(p), nameOrPath, StringComparison.OrdinalIgnoreCase))
                    return p;
            // Not in the build list — search the project (tool scenes, retired singleplayer scenes).
            foreach (var full in Db.AllAssetPaths)
                if (full.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileNameWithoutExtension(full), nameOrPath, StringComparison.OrdinalIgnoreCase))
                    return Db.ProjectRelative(full);
            return null;
        }

        // ── Boot ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reflex project scope: build the root container from the root-scope prefabs named in
        /// <c>Resources/ReflexSettings</c>. Reflex runs <c>InstallBindings</c> on the prefab
        /// ASSETS — it never instantiates them — so the installers run on the inactive prefab
        /// templates (serialized fields populated, no Awake), and the live objects those
        /// installers configure are the ones the first scene brings (e.g. Bootstrap's AppManager).
        /// </summary>
        public void BootRootScopes()
        {
            if (!_resources.TryGetValue("ReflexSettings", out var settingsPath)) return;
            var guid = Db.GuidOf(settingsPath);
            var doc = Db.Load(guid)?.Documents.FirstOrDefault(d => d.ClassId == 114);
            var roots = doc?.Body["<RootScopes>k__BackingField"]?.Items ?? Array.Empty<YNode>();

            var installers = new List<IInstaller>();
            foreach (var r in roots)
            {
                var prefabRef = ObjRef.From(r);
                if (Assets.Load(new ObjRef(100100000, prefabRef.Guid, 3), typeof(GameObject)) is not GameObject template) continue;
                installers.AddRange(template.GetComponentsInChildren<IInstaller>(true));
            }

            var builder = new ContainerBuilder();
            foreach (var installer in installers)
            {
                try { installer.InstallBindings(builder); }
                catch (Exception e) { Debug.LogException(e); }
            }
            RootContainer = builder.Build();
        }

        static void Inject(Container container, GameObject go)
        {
            try { container.InjectGameObject(go, recursive: true); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ── ISceneContentBackend ─────────────────────────────────────────────

        public void Load(string sceneName, LoadSceneMode mode, Scene scene)
        {
            var path = ResolveScenePath(sceneName);
            if (path == null)
            {
                Debug.LogError($"[Content] Scene '{sceneName}' not found in build settings or project.");
                return;
            }
            int index = BuildScenes.FindIndex(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase));
            if (mode == LoadSceneMode.Single)
            {
                scene.name = Path.GetFileNameWithoutExtension(path);
                if (index >= 0) scene.buildIndex = index;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var file = Db.LoadPath(path);
            var loaded = Instantiate(PrefabGraph.Build(Db, file), activate: Options.Activate);
            Loads.Add((scene.name, loaded));
            if (Environment.GetEnvironmentVariable("CS_PORT_VERBOSE") == "1")
                foreach (var kv in loaded.MissingScripts.OrderByDescending(k => k.Value))
                    Console.WriteLine($"[content]   no script ×{kv.Value}: {Db.PathOf(kv.Key) ?? kv.Key}");
            Console.WriteLine($"[content] {mode} load '{Path.GetFileNameWithoutExtension(path)}' — {loaded.GameObjects} GameObjects, {loaded.Components} components, {sw.ElapsedMilliseconds} ms");

            // Reflex scene scope: a child of the root container injects every scene object
            // (after Awake/OnEnable, before Start — the timing the codebase documents).
            if (RootContainer != null)
            {
                var sceneContainer = RootContainer.CreateChild();
                foreach (var root in loaded.Roots) Inject(sceneContainer, root);
            }
        }

        LoadedScene Instantiate(PrefabGraph graph, bool activate, Transform parent = null)
        {
            var opts = new InstantiateOptions
            {
                IncludeScript = Options.IncludeScript,
                WirePersistentCalls = Options.WirePersistentCalls,
                Activate = activate,
                Parent = parent,
            };
            return new SceneInstantiator(Assets, opts).Instantiate(graph);
        }

        // ── Resources ────────────────────────────────────────────────────────

        EngineObject LoadResource(string path, Type type)
        {
            if (!_resources.TryGetValue(path, out var full)) return null;
            var guid = Db.GuidOf(full);
            if (guid == null) return null;
            var ext = Path.GetExtension(full).ToLowerInvariant();
            long fileId = ext switch
            {
                ".prefab" => MainObjectOfPrefab(guid),
                ".asset" or ".mat" => MainObjectOfAsset(guid),
                ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" => typeof(Sprite).IsAssignableFrom(type) ? 21300000 : 2800000,
                _ => 0,
            };
            if (fileId == 0) return null;
            return Assets.Load(new ObjRef(fileId, guid, 2), type ?? typeof(EngineObject));
        }

        long MainObjectOfAsset(string guid)
        {
            var f = Db.Load(guid);
            if (f == null) return 0;
            var main = f.Documents.FirstOrDefault(d => d.FileId == 11400000 || d.FileId == 2100000)
                ?? f.Documents.FirstOrDefault(d => !d.Stripped);
            return main?.FileId ?? 0;
        }

        long MainObjectOfPrefab(string guid)
        {
            var f = Db.Load(guid);
            if (f == null) return 0;
            var g = PrefabGraph.Build(Db, f);
            return g.FindPrefabRoot()?.Id ?? 0;
        }

        // ── Prefab assets as inactive templates ──────────────────────────────

        static readonly bool s_traceTemplates = Environment.GetEnvironmentVariable("CS_PORT_TRACE_TEMPLATES") == "1";

        /// <summary>How many prefab assets have been materialized as templates.</summary>
        public int PrefabTemplateCount { get; private set; }

        EngineObject LoadPrefabObject(ObjRef r)
        {
            var path = Db.PathOf(r.Guid);
            if (path == null) return null;
            bool model = AssetDatabase.IsModelPath(path);
            if (!model && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return null;
            if (GameLoop.Current == null) return null;
            // A model file's refs mostly name sub-assets (meshes, materials): only materialize
            // its model prefab for a ref that names the prefab or one of its objects.
            if (model && (Db.LoadModel(r.Guid) is not { } imported || !ModelPrefabGraph.OwnsObject(imported, r.FileId)))
                return null;

            if (!_prefabTemplates.TryGetValue(r.Guid, out var entry))
            {
                if (_templatesRoot == null || !_templatesRoot)
                {
                    _templatesRoot = new GameObject("__prefab_assets");
                    _templatesRoot.SetActive(false);
                    _templatesRoot.MarkAsPrefabAsset();
                    EngineObject.DontDestroyOnLoad(_templatesRoot);
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var file = Db.Load(r.Guid);
                var graph = PrefabGraph.Build(Db, file);
                long buildMs = sw.ElapsedMilliseconds;
                _prefabTemplates[r.Guid] = entry = (graph, null); // re-entrancy guard for self-referencing prefabs
                var loaded = Instantiate(graph, activate: false, parent: _templatesRoot.transform);
                _prefabTemplates[r.Guid] = entry = (graph, loaded);
                PrefabTemplateCount++;
                if (s_traceTemplates)
                    Console.WriteLine($"[content] template {Path.GetFileName(path)}: {loaded.GameObjects} GOs, graph {buildMs} ms, total {sw.ElapsedMilliseconds} ms (inclusive)");
            }
            if (entry.loaded == null) return null;
            // 100100000 names the prefab asset itself → its root GameObject.
            long id = r.FileId == 100100000 ? entry.graph.FindPrefabRoot()?.Id ?? 0 : entry.graph.Resolve(r.FileId);
            return entry.loaded.ById.TryGetValue(id, out var o) ? o : null;
        }
    }
}
