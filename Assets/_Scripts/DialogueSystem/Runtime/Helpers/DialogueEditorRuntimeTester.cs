// using UnityEngine;
// using CosmicShore.DialogueSystem.Models;
// using UnityEditor;
// using CosmicShore.DialogueSystem.Controller;
//
// namespace CosmicShore.DialogueSystem.Editor
// {
//     public static class DialogueEditorRuntimeTester
//     {
//         public static void Test(DialogueSet set)
//         {
//             Debug.Log($"[TestInEditor] Playing Dialogue Set: {set.name} ({set.lines.Count} lines)");
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