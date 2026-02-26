using UnityEngine;

namespace CosmicShore.Core
{
    [System.Serializable]
    public class TutorialSection
    {
        [TextArea(2, 5)] public string tutorialText;
        public bool hasArrowPrompt;
        public RectTransform designatedClickRegion;
    }
}
