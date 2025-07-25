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

        // foldout toggle
        bool showAdvanced = false;

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

            // 1) Always show a one-line summary
            var settings = (CameraSettingsSO)target;
            EditorGUILayout.HelpBox(
                $"Overrides: {settings.controlOverrides}\n" +
                $"Default Offset: {settings.followOffset}",
                MessageType.Info);

            // 2) Let them pick which modes to use
            EditorGUILayout.LabelField("Select Override Modes", EditorStyles.boldLabel);
            settings.controlOverrides = (ControlOverrideFlags)
                EditorGUILayout.EnumFlagsField(controlOverridesProp.displayName, settings.controlOverrides);

            EditorGUILayout.Space();

            // 3) Show only the fields that matter for each flag
            var flags = settings.controlOverrides;
            if (flags.HasFlag(ControlOverrideFlags.CloseCam) || flags.HasFlag(ControlOverrideFlags.FarCam))
            {
                EditorGUILayout.LabelField("Close/Far Distances", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(closeCamDistanceProp);
                EditorGUILayout.PropertyField(farCamDistanceProp);
                EditorGUILayout.Space();
            }

            if (flags.HasFlag(ControlOverrideFlags.FollowTarget))
            {
                EditorGUILayout.LabelField("Follow-Target Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(followTargetPositionProp);
                EditorGUILayout.Space();
            }

            if (flags.HasFlag(ControlOverrideFlags.FixedOffset))
            {
                EditorGUILayout.LabelField("Fixed-Offset Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(fixedOffsetPositionProp);
                EditorGUILayout.Space();
            }

            if (flags.HasFlag(ControlOverrideFlags.Orthographic))
            {
                EditorGUILayout.LabelField("Orthographic Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(orthographicSizeProp);
                EditorGUILayout.Space();
            }

            // 4) Advanced foldout for the rest
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Settings");
            if (showAdvanced)
            {
                EditorGUILayout.LabelField("Follow & Rotation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(followOffsetProp);
                EditorGUILayout.PropertyField(followSmoothTimeProp);
                EditorGUILayout.PropertyField(rotationSmoothTimeProp);
                EditorGUILayout.PropertyField(disableRotationLerpProp);
                EditorGUILayout.PropertyField(useFixedUpdateProp);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("View Frustum", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(nearClipPlaneProp);
                EditorGUILayout.PropertyField(farClipPlaneProp);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
