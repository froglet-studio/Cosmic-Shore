#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [CustomEditor(typeof(CameraSettingsSO))]
    public class CameraSettingsSOEditor : Editor
    {
        SerializedProperty modeProp;
        SerializedProperty followOffsetProp;

        SerializedProperty dynamicMinProp, dynamicMaxProp;
        SerializedProperty followSmoothTimeProp, rotationSmoothTimeProp, disableSmoothingProp;

        SerializedProperty nearClipProp, farClipProp;

        SerializedProperty followTargetPosProp, fixedOffsetPosProp, orthoSizeProp;

        void OnEnable()
        {
            var so = serializedObject;
            modeProp               = so.FindProperty(nameof(CameraSettingsSO.mode));
            followOffsetProp       = so.FindProperty(nameof(CameraSettingsSO.followOffset));
            dynamicMinProp         = so.FindProperty(nameof(CameraSettingsSO.dynamicMinDistance));
            dynamicMaxProp         = so.FindProperty(nameof(CameraSettingsSO.dynamicMaxDistance));
            followSmoothTimeProp   = so.FindProperty(nameof(CameraSettingsSO.followSmoothTime));
            rotationSmoothTimeProp = so.FindProperty(nameof(CameraSettingsSO.rotationSmoothTime));
            disableSmoothingProp   = so.FindProperty(nameof(CameraSettingsSO.disableSmoothing));
            nearClipProp           = so.FindProperty(nameof(CameraSettingsSO.nearClipPlane));
            farClipProp            = so.FindProperty(nameof(CameraSettingsSO.farClipPlane));
            followTargetPosProp    = so.FindProperty(nameof(CameraSettingsSO.followTargetPosition));
            fixedOffsetPosProp     = so.FindProperty(nameof(CameraSettingsSO.fixedOffsetPosition));
            orthoSizeProp          = so.FindProperty(nameof(CameraSettingsSO.orthographicSize));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = (CameraSettingsSO)target;

            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            settings.mode = (CameraMode)EditorGUILayout.EnumPopup("Camera Mode", settings.mode);
            EditorGUILayout.Space();

            switch (settings.mode)
            {
                case CameraMode.FixedCamera:
                    EditorGUILayout.LabelField("Fixed Offset", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(followOffsetProp, new GUIContent("Offset X/Y/Z"));
                    break;

                case CameraMode.DynamicCamera:
                    EditorGUILayout.LabelField("Dynamic Distances", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(dynamicMinProp, new GUIContent("Min Distance"));
                    EditorGUILayout.PropertyField(dynamicMaxProp, new GUIContent("Max Distance"));
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Smoothing", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(followSmoothTimeProp, new GUIContent("Follow Smooth Time"));
                    EditorGUILayout.PropertyField(rotationSmoothTimeProp, new GUIContent("Rotation Smooth Time"));
                    EditorGUILayout.PropertyField(disableSmoothingProp,  new GUIContent("Disable Smoothing"));
                    break;

                case CameraMode.FollowTarget:
                    EditorGUILayout.LabelField("Follow-Target Mode", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(followTargetPosProp, new GUIContent("Target Position"));
                    break;

                case CameraMode.FixedOffset:
                    EditorGUILayout.LabelField("Fixed-Offset Mode", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(fixedOffsetPosProp, new GUIContent("World Offset"));
                    break;

                case CameraMode.Orthographic:
                    EditorGUILayout.LabelField("Orthographic Mode", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(orthoSizeProp, new GUIContent("Ortho Size"));
                    break;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("View Frustum", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nearClipProp, new GUIContent("Near Clip Plane"));
            EditorGUILayout.PropertyField(farClipProp,  new GUIContent("Far Clip Plane"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
