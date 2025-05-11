using UnityEngine;

namespace CosmicShore.FTUE
{
    [CreateAssetMenu(menuName = "CosmicShore/FTUE/Progress Tracker")]
    public class FTUEProgress : ScriptableObject
    {
        public TutorialStep pendingStep;

        public void Clear() => pendingStep = null;
    }
}
