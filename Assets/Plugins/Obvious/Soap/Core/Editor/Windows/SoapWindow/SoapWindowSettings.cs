using System;
using UnityEditor;
using UnityEngine;

namespace Obvious.Soap.Editor
{
    public class SoapWindowSettings
    {
        private FloatVariable _floatVariable;
        private readonly SerializedObject _exampleClassSerializedObject;
        private readonly SerializedProperty _currentHealthProperty;
        private readonly Texture[] _icons;
        private readonly GUISkin _skin;
        private readonly GUIStyle _exampleBoxStyle;
        private Vector2 _scrollPosition = Vector2.zero;
        private SoapSettings _settings;

        public SoapWindowSettings(GUISkin skin)
        {
            var exampleClass = ScriptableObject.CreateInstance<ExampleClass>();
            _exampleClassSerializedObject = new SerializedObject(exampleClass);
            _currentHealthProperty = _exampleClassSerializedObject.FindProperty("CurrentHealth");
            _icons = new Texture[1];
            _icons[0] = EditorGUIUtility.IconContent("cs Script Icon").image;
            _skin = skin;
            _exampleBoxStyle= new GUIStyle(_skin.box);
            _exampleBoxStyle.normal.background = Texture2D.grayTexture;
        }

        public void Draw()
        {
            if (_settings == null)
                _settings = SoapEditorUtils.GetOrCreateSoapSettings();
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawVariableDisplayMode();
            GUILayout.Space(20);
            DrawNamingModeOnCreation();
            GUILayout.Space(20);
            DrawReferencesRefreshMode();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawVariableDisplayMode()
        {
            EditorGUILayout.BeginHorizontal(_skin.box);
            EditorGUI.BeginChangeCheck();
            _settings.VariableDisplayMode =
                (EVariableDisplayMode)EditorGUILayout.EnumPopup("Variable display mode",
                    _settings.VariableDisplayMode, GUILayout.Width(225));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            var infoText = _settings.VariableDisplayMode == EVariableDisplayMode.Default
                ? "Displays all the parameters of variables."
                : "Only displays the value.";
            EditorGUILayout.LabelField(infoText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            //Example
            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
            var boxStyle = new GUIStyle(_skin.box);
            boxStyle.normal.background = Texture2D.grayTexture;
            EditorGUILayout.BeginVertical(boxStyle);
            if (_floatVariable == null)
                _floatVariable = ScriptableObject.CreateInstance<FloatVariable>();
            var editor = UnityEditor.Editor.CreateEditor(_floatVariable);
            editor.OnInspectorGUI();
            EditorGUILayout.EndVertical();
        }

        private void DrawNamingModeOnCreation()
        {
            EditorGUILayout.BeginHorizontal(_skin.box);
            EditorGUI.BeginChangeCheck();
            _settings.NamingOnCreationMode =
                (ENamingCreationMode)EditorGUILayout.EnumPopup("Naming mode on creation",
                    _settings.NamingOnCreationMode, GUILayout.Width(225));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            var infoText = _settings.NamingOnCreationMode == ENamingCreationMode.Auto
                ? "Automatically assigns a name on creation."
                : "Focus the created SO and let's you rename it.";
            EditorGUILayout.LabelField(infoText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            //Example
            EditorGUILayout.LabelField("Preview: (Press Create to try)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_exampleBoxStyle);
            _exampleClassSerializedObject?.Update();
            EditorGUILayout.BeginHorizontal();
            var guiStyle = new GUIStyle(GUIStyle.none);
            guiStyle.contentOffset = new Vector2(0, 2);
            GUILayout.Box(_icons[0], guiStyle, GUILayout.Width(18), GUILayout.Height(18));
            GUILayout.Space(16);
            EditorGUILayout.LabelField("Example Class (Script)", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            SoapInspectorUtils.DrawColoredLine(1, new Color(0f, 0f, 0f, 0.2f));
            EditorGUILayout.PropertyField(_currentHealthProperty);
            _exampleClassSerializedObject?.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        private void DrawReferencesRefreshMode()
        {
            EditorGUILayout.BeginHorizontal(_skin.box);
            EditorGUI.BeginChangeCheck();
            _settings.ReferencesRefreshMode =
                (EReferencesRefreshMode)EditorGUILayout.EnumPopup("References Refresh Mode",
                    _settings.ReferencesRefreshMode, GUILayout.Width(225));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            var infoText = _settings.ReferencesRefreshMode == EReferencesRefreshMode.Auto
                ? "Refresh references whenever the scene/project is changed"
                : "Refresh references only when button is clicked.";
            EditorGUILayout.LabelField(infoText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        
        }
    }

    [Serializable]
    public class ExampleClass : ScriptableObject
    {
        public FloatVariable CurrentHealth;
    }
}