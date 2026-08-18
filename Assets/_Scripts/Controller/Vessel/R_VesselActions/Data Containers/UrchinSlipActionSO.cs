using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's escape: let go of the trail you are riding and phase out for a moment so
    /// you can leave without immediately re-latching onto the mass you are still inside.
    ///
    /// One button does both halves because they are one intent. Detaching alone drops the
    /// vessel into free flight still overlapping the ribbon, whose next prism re-triggers
    /// <c>VesselAttachPrismEffectSO</c> and snaps it straight back on — so the ghost window is
    /// not a flourish, it is what makes the detach mean anything.
    ///
    /// TIME is the element: how long the ghost lasts, i.e. how much room the escape buys.
    /// </summary>
    [CreateAssetMenu(fileName = "UrchinSlipAction",
        menuName = "ScriptableObjects/Vessel Actions/Urchin Slip")]
    public class UrchinSlipActionSO : ShipActionSO
    {
        [Header("Ghost")]
        [Tooltip("Ghost duration at RESTING Time (level 0), in seconds.")]
        [SerializeField, Min(0f)] float ghostSecondsAtRestingTime = 0.6f;

        [Tooltip("The same at Time level 10. Linear in LEVEL and extrapolated across the " +
                 "element system's full [-5, 15] band.")]
        [SerializeField, Min(0f)] float ghostSecondsAtFullTime = 1.6f;

        [Tooltip("Impulse along the vessel's own up axis when it lets go, so a detach visibly " +
                 "leaves the ribbon instead of sliding off the end of it.")]
        [SerializeField] float detachImpulse = 0f;

        /// <summary>
        /// How long the vessel phases out, from its LIVE Time level. Read at use time.
        /// </summary>
        public float ResolveGhostSeconds(IVesselStatus status)
        {
            var resources = status?.ResourceSystem;
            int level = resources ? resources.GetLevel(Element.Time) : 0;
            return GhostSecondsForLevel(level, ghostSecondsAtRestingTime, ghostSecondsAtFullTime);
        }

        /// <summary>Pure, so it is edit-mode testable. Extrapolated, not clamped.</summary>
        public static float GhostSecondsForLevel(int timeLevel, float atResting, float atFull)
            => Mathf.Max(0f, Mathf.LerpUnclamped(atResting, atFull, timeLevel / 10f));

        public float DetachImpulse => detachImpulse;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<UrchinSlipActionExecutor>()?.Slip(this);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
