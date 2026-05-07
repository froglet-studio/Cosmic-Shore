using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.Recording
{
    [CreateAssetMenu(
        fileName = "VideoMacroLibrary",
        menuName = "ScriptableObjects/Video Tools/Video Macro Library")]
    public class VideoMacroLibrarySO : ScriptableObject
    {
        public List<VideoMacro> Macros = new();
    }
}
