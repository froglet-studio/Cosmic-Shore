using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Editing;
using CosmicShore.Content.Yaml;

namespace CosmicShore.AssetTool
{
    /// <summary>
    /// <c>cs-asset new-asset TYPE Assets/DIR/NAME.asset</c> (E2c): a new data file of a
    /// ScriptableObject type, as Unity's Create menu writes it - one MonoBehaviour document at
    /// fileID 11400000 with no GameObject, m_Name the file's name, every serialized field at the
    /// script's own default (<see cref="ComponentSerializer"/>, the same writer `add` uses, whose
    /// layout `cs-asset schema` measures against Unity's), and a NativeFormatImporter .meta with a
    /// fresh GUID.
    /// </summary>
    static partial class Program
    {
        static int NewAssetCommand(List<string> pos, Dictionary<string, string> opts)
        {
            var r = NewAsset(Scripts.Db, Scripts.Catalog, pos[1], pos[2], opts.ContainsKey("dry-run"));
            return r is string text ? Print(text) : EditorData.Write(r);
            static int Print(string t) { Console.Write(t); return 0; }
        }

        /// <summary>Writes the file (or returns its text on a dry run); the result says what was written.</summary>
        internal static object NewAsset(AssetDatabase db, ScriptCatalog catalog, string typeName, string path, bool dryRun = false)
        {
            var full = Path.GetFullPath(Path.Combine(db.ProjectRoot, path));
            if (!full.StartsWith(db.AssetsRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("the file must be under Assets/");
            if (!full.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("a data file ends in .asset");
            if (File.Exists(full) || File.Exists(full + ".meta")) throw new ArgumentException($"{db.ProjectRelative(full)} already exists");
            var matches = catalog.Find(typeName).Where(s => s.Type != null && typeof(CosmicShore.Engine.ScriptableObject).IsAssignableFrom(s.Type) && !s.Type.IsAbstract).ToList();
            if (matches.Count == 0) throw new ArgumentException($"no concrete ScriptableObject script named {typeName}");
            if (matches.Count > 1) throw new ArgumentException($"{typeName} is ambiguous: {string.Join(", ", matches.Select(m => m.Type.FullName))}");
            var script = matches[0];

            var body = new YMap();
            foreach (var kv in ComponentSerializer.MonoBehaviourBody(script.Type, 0, script.Guid, script.EditorClassIdentifier).Entries)
                body.Add(kv.Key, kv.Value);
            body.Set("m_Name", new YScalar(Path.GetFileNameWithoutExtension(full)));
            var file = UnityYamlFile.CreateEmpty();
            file.Documents.Add(new UnityDocument { ClassId = 114, FileId = 11400000, TypeName = "MonoBehaviour", Body = body });
            string text = file.Write();
            if (dryRun) return text;

            var dir = Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(dir);
            EnsureFolderMetas(db, dir);
            string guid = NewGuid(db);
            File.WriteAllText(full, text);
            File.WriteAllText(full + ".meta", $"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
            db.Register(full, guid);
            return new { path = db.ProjectRelative(full), guid, type = script.Type.FullName, script = script.Path, fields = body.Entries.Count() - 10 };
        }
    }
}
