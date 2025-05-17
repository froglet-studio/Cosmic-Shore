using UnityEngine;

[CreateAssetMenu(menuName = "CosmicShore/FTUE/Progress Tracker")]
public class FTUEProgress : ScriptableObject
{
    public TutorialSequenceSet pendingSet;
    public int nextIndex;

    public void Clear()
    {
        pendingSet = null;
        nextIndex = 0;
    }
}
