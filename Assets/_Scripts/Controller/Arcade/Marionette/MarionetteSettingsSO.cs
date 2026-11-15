using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marionette's tunables other than the strike target (EndConditionOverridesSO) and the hit values
    /// (MarionetteScoringRuleSO): the AI puppeteers. The arena is Kelpwood's (its kelp and its herds) and the crystals the
    /// scene's CrystalManager's. Config separation - authored by <c>Tools/Build/author_marionette_assets.py</c>, never by
    /// hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MarionetteSettings", menuName = "ScriptableObjects/Arcade/MarionetteSettings")]
    public class MarionetteSettingsSO : ScriptableObject
    {
        [Header("AI puppeteers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival to go after and which creature to take for it.")]
        public float[] aiRetargetSeconds = { 3f, 2.2f, 1.5f, 1f };

        [Tooltip("The chance the AI takes the creature NEAREST its rival rather than any creature at all. The wasp " +
                 "stings what is ahead of its nose, so this is where the nose is turned while it is free.")]
        public float[] aiHostChoosiness = { 0.25f, 0.5f, 0.75f, 0.95f };

        [Tooltip("How far (u) off the chosen creature the AI's free nose is aimed, re-rolled each retarget. The wasp's " +
                 "own AI stings the creature nearest its aim line inside 35 degrees, so a wide miss takes another one.")]
        public float[] aiAimError = { 60f, 35f, 15f, 5f };

        [Header("AI puppeteers - shared")]
        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times nearer to steal the " +
                 "hunt. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 1.5f;
    }
}
