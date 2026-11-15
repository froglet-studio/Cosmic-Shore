using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thornbush's tunables other than the round length (EndConditionOverridesSO): the lead feedback and the AI
    /// builders. The heap is Jackstraw's (its cells, referenced read-only) and the crystals the scene's
    /// CrystalManager's. Config separation - authored by <c>Tools/Build/author_thornbush_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ThornbushSettings", menuName = "ScriptableObjects/Arcade/ThornbushSettings")]
    public class ThornbushSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("Seconds between the server's lead checks (the 130 lead-change toast).")]
        [Min(0.1f)] public float leadSampleSeconds = 1f;

        [Tooltip("No lead change is called before this many seconds into the round (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 15f;

        [Tooltip("At most one lead-change toast per this many seconds.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 8f;

        [Header("Arena")]
        [Tooltip("The heap's radius (u) at each INTENSITY (element 0 is intensity 1): the room the AI picks its perches " +
                 "in and scans for rival nests. Read off each rung's SpawnableJackstraw prefab by the generator - the map, " +
                 "never the AI's skill.")]
        public float[] heapRadiusByIntensity = { 360f, 470f, 570f, 680f };

        [Header("AI builders - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds the AI flies at one perch in the heap before it gives up on it and picks another (it keeps a " +
                 "perch it is docked on: the Shrike's own autopilot leaves a nest and detaches).")]
        public float[] aiPerchSeconds = { 9f, 7f, 5.5f, 4f };

        [Tooltip("Chance, each time the AI picks its next goal, that it raids the nearest rival nest instead of building " +
                 "(its nose on the rival's blocks: the hull's own autopilot lances and paints what is there).")]
        public float[] aiRaidChance = { 0f, 0.2f, 0.35f, 0.5f };

        [Tooltip("How far (u) off a raided nest the AI's nose is aimed.")]
        public float[] aiAimError = { 40f, 25f, 12f, 4f };

        [Tooltip("Seconds a raid lasts before the AI goes back to building.")]
        public float[] aiRaidSeconds = { 3f, 4f, 5f, 6f };

        [Header("AI builders - shared")]
        [Tooltip("A new perch is at least this far (u) from the AI's last one, so its nests spread through the heap " +
                 "rather than stacking in one place a single missile salvo can crack.")]
        [Min(0f)] public float aiPerchSpacing = 120f;

        [Tooltip("Seconds between the server's scans for rival nest blocks (one scan serves every AI).")]
        [Min(0.1f)] public float aiScanSeconds = 1f;

        [Tooltip("How far (u) past the heap's rim the scan for rival nest blocks reaches.")]
        [Min(0f)] public float aiScanMargin = 200f;
    }
}
