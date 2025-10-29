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

        [Header("Prism Cloak")]
        [Tooltip("Mandatory: prisms spawned during cloak will be SWAPPED to this material.")]
        [SerializeField] private Material prismCloakMaterial;

        [Header("Ship Cloak")]
        [Tooltip("If set, ship swaps to this; else we fade to this alpha.")]
        [SerializeField] private Material shipCloakMaterialOptional;
        [Range(0f,1f)] [SerializeField] private float shipCloakAlphaFallback = 0.2f;

        public float CooldownSeconds => cooldownSeconds;

        public override void StartAction(ActionExecutorRegistry execs)
            => execs?.Get<CloakSeedWallActionExecutor>()?.Toggle(this, ShipStatus);

        public override void StopAction(ActionExecutorRegistry execs) { }
    }
}