#if UNITY_EDITOR
using UnityEngine;
using CosmicShore.Core;
using UnityEditor;

namespace CosmicShore.Core
{
    public static class DialogueAudioBatchLinker
    {
        public static void LinkMissingAudio(DialogueSet set)
        {
            foreach (var line in set.lines)
            {
                if (line.voiceClip == null)
                {
                    // Optional: Implement name-based matching from Resources folder
                }
            }

            EditorUtility.SetDirty(set);
        }
    }
}
#endif
