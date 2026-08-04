using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Dolphin's crystal seeding: hold to preview a crystal out in front of the
    /// vessel, release to plant it. What gets planted is a TEAM crystal — only the pilot's domain
    /// can collect it, the same rule Skim Race's track crystals run on.
    ///
    /// Element → parameter: CHARGE owns this ability. Its multiplier divides the recharge, and its
    /// level-5 upgrade raises the carry limit to <see cref="UpgradedCharges"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "DeployTeamCrystalAction", menuName = "ScriptableObjects/Vessel Actions/Deploy Team Crystal")]
    public class DeployTeamCrystalActionSO : ShipActionSO
    {
        [Header("Setup")]
        [Tooltip("How far out in front of the nose the preview sits, in local units.")]
        [SerializeField] private float forwardOffset = 12f;
        [Tooltip("Opacity the preview is held at so it reads as 'not planted yet' (0-1).")]
        [SerializeField] private float fadeValue = 0.5f;
        [SerializeField] private LayerMask rayMask;

        [Header("Cooldown")]
        [Tooltip("Seconds to recharge one crystal at the RESTING charge level.")]
        [SerializeField] private float cooldown = 30f;
        [Tooltip("Absolute floor on the recharge in seconds, so the ability can never become free.")]
        [SerializeField, Min(0.1f)] private float minCooldown = 4f;
        [Tooltip("Crystals the Dolphin can carry at once once Charge's level-5 upgrade is active. " +
                 "Below the upgrade the limit is always 1.")]
        [SerializeField, Min(1)] private int upgradedCharges = 2;

        [Header("Elemental (Charge)")]
        [Tooltip("CHARGE -> cooldown: multiplier on Cooldown at Charge level 10 (1 at the resting " +
                 "level, extrapolating into the deficit band so debuffed Charge LENGTHENS the " +
                 "recharge). Authored HERE rather than through the map's generic multiplier, which " +
                 "ChargeBoostActionExecutor already consumes for the boost peak - reading both " +
                 "would double-dip one element into two parameters.")]
        [SerializeField] private float cooldownMultiplierAtFullCharge = 0.5f;
        [Tooltip("Floor for the Charge cooldown multiplier so overcharge can never zero the recharge.")]
        [SerializeField] private float minCooldownMultiplier = 0.35f;

        [Header("Preview (continuity of existence)")]
        [Tooltip("Seconds the preview takes to bloom into being. Nothing pops in.")]
        [SerializeField, Min(0.01f)] private float previewBloomDuration = 0.25f;
        [Tooltip("Seconds the preview takes to wither away when it is cancelled. Nothing pops out.")]
        [SerializeField, Min(0.01f)] private float previewWitherDuration = 0.18f;

        public float ForwardOffset => forwardOffset;
        public float FadeValue => fadeValue;
        public LayerMask RayMask => rayMask;
        public float Cooldown => cooldown;
        public float MinCooldown => minCooldown;
        public int UpgradedCharges => upgradedCharges;
        public float CooldownMultiplierAtFullCharge => cooldownMultiplierAtFullCharge;
        public float MinCooldownMultiplier => Mathf.Max(0.01f, minCooldownMultiplier);
        public float PreviewBloomDuration => previewBloomDuration;
        public float PreviewWitherDuration => previewWitherDuration;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<DeployTeamCrystalActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<DeployTeamCrystalActionExecutor>()?.Commit(this, vesselStatus);
    }
}
