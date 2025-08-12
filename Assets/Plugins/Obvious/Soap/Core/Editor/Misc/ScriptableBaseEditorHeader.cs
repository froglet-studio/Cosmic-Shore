using UnityEngine;

namespace Obvious.Soap.Editor
{
    using UnityEditor;

    [InitializeOnLoad]
    static class ScriptableBaseEditorHeader
    {
        private static SoapSettings _soapSettings;
        private static bool _isEditingDescription;
        private static string _newDescription;
        private static Texture[] _icons;
        private static GUIStyle _buttonStyle;

        static ScriptableBaseEditorHeader()
        {
            _soapSettings = SoapEditorUtils.GetOrCreateSoapSettings();
            _icons = new Texture[2];
            _icons[0] = Resources.Load<Texture>("Icons/icon_cancel");
            _icons[1] = Resources.Load<Texture>("Icons/icon_edit");

            Editor.finishedDefaultHeaderGUI += DrawHeader;
        }

        private static void DrawHeader(Editor editor)
        {
            if (!EditorUtility.IsPersistent(editor.target))
                return;

            if (editor.targets.Length > 1)
            {
                //If there is more than one target, we check if they are all ScriptableBase
                foreach (var target in editor.targets)
                {
                    var scriptableBase = target as ScriptableBase;
                    if (scriptableBase == null)
                        return;
                }

                //Only draws the category for the selected target
                var scriptableTarget = editor.target as ScriptableBase;
                if (DrawCategory(scriptableTarget))
                {
                    //Assign the category to all the targets
                    foreach (var target in editor.targets)
                    {
                        if (target == editor.target)
                            continue;
                        var scriptableBase = target as ScriptableBase;
                        Undo.RecordObject(scriptableBase, "Change Category");
                        scriptableBase.CategoryIndex = scriptableTarget.CategoryIndex;
                        EditorUtility.SetDirty(scriptableBase);
                    }
                }
            }
            else
            {
                var scriptableBase = editor.target as ScriptableBase;
                if (scriptableBase == null)
                    return;

                DrawDescriptionAndCategory(scriptableBase);
            }

            void DrawDescriptionAndCategory(ScriptableBase scriptableBase)
            {
                EditorGUILayout.BeginHorizontal();
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Description:", labelStyle, GUILayout.Width(65));

                var iconIndex = _isEditingDescription ? 0 : 1;
                var tooltip = _isEditingDescription ? "Cancel" : "Edit Description";
                var buttonContent = new GUIContent(_icons[iconIndex], tooltip);
                if (_buttonStyle == null)
                {
                    _buttonStyle = new GUIStyle(GUI.skin.button);
                    _buttonStyle.padding = new RectOffset(4, 4, 4, 4);
                }
                if (GUILayout.Button(buttonContent, _buttonStyle, GUILayout.Height(18), GUILayout.Width(18)))
                {
                    if (_isEditingDescription)
                    {
                        _newDescription = scriptableBase.Description;
                        _isEditingDescription = false;
                        EditorGUILayout.EndHorizontal();
                        return;
                    }

                    _isEditingDescription = true;
                    _newDescription = scriptableBase.Description;
                }

                GUILayout.FlexibleSpace();
                DrawCategory(scriptableBase);
                EditorGUILayout.EndHorizontal();

                if (_isEditingDescription)
                {
                    //Draw the text area
                    GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
                    textAreaStyle.wordWrap = true;
                    _newDescription = EditorGUILayout.TextArea(_newDescription, textAreaStyle, GUILayout.Height(50));

                    //Draw the confirm and cancel buttons
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Confirm", GUILayout.MaxHeight(30f)))
                    {
                        Undo.RecordObject(scriptableBase, "Change Description");
                        scriptableBase.Description = _newDescription;
                        EditorUtility.SetDirty(scriptableBase);
                        _isEditingDescription = false;
                    }

                    if (GUILayout.Button("Cancel", GUILayout.MaxHeight(30f)) || Event.current.keyCode == KeyCode.Escape)
                    {
                        _newDescription = scriptableBase.Description;
                        _isEditingDescription = false;
                    }

                    EditorGUILayout.EndHorizontal();
                    return;
                }

                if (string.IsNullOrEmpty(scriptableBase.Description))
                    return;

                EditorGUILayout.HelpBox(scriptableBase.Description, MessageType.None);
            }

            bool DrawCategory(ScriptableBase scriptableBase)
            {
                if (_soapSettings == null)
                    _soapSettings = SoapEditorUtils.GetOrCreateSoapSettings();

                var hasChanged = false;
                var categories = _soapSettings.Categories.ToArray();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category:", EditorStyles.miniBoldLabel, GUILayout.Width(55f));
                EditorGUI.BeginChangeCheck();
                int newCategoryIndex = EditorGUILayout.Popup(scriptableBase.CategoryIndex, categories,GUILayout.MaxWidth(175));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(scriptableBase, "Change Category");
                    scriptableBase.CategoryIndex = newCategoryIndex;
                    EditorUtility.SetDirty(scriptableBase);
                    hasChanged = true;
                }

                EditorGUILayout.EndHorizontal();
                return hasChanged;
            }
        }
    }
}