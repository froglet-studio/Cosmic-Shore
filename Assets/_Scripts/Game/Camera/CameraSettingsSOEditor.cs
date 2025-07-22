// Assets/Editor/CameraSettingsSOEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [CustomEditor(typeof(CameraSettingsSO))]
    public class CameraSettingsSOEditor : Editor
    {
        SerializedProperty followOffsetProp,
                           followSmoothTimeProp,
                           rotationSmoothTimeProp,
                           disableRotationLerpProp,
                           useFixedUpdateProp;

        SerializedProperty nearClipPlaneProp,
                           farClipPlaneProp;

        SerializedProperty controlOverridesProp;

        SerializedProperty closeCamDistanceProp,
                           farCamDistanceProp,
                           followTargetPositionProp,
                           fixedOffsetPositionProp,
                           orthographicSizeProp;

        void OnEnable()
        {
            var so = serializedObject;
            followOffsetProp         = so.FindProperty(nameof(CameraSettingsSO.followOffset));
            followSmoothTimeProp     = so.FindProperty(nameof(CameraSettingsSO.followSmoothTime));
            rotationSmoothTimeProp   = so.FindProperty(nameof(CameraSettingsSO.rotationSmoothTime));
            disableRotationLerpProp  = so.FindProperty(nameof(CameraSettingsSO.disableRotationLerp));
            useFixedUpdateProp       = so.FindProperty(nameof(CameraSettingsSO.useFixedUpdate));

            nearClipPlaneProp        = so.FindProperty(nameof(CameraSettingsSO.nearClipPlane));
            farClipPlaneProp         = so.FindProperty(nameof(CameraSettingsSO.farClipPlane));

            controlOverridesProp     = so.FindProperty(nameof(CameraSettingsSO.controlOverrides));

            closeCamDistanceProp     = so.FindProperty(nameof(CameraSettingsSO.closeCamDistance));
            farCamDistanceProp       = so.FindProperty(nameof(CameraSettingsSO.farCamDistance));
            followTargetPositionProp = so.FindProperty(nameof(CameraSettingsSO.followTargetPosition));
            fixedOffsetPositionProp  = so.FindProperty(nameof(CameraSettingsSO.fixedOffsetPosition));
            orthographicSizeProp     = so.FindProperty(nameof(CameraSettingsSO.orthographicSize));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("📌 Common Follow & Rotation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(followOffsetProp);
            EditorGUILayout.PropertyField(followSmoothTimeProp);
            EditorGUILayout.PropertyField(rotationSmoothTimeProp);
            EditorGUILayout.PropertyField(disableRotationLerpProp);
            EditorGUILayout.PropertyField(useFixedUpdateProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🔭 View Frustum", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nearClipPlaneProp);
            EditorGUILayout.PropertyField(farClipPlaneProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🚩 Control Overrides", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(controlOverridesProp);

            var flags = (ControlOverrideFlags)controlOverridesProp.intValue;

            if (flags.HasFlag(ControlOverrideFlags.CloseCam) ||
                flags.HasFlag(ControlOverrideFlags.FarCam))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("🔍 Close & Far Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(closeCamDistanceProp);
                EditorGUILayout.PropertyField(farCamDistanceProp);
            }

            if (flags.HasFlag(ControlOverrideFlags.FollowTarget))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("🎯 Follow-Target Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(followTargetPositionProp);
            }

            if (flags.HasFlag(ControlOverrideFlags.FixedOffset))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("📌 Fixed-Offset Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(fixedOffsetPositionProp);
            }

            if (flags.HasFlag(ControlOverrideFlags.Orthographic))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("🔲 Orthographic Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(orthographicSizeProp);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
