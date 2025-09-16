using CosmicShore;
using UnityEngine;

[CreateAssetMenu(fileName = "CloakSeedWallAction", menuName = "ScriptableObjects/Vessel Actions/Cloak + Seed Wall")]
public class CloakSeedWallActionSO : ShipActionSO
{
    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 20f;

    [Header("Vessel Visibility")]
    [SerializeField] private bool hideShipDuringCooldown = true;

    [Header("Local vs Remote Visibility")]
    [SerializeField, Range(0f, 1f)] private float localCloakAlpha = 0.2f;
    [SerializeField] private bool remoteFullInvisible = true;

    [Header("Vessel Fade")]
    [SerializeField] private float fadeOutSeconds = 0.25f;
    [SerializeField] private float fadeInSeconds  = 0.25f;
    [SerializeField] private bool hardToggleIfAnyOpaqueAtZero = true;

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

    // Expose config for the executor
    public float CooldownSeconds            => cooldownSeconds;
    public bool  HideShipDuringCooldown     => hideShipDuringCooldown;
    public float LocalCloakAlpha            => localCloakAlpha;
    public bool  RemoteFullInvisible        => remoteFullInvisible;
    public float FadeOutSeconds             => fadeOutSeconds;
    public float FadeInSeconds              => fadeInSeconds;
    public bool  HardToggleIfAnyOpaqueAtZero=> hardToggleIfAnyOpaqueAtZero;
    public bool  RequireExistingTrailBlock  => requireExistingTrailBlock;

    public float    GhostLifetime        => ghostLifetime;
    public float    GhostScaleMultiplier => ghostScaleMultiplier;
    public Material GhostMaterialOverride=> ghostMaterialOverride;
    public Vector3  GhostEulerOffset     => ghostEulerOffset;
    public bool     GhostIdleMotion      => ghostIdleMotion;
    public float    GhostBobAmplitude    => ghostBobAmplitude;
    public float    GhostBobSpeed        => ghostBobSpeed;
    public float    GhostYawSpeed        => ghostYawSpeed;

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<CloakSeedWallActionExecutor>()?.Begin(this, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<CloakSeedWallActionExecutor>()?.End();
}
