using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Glasshouse's tunables other than the round length (EndConditionOverridesSO): how often the garden is
    /// measured, the lead feedback and the AI gardeners. The seeds (omni crystals) are the scene's CrystalManager's,
    /// the plants, herds and caps are the cell's (its spawn profile per intensity). Config separation - authored by
    /// <c>Tools/Build/author_glasshouse_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "GlasshouseSettings",
        menuName = "ScriptableObjects/Arcade/GlasshouseSettings")]
    public class GlasshouseSettingsSO : ScriptableObject
    {
        [Header("The garden")]
        [Tooltip("Seconds between two measurements of every domain's garden (the plants standing in its colour outside " +
                 "the nucleus). Each measurement moves the pilots' VolumeRemaining by what the garden gained or lost " +
                 "since the last one. Server only; one pass over the live plants.")]
        [Min(0.1f)] public float gardenSampleSeconds = 1f;

        [Header("Lead feedback")]
        [Tooltip("No lead-change toast before this many seconds into the round (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 15f;

        [Tooltip("At least this many seconds between two lead-change toasts.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 8f;

        [Header("AI gardeners - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds the AI holds the lure over its garden each time it plants or tends: the light makes every " +
                 "plant in reach grow lureGrowth times as often, and letting go plants every seed it carries.")]
        public float[] aiLureHoldSeconds = { 2.5f, 3.5f, 4.5f, 5.5f };

        [Tooltip("The AI goes raiding once its school holds at least this many creatures: it steers at the nearest " +
                 "rival plant so the hull's own autopilot paints it and throws the school at it.")]
        public float[] aiRaidAtSchool = { 99f, 6f, 4f, 3f };

        [Tooltip("Seconds of one raid before the AI goes back to its seeds and its garden.")]
        public float[] aiRaidSeconds = { 0f, 6f, 9f, 12f };

        [Tooltip("The farthest (u) the AI goes for an omni crystal - a seed - rather than tending what it has.")]
        public float[] aiSeedReach = { 900f, 1400f, 2000f, 2600f };

        [Header("AI gardeners - shared")]
        [Tooltip("Where the AI plants: this fraction of the way from the nucleus's surface to the membrane, on a " +
                 "direction of its own (seat by seat round the cell), so two gardeners do not share a bed.")]
        [Range(0.05f, 0.95f)] public float aiGardenBandFraction = 0.35f;

        [Tooltip("The AI presses the lure once it is within this many units of its garden.")]
        [Min(1f)] public float aiArriveDistance = 60f;

        [Tooltip("Seconds between two tending visits when the AI holds no seed (it lures its garden to grow it).")]
        [Min(0f)] public float aiTendIntervalSeconds = 12f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}
