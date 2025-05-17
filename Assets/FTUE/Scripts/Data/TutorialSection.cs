using UnityEngine;

namespace CosmicShore.FTUE
{
    [System.Serializable]
    public class TutorialSection
    {
        [TextArea(2, 5)] public string tutorialText;
        public bool hasArrowPrompt;
        public RectTransform designatedClickRegion;
    }
}
