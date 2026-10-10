using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    [CreateAssetMenu(fileName = "CloakSeedWallAction", menuName = "ScriptableObjects/Vessel Actions/Cloak + Seed Wall")]
    public class CloakSeedWallActionSO : ShipActionSO
    {
        [Header("Cooldown")]
        [Min(0.01f)] 
        [SerializeField] private float cooldownSeconds = 20f;

        [Header("Resources (consumed by SeedAssemblerActionExecutor.StartSeed)")]
        [SerializeField] private SeedWallActionSO seedWallSo;
        public SeedWallActionSO SeedWallSo => seedWallSo;

        [Header("Ship Cloak (Owner Visual)")]
        [SerializeField] private Material ghostShipMaterial;
        public Material GhostShipMaterial => ghostShipMaterial;
        [Header("Prism Cloak (MaterialPropertyAnimator)")]
        [SerializeField] private Material prismCloakTransparent; // 20% style
        [SerializeField] private Material prismCloakOpaque;      // matching opaque look

        public Material PrismCloakTransparent => prismCloakTransparent;
        public Material PrismCloakOpaque      => prismCloakOpaque;

        [Header("Pilot's own view")]
        [Tooltip("How visible the cloaked hull stays to the pilot FLYING it (the ghost material's " +
                 "alpha on their screen only). Everyone else sees nothing. 0 hides it from the " +
                 "pilot too, which leaves them nothing to steer by.")]
        [SerializeField, Range(0f, 1f)] private float pilotGhostAlpha = 0.3f;
        [Tooltip("Brightness the pilot sees their own cloaked trail at (Prism.SetColorShade), so " +
                 "they can tell it is hidden from everyone else without losing it. Everyone else " +
                 "sees the trail cloaked.")]
        [SerializeField, Range(0f, 1f)] private float pilotTrailShade = 0.35f;

        [Header("Illusion")]
        [Tooltip("Seconds the illusion left at the cloak point takes to collapse into the " +
                 "super-shielded seed when the cloak ends, instead of vanishing.")]
        [SerializeField, Min(0f)] private float illusionMorphSeconds = 0.6f;

        public float PilotGhostAlpha => pilotGhostAlpha;
        public float PilotTrailShade => pilotTrailShade;
        public float IllusionMorphSeconds => illusionMorphSeconds;


        public float CooldownSeconds => cooldownSeconds;

        /// <summary>
        /// Cloak — UNLESS the pilot is looking down the scope, in which case this trigger belongs
        /// to the sniper shot instead (R_VesselActions/SERPENT_SNIPER_SCOPE.md).
        ///
        /// <para>The Serpent binds BOTH this and <c>SniperShotActionSO</c> to the right trigger,
        /// and <c>R_VesselActionHandler</c> runs every action bound to an input, so each one asks
        /// the scope whether the context is its own. Keeping the question here rather than in the
        /// handler means neither ability learns about the other's wiring, and re-binding either
        /// one changes nothing about the rule.</para>
        ///
        /// <para>Safe on any vessel: <c>ActionExecutorRegistry.Get</c> falls back to a child
        /// search and returns null on a hull that carries no scope, which reads as "not scoped"
        /// and leaves the cloak behaving exactly as it always has.</para>
        /// </summary>
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus status)
        {
            var scope = execs?.Get<SniperScopeActionExecutor>();
            if (scope != null && scope.IsScoped) return;

            execs?.Get<CloakSeedWallActionExecutor>()?.Toggle(this, status);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus status) { }
    }
}