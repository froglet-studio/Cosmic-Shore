using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every tunable number for Scarab Scramble in one asset
    /// (Assets/_SO_Assets/Games/ScarabScrambleSettings.asset) — the AstroLeagueSettingsSO
    /// pattern. The GOAL TARGET is deliberately NOT here: it lives in
    /// EndConditionOverridesSO (FrogletTools ▸ Game Modes ▸ End Game Conditions), like every
    /// other mode's win count.
    ///
    /// BALL PHYSICS is deliberately not here either: a forged ball reads its own physics
    /// (mass, bounciness, drag, maxSpeed, wallRestitution) from the settings asset serialized
    /// on AstroLeagueBall.prefab — the ball is the VESSEL's payload and behaves identically in
    /// every context it is forged in (Scramble, Astro League, freestyle). This asset owns only
    /// what the MODE owns: the court, the hoops, the cap, the AI, and the feel around scoring.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScarabScrambleSettings",
        menuName = "ScriptableObjects/Arcade/ScarabScrambleSettings")]
    public class ScarabScrambleSettingsSO : ScriptableObject
    {
        [Header("Court (the cell nucleus IS the court)")]
        [Tooltip("Court sphere radius per intensity (index 0 = intensity 1). The nucleus is " +
                 "resized to this and the ball reflects off it — a SPHERE court on purpose: " +
                 "every bounce carries the ball back toward the middle where the hoops are, " +
                 "which quietly forgives a new player's wild shots. (Wall restitution/drag are " +
                 "the BALL's own dials on the settings asset its prefab serializes — the ball " +
                 "behaves identically in every context it is forged in, so this mode " +
                 "deliberately owns no second copy of its physics.)")]
        public float[] courtRadiusByIntensity = { 480f, 560f, 640f, 720f };

        [Header("Hoops")]
        [Tooltip("How many scoring hoops per intensity (index 0 = intensity 1). Intensity 1 " +
                 "is the party setting: four big hoops so defenders cannot camp them all. A " +
                 "count of 1 places a single hoop at the court centre (axis = world up).")]
        public int[] hoopCountByIntensity = { 4, 3, 2, 1 };

        [Tooltip("Hoop mouth radius per intensity (index 0 = intensity 1). Big mouths = easy " +
                 "scoring = short forgiving matches; the mouth must comfortably clear the " +
                 "ball (world radius ~7, up to x4 at Space 10).")]
        public float[] hoopMouthRadiusByIntensity = { 60f, 54f, 48f, 42f };

        [Tooltip("Where multi-hoop layouts sit: distance from the court centre as a fraction " +
                 "of the court radius. Hoops ring the centre on the horizontal plane, mouths " +
                 "facing the middle (axis radial), threaded from either direction.")]
        [Range(0.1f, 0.9f)] public float hoopRingRadiusFraction = 0.45f;

        [Tooltip("Accent colour of the hoop rings (the ToyFactory ring body). Deliberately " +
                 "domain-NEUTRAL — a hoop belongs to nobody; ownership lives on the balls.")]
        public Color hoopAccentColor = new(0.78f, 0.92f, 1f);

        [Tooltip("Seconds a hoop's ring flares after a goal (feedback only, every peer).")]
        [Min(0.1f)] public float hoopFlareSeconds = 1.2f;

        [Header("Balls")]
        [Tooltip("Live-ball cap per PLAYER, enforced per DOMAIN at forge time (cap x domain " +
                 "roster = that team's ceiling). At the cap a crystal simply collects " +
                 "normally — the forge quietly declines, nothing is culled or expired.")]
        [Min(1)] public int ballsPerPlayer = 2;

        [Header("Fauna (the cleanup crew waits outside the court)")]
        [Tooltip("Hold the cell's fauna outside the court while the cell is Calm (the Astro " +
                 "League pattern): FaunaExclusionRadius sweeps to the court radius, and drops " +
                 "to 0 when the volume ladder says the pitch has silted up.")]
        public bool faunaWaitOutsideCourt = true;

        [Tooltip("Fraction of the court radius the fauna exclusion holds at while Calm.")]
        [Range(0.2f, 1.5f)] public float faunaExclusionCourtFraction = 1f;

        [Tooltip("Seconds the exclusion radius takes to sweep open/closed, so the swarm " +
                 "visibly pours over the wall instead of teleporting past it.")]
        [Min(0.1f)] public float faunaExclusionSweepSeconds = 3f;

        [Header("AI rollers")]
        [Tooltip("Seconds between an AI's target re-selections (which ball / which crystal). " +
                 "Between samples it keeps flying at the live position of what it picked.")]
        [Min(0.25f)] public float aiRetargetSeconds = 1f;

        [Tooltip("How far BEHIND its ball (on the far side from the chosen hoop) an AI aims, " +
                 "so driving to the aim point pushes the ball hoopward — the Astro League " +
                 "striker's approach-lead idea with the hoop as the goal.")]
        [Min(1f)] public float aiApproachLead = 45f;

        [Tooltip("Seconds of the ball's own velocity an AI leads a MOVING ball by, so it " +
                 "intercepts where the ball is going instead of trailing where it was.")]
        [Range(0f, 3f)] public float aiInterceptLeadSeconds = 0.5f;

        // ── Helpers (clamped per-intensity lookups; intensity is 1-based) ─────

        public float CourtRadiusForIntensity(int intensity) =>
            Lookup(courtRadiusByIntensity, intensity, 560f);

        public int HoopCountForIntensity(int intensity) =>
            Mathf.Max(1, LookupInt(hoopCountByIntensity, intensity, 3));

        public float HoopMouthRadiusForIntensity(int intensity) =>
            Lookup(hoopMouthRadiusByIntensity, intensity, 50f);

        float Lookup(float[] table, int intensity, float fallback)
        {
            if (table == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(intensity - 1, 0, table.Length - 1)];
        }

        int LookupInt(int[] table, int intensity, int fallback)
        {
            if (table == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(intensity - 1, 0, table.Length - 1)];
        }
    }
}
