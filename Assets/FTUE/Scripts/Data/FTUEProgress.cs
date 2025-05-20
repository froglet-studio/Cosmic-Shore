using UnityEngine;

[CreateAssetMenu(menuName = "CosmicShore/FTUE/Progress Tracker")]
public class FTUEProgress : ScriptableObject
{
    public TutorialSequenceSet pendingSet;
    public int nextIndex;
    public TutorialPhase currentPhase = TutorialPhase.Phase1_Intro;

    [Tooltip("This is a Debug Key. Toggling is 'on' will not run the FTUE.")]
    public bool ftueDebugKey;

    public void Clear()
    {
        pendingSet = null;
        nextIndex = 0;
    }
}
