using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

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

        [Header("Cloak look")]
        [Tooltip("The cloaked hull as its own pilot sees it, and the glow the hull reappears " +
                 "through for everyone when the cloak ends: CloakGhost.mat (CosmicShore/CloakGhost, " +
                 "an additive Fresnel outline). Tinted with the pilot's domain colour at runtime.")]
        [SerializeField] private Material ghostShipMaterial;
        [Tooltip("Runtime GameData: its ColorSet gives the ghost the pilot's domain colour, the " +
                 "same source the Seed Wall's crystal beams read.")]
        [SerializeField] private GameDataSO gameData;
        [Tooltip("How much of the pilot's own trail stays drawn while cloaked, through the prism " +
                 "screen-door dither (BlockGraph _Alpha, see PrismOcclusionCorridor.hlsl). Everyone else's " +
                 "view fades it to 0. Replaces darkening it, which read as a dead trail.")]
        [SerializeField, Range(0f, 1f)] private float pilotTrailAlpha = 0.4f;
        [Tooltip("Seconds the cloak takes to fade in: the trail dissolves and the pilot's hull " +
                 "settles from a flash into its ghost outline.")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.4f;
        [Tooltip("Seconds the reveal takes when the cloak ends: the ghost brightens, the hull " +
                 "returns under the flash, the glow fades off it and the trail fills back in.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.7f;
        [Tooltip("Peak of the ghost's whole-body glow (CloakGhost _Flare) at the two material " +
                 "swaps, into the cloak and back out. It is what hides the cut.")]
        [SerializeField, Range(0f, 4f)] private float transitionFlare = 1.5f;

        [Header("Illusion")]
        [Tooltip("Seconds the illusion left at the cloak point takes to collapse into the " +
                 "super-shielded seed when the cloak ends, instead of vanishing.")]
        [SerializeField, Min(0f)] private float illusionMorphSeconds = 0.6f;

        public Material GhostShipMaterial => ghostShipMaterial;
        public GameDataSO GameData => gameData;
        public float PilotTrailAlpha => pilotTrailAlpha;
        public float FadeInSeconds => fadeInSeconds;
        public float FadeOutSeconds => fadeOutSeconds;
        public float TransitionFlare => transitionFlare;
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