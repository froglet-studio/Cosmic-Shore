using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;

/// <summary>
/// Editor window to copy selected components from one GameObject to another.
/// </summary>
public class ComponentCopierWindow : EditorWindow
{
    private GameObject copyFrom;
    private GameObject copyTo;
    private Vector2 scrollPos;
    private List<Component> componentList = new List<Component>();
    private List<bool> selectedFlags = new List<bool>();

    [MenuItem(GameSetting.TopMenuName + "/Component Copier")]
    public static void ShowWindow()
    {
        GetWindow<ComponentCopierWindow>("Component Copier");
    }

    private void OnGUI()
    {
        GUILayout.Label("Component Copier", EditorStyles.boldLabel);

        // Object fields
        EditorGUI.BeginChangeCheck();
        copyFrom = EditorGUILayout.ObjectField("Copy From", copyFrom, typeof(GameObject), true) as GameObject;
        copyTo   = EditorGUILayout.ObjectField("Copy To",   copyTo,   typeof(GameObject), true) as GameObject;
        if (EditorGUI.EndChangeCheck())
        {
            RefreshComponentList();
        }

        // Manual refresh button
        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Components List"))
        {
            RefreshComponentList();
        }

        // Components selection
        if (copyFrom != null)
        {
            GUILayout.Label("Select Components to Copy:", EditorStyles.label);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            for (int i = 0; i < componentList.Count; i++)
            {
                selectedFlags[i] = EditorGUILayout.ToggleLeft(
                    componentList[i].GetType().Name,
                    selectedFlags[i]
                );
            }
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a 'Copy From' GameObject to list components.", MessageType.Info);
        }

        // Copy button
        EditorGUI.BeginDisabledGroup(copyFrom == null || copyTo == null);
        if (GUILayout.Button("Copy Selected Components"))
        {
            CopySelectedComponents();
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// Populate the component list from the source GameObject (excluding Transform).
    /// </summary>
    private void RefreshComponentList()
    {
        componentList.Clear();
        selectedFlags.Clear();

        if (copyFrom == null)
            return;

        foreach (var comp in copyFrom.GetComponents<Component>())
        {
            if (comp == null || comp is Transform)
                continue;

            componentList.Add(comp);
            selectedFlags.Add(false);
        }

        // Reset scroll position
        scrollPos = Vector2.zero;
    }

    /// <summary>
    /// Copy and paste selected components from source to destination.
    /// </summary>
    private void CopySelectedComponents()
    {
        Undo.RegisterCompleteObjectUndo(copyTo, "Copy Components");

        int copiedCount = 0;
        for (int i = 0; i < componentList.Count; i++)
        {
            if (!selectedFlags[i])
                continue;

            var sourceComp = componentList[i];
            ComponentUtility.CopyComponent(sourceComp);

            // Paste values into existing component or add new one
            var destComp = copyTo.GetComponent(sourceComp.GetType());
            if (destComp != null)
            {
                ComponentUtility.PasteComponentValues(destComp);
            }
            else
            {
                ComponentUtility.PasteComponentAsNew(copyTo);
            }

            copiedCount++;
        }

        Debug.Log($"Copied {copiedCount} components from '{copyFrom.name}' to '{copyTo.name}'.");
    }
}
