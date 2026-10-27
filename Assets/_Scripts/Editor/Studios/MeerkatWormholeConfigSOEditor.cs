using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// The Meerkat wormhole config's Inspector: Unity's own inspector of every field (the artifact's keys, its labels as
    /// tooltips), with two buttons on top that swap the tunnel's whole look in one click (Yash, 2026-10-27: "two options so
    /// that I can swap between my look versus his look"):
    /// <list type="bullet">
    /// <item><b>Yash's look</b>: the round-3 reticle (thin cyan spindles and the lime path) before RT, and the spindle bridge
    /// and glass after RT (<c>reticleLook</c> Spindles, <c>formedLook</c> Bridge).</item>
    /// <item><b>Garrett's look</b> (the Tube Lens Lab, <c>cece/lucid-brown-ykcmhu</c>): the reticle drawn as the bridge before
    /// RT, then only bent space, the contour field, after it (<c>reticleLook</c> Bridge, <c>formedLook</c> ContourField).</item>
    /// </list>
    /// The buttons set those two switches and nothing else, so each look keeps every number it was given. The executor and
    /// the view read the config every frame, so in Play mode the swap shows at once. WRITER of those two fields only.
    /// </summary>
    [CustomEditor(typeof(MeerkatWormholeConfigSO))]
    public sealed class MeerkatWormholeConfigSOEditor : UnityEditor.Editor
    {
        SerializedProperty _reticleLook, _formedLook;

        void OnEnable()
        {
            _reticleLook = serializedObject.FindProperty("reticleLook");
            _formedLook = serializedObject.FindProperty("formedLook");
        }

        /// <summary>Which named look two switch values make: "Yash's look", "Garrett's look", or null for a mix. Pure, tested.</summary>
        public static string LookName(MeerkatReticleLook reticle, MeerkatFormedLook formed) =>
            reticle == MeerkatReticleLook.Spindles && formed == MeerkatFormedLook.Bridge ? "Yash's look" :
            reticle == MeerkatReticleLook.Bridge && formed == MeerkatFormedLook.ContourField ? "Garrett's look" : null;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (_reticleLook != null && _formedLook != null)
            {
                var reticle = (MeerkatReticleLook)_reticleLook.intValue;
                var formed = (MeerkatFormedLook)_formedLook.intValue;
                string now = LookName(reticle, formed);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Tunnel look", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("YASH'S LOOK", FrogletEditorPalette.Cyan, 150f, 26f,
                            "Before RT: the thin cyan spindle reticle and its lime path. After RT: the spindle bridge and glass.",
                            outline: now != "Yash's look"))
                        Set(MeerkatReticleLook.Spindles, MeerkatFormedLook.Bridge);
                    if (FrogletEditorPalette.ColorButton("GARRETT'S LOOK", FrogletEditorPalette.Violet, 150f, 26f,
                            "Before RT: the reticle drawn as the bridge, in your domain colour. After RT: nothing drawn, only space " +
                            "bent in a thin shell on the wall (the contour field, the Tube Lens Lab).",
                            outline: now != "Garrett's look"))
                        Set(MeerkatReticleLook.Bridge, MeerkatFormedLook.ContourField);
                }
                EditorGUILayout.LabelField(now != null
                    ? $"Now: {now}." + (Application.isPlaying ? " It shows at once in Play mode." : "")
                    : $"Now: a mix (reticle {reticle}, formed tunnel {formed}).", EditorStyles.miniLabel);
                EditorGUILayout.Space(6f);
            }
            DrawDefaultInspector();
        }

        void Set(MeerkatReticleLook reticle, MeerkatFormedLook formed)
        {
            _reticleLook.intValue = (int)reticle;
            _formedLook.intValue = (int)formed;
            serializedObject.ApplyModifiedProperties();   // undoable, and marks the asset dirty
        }
    }
}
