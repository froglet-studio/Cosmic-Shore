using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Obvious.Soap.Editor
{
    public class CreateTypePopUpWindow : PopupWindowContent
    {
        private readonly GUISkin _skin;
        private readonly Rect _position;
        private string _typeText = "";
        private string _namespaceText = "";
        private bool _baseClass ;
        private bool _monoBehaviour;
        private bool _variable = true;
        private bool _event = true;
        private bool _eventListener = true;
        private bool _list = true;
        private bool _invalidTypeName = true;
        private bool _invalidNamespace;
        private bool _namespaceEnabled;
        private string _path;
        private readonly Vector2 _dimensions = new Vector2(350, 385);
        private readonly GUIStyle _bgStyle;
        private Texture[] _icons;
        private int _destinationFolderIndex = 0;
        private readonly string[] _destinationFolderOptions = { "Selected in Project", "Custom" };
        private readonly Color _validTypeColor = new Color(0.32f, 0.96f, 0.8f);

        public const string DestinationFolderIndexKey = "SoapWizard_DestinationFolderIndex";
        public const string DestinationFolderPathKey = "SoapWizard_DestinationFolderPath";

        public override Vector2 GetWindowSize() => _dimensions;

        public CreateTypePopUpWindow(Rect origin)
        {
            _position = origin;
            _skin = Resources.Load<GUISkin>("GUISkins/SoapWizardGUISkin");
            _bgStyle = new GUIStyle(GUIStyle.none);
            _bgStyle.normal.background = SoapInspectorUtils.CreateTexture(SoapEditorUtils.SoapColor);
            Load();
        }

        private void Load()
        {
            _icons = new Texture[6];
            _icons[0] = EditorGUIUtility.IconContent("cs Script Icon").image;
            _icons[1] = Resources.Load<Texture>("Icons/icon_scriptableVariable");
            _icons[2] = Resources.Load<Texture>("Icons/icon_scriptableEvent");
            _icons[3] = Resources.Load<Texture>("Icons/icon_eventListener");
            _icons[4] = Resources.Load<Texture>("Icons/icon_scriptableList");
            _icons[5] = EditorGUIUtility.IconContent("Error").image;
            _destinationFolderIndex = EditorPrefs.GetInt(DestinationFolderIndexKey, 0);
            _path = _destinationFolderIndex == 0
                ? SoapFileUtils.GetSelectedFolderPathInProjectWindow()
                : EditorPrefs.GetString(DestinationFolderPathKey, "Assets");
        }

        public override void OnGUI(Rect rect)
        {
            editorWindow.position = SoapInspectorUtils.CenterInWindow(editorWindow.position, _position);
            DrawTitle();
            DrawTextFields();
            GUILayout.Space(15);
            DrawTypeToggles();
            GUILayout.Space(20);
            DrawPath();
            GUILayout.Space(5);
            DrawButtons();
        }

        private void DrawTitle()
        {
            GUILayout.BeginVertical(_bgStyle);
            var titleStyle = _skin.customStyles.ToList().Find(x => x.name == "title");
            EditorGUILayout.LabelField("Create new Type", titleStyle);
            GUILayout.EndVertical();
        }

        private void DrawTextFields()
        {
            //Draw Namespace Text Field
            {
                EditorGUILayout.BeginHorizontal();
                Texture2D texture = new Texture2D(0, 0);
                var icon = _invalidNamespace ? _icons[5] : texture;
                var style = new GUIStyle(GUIStyle.none);
                style.margin = new RectOffset(10, 0, 5, 0);
                GUILayout.Box(icon, style, GUILayout.Width(18), GUILayout.Height(18));
                var labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                EditorGUILayout.LabelField("Namespace:", labelStyle, GUILayout.Width(75));
                EditorGUI.BeginChangeCheck();
                var textStyle = new GUIStyle(GUI.skin.textField);
                textStyle.focused.textColor = _invalidNamespace ? Color.red : Color.white;
                _namespaceText = EditorGUILayout.TextField(_namespaceText, textStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    _invalidNamespace = !SoapTypeUtils.IsNamespaceValid(_namespaceText);
                }

                EditorGUILayout.EndHorizontal();
            }

            //Draw TypeName Text Field
            {
                EditorGUILayout.BeginHorizontal();
                Texture2D texture = new Texture2D(0, 0);
                var icon = _invalidTypeName ? _icons[5] : texture;
                var style = new GUIStyle(GUIStyle.none);
                style.margin = new RectOffset(10, 0, 5, 0);
                GUILayout.Box(icon, style, GUILayout.Width(18), GUILayout.Height(18));
                EditorGUILayout.LabelField("Type Name:", GUILayout.Width(75));
                EditorGUI.BeginChangeCheck();
                var textStyle = new GUIStyle(GUI.skin.textField);
                textStyle.focused.textColor = _invalidTypeName ? Color.red : Color.white;
                _typeText = EditorGUILayout.TextField(_typeText, textStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    _invalidTypeName = !SoapTypeUtils.IsTypeNameValid(_typeText);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTypeToggles()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            var capitalizedType = $"{_typeText.CapitalizeFirstLetter()}";
            if (!SoapTypeUtils.IsIntrinsicType(_typeText))
            {
                DrawToggle(ref _baseClass, $"{capitalizedType}", "", 0, true, 140);
                if (_baseClass)
                    _monoBehaviour = GUILayout.Toggle(_monoBehaviour, "MonoBehaviour?");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            DrawToggle(ref _variable, $"{capitalizedType}", "Variable", 1, true, 140);
            GUILayout.Space(5);
            DrawToggle(ref _event, "ScriptableEvent", $"{capitalizedType}", 2);
            GUILayout.Space(5);
            if (!_event)
                _eventListener = false;
            GUI.enabled = _event;
            DrawToggle(ref _eventListener, "EventListener", $"{capitalizedType}", 3);
            GUI.enabled = true;
            GUILayout.Space(5);
            DrawToggle(ref _list, "ScriptableList", $"{capitalizedType}", 4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            return;

            void DrawToggle(ref bool toggleValue, string typeName, string second, int iconIndex,
                bool isFirstRed = false, int maxWidth = 200)
            {
                EditorGUILayout.BeginHorizontal();
                var icon = _icons[iconIndex];
                var style = new GUIStyle(GUIStyle.none);
                GUILayout.Box(icon, style, GUILayout.Width(18), GUILayout.Height(18));
                toggleValue = GUILayout.Toggle(toggleValue, "", GUILayout.Width(maxWidth));
                GUIStyle firstStyle = new GUIStyle(GUI.skin.label);
                firstStyle.padding.left = 15 - maxWidth;
                if (isFirstRed)
                    firstStyle.normal.textColor = _invalidTypeName ? SoapEditorUtils.SoapColor : _validTypeColor;
                GUILayout.Label(typeName, firstStyle);
                GUIStyle secondStyle = new GUIStyle(GUI.skin.label);
                secondStyle.padding.left = -6;
                if (!isFirstRed)
                    secondStyle.normal.textColor = _invalidTypeName ? SoapEditorUtils.SoapColor : _validTypeColor;
                GUILayout.Label(second, secondStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }


        private void DrawPath()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Destination Folder Path:", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            _destinationFolderIndex = GUILayout.SelectionGrid(_destinationFolderIndex, _destinationFolderOptions, 2);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetInt(DestinationFolderIndexKey, _destinationFolderIndex);

            if (_destinationFolderIndex == 0)
            {
                var guiStyle = new GUIStyle(EditorStyles.label);
                guiStyle.fontStyle = FontStyle.Italic;
                _path = SoapFileUtils.GetSelectedFolderPathInProjectWindow();
                EditorGUILayout.LabelField($"{_path}", guiStyle);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                _path = EditorGUILayout.TextField(_path);
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetString(DestinationFolderPathKey, _path);
            }
        }

        private void DrawButtons()
        {
            GUI.enabled = !_invalidTypeName && !_invalidNamespace;
            if (GUILayout.Button("Create", GUILayout.ExpandHeight(true)))
            {
                if (!SoapTypeUtils.IsTypeNameValid(_typeText))
                    return;

                var isIntrinsicType = SoapTypeUtils.IsIntrinsicType(_typeText);
                TextAsset newFile = null;
                var progress = 0f;
                EditorUtility.DisplayProgressBar("Progress", "Start", progress);

                if (_baseClass && !isIntrinsicType)
                {
                    var templateName = _monoBehaviour ? "NewTypeMonoTemplate.cs" : "NewTypeTemplate.cs";
                    if (!SoapEditorUtils.CreateClassFromTemplate(templateName, _namespaceText, _typeText, _path,
                            out newFile))
                    {
                        Close();
                        return;
                    }
                }

                progress += 0.2f;
                EditorUtility.DisplayProgressBar("Progress", "Generating...", progress);

                if (_variable)
                {
                    if (!SoapEditorUtils.CreateClassFromTemplate("ScriptableVariableTemplate.cs", _namespaceText,
                            _typeText, _path,
                            out newFile, isIntrinsicType, true))
                    {
                        Close();
                        return;
                    }
                }

                progress += 0.2f;
                EditorUtility.DisplayProgressBar("Progress", "Generating...", progress);

                if (_event)
                {
                    if (!SoapEditorUtils.CreateClassFromTemplate("ScriptableEventTemplate.cs", _namespaceText,
                            _typeText, _path,
                            out newFile, isIntrinsicType, true))
                    {
                        Close();
                        return;
                    }
                }

                progress += 0.2f;
                EditorUtility.DisplayProgressBar("Progress", "Generating...", progress);

                if (_eventListener)
                {
                    if (!SoapEditorUtils.CreateClassFromTemplate("EventListenerTemplate.cs", _namespaceText, _typeText,
                            _path,
                            out newFile, isIntrinsicType, true))
                    {
                        Close();
                        return;
                    }
                }

                progress += 0.2f;
                EditorUtility.DisplayProgressBar("Progress", "Generating...", progress);

                if (_list)
                {
                    if (!SoapEditorUtils.CreateClassFromTemplate("ScriptableListTemplate.cs", _namespaceText, _typeText,
                            _path,
                            out newFile, isIntrinsicType, true))
                    {
                        Close();
                        return;
                    }
                }

                progress += 0.2f;
                EditorUtility.DisplayProgressBar("Progress", "Completed!", progress);

                EditorUtility.DisplayDialog("Success", $"{_typeText} was created!", "OK");
                Close(false);
                EditorGUIUtility.PingObject(newFile);
            }

            GUI.enabled = true;
            if (GUILayout.Button("Cancel", GUILayout.ExpandHeight(true)))
            {
                editorWindow.Close();
            }
        }

        private void Close(bool hasError = true)
        {
            EditorUtility.ClearProgressBar();
            editorWindow.Close();
            if (hasError)
                EditorUtility.DisplayDialog("Error", $"Failed to create {_typeText}", "OK");
        }
    }
}