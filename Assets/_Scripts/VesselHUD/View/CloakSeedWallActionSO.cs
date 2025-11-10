using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "CloakSeedWallAction", menuName = "ScriptableObjects/Vessel Actions/Cloak + Seed Wall")]
    public class CloakSeedWallActionSO : ShipActionSO
    {
        [Header("Cooldown")]
        [Min(0.01f)] [SerializeField] private float cooldownSeconds = 20f;

        [Header("Resources (consumed by SeedAssemblerActionExecutor.StartSeed)")]
        [SerializeField] private SeedWallActionSO seedWallSo;
        public SeedWallActionSO SeedWallSo => seedWallSo;

        [Header("Prism Cloak")] [SerializeField]
        private Material prismCloakMaterial;

        [Header("Ship Cloak (Owner Visual)")]
        [SerializeField] private Material ghostShipMaterial;

        [Header("Prism Cloak Materials")]
        [SerializeField] private Material prismLocalCloakMaterial;
        [SerializeField] private Material prismRemoteCloakMaterial; 

        [Header("Ship Cloak (Fallback for non-owners)")]
        [SerializeField] private Material shipCloakMaterialOptional;
        [Range(0f,1f)] [SerializeField] private float shipCloakAlphaFallback = 0.2f;

        public float CooldownSeconds => cooldownSeconds;

        public Material GhostShipMaterial => ghostShipMaterial;
        public Material PrismLocalCloakMaterial => prismLocalCloakMaterial;
        public Material PrismRemoteCloakMaterial => prismRemoteCloakMaterial;
        public Material PrismCloakMaterialLegacy => prismCloakMaterial;

        public override void StartAction(ActionExecutorRegistry execs)
            => execs?.Get<CloakSeedWallActionExecutor>()?.Toggle(this, ShipStatus);

        public override void StopAction(ActionExecutorRegistry execs) { }
    }
}