using UnityEngine;

namespace CosmicShore.FTUE.Data
{
    [System.Serializable]
    public class TutorialStepPayload
    {
        public PayloadType payloadType;
    }

    public enum PayloadType
    {
        None,
        OpenArcadeAction,
        UserChoice,
        SceneActivation
    }
}
