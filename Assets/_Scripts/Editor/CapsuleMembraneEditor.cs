using CosmicShore.Game;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Inspector for <see cref="CapsuleMembrane"/>. Adds a one-click bake that
    /// precomputes the membrane wobble into a <see cref="CapsuleMembraneAnimationSO"/>
    /// preset, plus a status box that flags a missing or stale bake.
    /// </summary>
    [CustomEditor(typeof(CapsuleMembrane))]
    public class CapsuleMembraneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var membrane = (CapsuleMembrane)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Animation Bake", EditorStyles.boldLabel);

            DrawStatus(membrane);

            EditorGUILayout.Space(4);

            string buttonLabel = membrane.AnimationPreset != null
                ? "Re-Bake Animation Preset"
                : "Bake Animation Preset...";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(28)))
                Bake(membrane);

            if (Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Bake while not in Play Mode - the result is picked up on the next play.",
                    MessageType.None);
        }

        static void DrawStatus(CapsuleMembrane membrane)
        {
            var preset = membrane.AnimationPreset;

            if (preset == null)
            {
                EditorGUILayout.HelpBox(
                    "No baked preset assigned. The membrane runs the EXPENSIVE per-frame fallback " +
                    "(Perlin noise + quaternion + matrix math for every capsule, every frame). " +
                    "Click below to bake a cheap playback preset.",
                    MessageType.Warning);
                return;
            }

            if (!preset.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "Assigned preset has no baked data yet. Click below to bake.",
                    MessageType.Warning);
                return;
            }

            if (!preset.Signature.Matches(membrane.CurrentSignature))
            {
                EditorGUILayout.HelpBox(
                    "Baked preset is STALE - membrane settings changed since the last bake. " +
                    "The membrane falls back to per-frame computation until you re-bake.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Preset up to date - {preset.FrameCount} frames × {preset.CapsuleCount} capsules, " +
                $"{preset.LoopDuration:0.#}s loop. Membrane plays back cheaply.",
                MessageType.Info);
        }

        void Bake(CapsuleMembrane membrane)
        {
            var preset = membrane.AnimationPreset;
            bool created = false;

            if (preset == null)
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Capsule Membrane Animation",
                    "CapsuleMembraneAnimation", "asset",
                    "Choose where to save the baked membrane animation preset.");
                if (string.IsNullOrEmpty(path))
                    return;

                preset = ScriptableObject.CreateInstance<CapsuleMembraneAnimationSO>();
                AssetDatabase.CreateAsset(preset, path);
                created = true;
            }

            try
            {
                membrane.BakeAnimationInto(preset, p =>
                    EditorUtility.DisplayProgressBar(
                        "Baking Capsule Membrane", "Computing animation frames...", p));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.SetDirty(preset);

            if (created)
            {
                serializedObject.Update();
                serializedObject.FindProperty("animationPreset").objectReferenceValue = preset;
                serializedObject.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[CapsuleMembrane] Baked {preset.FrameCount} frames for {preset.CapsuleCount} capsules " +
                $"into '{AssetDatabase.GetAssetPath(preset)}'.", preset);
        }
    }
}
