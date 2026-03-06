using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    public class CallToActionTarget : MonoBehaviour
    {
        [SerializeField] public CallToActionTargetType TargetID;
        [SerializeField] GameObject ActiveIndicator;

        void Start()
        {
            if (CallToActionSystem.Instance == null)
            {
                CSDebug.LogWarning($"{nameof(CallToActionTarget)}: CallToActionSystem.Instance is null — skipping registration. GameObject: {gameObject.name}");
                return;
            }

            CallToActionSystem.Instance.RegisterCallToActionTarget(TargetID, WhenDutyCalls, WhenTheCallHasBeenAnswered);

            if (AmIActive())
                WhenDutyCalls();
        }

        void WhenDutyCalls()
        {
            if (ActiveIndicator == null)
                CSDebug.LogWarning($"CallToActionTarget does not have an ActiveIndicator set. GameObject Name: {gameObject.name}");
            else
                ActiveIndicator.SetActive(true);
        }

        void WhenTheCallHasBeenAnswered()
        {
            if (ActiveIndicator == null)
                return;

            ActiveIndicator.SetActive(false);
        }

        bool AmIActive()
        {
            return CallToActionSystem.Instance != null && CallToActionSystem.Instance.IsCallToActionTargetActive(TargetID);
        }
    }
}