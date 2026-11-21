using CosmicShore.UI;
using UnityEditor;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Inspector for <see cref="KitButton"/> and <see cref="KitTile"/>. Unity's ButtonEditor is
    /// registered for every Button subclass and draws only Selectable + onClick, which would hide
    /// the kit's own fields; this replaces it with the default inspector minus the colour-tint /
    /// sprite-swap / animation transition fields - the kit forces the transition to None and owns
    /// every state visual itself, so those fields would only invite edits that do nothing.
    /// </summary>
    [CustomEditor(typeof(KitSelectableBase), true)]
    [CanEditMultipleObjects]
    public class KitSelectableEditor : UnityEditor.Editor
    {
        private static readonly string[] Hidden =
        {
            "m_Transition", "m_Colors", "m_SpriteState", "m_AnimationTriggers", "m_TargetGraphic",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, Hidden);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
