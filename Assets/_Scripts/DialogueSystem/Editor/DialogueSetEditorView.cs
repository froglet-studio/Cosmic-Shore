//using UnityEditor;
//using UnityEngine;
//using CosmicShore.DialogueSystem.Models;

//namespace CosmicShore.DialogueSystem.Editor
//{
//    public static class DialogueSetEditorView
//    {
//        public static void Draw(DialogueSet set)
//        {
//            if (set == null) return;

//            EditorGUILayout.Space(10);
//            EditorGUILayout.LabelField("Dialogue Lines", EditorStyles.boldLabel);

//            for (int i = 0; i < set.lines.Count; i++)
//            {
//                DrawDialogueLine(set, i);
//            }

//            EditorGUILayout.Space();
//            DrawButtonBar(set);
//        }

//        private static void DrawDialogueLine(DialogueSet set, int index)
//        {
//            var line = set.lines[index];
//            var color = DialogueModeType.GetColor(set.mode);

//            GUI.backgroundColor = color;
//            EditorGUILayout.BeginHorizontal("box");

//            EditorGUILayout.LabelField($"{line.speakerName} – {TruncateText(line.text, 40)}", GUILayout.MaxWidth(400));

//            if (GUILayout.Button("X", GUILayout.Width(20)))
//            {
//                set.lines.RemoveAt(index);
//                EditorUtility.SetDirty(set);
//            }

//            EditorGUILayout.EndHorizontal();
//            GUI.backgroundColor = Color.white;
//        }

//        private static void DrawButtonBar(DialogueSet set)
//        {
//            EditorGUILayout.BeginHorizontal();

//            if (GUILayout.Button("Add New Line"))
//            {
//                set.lines.Add(new DialogueLine { speakerName = "Speaker", text = "..." });
//                EditorUtility.SetDirty(set);
//            }

//            if (GUILayout.Button("Test In Editor"))
//            {
//                DialogueEditorRuntimeTester.Test(set);
//            }

//            if (GUILayout.Button("Link Audio"))
//            {
//                DialogueAudioBatchLinker.LinkMissingAudio(set);
//            }

//            EditorGUILayout.EndHorizontal();
//        }

//        private static string TruncateText(string text, int maxLength)
//        {
//            if (string.IsNullOrEmpty(text)) return "...";
//            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
//        }
//    }
//}
