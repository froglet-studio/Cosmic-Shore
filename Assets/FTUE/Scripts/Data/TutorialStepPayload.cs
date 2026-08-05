using UnityEngine;

namespace CosmicShore.Core
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
