using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the predator's jaw rig from its hunt-pulse aggression state
    /// (<see cref="LightFauna.IsActivelyHunting"/>): the two mouth MultiAimConstraints
    /// blend weight 0 (FBX swim pose — closed) → 1 (aimed at MawTarget — open), so the
    /// mouth yawns open when a hunt window begins and eases shut when the predator goes
    /// back to cruising. Opening is deliberately faster than closing; the rig's
    /// DampedTransform constraints add organic lag on top, so nothing ever snaps.
    ///
    /// Perf: the rig itself already evaluates every frame (RigBuilder + Animator on this
    /// prefab, weights authored at 1), so this adds no rig cost. The driver early-outs
    /// on one float compare per frame whenever the mouth is settled — weight writes only
    /// happen during the few seconds of transition per hunt cycle.
    /// </summary>
    public class SharkJawDriver : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("The mouth aim constraints (top + bottom jaw). Auto-found in children when left empty.")]
        [SerializeField] MultiAimConstraint[] jawConstraints;

        [Header("Timing")]
        [Tooltip("Seconds for the mouth to yawn fully open when a hunt window begins.")]
        [Min(0.05f)] [SerializeField] float openSeconds = 0.6f;
        [Tooltip("Seconds for the mouth to ease fully shut when the predator returns to cruising " +
                 "(also runs as it withers — the dying shark's mouth closes). Slower than opening " +
                 "so the strike reads sharp and the calm-down reads lazy.")]
        [Min(0.05f)] [SerializeField] float closeSeconds = 1.8f;

        LightFauna _fauna;
        float _openness;

        void Awake()
        {
            _fauna = GetComponentInParent<LightFauna>();
            if (jawConstraints == null || jawConstraints.Length == 0)
                jawConstraints = GetComponentsInChildren<MultiAimConstraint>(true);

            // Start from the authored weight and RAMP to the live state instead of
            // snapping (continuity: nothing pops, not even a jaw) — a fresh spawn
            // opens with a rest stretch, so the authored-open mouth eases shut while
            // the body grows in.
            _openness = jawConstraints != null && jawConstraints.Length > 0 && jawConstraints[0]
                ? jawConstraints[0].weight
                : 0f;
        }

        void Update()
        {
            float target = _fauna && _fauna.IsActivelyHunting ? 1f : 0f;

            // Settled between pulse transitions — the whole driver is this one compare.
            if (_openness == target) return;

            _openness = Mathf.MoveTowards(_openness, target,
                Time.deltaTime / (target > _openness ? openSeconds : closeSeconds));

            for (int i = 0; i < jawConstraints.Length; i++)
            {
                var constraint = jawConstraints[i];
                if (constraint) constraint.weight = _openness;
            }
        }
    }
}
