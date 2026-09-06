using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every tunable number for Tollway in one asset
    /// (Assets/_SO_Assets/Games/TollwaySettings.asset) — the ScarabScrambleSettingsSO /
    /// AstroLeagueSettingsSO pattern. The TOLL TARGET is deliberately NOT here: it lives in
    /// EndConditionOverridesSO (FrogletTools ▸ Game Modes ▸ End Game Conditions), like every
    /// other mode's win count.
    ///
    /// Two other things are deliberately absent. BALL PHYSICS belongs to the ball
    /// (AstroLeagueBall reads the settings asset serialized on its own prefab, so a forged ball
    /// behaves identically in every context). And the SWITCH's own numbers — ring radius,
    /// recharge cadence, the standing-switch ceiling, the refund a threading pays — belong to
    /// PlaceSwitchActionSO, because they are the VESSEL's, not this mode's: a Scarab plants
    /// rings in freestyle and in Scramble too, and a mode-local copy would be a second author
    /// of the same quantity. This asset owns only the COURT, the AI, and the feel around
    /// scoring.
    /// </summary>
    [CreateAssetMenu(fileName = "TollwaySettings", menuName = "ScriptableObjects/Arcade/TollwaySettings")]
    public class TollwaySettingsSO : ScriptableObject
    {
        [Header("Court (the cell nucleus IS the court)")]
        [Tooltip("Court sphere radius per intensity (index 0 = intensity 1). The nucleus is " +
                 "resized to this and the ball reflects off it — the ball supplies that wall " +
                 "itself (AstroLeagueBall.ResolveNucleusBoundary), so resizing the nucleus IS " +
                 "the whole act of building the court. A SPHERE on purpose: every carom sends a " +
                 "ball back through the middle, and in this mode a ball crossing the middle is " +
                 "a ball that might pay somebody's toll.")]
        public float[] courtRadiusByIntensity = { 480f, 560f, 640f, 720f };

        [Header("Toll posts (the CERTAIN PLACE a ring has to go)")]
        [Tooltip("How many sockets the court offers, per intensity (index 0 = intensity 1). A " +
                 "ring may only be planted AT a post, so this is simultaneously how much choice a " +
                 "pilot has and how contested the good lanes are. Too few and every match is the " +
                 "same three shots; too many and a post is always conveniently to hand, which is " +
                 "the degenerate placement this whole mechanism exists to remove.")]
        public int[] postCountByIntensity = { 10, 12, 14, 16 };

        [Tooltip("Inner edge of the band the posts are drawn in, as a fraction of the court " +
                 "radius. Held well off the middle on purpose: the core is where balls are forged " +
                 "and where the crystals respawn, so a socket there would be the old trivial " +
                 "placement wearing a new hat.")]
        [Range(0.15f, 0.95f)] public float postInnerCourtFraction = 0.4f;

        [Tooltip("Outer edge of the post band, as a fraction of the court radius. Kept inside the " +
                 "wall so a ring is never flush against it — a ball has to be able to come at a " +
                 "post from behind.")]
        [Range(0.2f, 0.98f)] public float postOuterCourtFraction = 0.85f;

        [Tooltip("How near a requested ring centre must land to a free post for the placement to " +
                 "be admitted (and snapped onto it). The centre is PlaceSwitchActionSO's " +
                 "placementDistance out along the pilot's course, so this is the mode's aiming " +
                 "window: you fly at a post and press when it is about that far ahead. Widen it " +
                 "and planting stops being a manoeuvre; narrow it and it stops being learnable.")]
        [Min(10f)] public float postClaimRadius = 70f;

        [Tooltip("Radius of a post's marker halo. Set to the switch's own unupgraded mouth so a " +
                 "post visibly states the size of ring it will accept; a MASS-heavy Scarab's ring " +
                 "then overhangs its socket, which is the upgrade reading itself out on the court.")]
        [Min(2f)] public float postMarkerRadius = 24f;

        [Header("Scoring feel")]
        [Tooltip("Seconds within which a SECOND toll paid by the SAME ball counts as a chain. " +
                 "One shot threading two rings is this mode's signature screamer, the way a " +
                 "multi-carom bank goal is Scramble's — the toast needs a window so a ball that " +
                 "wanders back through a ring a minute later does not claim one.")]
        [Min(0.5f)] public float chainWindowSeconds = 4f;

        [Header("Fauna (the cleanup crew waits outside the court)")]
        [Tooltip("Hold the cell's fauna outside the court while the cell is Calm (the Astro " +
                 "League / Scramble pattern): FaunaExclusionRadius sweeps to the court radius " +
                 "and drops to 0 once the volume ladder says the court has silted up. Here that " +
                 "silt is the MONUMENTS the players built, so the swarm arriving is the arena " +
                 "telling you how much has been scored.")]
        public bool faunaWaitOutsideCourt = true;

        [Tooltip("Fraction of the court radius the fauna exclusion holds at while Calm.")]
        [Range(0.2f, 1.5f)] public float faunaExclusionCourtFraction = 1f;

        [Tooltip("Seconds the exclusion radius takes to sweep open/closed, so the swarm visibly " +
                 "pours over the wall instead of teleporting past it.")]
        [Min(0.1f)] public float faunaExclusionSweepSeconds = 3f;

        [Header("AI tollkeepers")]
        [Tooltip("Seconds between an AI's target re-selections (which ball / which crystal). " +
                 "Between samples it keeps flying at the live position of what it picked.")]
        [Min(0.25f)] public float aiRetargetSeconds = 1f;

        [Tooltip("How far BEHIND its ball (on the far side from the ring it has chosen) an AI " +
                 "aims, so driving to the aim point pushes the ball ringward.")]
        [Min(1f)] public float aiApproachLead = 45f;

        [Tooltip("Seconds of the ball's own velocity an AI leads a MOVING ball by, so it " +
                 "intercepts where the ball is going instead of trailing where it was.")]
        [Range(0f, 3f)] public float aiInterceptLeadSeconds = 0.5f;

        [Tooltip("Minimum seconds between an AI's switch placements. It is a COOLDOWN, not a " +
                 "metronome: since rings may only go into toll posts, an AI presses when it is " +
                 "lined up on a free one and this only stops it emptying its whole bank at the " +
                 "first socket it reaches. An AI that never plants a ring can never score in this " +
                 "mode, so this is not polish — an all-AI domain would be an opponent that cannot " +
                 "play. Pace it near the vessel's own recharge " +
                 "(PlaceSwitchActionSO.rechargeSecondsPerCharge, 20s) so the AI spends roughly " +
                 "what it earns rather than banking charges it never uses.")]
        [Min(2f)] public float aiSwitchIntervalSeconds = 22f;

        [Tooltip("Seconds after the countdown before an AI plants its FIRST ring. Non-zero so " +
                 "the bots have flown somewhere before they start building, instead of stacking " +
                 "their opening rings on the spawn ring.")]
        [Min(0f)] public float aiFirstSwitchDelaySeconds = 5f;

        // ── Helpers (clamped per-intensity lookup; intensity is 1-based) ──────

        public float CourtRadiusForIntensity(int intensity)
        {
            if (courtRadiusByIntensity == null || courtRadiusByIntensity.Length == 0) return 560f;
            return courtRadiusByIntensity[Mathf.Clamp(intensity - 1, 0, courtRadiusByIntensity.Length - 1)];
        }

        public int PostCountForIntensity(int intensity)
        {
            if (postCountByIntensity == null || postCountByIntensity.Length == 0) return 12;
            return Mathf.Max(1,
                postCountByIntensity[Mathf.Clamp(intensity - 1, 0, postCountByIntensity.Length - 1)]);
        }
    }
}
