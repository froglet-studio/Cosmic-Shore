using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// The Meerkat wormhole config's Inspector: Unity's own inspector of every field (the artifact's keys, its labels as
    /// tooltips), with two buttons on top that swap the formed tunnel's look in one click (Yash, 2026-10-27: "two options so
    /// that I can swap between my look versus his look"). Since round 9 the reticle is always the wormhole's wireframe (one
    /// geometry, the bridge's), so the looks differ only after RT:
    /// <list type="bullet">
    /// <item><b>Yash's look</b>: the spindle bridge and its glass (<c>formedLook</c> Bridge).</item>
    /// <item><b>Garrett's look</b> (the Tube Lens Lab, <c>cece/lucid-brown-ykcmhu</c>): only bent space, the contour field
    /// (<c>formedLook</c> ContourField).</item>
    /// </list>
    /// The buttons set that one switch and nothing else, so each look keeps every number it was given. The view reads the
    /// config every frame, so in Play mode the swap shows at once. WRITER of that one field only.
    /// </summary>
    [CustomEditor(typeof(MeerkatWormholeConfigSO))]
    public sealed class MeerkatWormholeConfigSOEditor : UnityEditor.Editor
    {
        SerializedProperty _formedLook;

        void OnEnable() => _formedLook = serializedObject.FindProperty("formedLook");

        /// <summary>Which named look a formed-tunnel switch makes: "Yash's look" or "Garrett's look". Pure, tested.</summary>
        public static string LookName(MeerkatFormedLook formed) =>
            formed == MeerkatFormedLook.ContourField ? "Garrett's look" : "Yash's look";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (_formedLook != null)
            {
                var formed = (MeerkatFormedLook)_formedLook.intValue;
                string now = LookName(formed);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Tunnel look", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("YASH'S LOOK", FrogletEditorPalette.Cyan, 150f, 26f,
                            "After RT: the spindle bridge and its glass (your reticle is always its wireframe). In the PC look " +
                            "(MeerkatTubeLook HighQuality) the lens also bends space, with a visible outer layer of water-glass.",
                            outline: now != "Yash's look"))
                        Set(MeerkatFormedLook.Bridge);
                    if (FrogletEditorPalette.ColorButton("GARRETT'S LOOK", FrogletEditorPalette.Violet, 150f, 26f,
                            "After RT: nothing drawn, only space bent in a thin shell on the wall (the contour field, the Tube Lens " +
                            "Lab). Your reticle is the same wireframe either way. In the PC look (MeerkatTubeLook HighQuality) the " +
                            "outer layer is only a whisper: the lensing sells it.",
                            outline: now != "Garrett's look"))
                        Set(MeerkatFormedLook.ContourField);
                }
                EditorGUILayout.LabelField($"Now: {now}." + (Application.isPlaying ? " It shows at once in Play mode." : ""),
                    EditorStyles.miniLabel);
                EditorGUILayout.Space(6f);
            }
            DrawDefaultInspector();
        }

        void Set(MeerkatFormedLook formed)
        {
            _formedLook.intValue = (int)formed;
            serializedObject.ApplyModifiedProperties();   // undoable, and marks the asset dirty
        }
    }
}
