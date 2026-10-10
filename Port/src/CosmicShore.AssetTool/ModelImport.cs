using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Content;
using CosmicShore.Content.Scenes;
using CosmicShore.Engine;

namespace CosmicShore.AssetTool
{
    /// <summary>
    /// <c>cs-asset model-import SRC --to Assets/DIR [--name NAME] [--prefab]</c> (E3a): drops a new
    /// FBX (or .blend/.ma/.mb) into the project the way Unity's first import would leave it - the
    /// file, plus a <c>.meta</c> with a fresh GUID and the ModelImporter settings the project's
    /// existing models share (the donor is the cleanest meta at the newest importer version, its
    /// tallied settings set to the majority value, every model-specific table emptied). It then
    /// imports the model through Prisma's own importer, and with <c>--prefab</c> writes
    /// <c>NAME.prefab</c> nesting it and loads that prefab through the engine as the proof.
    /// </summary>
    static partial class Program
    {
        /// <summary>Settings tallied across the project's model metas; the majority value wins.</summary>
        static readonly string[] MajorityKeys =
        {
            "globalScale", "useFileScale", "importAnimation", "importBlendShapes", "materialImportMode", "meshCompression",
            "isReadable", "weldVertices", "importCameras", "importLights", "animationType", "normalImportMode",
            "tangentImportMode", "bakeAxisConversion", "preserveHierarchy", "avatarSetup", "importVisibility", "addColliders",
        };

        /// <summary>Lists and maps a meta carries for ONE model, emptied for a new one.</summary>
        static readonly (string Key, string Empty)[] ModelSpecific =
        {
            ("internalIDToNameTable", "[]"), ("externalObjects", "{}"), ("clipAnimations", "[]"), ("extraExposedTransformPaths", "[]"),
            ("extraUserProperties", "[]"), ("referencedClips", "[]"), ("human", "[]"), ("skeleton", "[]"), ("lODScreenPercentages", "[]"),
            ("lastHumanDescriptionAvatarSource", "{instanceID: 0}"), ("fileIDToRecycleName", "{}"),
        };

        /// <summary>The .meta text a new model gets in this project (GUID line left as <c>guid: {GUID}</c>).</summary>
        internal static string ModelMetaTemplate(AssetDatabase db)
        {
            var metas = Directory.EnumerateFiles(db.AssetsRoot, "*.meta", SearchOption.AllDirectories)
                .Where(m => AssetDatabase.IsModelPath(m[..^5]) && m[..^5].EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Select(m => File.ReadAllText(m).Replace("\r\n", "\n")).ToList();
            if (metas.Count == 0) throw new InvalidOperationException("no model .meta in the project to take settings from");
            int Version(string t) => Regex.Match(t, @"ModelImporter:\n  serializedVersion: (\d+)") is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;
            int newest = metas.Max(Version);
            var donor = metas.Where(t => Version(t) == newest).OrderBy(t => t.Length).First();
            foreach (var key in MajorityKeys)
            {
                var values = metas.Select(t => Regex.Match(t, @"\n(\s+)" + key + @": ([^\n]*)")).Where(m => m.Success).Select(m => m.Groups[2].Value).ToList();
                if (values.Count == 0) continue;
                var majority = values.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
                donor = Regex.Replace(donor, @"(\n\s+" + key + @": )[^\n]*", "${1}" + majority.Replace("$", "$$"));
            }
            foreach (var (key, empty) in ModelSpecific) donor = EmptyBlock(donor, key, empty);
            donor = Regex.Replace(donor, @"\nguid: [0-9a-f]{32}", "\nguid: {GUID}");
            donor = Regex.Replace(donor, @"(\n\s+userData: )[^\n]*", "$1");
            return donor;
        }

        /// <summary>Replaces <c>key:</c> and everything indented under it with <c>key: EMPTY</c>.</summary>
        static string EmptyBlock(string text, string key, string empty)
        {
            var lines = text.Split('\n').ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var m = Regex.Match(lines[i], @"^(\s*)" + Regex.Escape(key) + @":(.*)$");
                if (!m.Success) continue;
                string indent = m.Groups[1].Value;
                int j = i + 1;
                // A block value: deeper-indented lines, or "- " items at the same indent.
                while (j < lines.Count && lines[j].Length > 0 &&
                       (Indent(lines[j]) > indent.Length || (Indent(lines[j]) == indent.Length && lines[j].TrimStart().StartsWith("- "))))
                    j++;
                lines[i] = indent + key + ": " + empty;
                lines.RemoveRange(i + 1, j - i - 1);
            }
            return string.Join("\n", lines);
        }

        static int Indent(string line) => line.Length - line.TrimStart().Length;

        static int ModelImportCommand(List<string> pos, Dictionary<string, string> opts)
        {
            var result = ModelImport(Scripts.Db, pos[1], opts);
            return result is string meta ? Print(meta) : EditorData.Write(result);
            static int Print(string text) { Console.Write(text); return 0; }
        }

        /// <summary>The import itself (the command's body, testable on any project): the result JSON object, or the meta text on --dry-run.</summary>
        internal static object ModelImport(AssetDatabase db, string source, Dictionary<string, string> opts)
        {
            var src = Path.GetFullPath(source);
            if (!File.Exists(src)) throw new ArgumentException($"no file at {source}");
            if (!AssetDatabase.IsModelPath(src)) throw new ArgumentException($"{Path.GetFileName(src)} is not a model Unity imports (.fbx, .blend, .ma, .mb)");
            var toRel = opts.TryGetValue("to", out var to) ? to : throw new ArgumentException("--to Assets/<folder> is needed");
            var dir = Path.GetFullPath(Path.Combine(db.ProjectRoot, toRel));
            if (!dir.StartsWith(db.AssetsRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--to must be a folder under Assets/");
            var name = opts.TryGetValue("name", out var n) && n.Length > 0 ? n : Path.GetFileNameWithoutExtension(src);
            var dest = Path.Combine(dir, name + Path.GetExtension(src));
            if (File.Exists(dest) || File.Exists(dest + ".meta")) throw new ArgumentException($"{db.ProjectRelative(dest)} already exists");
            bool dry = opts.ContainsKey("dry-run");

            string guid = NewGuid(db);
            string meta = ModelMetaTemplate(db).Replace("{GUID}", guid);
            if (dry) return meta;
            Directory.CreateDirectory(dir);
            EnsureFolderMetas(db, dir);
            File.Copy(src, dest);
            File.WriteAllText(dest + ".meta", meta);
            db.Register(dest, guid);

            var model = db.LoadModel(guid);
            if (model == null) throw new InvalidOperationException($"{db.ProjectRelative(dest)} was written but Amoebius's importer could not read it");
            int meshes = model.Meshes.Count(m => m.Mesh != null), tris = model.Meshes.Sum(m => (m.Mesh?.triangles.Length ?? 0) / 3);

            string prefabRel = null; int renderers = 0;
            if (opts.ContainsKey("prefab"))
            {
                var prefab = Path.Combine(dir, name + ".prefab");
                if (File.Exists(prefab)) throw new ArgumentException($"{db.ProjectRelative(prefab)} already exists");
                string pguid = NewGuid(db);
                File.WriteAllText(prefab, EmptyPrefab(name));
                File.WriteAllText(prefab + ".meta", $"fileFormatVersion: 2\nguid: {pguid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
                db.Register(prefab, pguid);
                var s = new Session(prefab, db);
                var root = ResolveObject(s, name, new());
                s.Pie.Instantiate(db.ProjectRelative(dest), root, name + " model");
                File.WriteAllText(prefab, s.File.Write());
                prefabRel = db.ProjectRelative(prefab);
                renderers = LoadedRenderers(db, prefab);
                if (renderers == 0) throw new InvalidOperationException($"{prefabRel} loads with no renderer drawing the model");
            }
            return new { path = db.ProjectRelative(dest), guid, meshes, triangles = tris, prefab = prefabRel, renderers };
        }

        /// <summary>The prefab loaded through the engine's instantiator: how many renderers draw a mesh.</summary>
        static int LoadedRenderers(AssetDatabase db, string prefab)
        {
            using var loop = new GameLoop("model-import");
            var types = new ScriptTypeMap(db, new[] { typeof(GameObject).Assembly });
            var scene = new SceneInstantiator(new AssetLoader(db, types), new InstantiateOptions
            {
                IncludeScript = t => t.Namespace?.StartsWith("CosmicShore.Engine") == true,
                Activate = true,
            })
                .Instantiate(PrefabGraph.Build(db, db.LoadPath(prefab)));
            return scene.Roots.Sum(r => r.GetComponentsInChildren<MeshRenderer>(true).Count(mr => mr.GetComponent<MeshFilter>()?.sharedMesh != null)
                                      + r.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(smr => smr.sharedMesh != null));
        }

        static string NewGuid(AssetDatabase db)
        {
            string g;
            do g = Guid.NewGuid().ToString("N"); while (db.PathOf(g) != null);
            return g;
        }

        /// <summary>A new folder needs its own .meta, as Unity writes on import.</summary>
        static void EnsureFolderMetas(AssetDatabase db, string dir)
        {
            for (var d = dir; d.Length > db.AssetsRoot.Length; d = Path.GetDirectoryName(d)!)
            {
                if (File.Exists(d + ".meta")) continue;
                var g = NewGuid(db);
                File.WriteAllText(d + ".meta", $"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
                db.Register(d, g);
            }
        }

        /// <summary>A prefab with one root GameObject and its Transform, as Unity serializes an empty prefab.</summary>
        static string EmptyPrefab(string name) => $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1154230183473010293
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 7329483209573491106}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &7329483209573491106
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1154230183473010293}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
".Replace("\r\n", "\n");
    }
}
