using CosmicShore;
using CosmicShore.Core;
using UnityEngine;

[CreateAssetMenu(fileName = "SeedWallAction", menuName = "ScriptableObjects/Vessel Actions/Seed Wall")]
public class SeedWallActionSO : ShipActionSO
{
    public enum AssemblerKind { Wall, Gyroid }
    public enum ShieldMode   { None, Shield, SuperShield }

    [Header("Resource")]
    [Tooltip("Which resource pool to consume when seeding.")]
    [SerializeField] private int resourceIndex = 0;

    [Tooltip("Cost = MaxAmount / enhancementsPerFullAmmo")]
    [SerializeField] private float enhancementsPerFullAmmo = 3f;

    [Header("Seeding Rules")]
    [SerializeField] private bool requireExistingTrailBlock = true;
    [SerializeField] private AssemblerKind assemblerType = AssemblerKind.Wall;
    [SerializeField] private ShieldMode shieldOnSeed = ShieldMode.SuperShield;
    [SerializeField] private int bondingDepth = 50;

    // Expose
    public int ResourceIndex => resourceIndex;
    public float EnhancementsPerFullAmmo => enhancementsPerFullAmmo;
    public bool RequireExistingTrailBlock => requireExistingTrailBlock;
    public AssemblerKind AssemblerType => assemblerType;
    public ShieldMode ShieldOnSeed => shieldOnSeed;
    public int BondingDepth => bondingDepth;

    public float ComputeCost(ResourceSystem rs)
    {
        if (rs == null || resourceIndex < 0 || resourceIndex >= rs.Resources.Count) return 0f;
        var res = rs.Resources[resourceIndex];
        if (res == null || res.MaxAmount <= 0f) return 0f;
        var denom = Mathf.Max(0.0001f, enhancementsPerFullAmmo);
        return res.MaxAmount / denom;
    }

    public override void StartAction(ActionExecutorRegistry execs)
        => execs?.Get<SeedAssemblerActionExecutor>()?.StartSeed(this, ShipStatus);

    public override void StopAction(ActionExecutorRegistry execs)
        => execs?.Get<SeedAssemblerActionExecutor>()?.StopSeedCompletely();
}
