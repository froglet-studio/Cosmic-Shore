using CosmicShore.Utility;

// using UnityEngine;
// using CosmicShore.Core;
// using UnityEditor;
// using CosmicShore.Core;
//
// namespace CosmicShore.DialogueSystem.Editor
// {
//     public static class DialogueEditorRuntimeTester
//     {
//         public static void Test(DialogueSet set)
//         {
//             CSDebug.Log($"[TestInEditor] Playing Dialogue Set: {set.name} ({set.lines.Count} lines)");
//
// #if UNITY_EDITOR
//             if (!Application.isPlaying)
//                 EditorUtility.DisplayDialog("Dialogue Test", "You must be in Play Mode to test dialogue.", "OK");
//             else
//                 DialogueManager.Instance.PlayDialogueSet(set); // Assuming DialogueManager is runtime
// #endif
//         }
//     }
// }