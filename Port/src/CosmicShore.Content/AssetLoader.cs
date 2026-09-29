using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Content
{
    /// <summary>
    /// Loads project ASSETS (anything referenced by guid) into engine objects, cached per
    /// (guid, fileID): ScriptableObject .asset files through the reflective reader,
    /// materials from .mat, and — through pluggable importers — sprites, textures, fonts
    /// and meshes. An importer that isn't registered yet leaves its references null,
    /// which renders exactly like a missing reference in Unity.
    /// </summary>
    public sealed class AssetLoader : IReferenceResolver
    {
        /// <summary>Unity's built-in resources ("Resources/unity_builtin_extra" and "Library/unity default resources").</summary>
        public const string BuiltinExtraGuid = "0000000000000000f000000000000000";
        public const string BuiltinDefaultGuid = "0000000000000000e000000000000000";

        public readonly AssetDatabase Db;
        public readonly ScriptTypeMap Scripts;
        readonly SerializedReader _reader;
        readonly Dictionary<(string, long), EngineObject> _cache = new();

        /// <summary>Importer hooks keyed by the engine type they produce.</summary>
        public readonly Dictionary<Type, Func<ObjRef, EngineObject>> Importers = new();

        /// <summary>Persistent UnityEvent calls found inside assets (wired by whoever owns the scene).</summary>
        public Action<object, YNode, AssetFile> UnityEventSink;

        public AssetLoader(AssetDatabase db, ScriptTypeMap scripts)
        {
            Db = db;
            Scripts = scripts;
            _reader = new SerializedReader(this);
            Animator.ScriptTypeResolver ??= guid => scripts.Resolve(guid);
        }

        public SerializedReader Reader => _reader;

        /// <summary>An .anim clip or a model sub-asset clip by reference (cached like every asset).</summary>
        AnimationClip LoadClip(ObjRef r) => Load(r, typeof(AnimationClip)) as AnimationClip;

        public T Load<T>(ObjRef r) where T : EngineObject => Load(r, typeof(T)) as T;

        public EngineObject Load(ObjRef r, Type expected)
        {
            if (r.IsNull || string.IsNullOrEmpty(r.Guid)) return null;
            var key = (r.Guid, r.FileId);
            if (_cache.TryGetValue(key, out var hit)) return Adapt(hit, expected);

            EngineObject result = null;
            foreach (var kv in Importers)
            {
                // A prefab reference can name the GameObject OR a component on it (a field typed
                // as the component, or as an interface a component implements) — Unity resolves
                // all of them through the prefab asset.
                bool viaPrefab = kv.Key == typeof(GameObject) && (typeof(Component).IsAssignableFrom(expected) || expected.IsInterface);
                if (!viaPrefab && !expected.IsAssignableFrom(kv.Key) && !kv.Key.IsAssignableFrom(expected)) continue;
                result = kv.Value(r);
                if (result != null) break;
            }

            if (result == null && r.Guid != BuiltinExtraGuid && r.Guid != BuiltinDefaultGuid)
            {
                var path = Db.PathOf(r.Guid);
                if (path != null)
                {
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".asset" || ext == ".mat" || ext == ".prefab")
                        result = LoadFromYaml(r, path, expected);
                    else if (ext == ".controller" && Db.Load(r.Guid) is { } controllerFile)
                        result = AnimationImport.AnimatorImporter.LoadController(controllerFile, LoadClip);
                    else if (ext == ".anim" && Db.Load(r.Guid) is { } clipFile)
                        result = AnimationImport.AnimatorImporter.LoadClip(clipFile);
                    else if (AssetDatabase.IsModelPath(path))
                        result = LoadFromModel(r, expected);
                }
            }

            if (result != null) _cache[key] = result;
            return Adapt(result, expected);
        }

        /// <summary>
        /// A sub-asset of a model file: a Mesh by its fileID, or a Material — the project
        /// material the meta's <c>externalObjects</c> remaps it to, else a default material
        /// named after the FBX material (what Unity's importer embeds). The model prefab's
        /// GameObjects/components come through the GameObject importer (prefab templates);
        /// AnimationClip sub-assets are cut from the FBX takes (<see cref="Models.FbxAnimationImporter"/>);
        /// Avatar sub-assets are not modelled (a Generic avatar only maps paths, which bind directly).
        /// </summary>
        EngineObject LoadFromModel(ObjRef r, Type expected)
        {
            var model = Db.LoadModel(r.Guid);
            if (model == null) return null;

            if (model.MeshById.TryGetValue(r.FileId, out var mesh))
                return expected.IsAssignableFrom(typeof(Mesh)) ? mesh.Mesh : null;

            if (expected.IsAssignableFrom(typeof(AnimationClip)) && Models.FbxAnimationImporter.ImportClip(model, r.FileId) is { } clip)
                return clip;

            if (!expected.IsAssignableFrom(typeof(Material))) return null;
            foreach (var o in model.Scene.ObjectList)
            {
                if (o.Kind != "Material" || o.Name == null || Models.ModelFileIds.Material(o.Name) != r.FileId) continue;
                if (model.Settings.ExternalMaterials.TryGetValue(o.Name, out var ext) && !ext.IsNull)
                    return Load(ext, typeof(Material));
                return DefaultModelMaterial(model, o.Name);
            }
            return null;
        }

        static Material DefaultModelMaterial(Models.ImportedModel model, string name)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            foreach (var o in model.Scene.ObjectList)
            {
                if (o.Kind != "Material" || o.Name != name) continue;
                var diffuse = o.HasProp("DiffuseColor") ? o.PropVector("DiffuseColor", 1, 1, 1) : null;
                if (diffuse != null)
                {
                    var c = new Color((float)diffuse[0], (float)diffuse[1], (float)diffuse[2], 1f);
                    mat.SetColor("_BaseColor", c);
                    mat.SetColor("_Color", c);
                }
                break;
            }
            return mat;
        }

        // Unity runs a ScriptableObject's Awake/OnEnable when the asset loads.
        static void InvokeLoadHooks(ScriptableObject so)
        {
            const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var name in new[] { "Awake", "OnEnable" })
            {
                for (var t = so.GetType(); t != null && t != typeof(ScriptableObject); t = t.BaseType)
                {
                    var m = t.GetMethod(name, f | System.Reflection.BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (m == null) continue;
                    try { m.Invoke(so, null); }
                    catch (System.Reflection.TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); }
                    break;
                }
            }
        }

        static EngineObject Adapt(EngineObject o, Type expected)
        {
            if (o == null) return null;
            if (expected.IsInstanceOfType(o)) return o;
            // Asset refs to a prefab's GameObject read into a component-typed field.
            bool componentLike = typeof(Component).IsAssignableFrom(expected) || expected.IsInterface;
            if (o is GameObject go && componentLike) return go.GetComponent(expected) as EngineObject;
            if (o is Component c && componentLike) return c.gameObject.GetComponent(expected) as EngineObject;
            if (o is Component c2 && expected == typeof(GameObject)) return c2.gameObject;
            return null;
        }

        EngineObject LoadFromYaml(ObjRef r, string path, Type expected)
        {
            var file = Db.Load(r.Guid);
            var doc = file?.Get(r.FileId);
            if (doc == null) return null;

            switch (doc.ClassId)
            {
                case 114: // ScriptableObject
                {
                    var type = Scripts.Resolve(ObjRef.From(doc.Body["m_Script"]).Guid);
                    if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type) || type.IsAbstract) return null;
                    ScriptableObject so;
                    try { so = ScriptableObject.CreateInstance(type); }
                    catch (Exception) { return null; }
                    so.name = doc.Body.Str("m_Name") ?? type.Name;
                    _cache[(r.Guid, r.FileId)] = so; // before reading: cycles resolve to this instance
                    _reader.ReadInto(so, doc.Body, file);
                    InvokeLoadHooks(so);
                    return so;
                }
                case 21: // Material
                {
                    var mat = MaterialImporter.Build(doc, file, this);
                    return mat;
                }
                case 43: // Mesh
                    return Models.SerializedMeshImporter.Build(doc.Body);
                default:
                    return null;
            }
        }

        // ── IReferenceResolver (asset context: only guid refs can resolve) ────

        object IReferenceResolver.Resolve(ObjRef reference, Type fieldType, AssetFile origin)
        {
            if (reference.IsLocal)
            {
                // A local ref inside an asset file → another object in the same file.
                if (origin?.Guid == null) return null;
                return Load(new ObjRef(reference.FileId, origin.Guid, 2), fieldType);
            }
            return Load(reference, fieldType);
        }

        void IReferenceResolver.OnUnityEvent(object unityEvent, YNode persistentCalls, AssetFile origin)
            => UnityEventSink?.Invoke(unityEvent, persistentCalls, origin);
    }

    /// <summary>Builds an engine <see cref="Material"/> from a .mat document (class 21).</summary>
    public static class MaterialImporter
    {
        public static Material Build(UnityDocument doc, AssetFile file, AssetLoader loader)
        {
            var body = doc.Body;
            var shaderRef = ObjRef.From(body["m_Shader"]);
            string shaderName = ShaderNameFor(shaderRef, loader.Db);
            var mat = new Material(Shader.Find(shaderName)) { name = body.Str("m_Name") ?? "Material" };
            mat.renderQueue = body.Int("m_CustomRenderQueue", -1) is int q && q >= 0 ? q : 2000;

            var props = body["m_SavedProperties"];
            foreach (var entry in props?["m_TexEnvs"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap m && m.Entries.Count == 1)
                {
                    var name = m.Entries[0].Key;
                    var v = m.Entries[0].Value;
                    var texRef = ObjRef.From(v["m_Texture"]);
                    var scale = v["m_Scale"]; var offset = v["m_Offset"];
                    mat.SetTextureScaleOffset(name, new Vector4(scale?.Float("x", 1) ?? 1, scale?.Float("y", 1) ?? 1, offset?.Float("x") ?? 0, offset?.Float("y") ?? 0));
                    if (!texRef.IsNull)
                    {
                        var tex = loader.Load<Texture>(texRef.IsLocal ? new ObjRef(texRef.FileId, file.Guid, 2) : texRef);
                        if (tex != null) mat.SetTexture(name, tex);
                    }
                }
            foreach (var entry in props?["m_Floats"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap m && m.Entries.Count == 1 && YScalar.TryFloat(m.Entries[0].Value.Scalar, out var f))
                    mat.SetFloat(m.Entries[0].Key, f);
            foreach (var entry in props?["m_Ints"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap m && m.Entries.Count == 1 && YScalar.TryLong(m.Entries[0].Value.Scalar, out var iv))
                    mat.SetInt(m.Entries[0].Key, (int)iv);
            foreach (var entry in props?["m_Colors"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap m && m.Entries.Count == 1)
                {
                    var c = SerializedReader.ReadColor(m.Entries[0].Value);
                    mat.SetColor(m.Entries[0].Key, c);
                    mat.SetVector(m.Entries[0].Key, new Vector4(c.r, c.g, c.b, c.a));
                }
            foreach (var kw in (body.Str("m_ShaderKeywords") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                mat.EnableKeyword(kw);
            foreach (var kw in body["m_ValidKeywords"]?.Items ?? Array.Empty<YNode>())
                if (!string.IsNullOrEmpty(kw.Scalar)) mat.EnableKeyword(kw.Scalar);
            return mat;
        }

        /// <summary>
        /// Shader identity: built-ins by fileID (the handful the project uses), project
        /// shaders by their file's declared name (.shader "Shader \"Name\"") or graph name.
        /// </summary>
        public static string ShaderNameFor(ObjRef r, AssetDatabase db)
        {
            if (r.IsNull) return "Hidden/InternalErrorShader";
            if (r.Guid == AssetLoader.BuiltinExtraGuid || r.Guid == AssetLoader.BuiltinDefaultGuid)
                return r.FileId switch
                {
                    10770 => "UI/Default",
                    10753 => "Sprites/Default",
                    10703 => "Legacy Shaders/Diffuse",
                    46 => "Standard",
                    45 => "Standard (Specular setup)",
                    10750 => "Unlit/Transparent",
                    10755 => "Unlit/Color",
                    10752 => "Unlit/Texture",
                    _ => $"Builtin/{r.FileId}",
                };
            var path = db.PathOf(r.Guid);
            if (path == null) return $"Missing/{r.Guid}";
            if (path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        var t = line.TrimStart();
                        if (!t.StartsWith("Shader", StringComparison.Ordinal)) continue;
                        int a = t.IndexOf('"'), b = a >= 0 ? t.IndexOf('"', a + 1) : -1;
                        if (a >= 0 && b > a) return t.Substring(a + 1, b - a - 1);
                    }
                }
                catch (IOException) { }
            }
            // Shader Graphs are addressed as "Shader Graphs/<file name>".
            return "Shader Graphs/" + Path.GetFileNameWithoutExtension(path);
        }
    }
}
