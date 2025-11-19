using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

public class ElementalFloatEditor : EditorWindow
{
    static List<(GameObject, Component, FieldInfo, SerializedObject, SerializedProperty)> results = new List<(GameObject, Component, FieldInfo, SerializedObject, SerializedProperty)>();
    Vector2 scrollPosition;

    [MenuItem("FrogletTools/ElementalFloat Editor")]
    public static void ShowWindow()
    {
        GetWindow<ElementalFloatEditor>("ElementalFloat Editor");
        FindElementalFloatInstances();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Refresh"))
        {
            FindElementalFloatInstances();
        }

        if (results.Count > 0)
        {
            GUILayout.Label($"Found {results.Count} instances of ElementalFloat:");

            // Begin scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height - 50));

            foreach (var result in results)
            {
                EditorGUILayout.Space();
                EditorGUILayout.SelectableLabel($"{result.Item1.name} ({result.Item2.GetType().Name}.{result.Item3.Name})", EditorStyles.boldLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                SerializedObject serializedObject = result.Item4;
                SerializedProperty elementalFloatProperty = result.Item5;

                serializedObject.Update();

                EditorGUILayout.PropertyField(elementalFloatProperty.FindPropertyRelative("Enabled"), new GUIContent("Enabled"));
                EditorGUILayout.PropertyField(elementalFloatProperty.FindPropertyRelative("Value"), new GUIContent("Value"));

                if (elementalFloatProperty.FindPropertyRelative("Enabled").boolValue)
                {
                    EditorGUILayout.PropertyField(elementalFloatProperty.FindPropertyRelative("Min"), new GUIContent("Min"));
                    EditorGUILayout.PropertyField(elementalFloatProperty.FindPropertyRelative("Max"), new GUIContent("Max"));
                    EditorGUILayout.PropertyField(elementalFloatProperty.FindPropertyRelative("element"), new GUIContent("Element"));
                }

                serializedObject.ApplyModifiedProperties();
            }

            // End scroll view
            EditorGUILayout.EndScrollView();
        }
    }

    static void FindElementalFloatInstances()
    {
        results.Clear();
        var selectedGameObject = Selection.activeGameObject;

        if (selectedGameObject == null)
        {
            Debug.LogWarning("No GameObject selected!");
            return;
        }

        TraverseGameObjectHierarchy(selectedGameObject);
    }

    static void TraverseGameObjectHierarchy(GameObject gameObject)
    {
        var components = gameObject.GetComponents<MonoBehaviour>();

        foreach (var component in components)
        {
            var fields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(ElementalFloat))
                {
                    var serializedObject = new SerializedObject(component);
                    var elementalFloatProperty = serializedObject.FindProperty(field.Name);
                    results.Add((gameObject, component, field, serializedObject, elementalFloatProperty));
                }
            }
        }

        // Traverse all child objects
        foreach (Transform child in gameObject.transform)
        {
            TraverseGameObjectHierarchy(child.gameObject);
        }
    }
}