using UnityEngine;
using CosmicShore.Utility.Recording;
using CosmicShore.Models.Enums;

namespace CosmicShore.Systems.CallToAction
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
            if (ActiveIndicator == null)
                CSDebug.LogWarning($"CallToActionTarget does not have an ActiveIndicator set. GameObject Name: {gameObject.name}");
            else
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