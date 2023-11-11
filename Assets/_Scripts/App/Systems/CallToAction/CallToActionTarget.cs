using UnityEngine;

namespace CosmicShore.App.Systems.CallToAction
{
    public class CallToActionTarget : MonoBehaviour
    {
        [SerializeField] public CallToActionTargetType TargetID;
        [SerializeField] GameObject ActiveIndicator;

        void Start()
        {
            CallToActionSystem.Instance.RegisterCallToActionTarget(TargetID, WhenDutyCalls, WhenTheCallHasBeenAnswered);

            if (AmIActive())
                WhenDutyCalls();
        }

        void WhenDutyCalls()
        {
            ActiveIndicator.SetActive(true);
        }

        void WhenTheCallHasBeenAnswered()
        {
            ActiveIndicator.SetActive(false);
        }

        bool AmIActive()
        {
            return CallToActionSystem.Instance.IsCallToActionTargetActive(TargetID);
        }
    }
}