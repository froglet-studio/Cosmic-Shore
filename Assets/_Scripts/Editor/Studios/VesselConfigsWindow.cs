using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// One vessel's config assets, opened from its card on the Vessel Studio home (the card's CONFIGS button): every asset
    /// that holds the vessel's numbers, each opened in Unity's OWN Inspector (Yash, 2026-10-27: "if I choose my vessel, I will
    /// get its configuration ... at least I should have the inspector panel of all the configuration parameters that I have
    /// in the artifact ... every vessel their own").
    ///
    /// <para><b>Not a tuner</b> (/vessel-studio D33, D41). Nothing here redraws a parameter: the Inspector shows the asset's
    /// own fields, which carry the artifact's keys as names and its labels as tooltips. The list is found, never kept by
    /// hand: the vessel's studio map (<c>Tools/Build/studio_to_unity/&lt;id&gt;.json</c>, the assets the artifact's numbers
    /// are cloned into), then its prefab (<c>Assets/_Prefabs/Spacevessels/&lt;Id&gt;.prefab</c>), the vessel's own
    /// ScriptableObjects that prefab uses, and its element map.</para>
    ///
    /// <para><b>The artifact still owns the mapped numbers.</b> Executors read their config every frame, so a change in Play
    /// mode shows at once, and a ScriptableObject keeps it after Play mode ends. The next clone
    /// (<c>/artifact-to-unity</c>) writes the artifact's values back, and <c>studio_to_unity.py --check</c> lists anything
    /// that differs: to keep a value, set it in the artifact and Record it. READER: this window writes nothing.</para>
    /// </summary>
    public sealed class VesselConfigsWindow : EditorWindow
    {
        const string MapDir = "Tools/Build/studio_to_unity";
        const string PrefabDir = "Assets/_Prefabs/Spacevessels";
        const string ActionsDir = "Assets/_SO_Assets/VesselActions/";
        const string ElementMapDir = "Assets/Resources/ElementalAbilityMaps";

        /// <summary>One asset in the list: its path, and how many artifact keys the studio map clones into it.</summary>
        public readonly struct Entry
        {
            public readonly string Path;
            public readonly int ArtifactKeys;
            public Entry(string path, int artifactKeys) { Path = path; ArtifactKeys = artifactKeys; }
        }

        [Serializable] sealed class Map { public string vessel; public Row[] rows; }
        [Serializable] sealed class Row { public string key, asset, field; }

        string _studioId, _studioName, _prefab;
        List<Entry> _entries = new();
        Vector2 _scroll;

        /// <summary>Opens the configs of studio <paramref name="studioId"/> (its studios.json id), named <paramref name="studioName"/>.</summary>
        public static void Open(string studioId, string studioName)
        {
            var w = GetWindow<VesselConfigsWindow>();
            w.titleContent = new GUIContent($"{studioName} configs");
            w.minSize = new Vector2(420f, 260f);
            w.Load(studioId, studioName);
            w.Show();
            w.Focus();
        }

        void Load(string studioId, string studioName)
        {
            _studioId = studioId;
            _studioName = studioName;
            _prefab = FindPrefab(studioId);
            var map = ReadMap(studioId);
            var deps = _prefab != null ? AssetDatabase.GetDependencies(_prefab, true) : Array.Empty<string>();
            string vesselName = _prefab != null ? System.IO.Path.GetFileNameWithoutExtension(_prefab) : map?.vessel ?? studioName;
            _entries = Collect(vesselName, map?.rows?.Select(r => r.asset), deps, _prefab,
                File.Exists(System.IO.Path.Combine(Root, ElementMapDir, vesselName + ".asset")) ? $"{ElementMapDir}/{vesselName}.asset" : null,
                IsScriptableObject);
        }

        static string Root => System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".";

        /// <summary>The vessel's prefab: <c>Spacevessels/&lt;name&gt;.prefab</c> whose name is the studio id, ignoring case and spaces.</summary>
        static string FindPrefab(string studioId)
        {
            string want = Squash(studioId);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetDirectoryName(p)?.Replace('\\', '/') == PrefabDir &&
                    Squash(System.IO.Path.GetFileNameWithoutExtension(p)) == want) return p;
            }
            return null;
        }

        static Map ReadMap(string studioId)
        {
            string p = System.IO.Path.Combine(Root, MapDir, studioId + ".json");
            if (!File.Exists(p)) return null;
            try { return JsonUtility.FromJson<Map>(File.ReadAllText(p)); }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[VesselConfigs] {MapDir}/{studioId}.json does not parse: {e.Message}");
                return null;
            }
        }

        static bool IsScriptableObject(string path) =>
            typeof(ScriptableObject).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path));

        static string Squash(string s) => (s ?? "").Replace(" ", "").ToLowerInvariant();

        /// <summary>
        /// The list, in order: the assets the studio map clones the artifact into (most artifact keys first), the prefab,
        /// the vessel's own ScriptableObjects among the prefab's dependencies (under <c>VesselActions/&lt;vessel&gt;*/</c>, or
        /// named after the vessel), then its element map. No path twice. Pure, tested.
        /// </summary>
        public static List<Entry> Collect(string vesselName, IEnumerable<string> mapAssets, IEnumerable<string> prefabDeps,
            string prefab, string elementMap, Func<string, bool> isScriptableObject)
        {
            var list = new List<Entry>();
            var seen = new HashSet<string>();
            foreach (var g in (mapAssets ?? Enumerable.Empty<string>()).Where(a => !string.IsNullOrEmpty(a))
                     .GroupBy(a => a).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                if (seen.Add(g.Key)) list.Add(new Entry(g.Key, g.Count()));
            if (!string.IsNullOrEmpty(prefab) && seen.Add(prefab)) list.Add(new Entry(prefab, 0));
            string v = Squash(vesselName);
            foreach (var d in (prefabDeps ?? Enumerable.Empty<string>()).OrderBy(d => d, StringComparer.Ordinal))
            {
                if (!d.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || seen.Contains(d)) continue;
                bool own = IsVesselsOwn(d, v);
                if (own && isScriptableObject(d) && seen.Add(d)) list.Add(new Entry(d, 0));
            }
            if (!string.IsNullOrEmpty(elementMap) && seen.Add(elementMap)) list.Add(new Entry(elementMap, 0));
            return list;
        }

        /// <summary>An asset is the vessel's own when it sits under <c>VesselActions/&lt;vessel…&gt;/</c> or its file is named after it.</summary>
        public static bool IsVesselsOwn(string path, string squashedVessel)
        {
            if (string.IsNullOrEmpty(squashedVessel)) return false;
            string p = path.Replace('\\', '/');
            int at = p.IndexOf(ActionsDir, StringComparison.Ordinal);
            if (at >= 0)
            {
                string folder = p.Substring(at + ActionsDir.Length).Split('/')[0];
                if (Squash(folder).StartsWith(squashedVessel, StringComparison.Ordinal)) return true;
            }
            return Squash(System.IO.Path.GetFileNameWithoutExtension(p)).StartsWith(squashedVessel, StringComparison.Ordinal);
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner($"{_studioName} configs",
                "Every asset that holds this vessel's numbers, each in Unity's own Inspector.", FrogletEditorPalette.Gold);
            if (string.IsNullOrEmpty(_studioId))
            {
                EditorGUILayout.HelpBox("Open this from a vessel's card in FrogletTools ▸ Vessels ▸ Vessel Studio.", MessageType.Info);
                return;
            }
            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox($"{_studioName} has no vessel in Unity yet: its numbers live only in its studio page " +
                                        "(the Vessel Studio artifact). /artifact-to-unity brings a vessel into the game.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("INSPECT ALL", FrogletEditorPalette.Gold, 120f, 24f,
                        "Each asset in its own Inspector window"))
                    foreach (var e in _entries) Inspect(e.Path);
                if (FrogletEditorPalette.ColorButton("RELOAD", FrogletEditorPalette.Slate, 90f, 24f, "Find the list again", outline: true))
                    Load(_studioId, _studioName);
            }
            EditorGUILayout.Space(6f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var e in _entries)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(e.Path);
                    var icon = obj ? AssetPreview.GetMiniThumbnail(obj) : null;
                    string what = e.ArtifactKeys > 0 ? $"{e.ArtifactKeys} artifact keys" :
                        e.Path == _prefab ? "the vessel prefab" : e.Path.StartsWith(ElementMapDir) ? "element map" : "";
                    EditorGUILayout.LabelField(new GUIContent(System.IO.Path.GetFileNameWithoutExtension(e.Path), icon, e.Path),
                        new GUIContent(what), GUILayout.MinWidth(200f));
                    if (FrogletEditorPalette.ColorButton("PING", FrogletEditorPalette.Slate, 52f, 20f, "Show it in the Project window", outline: true) && obj)
                        EditorGUIUtility.PingObject(obj);
                    if (FrogletEditorPalette.ColorButton("INSPECT", FrogletEditorPalette.Gold, 72f, 20f, "Open it in its own Inspector window"))
                        Inspect(e.Path);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox(
                "Executors read their config every frame, so a change in Play mode shows at once, and the asset keeps it after Play " +
                "mode. The artifact owns the artifact keys: the next /artifact-to-unity clone writes its values back, and " +
                "studio_to_unity.py --check lists what differs. To keep a value, set it in the studio and Record it.", MessageType.None);
        }

        static void Inspect(string path)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj) EditorUtility.OpenPropertyEditor(obj);
        }
    }
}
