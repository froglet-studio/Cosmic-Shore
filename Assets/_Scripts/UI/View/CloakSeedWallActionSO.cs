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


        // NOTE:
        // Prism cloak is now handled by MaterialPropertyAnimator on each Prism
        // via Prism.SetTransparency(true/false). No prism cloak materials here.

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