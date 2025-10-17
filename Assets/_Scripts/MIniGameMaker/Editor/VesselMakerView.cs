using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tools.MiniGameMaker
{
    public sealed class VesselMakerView : IToolView
    {
        public void DrawGUI(object subTab, ColorThemeSO theme)
        {
            GUILayout.Space(2);
            switch (subTab)
            {
                case var _ when subTab.ToString() == "Overview":
                    EditorGUILayout.LabelField("Vessel Overview", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Define core identity: class, role, size, FX profile.", MessageType.None);
                    break;

                case var _ when subTab.ToString() == "Config":
                    EditorGUILayout.LabelField("Vessel Config", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Assign ScriptableObjects and prefabs here. (Coming next)", MessageType.Info);
                    break;

                case var _ when subTab.ToString() == "Validate":
                    EditorGUILayout.LabelField("Vessel Validation", EditorStyles.boldLabel);
                    if (GUILayout.Button("Run Checks"))
                        EditorUtility.DisplayDialog("Validation", "Validators will run here.", "OK");
                    break;

                case var _ when subTab.ToString() == "Utilities":
                    EditorGUILayout.LabelField("Vessel Utilities", EditorStyles.boldLabel);
                    if (GUILayout.Button("Open Folder"))
                        EditorUtility.RevealInFinder(Application.dataPath);
                    break;
            }
        }
    }
}
