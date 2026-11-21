using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Editing
{
    /// <summary>A component body to add, and how it was obtained.</summary>
    public sealed record ComponentTemplate(int ClassId, string TypeName, YMap Body, int SampleCount, string Source);

    /// <summary>
    /// Templates for components whose serialized layout cannot be read off a C# type in this
    /// port: Unity's BUILT-IN components (BoxCollider, MeshRenderer, Rigidbody … — native
    /// classes with no script) and PACKAGE scripts (uGUI, TextMeshPro, Netcode — whose port
    /// stand-ins do not declare Unity's private fields).
    ///
    /// <para>A template is MEASURED from the project's own Unity-written scenes and prefabs:
    /// the most common field layout among every saved instance of that component (which is the
    /// layout this Unity version writes), and for each field the most common value (which, for a
    /// field most people never touch, is Unity's default). Local references are cleared, since
    /// they name objects in another file, and a short table pins the defaults that matter and
    /// that a mode could get wrong (a BoxCollider's size, an Image's sprite, a text's string).
    /// Files written by the repo's Python generators are not samples: they are not Unity's
    /// output.</para>
    /// </summary>
    public sealed class ComponentTemplates
    {
        readonly string _assetsRoot;
        readonly Dictionary<string, ComponentTemplate> _cache = new(StringComparer.Ordinal);
        List<string> _files;

        public ComponentTemplates(string assetsRoot) { _assetsRoot = assetsRoot; }

        /// <summary>Unity class IDs of built-in components, by type name (Unity's YAML class ID reference).</summary>
        public static readonly IReadOnlyDictionary<string, int> BuiltInClassIds = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Transform"] = 4, ["Camera"] = 20, ["MeshRenderer"] = 23, ["MeshFilter"] = 33, ["Rigidbody"] = 54,
            ["MeshCollider"] = 64, ["BoxCollider"] = 65, ["AudioListener"] = 81, ["AudioSource"] = 82, ["Animator"] = 95,
            ["TrailRenderer"] = 96, ["Light"] = 108, ["Animation"] = 111, ["LineRenderer"] = 120, ["SphereCollider"] = 135,
            ["CapsuleCollider"] = 136, ["SkinnedMeshRenderer"] = 137, ["ParticleSystem"] = 198, ["ParticleSystemRenderer"] = 199,
            ["LODGroup"] = 205, ["SortingGroup"] = 210, ["SpriteRenderer"] = 212, ["CanvasRenderer"] = 222, ["Canvas"] = 223,
            ["RectTransform"] = 224, ["CanvasGroup"] = 225, ["VideoPlayer"] = 328, ["Rigidbody2D"] = 50,
            ["CircleCollider2D"] = 58, ["BoxCollider2D"] = 61, ["PolygonCollider2D"] = 60,
        };

        /// <summary>Defaults pinned after measuring: a value a "most common in this project" vote could get wrong.</summary>
        static readonly Dictionary<string, (string key, string yaml)[]> Pinned = new(StringComparer.Ordinal)
        {
            ["BoxCollider"] = new[] { ("m_IsTrigger", "0"), ("m_Size", "{x: 1, y: 1, z: 1}"), ("m_Center", "{x: 0, y: 0, z: 0}") },
            ["SphereCollider"] = new[] { ("m_IsTrigger", "0"), ("m_Radius", "0.5"), ("m_Center", "{x: 0, y: 0, z: 0}") },
            ["CapsuleCollider"] = new[] { ("m_IsTrigger", "0"), ("m_Radius", "0.5"), ("m_Height", "2"), ("m_Direction", "1"), ("m_Center", "{x: 0, y: 0, z: 0}") },
            ["MeshCollider"] = new[] { ("m_IsTrigger", "0"), ("m_Convex", "0"), ("m_Mesh", "{fileID: 0}") },
            ["MeshFilter"] = new[] { ("m_Mesh", "{fileID: 0}") },
            ["MeshRenderer"] = new[] { ("m_Materials", "[]") },
            ["SkinnedMeshRenderer"] = new[] { ("m_Materials", "[]"), ("m_Mesh", "{fileID: 0}"), ("m_Bones", "[]") },
            ["Rigidbody"] = new[] { ("m_Mass", "1"), ("m_UseGravity", "1"), ("m_IsKinematic", "0") },
            ["CanvasGroup"] = new[] { ("m_Alpha", "1"), ("m_Interactable", "1"), ("m_BlocksRaycasts", "1"), ("m_IgnoreParentGroups", "0") },
            ["Image"] = new[] { ("m_Sprite", "{fileID: 0}"), ("m_Color", "{r: 1, g: 1, b: 1, a: 1}"), ("m_Type", "0"), ("m_FillAmount", "1") },
            ["RawImage"] = new[] { ("m_Texture", "{fileID: 0}"), ("m_Color", "{r: 1, g: 1, b: 1, a: 1}") },
            ["TextMeshProUGUI"] = new[] { ("m_text", "") },
            ["TextMeshPro"] = new[] { ("m_text", "") },
        };

        static readonly HashSet<string> HeaderKeys = new(StringComparer.Ordinal)
        { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject" };

        /// <summary>A built-in component's template, by class ID.</summary>
        public ComponentTemplate ForClass(int classId, string typeName) => Build(classId, null, typeName);

        /// <summary>A package script's template, by script guid.</summary>
        public ComponentTemplate ForScript(string scriptGuid, string typeName) => Build(114, scriptGuid, typeName);

        ComponentTemplate Build(int classId, string guid, string typeName)
        {
            string cacheKey = classId + ":" + guid;
            if (_cache.TryGetValue(cacheKey, out var hit)) return hit;

            _files ??= Directory.EnumerateFiles(_assetsRoot, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .ToList();
            string header = $"--- !u!{classId} &";
            var samples = new List<UnityDocument>();
            foreach (var f in _files)
            {
                string text = File.ReadAllText(f);
                if (!text.Contains(header) || (guid != null && !text.Contains(guid))) continue;
                if (text.Contains("m_EditorClassIdentifier:\n")) continue; // generator-written
                foreach (var d in UnityYaml.ParseDocuments(text))
                {
                    if (d.ClassId != classId || d.Stripped) continue;
                    if (guid != null && d.Body["m_Script"]?.Str("guid") != guid) continue;
                    samples.Add(d);
                }
            }
            if (samples.Count == 0) return _cache[cacheKey] = null;

            // The layout this Unity writes: the most common key sequence.
            var byLayout = samples.GroupBy(d => string.Join("\u0001", d.Body.Entries.Select(e => e.Key)))
                                  .OrderByDescending(g => g.Count()).First().ToList();
            var first = byLayout[0];
            var body = new YMap();
            foreach (var e in first.Body.Entries)
            {
                // For each field, the most common value among instances with this layout.
                var votes = byLayout.Select(d => d.Body[e.Key]).Where(v => v != null)
                                    .GroupBy(UnityYamlFile.FormatValue).OrderByDescending(g => g.Count()).First();
                var value = votes.First().Clone();
                if (HeaderKeys.Contains(e.Key)) value = YMap.Ref(0);
                else value = ClearLocalRefs(value);
                body.Add(e.Key, value);
            }
            if (Pinned.TryGetValue(typeName ?? first.TypeName, out var pins))
                foreach (var (k, yaml) in pins)
                    if (body.Has(k)) body.Set(k, UnityYaml.ParseValue(yaml));
            if (body.Has("m_Enabled")) body.Set("m_Enabled", new YScalar("1"));
            if (body.Has("m_Name") && classId == 114) body.Set("m_Name", new YScalar(""));

            var t = new ComponentTemplate(classId, first.TypeName, body, samples.Count,
                $"most common layout of {samples.Count} saved instance(s), {byLayout.Count} with that layout");
            return _cache[cacheKey] = t;
        }

        /// <summary>A reference to an object in the SAMPLE's file means nothing in another file.</summary>
        static YNode ClearLocalRefs(YNode n)
        {
            switch (n)
            {
                case YMap m when m.Flow && m.Has("fileID") && string.IsNullOrEmpty(m.Str("guid")):
                    return m.Long("fileID") == 0 ? m : YMap.Ref(0);
                case YMap m:
                    for (int i = 0; i < m.Entries.Count; i++)
                        m.SetAt(i, ClearLocalRefs(m.Entries[i].Value));
                    return m;
                case YSeq q:
                    for (int i = 0; i < q.List.Count; i++) q.List[i] = ClearLocalRefs(q.List[i]);
                    return q;
                default:
                    return n;
            }
        }
    }
}
