using CosmicShore;
using UnityEngine;

[CreateAssetMenu(fileName = "CloakSeedWallAction", menuName = "ScriptableObjects/Vessel Actions/Cloak + Seed Wall")]
public class CloakSeedWallActionSO : ShipActionSO
{
    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 20f;

    [Header("Vessel Visibility")]
    [SerializeField] private bool hideShipDuringCooldown = true;
    
    [Header("Seed Wall")]
    [Tooltip("If true, action requires at least one existing trail block to plant seed on.")]
    [SerializeField] private bool requireExistingTrailBlock = true;

    [Header("Ghost Vessel")]
    [SerializeField] private float  ghostLifetime         = 0f;  // 0 = same as cooldown
    [SerializeField] private float  ghostScaleMultiplier  = 1f;
    [SerializeField] private Material ghostMaterialOverride;
    [Tooltip("Applied after reading vessel rotation; use (0,180,0) if baked mesh is flipped.")]
    [SerializeField] private Vector3 ghostEulerOffset = new Vector3(0f, 180f, 0f);
    [Tooltip("Enable subtle idle motion so the ghost feels alive.")]
    [SerializeField] private bool   ghostIdleMotion  = true;
    [SerializeField] private float  ghostBobAmplitude = 0.15f;
    [SerializeField] private float  ghostBobSpeed     = 1.2f;
    [SerializeField] private float  ghostYawSpeed     = 10f; // deg/sec
    
    [Header("Cloak Materials")]
    [SerializeField] private Material shipCloakMaterial;
    [SerializeField] private Material prismCloakMaterial;

    public float CooldownSeconds            => cooldownSeconds;
    public bool  HideShipDuringCooldown     => hideShipDuringCooldown;
    public bool  RequireExistingTrailBlock  => requireExistingTrailBlock;

    public float    GhostLifetime        => ghostLifetime;
    public float    GhostScaleMultiplier => ghostScaleMultiplier;
    public Material GhostMaterialOverride=> ghostMaterialOverride;
    public Vector3  GhostEulerOffset     => ghostEulerOffset;
    public bool     GhostIdleMotion      => ghostIdleMotion;
    public float    GhostBobAmplitude    => ghostBobAmplitude;
    public float    GhostBobSpeed        => ghostBobSpeed;
    public float    GhostYawSpeed        => ghostYawSpeed;

    public Material ShipCloakMaterial  => shipCloakMaterial;
    public Material PrismCloakMaterial => prismCloakMaterial;


    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<CloakSeedWallActionExecutor>()?.Begin(this, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<CloakSeedWallActionExecutor>()?.End();
}
