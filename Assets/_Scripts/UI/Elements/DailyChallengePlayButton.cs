using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class DailyChallengePlayButton : MonoBehaviour
    {
        void Start()
        {
            // Daily Challenge is not yet available — disable the button
            var button = GetComponent<Button>();
            if (button)
                button.interactable = false;
        }

        public void Play()
        {
            // Disabled — daily challenge coming soon
        }
    }
}