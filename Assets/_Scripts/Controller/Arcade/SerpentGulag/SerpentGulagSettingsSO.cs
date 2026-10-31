using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Gulag's tunables other than the snipe target (EndConditionOverridesSO) and the snipe
    /// value (SerpentGulagScoringRuleSO): progress feedback, the crystal field in the middle of the
    /// arena, and the AI marksmen. Config separation - authored by
    /// <c>Tools/Build/author_serpent_gulag_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the intensity row.</b> Every AI strength is a four-rung ladder (index =
    /// intensity - 1), the shape Garrett's Serpent Charge marksman uses (SERPENTCHARGE.md on
    /// <c>cece/vibrant-archimedes-n3xn3x</c>). The generator asserts each one is monotone toward harder
    /// and that intensity 4's aim error is inside the rifle's 1.5 deg cone while intensity 1's is outside
    /// it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SerpentGulagSettings",
        menuName = "ScriptableObjects/Arcade/SerpentGulagSettings")]
    public class SerpentGulagSettingsSO : ScriptableObject
    {
        [Header("Progress feedback")]
        [Tooltip("Seconds between the scope hint's idle checks and the server's other light samples.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("Crystal field (the middle of the arena, every intensity)")]
        [Tooltip("Elemental crystals in the field. Mixed elements; Charge shortens the Sniper Shot's " +
                 "recovery (12 s at rest to 5.4 s at Charge 10), so the field is where a sniper reloads " +
                 "faster - in the open, in everyone's sights. 0 = no field.")]
        [Min(0)] public int fieldCrystalCount = 24;

        [Tooltip("Radius (u) of the sphere around the arena centre the field fills.")]
        [Min(1f)] public float fieldRadius = 380f;

        [Tooltip("Seconds before a collected crystal grows back in its place, so the field is never " +
                 "stripped bare for the rest of the match.")]
        [Min(0.5f)] public float fieldRegrowSeconds = 8f;

        [Tooltip("Seed for the field's layout and elements. Every peer lays the same field from it - " +
                 "no network message.")]
        public int fieldSeed = 77;

        [Header("AI marksmen - per intensity (index = intensity - 1)")]
        [Tooltip("Seconds between re-choosing which rival to hunt (and re-rolling the aim error).")]
        public float[] aiRetargetSeconds = { 1.2f, 0.9f, 0.6f, 0.4f };

        [Tooltip("Aim error (deg) the AI carries for a plan's life. The rifle's cone is 1.5 deg, so an " +
                 "error above it misses unless the rival moves into the shot.")]
        public float[] aiAimErrorDegrees = { 3f, 2f, 1.2f, 0.5f };

        [Tooltip("Seconds the AI holds its scope before it may fire (its reaction).")]
        public float[] aiScopeSettleSeconds = { 1f, 0.6f, 0.3f, 0.15f };

        [Tooltip("The farthest rival (u) the AI hunts. Past it, it flies toward the field.")]
        public float[] aiHuntRange = { 1500f, 2000f, 2500f, 3000f };

        [Tooltip("The AI flies to the crystal field while its rifle has at least this many seconds of " +
                 "recovery left (it reloads faster on Charge). A large number = never.")]
        public float[] aiForageMinCooldownSeconds = { 999f, 7f, 5f, 3.5f };

        [Tooltip("Chance the AI cloaks right after a shot, to vanish from the pilot it just fired on.")]
        public float[] aiCloakAfterShotChance = { 0f, 0.25f, 0.5f, 0.75f };

        [Header("AI marksmen - shared")]
        [Tooltip("Inside this distance (u) the AI breaks away instead of lining up: a sniper cannot aim " +
                 "at a pilot it is about to ram.")]
        [Min(0f)] public float aiMinAimDistance = 250f;

        [Tooltip("How far (u) the AI breaks away to before it turns back in.")]
        [Min(0f)] public float aiStandoffDistance = 650f;

        [Tooltip("The AI raises its scope only when its rival is within this many degrees of its nose.")]
        [Range(1f, 60f)] public float aiScopeOpenAngle = 15f;

        [Tooltip("Seconds the AI tries to line up a shot before it lowers its scope.")]
        [Min(0.1f)] public float aiShotGiveUpSeconds = 3f;

        [Tooltip("Seconds of the rival's own velocity the AI leads its aim point by.")]
        [Range(0f, 2f)] public float aiLeadSeconds = 0.25f;

        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times cheaper " +
                 "to steal the hunt. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 2f;

        /// <summary>The rung for <paramref name="intensity"/> (1-4, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int intensity)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(intensity - 1, 0, ladder.Length - 1)];
        }
    }
}
