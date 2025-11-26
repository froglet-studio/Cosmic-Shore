#if !LINUX_BUILD
using UnityEngine;
using CosmicShore.DialogueSystem.Models;
using UnityEditor;

namespace CosmicShore.DialogueSystem.Editor
{
    public static class DialogueAudioBatchLinker
    {
        public static void LinkMissingAudio(DialogueSet set)
        {
            Debug.Log($"[LinkAudio] Attempting to link audio for: {set.name}");

            foreach (var line in set.lines)
            {
                if (line.voiceClip == null)
                {
                    // Optional: Implement name-based matching from Resources folder
                    Debug.Log($"[LinkAudio] No audio found for: {line.text}");
                }
            }

            EditorUtility.SetDirty(set);
        }
    }
}
#endif