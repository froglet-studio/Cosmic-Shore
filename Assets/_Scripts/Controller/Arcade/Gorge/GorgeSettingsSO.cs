using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Gorge's tunables other than the round length (EndConditionOverridesSO): the lead feedback and the AI gluttons. The
    /// reef is Chum's (<see cref="ChumReef"/>, laid by <see cref="GorgeController"/>), the cells and their herds Chum's.
    /// Config separation - authored by <c>Tools/Build/author_gorge_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GorgeSettings", menuName = "ScriptableObjects/Arcade/GorgeSettings")]
    public class GorgeSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("No lead-change toast before this many seconds of the round (the opening scramble is not a lead).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 15f;

        [Tooltip("At least this many seconds between two lead-change toasts.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 8f;

        [Tooltip("How often (s) the server samples the leader.")]
        [Min(0.1f)] public float leadSampleSeconds = 1f;

        [Header("AI gluttons - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which shoal to feed on and which creature to take there.")]
        public float[] aiRetargetSeconds = { 6f, 4.5f, 3.5f, 2.5f };

        [Tooltip("The chance the AI takes the creature NEAREST its shoal rather than any creature at all. The wasp " +
                 "stings what is ahead of its nose, so this is where the nose is turned while it is free.")]
        public float[] aiHostChoosiness = { 0.25f, 0.5f, 0.75f, 0.95f };

        [Tooltip("How far (u) off the chosen creature the AI's free nose is aimed, re-rolled each retarget.")]
        public float[] aiAimError = { 60f, 35f, 15f, 5f };

        [Tooltip("1 = feed on the shoal with the most prisms standing; 0 = the nearest shoal with any. In between, the " +
                 "AI weighs the two (a hungry expert flies further for a fuller shoal).")]
        public float[] aiFullShoalWeight = { 0f, 0.3f, 0.6f, 0.9f };
    }
}
