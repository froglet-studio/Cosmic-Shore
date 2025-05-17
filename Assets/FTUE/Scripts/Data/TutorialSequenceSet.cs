using CosmicShore.FTUE;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CosmicShore/FTUE/Tutorial Sequence Set")]
public class TutorialSequenceSet : ScriptableObject
{
    public List<TutorialStep> steps;
}
