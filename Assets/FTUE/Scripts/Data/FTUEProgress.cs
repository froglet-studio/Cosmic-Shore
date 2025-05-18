using UnityEngine;

[CreateAssetMenu(menuName = "CosmicShore/FTUE/Progress Tracker")]
public class FTUEProgress : ScriptableObject
{
    public TutorialSequenceSet pendingSet;
    public int nextIndex;
    public TutorialPhase currentPhase = TutorialPhase.Phase1_Intro;

    public void Clear()
    {
        pendingSet = null;
        nextIndex = 0;
    }
}
