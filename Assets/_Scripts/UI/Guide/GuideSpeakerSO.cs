using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Who speaks on a guided path (Rev 36 B, Docs/MENU_REDESIGN_B.md section 102.1; Shombith: "Lets go with B"): the captain's
    /// name, title and portrait, and the comms window <see cref="MenuSpotlight"/> shows instead of its caption when a step
    /// has a speaker. The words of each step stay on the node that drives the guide; this asset is the voice they come in.
    /// </summary>
    [CreateAssetMenu(fileName = "GuideSpeaker", menuName = "ScriptableObjects/UI/Guide Speaker", order = 41)]
    public class GuideSpeakerSO : ScriptableObject
    {
        [Header("Who")]
        [Tooltip("The speaker's name, shown large on the comms window (Trace).")]
        public string speakerName = "Trace";
        [Tooltip("The line under the name (Your captain · Squirrel). Empty hides it.")]
        public string speakerTitle = "Your captain · Squirrel";
        [Tooltip("The small word at the top of the window while the call is open (Incoming). Empty hides it.")]
        public string eyebrow = "Incoming";
        [Tooltip("The speaker's portrait (the pilot roster's portrait), cropped to the window's top band.")]
        public Sprite portrait;

        [Header("Window")]
        [Tooltip("The comms window (Tools/Build/author_guide_comms.py). Empty: the spotlight shows its plain caption.")]
        public GuideCommsView commsPrefab;

        [Header("Timing")]
        [Tooltip("Characters a second the line types at.")]
        [Min(1f)] public float charactersPerSecond = 45f;
        [Tooltip("Seconds the window takes to dock in from the screen edge.")]
        [Min(0.05f)] public float slideSeconds = 0.5f;
    }
}
