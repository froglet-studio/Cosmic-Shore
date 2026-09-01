using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The swordfish flagship's in-editor safety net (Docs/ECOSYSTEM.md §42).
    ///
    /// <para>The prefab is GENERATED (Tools/Build/author_swordfish_fauna.py) and nests the parts
    /// FBX. Every binding into that FBX - which bone a prism mount hangs under, which renderer a
    /// Spindle fades, which Animator gets the controller - is a fileID Unity assigns at import by
    /// hashing, an algorithm the generator cannot reproduce, so the generator PINS its ids through
    /// the importer's <c>internalIDToNameTable</c>. That mechanism is Unity's own (it is how ids
    /// survive a rename), but it is the one step no script outside the editor can verify.</para>
    ///
    /// <para><b>Validate</b> loads the prefab and checks every binding resolved. <b>Rebind</b> is
    /// the repair if they did not: it reads the ids Unity actually assigned, rewrites the prefab's
    /// references (pinned id -> real id, and the derived stripped ids with them), and records the
    /// real ids in <c>Tools/Build/swordfish_fbx_ids.json</c> so the generator reproduces them from
    /// then on. Rebind WRITES the prefab, so the ship contract applies (Docs/TOOLING.md).</para>
    /// </summary>
    public class SwordfishFlagshipTool : EditorWindow
    {
        const string ToolName = "Swordfish Flagship";
        const string PrefabPath = "Assets/_Prefabs/FloraAndFauna/SwordfishFauna.prefab";
        const string FbxPath = "Assets/_Models/Fauna/SwordFish_A_Parts.fbx";
        const string IdsJsonPath = "Tools/Build/swordfish_fbx_ids.json";
        const int ExpectedParts = 8;
        const int ExpectedPrisms = 12;
        const int ExpectedNeedles = 3;

        static readonly string[] PartNames =
        {
            "Bill", "Trunk", "Sail", "AnalFin", "PectoralL", "PectoralR", "TailUpper", "TailLower",
        };

        readonly FrogletToolShipContext _ship = new(ToolName)
        {
            CommitType = "fix",
            CommitScope = "ecology",
            Validate = () => RunValidation(log: false),
        };

        Vector2 _scroll;
        FrogletToolValidation? _last;

        [MenuItem("FrogletTools/Ecology/Swordfish Flagship")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 3,
            Description = "Validate the generated swordfish prefab's bindings into its parts FBX; rebind to Unity's real ids if the pinned table was not honoured.")]
        public static void Open() => GetWindow<SwordfishFlagshipTool>("Swordfish Flagship");

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Swordfish Flagship",
                "Generated prefab + nested parts FBX. Validate after any import of either; Rebind only if Validate fails on ids.",
                FrogletEditorPalette.Cyan);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (GUILayout.Button("Validate bindings", GUILayout.Height(28)))
                _last = RunValidation(log: true);

            if (_last.HasValue)
            {
                var v = _last.Value;
                FrogletEditorPalette.StatusPill(GUILayoutUtility.GetRect(90f, 22f), v.Passed ? "PASS" : "FAIL",
                    v.Passed ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);
                EditorGUILayout.HelpBox(v.Summary, v.Passed ? MessageType.Info : MessageType.Warning);
                foreach (var p in v.Problems) EditorGUILayout.LabelField("• " + p, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Rebind rewrites the prefab so every nested-FBX reference points at the ids Unity actually " +
                "imported, and writes those ids to " + IdsJsonPath + " for the generator. Run it only when " +
                "Validate reports unresolved bindings.", MessageType.None);
            if (GUILayout.Button("Rebind prefab to imported FBX ids", GUILayout.Height(24)))
                Rebind();

            EditorGUILayout.Space(12);
            FrogletToolShipPanel.Draw(_ship, this);
            EditorGUILayout.EndScrollView();
        }

        // ── Validate ──────────────────────────────────────────────────────────

        static FrogletToolValidation RunValidation(bool log)
        {
            var problems = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (!root) { problems.Add($"prefab not found at {PrefabPath}"); }
                else
                {
                    var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (renderers.Length != ExpectedParts)
                        problems.Add($"{renderers.Length} skinned parts, expected {ExpectedParts} (the parts FBX did not import as eight bone-parts)");
                    foreach (var r in renderers)
                        if (!r.sharedMaterial) problems.Add($"{r.name}: no material (externalObjects remap to SpindleMaterial missing)");

                    var spindles = root.GetComponentsInChildren<Spindle>(true);
                    if (spindles.Length != ExpectedParts)
                        problems.Add($"{spindles.Length} spindles, expected {ExpectedParts} (added components did not land on the part GameObjects)");
                    foreach (var s in spindles)
                    {
                        if (!s.RenderedObject) problems.Add($"Spindle on {s.gameObject.name}: RenderedObject unresolved");
                        else if (s.RenderedObject.gameObject != s.gameObject)
                            problems.Add($"Spindle on {s.gameObject.name}: renders {s.RenderedObject.name} (BodyMaterial swaps need the renderer on the spindle's own object)");
                    }

                    var prisms = root.GetComponentsInChildren<HealthPrism>(true);
                    if (prisms.Length != ExpectedPrisms) problems.Add($"{prisms.Length} body prisms, expected {ExpectedPrisms}");
                    int needles = 0;
                    foreach (var hp in prisms)
                    {
                        if (hp.name.StartsWith("DangerBlock")) needles++;
                        var so = new SerializedObject(hp);
                        if (!so.FindProperty("spindle").objectReferenceValue)
                            problems.Add($"{hp.name}: spindle reference unresolved");
                        var mount = hp.transform.parent;
                        var bone = mount ? mount.parent : null;
                        if (!mount || !mount.name.EndsWith("Prisms"))
                            problems.Add($"{hp.name}: not under a prism mount");
                        else if (!bone || !bone.name.StartsWith("Bone"))
                            problems.Add($"{hp.name}: mount {mount.name} is not under a bone (it detached from the FBX - pinned bone ids not honoured)");
                    }
                    if (needles != ExpectedNeedles) problems.Add($"{needles} danger needles on the bill, expected {ExpectedNeedles}");

                    var mounts = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("Prisms")).ToList();
                    foreach (var name in PartNames)
                        if (!mounts.Any(m => m.name == name + "Prisms")) problems.Add($"mount {name}Prisms missing");

                    var animator = root.GetComponentInChildren<Animator>(true);
                    if (!animator) problems.Add("no Animator on the nested model");
                    else if (!animator.runtimeAnimatorController) problems.Add("Animator has no controller (the pinned Animator id was not honoured; the driver assigns one at runtime, but the override should hold)");

                    if (!root.GetComponent<SwordfishFauna>()) problems.Add("root has no SwordfishFauna");
                    if (!root.GetComponent<SwordfishChargeDriver>()) problems.Add("root has no SwordfishChargeDriver");
                    if (!root.GetComponentInChildren<Crystal>(true)) problems.Add("no authored heart (Crystal) - the lifeform invariant would be met by runtime provisioning only");
                }
            }
            finally
            {
                if (root) PrefabUtility.UnloadPrefabContents(root);
            }

            problems.AddRange(CheckPinnedIds());

            var summary = problems.Count == 0
                ? "Every binding into the parts FBX resolved. Play-test the strike in Menu_Main freestyle."
                : $"{problems.Count} problem(s). If ids are the cause, Rebind fixes the prefab and records the real ids.";
            if (log)
            {
                Debug.Log($"[{ToolName}] {(problems.Count == 0 ? "✅" : "❌")} {summary}");
                foreach (var p in problems) Debug.LogWarning($"[{ToolName}] ❌ {p}");
            }
            return problems.Count == 0
                ? FrogletToolValidation.Pass(summary)
                : FrogletToolValidation.Fail(summary, problems);
        }

        // ── Ids ───────────────────────────────────────────────────────────────

        /// <summary>Mirror of the generator's <c>fid("fbx/{cls}/{name}")</c>.</summary>
        static long PinnedId(int cls, string name)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes($"CosmicShore/fileID/fbx/{cls}/{name}"));
            ulong h = 0;
            for (int i = 0; i < 8; i++) h = (h << 8) | bytes[i];
            return (long)((h & 0x3FFFFFFFFFFFFFFFUL) | 0x1000000000000000UL);
        }

        /// <summary>(class, name key) -> the id Unity actually assigned on import.</summary>
        static Dictionary<(int, string), long> ImportedIds()
        {
            var ids = new Dictionary<(int, string), long>();
            var main = AssetDatabase.LoadMainAssetAtPath(FbxPath) as GameObject;
            if (!main) return ids;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (!obj || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long id)) continue;
                switch (obj)
                {
                    case GameObject go:
                        ids[(1, go == main ? "//RootNode" : go.name)] = id; break;
                    case Transform t:
                        ids[(4, t.gameObject == main ? "//RootNode" : t.name)] = id; break;
                    case SkinnedMeshRenderer smr:
                        ids[(137, smr.name)] = id; break;
                    case Animator a:
                        ids[(95, a.gameObject == main ? "//RootNode" : a.name)] = id; break;
                }
            }
            return ids;
        }

        static IEnumerable<(int cls, string name)> PinnedKeys()
        {
            yield return (1, "//RootNode"); yield return (4, "//RootNode"); yield return (95, "//RootNode");
            foreach (var b in new[] { "Armature.024", "Bone", "Bone.001", "Bone.002", "Bone.003", "Bone.004", "Bone.005", "Bone.006" })
            { yield return (1, b); yield return (4, b); }
            foreach (var p in PartNames)
            { yield return (1, "Swordfish_" + p); yield return (4, "Swordfish_" + p); yield return (137, "Swordfish_" + p); }
        }

        static List<string> CheckPinnedIds()
        {
            var problems = new List<string>();
            var imported = ImportedIds();
            if (imported.Count == 0) { problems.Add($"parts FBX not imported at {FbxPath}"); return problems; }
            var overrides = LoadIdOverrides();
            foreach (var (cls, name) in PinnedKeys())
            {
                long expected = overrides.TryGetValue($"{cls}/{name}", out var o) ? o : PinnedId(cls, name);
                if (!imported.TryGetValue((cls, name), out var actual))
                    problems.Add($"FBX has no imported object for class {cls} '{name}'");
                else if (actual != expected)
                    problems.Add($"class {cls} '{name}': prefab expects id {expected}, Unity imported {actual} - Rebind");
            }
            return problems;
        }

        static Dictionary<string, long> LoadIdOverrides()
        {
            var path = Path.Combine(FrogletGit.RepoRoot, IdsJsonPath);
            var result = new Dictionary<string, long>();
            if (!File.Exists(path)) return result;
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"([^\"]+)\"\\s*:\\s*(-?\\d+)"))
                result[m.Groups[1].Value] = long.Parse(m.Groups[2].Value);
            return result;
        }

        // ── Rebind ────────────────────────────────────────────────────────────

        static void Rebind()
        {
            var imported = ImportedIds();
            if (imported.Count == 0) { Debug.LogError($"[{ToolName}] parts FBX not imported; nothing to rebind to."); return; }
            var overrides = LoadIdOverrides();
            var fbxGuid = AssetDatabase.AssetPathToGUID(FbxPath);
            var fullPrefab = Path.Combine(FrogletGit.RepoRoot, PrefabPath);
            var text = File.ReadAllText(fullPrefab);

            var inst = Regex.Match(text,
                @"--- !u!1001 &(\d+)\nPrefabInstance:(?:(?!--- !u!).)*?m_SourcePrefab: \{fileID: 100100000, guid: " + fbxGuid,
                RegexOptions.Singleline);
            if (!inst.Success) { Debug.LogError($"[{ToolName}] prefab has no instance of {FbxPath}"); return; }
            long instance = long.Parse(inst.Groups[1].Value);

            var pairs = new List<(long from, long to)>();
            var json = new StringBuilder("{\n");
            foreach (var (cls, name) in PinnedKeys())
            {
                long expected = overrides.TryGetValue($"{cls}/{name}", out var o) ? o : PinnedId(cls, name);
                if (!imported.TryGetValue((cls, name), out var actual)) continue;
                json.Append($"  \"{cls}/{name}\": {actual},\n");
                if (actual == expected) continue;
                pairs.Add((expected, actual));
                pairs.Add((expected ^ instance, actual ^ instance));   // the stripped docs and their references
            }
            var jsonText = json.ToString().TrimEnd('\n', ',') + "\n}\n";

            int replaced = 0;
            foreach (var (from, to) in pairs)
            {
                var re = new Regex($@"(?<![\d-]){from}(?!\d)");
                text = re.Replace(text, m => { replaced++; return to.ToString(); });
            }

            var jsonPath = Path.Combine(FrogletGit.RepoRoot, IdsJsonPath);
            File.WriteAllText(jsonPath, jsonText);
            FrogletToolChangeLedger.Record(ToolName, IdsJsonPath);
            if (replaced > 0)
            {
                File.WriteAllText(fullPrefab, text);
                FrogletToolChangeLedger.Record(ToolName, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log($"[{ToolName}] rebind: {pairs.Count / 2} id(s) differed, {replaced} reference(s) rewritten; real ids recorded in {IdsJsonPath}. " +
                      "Re-run Tools/Build/author_swordfish_fauna.py --check (it reads the json) and Validate again.");
        }
    }
}
