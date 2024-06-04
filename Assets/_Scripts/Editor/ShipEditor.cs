using System;
using UnityEditor;
using System.Reflection;
using CosmicShore.Core;

namespace CosmicShore.Editor
{
    [CustomEditor(typeof(Ship))]
    public class ShipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var shipScript = (Ship)target;

            SerializedProperty property = serializedObject.GetIterator();

            while (property.NextVisible(true))
            {
                FieldInfo fieldInfo = shipScript.GetType().GetField(property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                if (fieldInfo != null)
                {
                    ShowIfAttribute showIfControlOverrideAttribute = (ShowIfAttribute)Attribute.GetCustomAttribute(
                        fieldInfo,
                        typeof(ShowIfAttribute));

                    if (showIfControlOverrideAttribute == null 
                        || shipScript.crystalImpactEffects.Contains(showIfControlOverrideAttribute.CrystalImpactEffect))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}