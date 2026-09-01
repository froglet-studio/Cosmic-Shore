using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The swordfish's VESSEL STRIKE - everything about the charge that is not ordinary predator
    /// data (<see cref="LightFaunaDataSO"/> still owns cruising, prey hunting, territory and the
    /// hunt-pulse clock the strike is gated on). One asset per species, plus one strike profile
    /// per ELEMENT so the four variants of the flagship feel different without a per-element
    /// prefab (Docs/ECOSYSTEM.md §40: the element is the whole variation a species has, and §42:
    /// the swordfish). Tuning lives here, never on the prefab.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SwordfishStrikeData",
        menuName = "ScriptableObjects/LifeForms/FaunaPrefab/Swordfish Strike Data")]
    public class SwordfishStrikeDataSO : ScriptableObject
    {
        [Header("Sensing (hunt windows only)")]
        [Tooltip("A pilot inside this radius of the swordfish is a threat it will pursue - only " +
                 "while its hunt window is open (LightFaunaDataSO.huntIntervalSeconds / " +
                 "huntDurationSeconds), so the creature rests between attacks like every predator.")]
        [Min(0f)] public float aggroRadius = 280f;
        [Tooltip("Pursuit closes to this distance, then the wind-up begins.")]
        [Min(1f)] public float strikeRange = 110f;

        [Header("Telegraph (the readable wind-up)")]
        [Tooltip("Seconds the swordfish coils before it commits. The strike point locks at the END " +
                 "of this - everything after is dodgeable by moving.")]
        [Min(0.1f)] public float telegraphSeconds = 1.1f;
        [Tooltip("It backs off slowly, nose on the target, while it coils.")]
        [Min(0f)] public float telegraphRetreatSpeed = 12f;

        [Header("Lunge (the sword)")]
        [Tooltip("Straight-line speed of the charge. The bill's danger prisms are the ONLY damage - " +
                 "the ordinary danger-prism contact chain, nothing bespoke.")]
        [Min(0f)] public float lungeSpeed = 150f;
        [Tooltip("The lunge aims THROUGH the pilot: this many units past the locked point, so the " +
                 "bill runs the whole way through instead of stopping on the hull.")]
        [Min(0f)] public float lungeOvershoot = 30f;
        [Tooltip("Hard cap on a lunge - it ends here even if the point was never reached.")]
        [Min(0.1f)] public float lungeMaxSeconds = 1.5f;
        [Tooltip("Arriving within this distance of the lunge point ends the lunge.")]
        [Min(0.1f)] public float lungeArriveRadius = 12f;

        [Header("Recover (the punish window)")]
        [Tooltip("Seconds it drifts spent after a lunge - fins flared, slow, exposed.")]
        [Min(0f)] public float recoverSeconds = 2f;
        [Tooltip("Speed while recovering, as a fraction of cruise.")]
        [Range(0f, 1f)] public float recoverSpeedFraction = 0.3f;
        [Tooltip("Minimum seconds between two strikes, on top of the hunt-pulse gate.")]
        [Min(0f)] public float strikeCooldownSeconds = 7f;

        [Header("Targets")]
        [Tooltip("Only pilots of OTHER domains are threats: a cell's swordfish is its guardian, not " +
                 "a hazard to the pilots whose colour it spawned in. (Its bill still hurts everyone " +
                 "it touches - danger prisms are never safe to their own domain, the locked rule.)")]
        public bool opposingDomainsOnly = true;

        [Header("Element identity")]
        [Tooltip("Per-element multipliers on the numbers above. Charge strikes fastest, Mass is the " +
                 "biggest body and the slowest wind-up, Space reaches furthest, Time strikes most often.")]
        public ElementStrikeProfile[] profiles = Array.Empty<ElementStrikeProfile>();

        static readonly ElementStrikeProfile Neutral = new();

        /// <summary>The profile for <paramref name="element"/>, or a neutral one (all x1).</summary>
        public ElementStrikeProfile ProfileFor(Element element)
        {
            for (int i = 0; i < profiles.Length; i++)
                if (profiles[i] != null && profiles[i].element == element) return profiles[i];
            return Neutral;
        }
    }

    [Serializable]
    public class ElementStrikeProfile
    {
        public Element element = Element.None;
        [Min(0.1f)] public float lungeSpeedMultiplier = 1f;
        [Min(0.1f)] public float telegraphMultiplier = 1f;
        [Min(0.1f)] public float rangeMultiplier = 1f;
        [Min(0.1f)] public float cooldownMultiplier = 1f;
    }
}
