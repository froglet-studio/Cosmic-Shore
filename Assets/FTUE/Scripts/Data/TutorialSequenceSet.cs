using CosmicShore.FTUE;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "CosmicShore/FTUE/Tutorial Sequence Set")]
public class TutorialSequenceSet : ScriptableObject
{
    [Serializable]
    public class PhaseSequence
    {
        public TutorialPhase phase;
        public List<TutorialStep> steps;
    }

    [Tooltip("Defines all FTUE phases and their step?lists")]
    public List<PhaseSequence> phases = new List<PhaseSequence>();

    /// <summary>Get the steps for a given phase (or empty if none).</summary>
    public List<TutorialStep> GetSteps(TutorialPhase phase)
        => phases.FirstOrDefault(p => p.phase == phase)?.steps
           ?? new List<TutorialStep>();

    /// <summary>Total step?count in this phase.</summary>
    public int GetStepCount(TutorialPhase phase)
        => GetSteps(phase).Count;
}
