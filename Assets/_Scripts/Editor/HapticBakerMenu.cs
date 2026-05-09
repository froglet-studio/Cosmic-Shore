using System.Collections.Generic;
using System.IO;
using CosmicShore.Core;
using CosmicShore.Utility;
using Lofelt.NiceVibrations;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Menu items that drive HapticClipBaker. Two entry points:
    ///  - Project-view context: bake selected AudioClip(s) to mirrored .haptic files.
    ///  - Tools menu: bake every AudioClip field on the AudioSystem prefab, then
    ///    auto-wire the resulting HapticClip to a sibling field named
    ///    "[BaseName]HapticClip" (e.g. BlockDestroyAudioClip → BlockDestroyHapticClip).
    /// </summary>
    public static class HapticBakerMenu
    {
        const string AudioRoot = "Assets/_Audio/Sounds/";
        const string HapticRoot = "Assets/_Haptics/";
        const string AudioSystemPrefabPath = "Assets/_Prefabs/CORE/AudioSystem.prefab";

        [MenuItem("Assets/Cosmic Shore/Bake Haptic Clip from AudioClip", true)]
        static bool ValidateBakeSelected() => Selection.GetFiltered<AudioClip>(SelectionMode.Assets).Length > 0;

        [MenuItem("Assets/Cosmic Shore/Bake Haptic Clip from AudioClip", false, 2000)]
        static void BakeSelected()
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            int baked = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    EditorUtility.DisplayProgressBar("Baking Haptics", clip.name, (float)i / clips.Length);
                    string outPath = ResolveHapticOutputPath(clip);
                    if (HapticClipBaker.BakeAudioClipToHapticFile(clip, outPath, HapticEnvelopeAnalysis.Settings.Default) != null)
                        baked++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[HapticBaker] Baked {baked}/{clips.Length} haptic clips.");
        }

        [MenuItem("Tools/Cosmic Shore/Bake Haptics for AudioSystem", false, 100)]
        static void BakeAudioSystem()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSystemPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[HapticBaker] AudioSystem prefab not found at {AudioSystemPrefabPath}");
                return;
            }

            var instanceRoot = PrefabUtility.LoadPrefabContents(AudioSystemPrefabPath);
            try
            {
                var audioSystem = instanceRoot.GetComponent<AudioSystem>();
                if (audioSystem == null)
                {
                    Debug.LogError("[HapticBaker] AudioSystem component not found on prefab root.");
                    return;
                }

                var so = new SerializedObject(audioSystem);
                var pairs = CollectAudioHapticPairs(so);
                Debug.Log($"[HapticBaker] Found {pairs.Count} AudioClip fields on AudioSystem.");

                int baked = 0, wired = 0;
                try
                {
                    AssetDatabase.StartAssetEditing();
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        var pair = pairs[i];
                        EditorUtility.DisplayProgressBar("Baking AudioSystem Haptics", pair.audioFieldName, (float)i / pairs.Count);

                        if (pair.audio == null)
                        {
                            Debug.LogWarning($"[HapticBaker] {pair.audioFieldName} has no AudioClip wired — skipping.");
                            continue;
                        }

                        string outPath = ResolveHapticOutputPath(pair.audio);
                        var bakedPath = HapticClipBaker.BakeAudioClipToHapticFile(pair.audio, outPath, HapticEnvelopeAnalysis.Settings.Default);
                        if (bakedPath == null) continue;
                        baked++;
                        pair.bakedAssetPath = bakedPath;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();
                }

                AssetDatabase.Refresh();

                so.Update();
                foreach (var pair in pairs)
                {
                    if (pair.hapticProperty == null || string.IsNullOrEmpty(pair.bakedAssetPath)) continue;
                    var hapticClip = AssetDatabase.LoadAssetAtPath<HapticClip>(pair.bakedAssetPath);
                    if (hapticClip == null)
                    {
                        Debug.LogWarning($"[HapticBaker] Baked file {pair.bakedAssetPath} did not import as HapticClip.");
                        continue;
                    }
                    pair.hapticProperty.objectReferenceValue = hapticClip;
                    wired++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(instanceRoot, AudioSystemPrefabPath);
                Debug.Log($"[HapticBaker] Baked {baked} clips, wired {wired} HapticClip references on AudioSystem.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instanceRoot);
            }
        }

        class FieldPair
        {
            public string audioFieldName;
            public AudioClip audio;
            public SerializedProperty hapticProperty;
            public string bakedAssetPath;
        }

        static List<FieldPair> CollectAudioHapticPairs(SerializedObject so)
        {
            var result = new List<FieldPair>();
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.name.Length < 2) continue;
                if (!prop.name.EndsWith("AudioClip")) continue;

                var audio = prop.objectReferenceValue as AudioClip;
                string baseName = prop.name.Substring(0, prop.name.Length - "AudioClip".Length);
                string hapticFieldName = baseName + "HapticClip";
                var hapticProp = so.FindProperty(hapticFieldName);

                result.Add(new FieldPair
                {
                    audioFieldName = prop.name,
                    audio = audio,
                    hapticProperty = hapticProp,
                });
            }
            return result;
        }

        /// <summary>
        /// Mirrors the audio asset's path under Assets/_Haptics/. e.g.
        ///   Assets/_Audio/Sounds/Menu/OptionClick.wav  ->  Assets/_Haptics/Menu/OptionClick.haptic
        /// Clips outside the audio root land flat in Assets/_Haptics/.
        /// </summary>
        public static string ResolveHapticOutputPath(AudioClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip).Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(assetPath);

            if (assetPath.StartsWith(AudioRoot))
            {
                string relative = assetPath.Substring(AudioRoot.Length);
                string dir = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";
                return string.IsNullOrEmpty(dir)
                    ? $"{HapticRoot}{fileName}.haptic"
                    : $"{HapticRoot}{dir}/{fileName}.haptic";
            }
            return $"{HapticRoot}{fileName}.haptic";
        }
    }
}
