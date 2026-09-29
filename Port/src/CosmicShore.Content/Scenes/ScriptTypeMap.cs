using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CosmicShore.Content.Scenes
{
    /// <summary>
    /// Resolves a serialized <c>m_Script</c> guid to the port's .NET type. First-party
    /// scripts resolve through their .cs file in <c>Assets/</c> (class name = file name,
    /// namespace read from the file — exactly Unity's MonoScript rule). Package scripts
    /// (uGUI, TextMeshPro, Input System, Netcode…) live outside <c>Assets/</c>, so their
    /// stable guids are mapped to the engine's replacements by name here.
    /// </summary>
    public sealed class ScriptTypeMap
    {
        /// <summary>Package script guid → type simple name (resolved against the engine/game assemblies).</summary>
        public static readonly IReadOnlyDictionary<string, string> PackageScripts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // com.unity.ugui
            ["fe87c0e1cc204ed48ad3b37840f39efc"] = "Image",
            ["1344c3c82d62a2a41a3576d8abb8e3ea"] = "RawImage",
            ["5f7201a12d95ffc409449d95f23cf332"] = "Text",
            ["4e29b1a8efbd4b44bb3f3716e73f07ff"] = "Button",
            ["9085046f02f69544eb97fd06b6048fe2"] = "Toggle",
            ["2fafe2cfe61f6974895a912c3755e8f1"] = "ToggleGroup",
            ["67db9e8f0e2ae9c40bc1e2b64352a6b4"] = "Slider",
            ["2a4db7a114972834c8e4117be1d82ba3"] = "Scrollbar",
            ["1aa08ab6e0800fa44ae55d278d1423e3"] = "ScrollRect",
            ["31a19414c41e5ae4aae2af33fee712f6"] = "Mask",
            ["3312d7739989d2b4e91e6319e9a96d76"] = "RectMask2D",
            ["30649d3a9faa99c48a7b1166b86bf2a0"] = "HorizontalLayoutGroup",
            ["59f8146938fff824cb5fd77236b75775"] = "VerticalLayoutGroup",
            ["8a8695521f0d02e499659fee002a26c2"] = "GridLayoutGroup",
            ["306cc8c2b49d7114eaa3623786fc2126"] = "LayoutElement",
            ["3245ec927659c4140ac4f8d17403cc18"] = "ContentSizeFitter",
            ["86710e43de46f6f4bac7c8e1a8b5e5d5"] = "AspectRatioFitter",
            ["0cd44c1031e13a943bb63640046fad76"] = "CanvasScaler",
            ["dc42784cf147c0c48a680349fa168899"] = "GraphicRaycaster",
            ["76c392e42b5098c458856cdf6ecaaaa1"] = "EventSystem",
            ["4f231c4fb786f3946a6b90b886c48677"] = "StandaloneInputModule",
            ["d0b148fe25e99eb48b9724523833bab1"] = "EventTrigger",
            ["cfabb0440166ab443bba8876756fdfa9"] = "Shadow",
            ["e19747de3f5aca642ab2be37e372fb86"] = "Outline",
            // com.unity.textmeshpro / ugui 2.x TMP
            ["f4688fdb7df04437aeb418b961361dc5"] = "TextMeshProUGUI",
            ["9541d86e2fd84c1d9990edf0852d74ab"] = "TextMeshPro",
            ["2da0c512f12947e489f739169773d7ca"] = "TMP_InputField",
            ["7b743370ac3e4ec2a1668f5455a8ef8a"] = "TMP_Dropdown",
            // com.unity.inputsystem — the port's UI input module stands in.
            ["01614664b831546d2ae94a42149d80ac"] = "InputSystemUIInputModule",
            // com.unity.netcode.gameobjects
            ["d5a57f767e5e46a458fc5d3c628d0cbb"] = "NetworkObject",
            ["593a2fe42fa9d37498c96f9a383b6521"] = "NetworkManager",
            ["e651dbb3fbac04af2b8f5abf007ddc23"] = "NetworkPrefabsList",
            ["6960e84d07fb87f47956e7a81d71c4e6"] = "UnityTransport",
        };

        readonly AssetDatabase _db;
        readonly Dictionary<string, List<Type>> _byName = new(StringComparer.Ordinal);
        readonly Dictionary<string, Type> _byFullName = new(StringComparer.Ordinal);
        readonly Dictionary<string, Type> _cache = new(StringComparer.Ordinal);

        /// <summary>Guids that resolved to no type — reported once per scene load.</summary>
        public readonly HashSet<string> Unresolved = new(StringComparer.Ordinal);

        public ScriptTypeMap(AssetDatabase db, IEnumerable<Assembly> assemblies)
        {
            _db = db;
            foreach (var asm in assemblies.Distinct())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t.IsGenericTypeDefinition || t.IsNested && !t.IsNestedPublic) continue;
                    if (!_byName.TryGetValue(t.Name, out var list)) _byName[t.Name] = list = new List<Type>();
                    list.Add(t);
                    if (t.FullName != null) _byFullName.TryAdd(t.FullName, t);
                }
            }
        }

        static readonly Regex NamespaceRx = new(@"^\s*namespace\s+([\w\.]+)", RegexOptions.Multiline);

        public Type Resolve(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            if (_cache.TryGetValue(guid, out var cached)) return cached;

            Type result = null;
            if (PackageScripts.TryGetValue(guid, out var packageName))
            {
                result = PickComponent(packageName, preferNamespacePrefix: "CosmicShore.Engine");
            }
            else
            {
                var path = _db.PathOf(guid);
                if (path != null && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    string className = Path.GetFileNameWithoutExtension(path);
                    string ns = null;
                    try
                    {
                        var m = NamespaceRx.Match(File.ReadAllText(path));
                        if (m.Success) ns = m.Groups[1].Value;
                    }
                    catch (IOException) { }
                    if (ns != null && _byFullName.TryGetValue(ns + "." + className, out var exact)) result = exact;
                    else result = PickComponent(className, ns);
                }
            }

            if (result == null) Unresolved.Add(guid);
            _cache[guid] = result;
            return result;
        }

        Type PickComponent(string name, string preferNamespacePrefix)
        {
            if (!_byName.TryGetValue(name, out var list) || list.Count == 0) return null;
            if (preferNamespacePrefix != null)
                foreach (var t in list)
                    if (t.Namespace != null && t.Namespace.StartsWith(preferNamespacePrefix, StringComparison.Ordinal)) return t;
            foreach (var t in list)
                if (typeof(CosmicShore.Engine.Object).IsAssignableFrom(t)) return t;
            return list[0];
        }

        /// <summary>Built-in (non-MonoBehaviour) component class IDs the port models.</summary>
        public Type BuiltIn(int classId) => classId switch
        {
            20 => Find("Camera"),
            23 => Find("MeshRenderer"),
            33 => Find("MeshFilter"),
            54 => Find("Rigidbody"),
            64 => Find("MeshCollider"),
            65 => Find("BoxCollider"),
            82 => Find("AudioSource"),
            96 => Find("TrailRenderer"),
            108 => Find("Light"),
            120 => Find("LineRenderer"),
            135 => Find("SphereCollider"),
            136 => Find("CapsuleCollider"),
            137 => Find("SkinnedMeshRenderer"),
            223 => Find("Canvas"),
            225 => Find("CanvasGroup"),
            328 => Find("VideoPlayer"),
            _ => null,
        };

        Type Find(string name) => PickComponent(name, "CosmicShore.Engine");
    }
}
