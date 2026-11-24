using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lancefall's tunables other than the prism target (EndConditionOverridesSO) and the point values (the rule): the
    /// AI edgers. The plates are Shatterplane's (its cells, referenced read-only) and the crystals the scene's
    /// CrystalManager's. Config separation - authored by <c>Tools/Build/author_lancefall_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "LancefallSettings", menuName = "ScriptableObjects/Arcade/LancefallSettings")]
    public class LancefallSettingsSO : ScriptableObject
    {
        [Header("AI edgers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between the AI's choices of the plate it lines up on (a plate that stops standing is dropped " +
                 "at once).")]
        public float[] aiRetargetSeconds = { 2.4f, 1.8f, 1.2f, 0.8f };

        [Tooltip("How far out from a plate's centre, IN the plate's plane, the AI goes to line up before it turns in. " +
                 "Farther gives the nose more room to settle edge-on.")]
        public float[] aiStandoff = { 200f, 240f, 280f, 320f };

        [Tooltip("Degrees out of a plate's plane the AI accepts as edge-on before it turns its nose at the plate. A " +
                 "lance laid in the plane breaks a whole strip; a few degrees out it breaks a handful.")]
        public float[] aiEdgeDegrees = { 20f, 14f, 9f, 5f };

        [Tooltip("How far (u) off the plate's centre the AI's line falls, in the plate's plane, re-rolled each retarget. " +
                 "A line off the centre is a shorter chord: fewer tiles.")]
        public float[] aiAimError = { 50f, 30f, 15f, 5f };

        [Tooltip("A crystal this close (u) is worth the detour: every crystal is a missile plant where it is taken and " +
                 "its Space widens the lance.")]
        public float[] aiCrystalDetourRange = { 150f, 220f, 300f, 400f };

        [Header("AI edgers - every level")]
        [Tooltip("How far (u) off a plate's centre, in its plane and across the approach, the AI lays its line. The " +
                 "reliquary's ribs run through every plate's centre row, so a line through the centre meets armour; " +
                 "three tiles off is still a near-full chord.")]
        [Min(0f)] public float aiChordOffset = 24f;
    }
}
