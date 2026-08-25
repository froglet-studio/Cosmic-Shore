using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// All tuning for the worm colony kaiju (Docs/ECOSYSTEM.md §23) — the boss-scale
    /// connected population of Head / Body / Tail segment fauna. One shared asset per
    /// deployment; per CLAUDE.md Config Separation no numbers live on the prefabs.
    ///
    /// The three clocks that are LEGAL here, and why (invariant review):
    ///  • Growth (new segments) is funded by FEEDING only — feedsPerSegment, never a
    ///    wall-clock (the old WormManager's 10s growth tick was the banned oscillator).
    ///  • End differentiation (a wound hardening into a new danger head/tail) is a
    ///    STATE CHANGE of existing mass, not mass creation — same legal class as
    ///    LifeForm's shield-regen cadence — and it is gated on the colony being fed
    ///    (a starving worm cannot harden its wounds).
    ///  • Starvation shedding imposes death only through the standard starvation
    ///    channel (population bounded by consumption, never a lifespan).
    /// </summary>
    [CreateAssetMenu(
        fileName = "WormColonyConfig",
        menuName = "ScriptableObjects/LifeForms/FaunaPrefab/Worm Colony Data")]
    public class WormColonyConfigSO : ScriptableObject
    {
        [Header("Colony shape")]
        [Tooltip("Segments a fresh colony spawns with (1 head + N-2 bodies + 1 tail). " +
                 "Minimum 3 so the spawned worm has all three fauna types.")]
        [Min(3)] public int SpawnSegmentCount = 8;
        [Tooltip("Rest distance between BODY segment centres, in MODEL units (multiplied " +
                 "by KaijuScale and the taper). Derived from the 2024 authoring, where the " +
                 "body model rendered at localScale 1 and the authored chain gaps were " +
                 "8.05 / 8.39 / 8.63 / 8.71 — so the segments nearly touch. gap÷modelScale " +
                 "is the invariant: keep this ≈ the body mesh's length or the worm reads " +
                 "as beads on a string instead of one animal.")]
        [Min(0.1f)] public float SegmentSpacing = 8.4f;
        [Tooltip("Uniform scale applied to every segment root at spawn — the kaiju dial. " +
                 "Also scales the effective spacing so the body stays connected.")]
        [Min(0.1f)] public float KaijuScale = 3f;
        [Tooltip("Per-segment size taper down the chain (segment i scales by this^i) — " +
                 "the head is the biggest thing on the worm and the tail tapers away. " +
                 "Recovered from the 2024 chain's authored 0.9-per-segment shrink. Links' " +
                 "rest spacing tapers with it so the gaps close toward the tail. Segments " +
                 "GLIDE to their taper target when topology changes (growth, splits) — " +
                 "the worm visibly re-proportions, never snaps.")]
        [Range(0.5f, 1f)] public float TaperPerSegment = 0.9f;
        [Tooltip("The head-to-first-segment gap, as a multiple of the body gap — the head " +
                 "needs room (recovered ratio from the 2024 chain: 21.5 ÷ 8.4 = 2.56).")]
        [Min(1f)] public float HeadGapMultiplier = 2.56f;
        [Tooltip("The into-tail gap, as a multiple of the body gap (2024 ratio: 15 ÷ 8.4 = 1.79).")]
        [Min(1f)] public float TailGapMultiplier = 1.79f;
        [Tooltip("Per-colony segment cap — a PERFORMANCE backstop (collider budget), not " +
                 "the population control (starvation is). Growth pauses at the cap; splits " +
                 "conserve the total so they can never exceed it.")]
        [Min(3)] public int MaxSegmentsPerWorm = 16;

        [Header("Movement (follow-the-leader slither)")]
        [Tooltip("Head cruise speed (world units/s).")]
        [Min(0f)] public float CruiseSpeed = 18f;
        [Tooltip("Head turn rate toward its goal (degrees/s) while cruising.")]
        [Min(0f)] public float TurnDegreesPerSecond = 40f;
        [Tooltip("Per-second exponential sharpness with which each segment closes onto its " +
                 "follow point behind its predecessor. Higher = stiffer chain.")]
        [Min(0f)] public float FollowSharpness = 5f;
        [Tooltip("Per-second slerp sharpness of each segment's look-at-predecessor rotation.")]
        [Min(0f)] public float RotationSharpness = 4f;
        [Tooltip("Slither wave: yaw oscillation (degrees) layered on the head's steered " +
                 "heading. The body inherits the wave through follow-the-leader.")]
        [Min(0f)] public float UndulationYawDegrees = 12f;
        [Tooltip("Slither temporal frequency (radians/s).")]
        [Min(0f)] public float UndulationFrequency = 2.2f;
        [Tooltip("Phase offset per whip segment down the chain (radians) — the traveling wave.")]
        public float UndulationPhaseStep = 0.9f;
        [Tooltip("Speed multipliers indexed by CellAggressionLevel (Level0/1/2) — the " +
                 "same escalation surface every fauna reads from the cell phase.")]
        public float[] SpeedByAggression = { 1f, 1.25f, 1.6f };
        [Tooltip("Behavior-tick cadence multipliers indexed by CellAggressionLevel.")]
        public float[] CadenceByAggression = { 1f, 0.7f, 0.45f };

        [Header("Colony separation (boid repulsion between worm POPULATIONS)")]
        [Tooltip("Another colony whose body comes within this distance of THIS colony's " +
                 "body pushes it away — the standard boid separation term, measured along " +
                 "the two worms' closest approach (both are long, so neither head-to-head " +
                 "nor head-to-their-nearest-segment describes how crowded they are). Size " +
                 "it around a body length so two kaiju share a cell without " +
                 "interpenetrating, and so a split's two halves peel apart. 0 = off.")]
        [Min(0f)] public float ColonySeparationRadius = 160f;
        [Tooltip("Weight of the separation term against the goal pull (goal weight is 1). " +
                 "The term is a unit direction scaled by a falloff that is 1 where the two " +
                 "bodies touch and 0 at the radius above, so this is a TRUE ratio: above 1 " +
                 "the repulsion beats the pull toward food at close range, which is what " +
                 "keeps a freshly split population from swimming home in convoy with the " +
                 "half it was cut from. Higher = worms give each other a wider berth and " +
                 "commit less to food.")]
        [Min(0f)] public float ColonySeparationWeight = 2.5f;

        [Header("Feeding — an APEX OMNIVORE (the head is the colony's mouth)")]
        [Tooltip("Seconds between colony behavior ticks (senses, feeding, growth, attacks).")]
        [Min(0.1f)] public float BehaviorTickSeconds = 1.5f;
        [Tooltip("Graze radius around the head. Edibility follows the canonical herbivore " +
                 "rule (Cell.IsPreyForHerbivore + Fauna.IsShieldedMass) — the kaiju grazes " +
                 "prism mass voraciously, AND hunts creatures (below), AND attacks players.")]
        [Min(0f)] public float MouthRadius = 28f;
        [Tooltip("PREDATOR half: any live creature whose root comes within this distance of " +
                 "the mouth (the head's fang centroid) is devoured — it breaks apart and " +
                 "suctions into the jaws, exactly like the shark's kill. Feeds the colony " +
                 "(so eating creatures also grows it). 0 = pure herbivore.")]
        [Min(0f)] public float FaunaBiteRange = 34f;
        [Tooltip("Cap on prisms suctioned per tick — bounds the implosion-VFX burst.")]
        [Min(1)] public int MaxBitesPerTick = 6;
        [Tooltip("Feeds (consumed prisms) that fund ONE new body segment blooming in " +
                 "behind the head. This is the only source of new worm mass — length is a " +
                 "readable record of how much the colony has eaten.")]
        [Min(1)] public int FeedsPerSegment = 24;
        [Tooltip("Scale-glide time constant (~95% settled in this many seconds): a grown " +
                 "segment blooms from zero, and every segment glides to its taper target " +
                 "when the chain re-proportions (growth, splits, differentiation) — " +
                 "continuity, nothing pops or snaps.")]
        [Min(0.05f)] public float SegmentBloomSeconds = 2f;

        [Header("Wound differentiation (head/tail regrow)")]
        [Tooltip("Seconds after losing an end before the adjacent body segment hardens " +
                 "into the missing head/tail (danger prisms engage + a heart is " +
                 "provisioned). THE souls-like window: chain end-kills faster than this " +
                 "and you always face soft tissue; slower and every kill is armored. " +
                 "Gated on the colony being fed — a starving worm cannot differentiate.")]
        [Min(0f)] public float EndRegrowSeconds = 18f;
        [Tooltip("Element of hearts provisioned for DIFFERENTIATED ends (authored, per " +
                 "the elemental contract — random is only the misconfig fallback). The " +
                 "spawn prefabs' authored crystals are unaffected.")]
        public Element RegrownEndElement = Element.Mass;

        [Header("Starvation (population bounded by consumption)")]
        [Tooltip("While the colony is starving (no feed for the prefab-authored " +
                 "starvationSeconds), it digests itself: the tail-most segment withers " +
                 "every this many seconds. A 1-segment starving worm dies outright.")]
        [Min(0.5f)] public float StarvationShedIntervalSeconds = 12f;

        [Header("Attacks (souls-like telegraph grammar)")]
        [Tooltip("Vessel detection radius around the head — inside it, during a hunt " +
                 "window, the kaiju PURSUES the pilot. Uses the shared physics scratch " +
                 "masked to non-prism layers — never a prism query.")]
        [Min(0f)] public float AggroRadius = 220f;
        [Tooltip("Distance at which a pursued pilot triggers the strike wind-up. Outside " +
                 "it the worm chases (nose-on, faster); inside it, it rears back and lunges.")]
        [Min(1f)] public float StrikeRange = 90f;
        [Tooltip("Speed multiplier while actively chasing a pilot — the pursuit visibly " +
                 "closes rather than drifts.")]
        [Min(1f)] public float PursuitSpeedMultiplier = 1.45f;
        [Tooltip("Turn-rate multiplier while chasing (the head tracks a juking pilot).")]
        [Min(1f)] public float PursuitTurnMultiplier = 2f;
        [Tooltip("Attack pulses: a hunt window opens every this many seconds (0 = always " +
                 "hunting). Outside the window the worm only cruises and grazes — " +
                 "guaranteed downtime between assaults, same pattern as the shark.")]
        [Min(0f)] public float HuntIntervalSeconds = 26f;
        [Tooltip("How long each hunt window lasts (clamped to the interval).")]
        [Min(0f)] public float HuntDurationSeconds = 12f;
        [Tooltip("Telegraph: seconds the head rears back, slowed and coiling, before the " +
                 "lunge — the readable wind-up that makes the strike dodgeable.")]
        [Min(0.1f)] public float TelegraphSeconds = 1.2f;
        [Tooltip("Undulation amplitude multiplier during the telegraph coil.")]
        [Min(1f)] public float TelegraphAmplitudeMultiplier = 2.5f;
        [Tooltip("Lunge speed (world units/s) toward the point locked at telegraph end.")]
        [Min(0f)] public float LungeSpeed = 70f;
        [Tooltip("Lunge ends when the head is within this distance of the locked point…")]
        [Min(0.1f)] public float LungeArriveRadius = 10f;
        [Tooltip("…or after this many seconds, whichever comes first.")]
        [Min(0.1f)] public float LungeMaxSeconds = 2.5f;
        [Tooltip("Recovery: seconds of slow, straightened drift after a lunge — the " +
                 "punish window where the body is easiest to strafe.")]
        [Min(0f)] public float RecoverSeconds = 2.5f;
        [Tooltip("Fraction of cruise speed during recovery.")]
        [Range(0f, 1f)] public float RecoverSpeedFraction = 0.35f;

        [Header("Tail whip")]
        [Tooltip("A vessel loitering within this radius of the tail (during a hunt " +
                 "window) provokes a whip — the rear segments' slither amplitude bursts " +
                 "so the danger tail sweeps through its neighborhood.")]
        [Min(0f)] public float TailWhipRadius = 45f;
        [Tooltip("Whip burst duration (seconds).")]
        [Min(0.1f)] public float TailWhipSeconds = 1.5f;
        [Tooltip("Cooldown between whips (seconds).")]
        [Min(0f)] public float TailWhipCooldownSeconds = 8f;
        [Tooltip("Lateral swing of the whipped segments' follow points (world units, " +
                 "multiplied by KaijuScale).")]
        [Min(0f)] public float WhipLateralAmplitude = 5f;
        [Tooltip("Whip oscillation frequency (radians/s) — faster than the cruise slither.")]
        [Min(0f)] public float WhipFrequency = 7f;
        [Tooltip("How many tail-most segments the whip amplifies.")]
        [Min(1)] public int WhipSegmentCount = 3;
    }
}
