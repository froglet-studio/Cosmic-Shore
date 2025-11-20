using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game
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

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus status)
            => execs?.Get<CloakSeedWallActionExecutor>()?.Toggle(this, status);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus status) { }
    }
}