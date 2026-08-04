#if UNITY_EDITOR
using UnityEngine;
using CosmicShore.Core;
using UnityEditor;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public static class DialogueAudioBatchLinker
    {
        public static void LinkMissingAudio(DialogueSet set)
        {
            CSDebug.Log($"[LinkAudio] Attempting to link audio for: {set.name}");

            foreach (var line in set.lines)
            {
                if (line.voiceClip == null)
                {
                    // Optional: Implement name-based matching from Resources folder
                    CSDebug.Log($"[LinkAudio] No audio found for: {line.text}");
                }
            }

            EditorUtility.SetDirty(set);
        }
    }
}
#endif
