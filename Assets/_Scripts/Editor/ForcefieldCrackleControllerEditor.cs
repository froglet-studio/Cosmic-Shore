using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    [CustomEditor(typeof(ForcefieldCrackleController))]
    public class ForcefieldCrackleControllerEditor : UnityEditor.Editor
    {
        static readonly string[] s_DirectionLabels =
        {
            "+X (Right)", "-X (Left)",
            "+Y (Top)",   "-Y (Bottom)",
            "+Z (Front)", "-Z (Back)",
            "Random"
        };

        static readonly Vector3[] s_Directions =
        {
            Vector3.right,   Vector3.left,
            Vector3.up,      Vector3.down,
            Vector3.forward, Vector3.back,
        };

        int _selectedDirection = 6; // default to Random
        float _testDuration   = 0.6f;
        float _testIntensity  = 1.5f;
        float _testRadius     = 0.25f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (ForcefieldCrackleController)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test Impacts", EditorStyles.boldLabel);

            _selectedDirection = EditorGUILayout.Popup("Impact Direction", _selectedDirection, s_DirectionLabels);
            _testDuration  = EditorGUILayout.Slider("Duration", _testDuration, 0.1f, 3f);
            _testIntensity = EditorGUILayout.Slider("Intensity", _testIntensity, 0.1f, 5f);
            _testRadius    = EditorGUILayout.Slider("Radius", _testRadius, 0.05f, 1f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Test Impact"))
            {
                Vector3 dir = _selectedDirection < s_Directions.Length
                    ? s_Directions[_selectedDirection]
                    : Random.onUnitSphere;

                // Convert local direction to world point on the sphere surface
                float worldRadius = controller.transform.lossyScale.x * 0.5f;
                Vector3 worldPoint = controller.transform.TransformPoint(dir * 0.5f);

                controller.AddImpact(worldPoint, _testDuration, _testIntensity, _testRadius);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Add 6 Impacts"))
            {
                for (int i = 0; i < s_Directions.Length; i++)
                {
                    Vector3 worldPoint = controller.transform.TransformPoint(s_Directions[i] * 0.5f);
                    controller.AddImpact(worldPoint, _testDuration, _testIntensity, _testRadius);
                }
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear All"))
            {
                controller.ClearAllImpacts();
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying && controller.gameObject.activeInHierarchy)
            {
                EditorGUILayout.HelpBox(
                    "Adjust visual params above — changes apply immediately in Scene view. " +
                    "Use test impacts to preview the arc pattern without playing.",
                    MessageType.Info);
            }
        }
    }
}
