using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Serialization;
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

        /// <summary>
        /// The most recent scene loads' diagnostics, most recent last. Bounded to
        /// <see cref="LoadHistory"/>: a LoadedScene holds its roots, so keeping every load would
        /// keep every unloaded scene's object graph alive across a long run of Single loads.
        /// </summary>
        public readonly List<(string scene, LoadedScene result)> Loads = new();
        public const int LoadHistory = 2;

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
            ReadGraphicsSettings();
            ReadTimeSettings();
            ReadPhysicsSettings();
            RegisterPackageResources();
            IndexResources();
            Assets.Importers[typeof(GameObject)] = LoadPrefabObject;
            Textures = new Textures.TextureImporter(Db);
            CosmicShore.Content.Textures.TextureImporter.Register(Assets, Textures);
            Fonts = new Fonts.TmpFontLibrary(Db);
            Assets.Importers[typeof(CosmicShore.Engine.UI.TMP_FontAsset)] = LoadFontAsset;
            Audio.MixerImporter.Register(Assets, new Audio.MixerImporter(Db));
            ChainImporter(typeof(Mesh), r => r.Guid == AssetLoader.BuiltinDefaultGuid ? BuiltinMeshes.ForFileId(r.FileId) : null);
            ChainImporter(typeof(Material), r => r.Guid == AssetLoader.BuiltinExtraGuid ? BuiltinMaterials.ForFileId(r.FileId) : null);
            ShaderProperties = new Shaders.ShaderPropertyCatalog(Db);
            Shader.PropertyCatalog = ShaderProperties.For;
        }

        /// <summary>The shaders' declared properties (what Material.HasProperty and unset-property reads answer from).</summary>
        public Shaders.ShaderPropertyCatalog ShaderProperties { get; }

        /// <summary>Adds an importer for <paramref name="type"/> in front of any already registered (first non-null wins).</summary>
        public void ChainImporter(Type type, Func<ObjRef, EngineObject> importer)
        {
            Assets.Importers.TryGetValue(type, out var previous);
            Assets.Importers[type] = previous == null ? importer : r => importer(r) ?? previous(r);
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

        /// <summary>
        /// ProjectSettings/TimeManager.asset: the physics step, the delta-time clamp and the
        /// initial time scale. The step is gameplay, not configuration — this project runs
        /// physics (and so every OnTrigger* message) at 0.04 s, and the engine default of 0.02
        /// doubled the rate every trigger was sampled at.
        /// </summary>
        void ReadTimeSettings()
        {
            var body = ReadSettingsBody("TimeManager.asset");
            if (body == null) return;
            if (TryFloat(body["Fixed Timestep"], out float step) && step > 0f) Time.fixedDeltaTime = step;
            if (TryFloat(body["Maximum Allowed Timestep"], out float max) && max > 0f) Time.maximumDeltaTime = max;
            if (TryFloat(body["m_TimeScale"], out float scale) && scale >= 0f) Time.timeScale = scale;
        }

        /// <summary>ProjectSettings/DynamicsManager.asset: gravity, and which transform poses and triggers a query sees.</summary>
        void ReadPhysicsSettings()
        {
            var body = ReadSettingsBody("DynamicsManager.asset");
            if (body == null) return;
            if (body["m_QueriesHitTriggers"] != null) Physics.queriesHitTriggers = body.Bool("m_QueriesHitTriggers");
            if (body["m_AutoSyncTransforms"] != null) Physics.autoSyncTransforms = body.Bool("m_AutoSyncTransforms");
            if (body["m_Gravity"] is YMap g) Physics.gravity = new Vector3(g.Float("x"), g.Float("y"), g.Float("z"));
        }

        YNode ReadSettingsBody(string file)
        {
            var path = Path.Combine(Db.ProjectRoot, "ProjectSettings", file);
            if (!File.Exists(path)) return null;
            return UnityYaml.ParseDocuments(File.ReadAllText(path)).FirstOrDefault()?.Body;
        }

        /// <summary>A plain number, or a Rational map ({m_Count, m_Rational: {m_Numerator, m_Denominator}}) as newer editors write the step.</summary>
        static bool TryFloat(YNode node, out float value)
        {
            value = 0f;
            if (node is YScalar sc) return YScalar.TryFloat(sc.Value, out value);
            if (node is YMap m && m["m_Rational"] is YMap r)
            {
                float den = r.Float("m_Denominator");
                if (den <= 0f) return false;
                value = r.Float("m_Numerator") / den;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The project's active render pipeline (ProjectSettings/GraphicsSettings.asset
        /// m_CustomRenderPipeline): in a build GraphicsSettings.currentRenderPipeline is that URP
        /// asset, which every SRP-dependent gate (Entities Graphics support included) reads.
        /// </summary>
        void ReadGraphicsSettings()
        {
            var path = Path.Combine(Db.ProjectRoot, "ProjectSettings", "GraphicsSettings.asset");
            if (!File.Exists(path)) return;
            var doc = UnityYaml.ParseDocuments(File.ReadAllText(path)).FirstOrDefault();
            var r = ObjRef.From(doc?.Body["m_CustomRenderPipeline"]);
            if (r.IsNull) return;
            var asset = Assets.Load<CosmicShore.Engine.Rendering.RenderPipelineAsset>(r);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CosmicShore.Engine.Rendering.UniversalRenderPipelineAsset>();
                asset.name = Path.GetFileNameWithoutExtension(Db.PathOf(r.Guid) ?? "URP_Asset");
            }
            CosmicShore.Engine.Rendering.GraphicsSettings.defaultRenderPipeline = asset;
        }

        /// <summary>
        /// Resources that ship inside packages rather than the project (Library/PackageCache is
        /// not part of the repository): the port stands in for the ones game code loads.
        /// </summary>
        static void RegisterPackageResources()
        {
            // com.unity.entities.graphics: the sparse uploader the Entities Graphics support probe loads.
            Resources.Register("SparseUploader", new ComputeShader("SparseUploader", "CopyKernel", "ReplaceKernel"));
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
            if (mode == LoadSceneMode.Single) ApplyRenderSettings(file);
            var loaded = Instantiate(PrefabGraph.Build(Db, file), activate: Options.Activate);
            Loads.Add((scene.name, loaded));
            if (Loads.Count > LoadHistory) Loads.RemoveRange(0, Loads.Count - LoadHistory);
            if (Environment.GetEnvironmentVariable("CS_PORT_VERBOSE") == "1")
            {
                foreach (var kv in loaded.MissingScripts.OrderByDescending(k => k.Value))
                    Console.WriteLine($"[content]   no script ×{kv.Value}: {Db.PathOf(kv.Key) ?? kv.Key}");
                foreach (var w in loaded.Warnings.Distinct())
                    Console.WriteLine($"[content]   warning: {w}");
            }
            Console.WriteLine($"[content] {mode} load '{Path.GetFileNameWithoutExtension(path)}' — {loaded.GameObjects} GameObjects, {loaded.Components} components, {sw.ElapsedMilliseconds} ms");

            // Reflex scene scope: a child of the root container injects every scene object
            // (after Awake/OnEnable, before Start — the timing the codebase documents).
            if (RootContainer != null)
            {
                var sceneContainer = RootContainer.CreateChild();
                foreach (var root in loaded.Roots) Inject(sceneContainer, root);
            }
        }

        /// <summary>A Single load adopts that scene's lighting environment (document class 104).</summary>
        void ApplyRenderSettings(AssetFile file)
        {
            var doc = file.Documents.FirstOrDefault(d => d.ClassId == 104);
            if (doc?.Body == null) return;
            var b = doc.Body;
            var sky = ObjRef.From(b["m_SkyboxMaterial"]);
            RenderSettings.skybox = sky.IsNull ? null : Assets.Load<Material>(sky);
            RenderSettings.fog = b.Int("m_Fog") != 0;
            if (b["m_FogColor"] != null) RenderSettings.fogColor = SerializedReader.ReadColor(b["m_FogColor"]);
            RenderSettings.fogMode = (FogMode)b.Int("m_FogMode", 3);
            RenderSettings.fogDensity = b.Float("m_FogDensity", 0.01f);
            RenderSettings.fogStartDistance = b.Float("m_LinearFogStart", 0f);
            RenderSettings.fogEndDistance = b.Float("m_LinearFogEnd", 300f);
            RenderSettings.ambientMode = (CosmicShore.Engine.Rendering.AmbientMode)b.Int("m_AmbientMode", 0);
            if (b["m_AmbientSkyColor"] != null) RenderSettings.ambientSkyColor = SerializedReader.ReadColor(b["m_AmbientSkyColor"]);
            if (b["m_AmbientEquatorColor"] != null) RenderSettings.ambientEquatorColor = SerializedReader.ReadColor(b["m_AmbientEquatorColor"]);
            if (b["m_AmbientGroundColor"] != null) RenderSettings.ambientGroundColor = SerializedReader.ReadColor(b["m_AmbientGroundColor"]);
            RenderSettings.ambientIntensity = b.Float("m_AmbientIntensity", 1f);
            RenderSettings.reflectionIntensity = b.Float("m_ReflectionIntensity", 1f);
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
