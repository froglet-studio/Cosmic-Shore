using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Content.Scenes;

namespace CosmicShore.Content.Editing
{
    /// <summary>A script that can be added as a component: its asset guid, its .NET type, and how Unity names it.</summary>
    public sealed record ScriptInfo(string Guid, Type Type, string Path, string EditorClassIdentifier)
    {
        public string Name => Type?.Name ?? System.IO.Path.GetFileNameWithoutExtension(Path ?? "");

        /// <summary>The class name Unity records for it (after "Assembly::").</summary>
        public string UnityFullName => EditorClassIdentifier is { } ec && ec.Contains("::") ? ec[(ec.IndexOf("::", StringComparison.Ordinal) + 2)..] : null;

        /// <summary>
        /// True when the port runs a STAND-IN for this script (package and plugin scripts: uGUI,
        /// TMP, Netcode, FMOD, SOAP …): the .NET type is not the original class, so its fields are
        /// not the original's serialized layout. A project script is its own type.
        /// </summary>
        public bool IsStandIn => Type != null && (Type.Assembly == typeof(CosmicShore.Engine.GameObject).Assembly || UnityFullName != Type.FullName);
    }

    /// <summary>
    /// Finds scripts by name for "add component": first-party scripts by their .cs file under
    /// Assets/ (Unity's rule — the class is the one named after the file), package scripts
    /// (uGUI, TextMeshPro, Netcode …) by the stable guids <see cref="ScriptTypeMap"/> knows.
    /// </summary>
    public sealed class ScriptCatalog
    {
        readonly AssetDatabase _db;
        readonly ScriptTypeMap _types;
        static readonly Regex NamespaceRx = new(@"^\s*namespace\s+([\w\.]+)", RegexOptions.Multiline);

        public ScriptCatalog(AssetDatabase db, ScriptTypeMap types) { _db = db; _types = types; }

        /// <summary>
        /// Every script matching <paramref name="name"/> — a class name (<c>Crystal</c>) or a
        /// full name (<c>CosmicShore.Gameplay.Crystal</c>). More than one result means the
        /// name is ambiguous and the caller should ask for the full name.
        /// </summary>
        public List<ScriptInfo> Find(string name)
        {
            string simple = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
            string ns = name.Contains('.') ? name[..name.LastIndexOf('.')] : null;
            var found = new List<ScriptInfo>();
            foreach (var path in _db.AllAssetPaths)
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                // Unity matches a script's class to its file name ignoring case (MinigameHUDView.cs
                // holds MiniGameHUDView, and Unity accepts it).
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), simple, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsEditorOnly(path)) continue;
                string fileNs = ReadNamespace(path);
                if (ns != null && fileNs != ns) continue;
                string guid = _db.GuidOf(path);
                var type = _types.Resolve(guid);
                string className = type?.Name ?? Path.GetFileNameWithoutExtension(path);
                string fullName = fileNs != null ? fileNs + "." + className : className;
                found.Add(new ScriptInfo(guid, type, path, AssemblyOf(path) + "::" + fullName));
            }
            foreach (var kv in ScriptTypeMap.PackageScripts)
            {
                if (kv.Value != simple) continue;
                var (asm, pkgNs) = PackageAssembly(kv.Key);
                if (ns != null && pkgNs != ns) continue;
                found.Add(new ScriptInfo(kv.Key, _types.Resolve(kv.Key), null, asm != null ? $"{asm}::{pkgNs}.{simple}" : ""));
            }
            return found;
        }

        public ScriptInfo FromGuid(string guid)
        {
            var path = _db.PathOf(guid);
            var type = _types.Resolve(guid);
            if (path != null)
            {
                string simple = type?.Name ?? Path.GetFileNameWithoutExtension(path);
                string ns = ReadNamespace(path);
                return new ScriptInfo(guid, type, path, AssemblyOf(path) + "::" + (ns != null ? ns + "." : "") + simple);
            }
            var (asm, pkgNs) = PackageAssembly(guid);
            return new ScriptInfo(guid, type, null, asm != null && type != null ? $"{asm}::{pkgNs}.{type.Name}" : "");
        }

        static bool IsEditorOnly(string path)
            => path.Replace('\\', '/').Split('/').Any(p => p == "Editor");

        static string ReadNamespace(string path)
        {
            try { var m = NamespaceRx.Match(File.ReadAllText(path)); return m.Success ? m.Groups[1].Value : null; }
            catch (IOException) { return null; }
        }

        /// <summary>The assembly Unity compiles a script into: the nearest .asmdef above it, else Assembly-CSharp.</summary>
        string AssemblyOf(string path)
        {
            for (var dir = Path.GetDirectoryName(path); dir != null && dir.Length >= _db.AssetsRoot.Length; dir = Path.GetDirectoryName(dir))
            {
                var asmdef = Directory.EnumerateFiles(dir, "*.asmdef").FirstOrDefault();
                if (asmdef != null)
                {
                    var m = Regex.Match(File.ReadAllText(asmdef), "\"name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            return "Assembly-CSharp";
        }

        /// <summary>The (assembly, namespace) Unity reports for a package script, from the guid's package.</summary>
        static (string asm, string ns) PackageAssembly(string guid)
        {
            if (!ScriptTypeMap.PackageScripts.TryGetValue(guid, out var name)) return (null, null);
            return name switch
            {
                "TextMeshProUGUI" or "TextMeshPro" or "TMP_InputField" or "TMP_Dropdown" => ("Unity.TextMeshPro", "TMPro"),
                "EventSystem" or "StandaloneInputModule" or "EventTrigger" => ("UnityEngine.UI", "UnityEngine.EventSystems"),
                "InputSystemUIInputModule" => ("Unity.InputSystem", "UnityEngine.InputSystem.UI"),
                "NetworkObject" or "NetworkManager" or "NetworkPrefabsList" or "NetworkTransform" or "NetworkRigidbody" => ("Unity.Netcode.Runtime", "Unity.Netcode"),
                "UnityTransport" => ("Unity.Netcode.Runtime", "Unity.Netcode.Transports.UTP"),
                "UniversalAdditionalCameraData" or "UniversalRenderPipelineAsset" => ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal"),
                "Volume" or "VolumeProfile" or "ProbeVolumesOptions" => ("Unity.RenderPipelines.Core.Runtime", "UnityEngine.Rendering"),
                _ when IsUgui(name) => ("UnityEngine.UI", "UnityEngine.UI"),
                _ => ("Unity.RenderPipelines.Universal.Runtime", "UnityEngine.Rendering.Universal"),
            };
        }

        static bool IsUgui(string name) => name is "Image" or "RawImage" or "Text" or "Button" or "Toggle" or "ToggleGroup" or "Slider"
            or "Scrollbar" or "ScrollRect" or "Mask" or "RectMask2D" or "HorizontalLayoutGroup" or "VerticalLayoutGroup"
            or "GridLayoutGroup" or "LayoutElement" or "ContentSizeFitter" or "AspectRatioFitter" or "CanvasScaler"
            or "GraphicRaycaster" or "Shadow" or "Outline";
    }
}
