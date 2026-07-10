// Ported from Assets/_Scripts/UI/Elements/DailyChallengeCard.cs (Arc F 2b-ii) — verbatim;
// TMPro / UnityEngine.UI → CosmicShore.Engine.UI, UnityEngine → CosmicShore.Engine.
using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace CosmicShore.UI
{
    public class DailyChallengeCard : MonoBehaviour
    {
        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] Image BackgroundImage;

        void Start()
        {
            // Daily Challenge is not yet available — disable interaction
            if (TimeRemaining)
                TimeRemaining.text = "COMING SOON";

            var button = GetComponent<Button>();
            if (button)
                button.interactable = false;
        }
    }
}
