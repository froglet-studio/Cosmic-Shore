using System;
using UnityEditor;
using System.Reflection;

namespace StarWriter.Core
{
    [CustomEditor(typeof(Ship))]
    public class ShipEditor : Editor
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
                        || shipScript.ControlOverrides.Contains(showIfControlOverrideAttribute.ControlOverride)
                        || shipScript.LevelEffects.Contains(showIfControlOverrideAttribute.LevelEffect)
                        || shipScript.crystalImpactEffects.Contains(showIfControlOverrideAttribute.CrystalImpactEffect)
                        || shipScript.fullSpeedStraightEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.rightStickEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.leftStickEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.flipEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.idleEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.minimumSpeedStraightEffects.Contains(showIfControlOverrideAttribute.Action))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}