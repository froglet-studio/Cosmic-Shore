using StarWriter.Core;
using StarWriter.Utility.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core.HangerBuilder;


[RequireComponent(typeof(Ship))]
public class TrailSpawner : MonoBehaviour
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Skimmer skimmer;

    [SerializeField] bool waitTillOutsideSkimmer = true;
    [SerializeField] bool shielded = false;

    public float offset = 0f;

    [SerializeField] float initialWavelength = 4f;
    [SerializeField] float minWavelength = 1f;

    float wavelength;

    public float gap;
    public Vector3 TargetScale;

    public int TrailLength { get { return trail.TrailList.Count; } }
    [SerializeField] float defaultWaitTime = .5f;
    
    public float waitTime = .5f;  // Time until the trail block appears - camera dependent
    public float startDelay = 2.1f;

    int spawnedTrailCount;

    Trail trail = new();
    Trail trail2 = new();

    Material blockMaterial;
    Material shieldedBlockMaterial;
    Ship ship;
    ShipStatus shipData;

    [SerializeField] bool warp = false;
    GameObject shards;

    Coroutine spawnTrailCoroutine;

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    public void SetShieldedBlockMaterial(Material material)
    {
        shieldedBlockMaterial = material;
    }

    public Material GetBlockMaterial()
    {
        return blockMaterial;
    }

    public List<TrailBlock> GetLastTwoBlocks() 
    {
        return new List<TrailBlock>
        {
            // ^1 is the hat operator for index of last element
            trail.TrailList[^1],
            trail2.TrailList[^1]
        };
    }

    public float TrailZScale => trailBlock.transform.localScale.z;

    public static GameObject TrailContainer;

    [Tooltip("This is serialized for debug visibility")]
    [SerializeField] bool spawnerEnabled = true;
    string ownerId;

    private void OnEnable()
    {
        GameManager.onGameOver += RestartAITrailSpawnerAfterDelay;
    }

    private void OnDisable()
    {
        GameManager.onGameOver -= RestartAITrailSpawnerAfterDelay;
    }

    void Start()
    {
        waitTime = defaultWaitTime;
        wavelength = initialWavelength;
        if (TrailContainer == null)
        {
            TrailContainer = new GameObject();
            TrailContainer.name = "TrailContainer";
        }

        shards = GameObject.FindGameObjectWithTag("field");
        ship = GetComponent<Ship>();
        shipData = GetComponent<ShipStatus>();

        spawnTrailCoroutine = StartCoroutine(SpawnTrailCoroutine());

        ownerId = ship.Player.PlayerUUID;
        XScaler = minBlockScale;
    }

    public void ToggleBlockWaitTime(bool state)
    {
        waitTime = state ? defaultWaitTime*3 : defaultWaitTime;
    }

    [Tooltip("Number of proximal blocks before trailBlock block size reaches minimum")]
    [SerializeField] public int MaxNearbyBlockCount = 10;
    [SerializeField] float minBlockScale = 1;
    [SerializeField] float maxBlockScale = 1;
    
    public float XScaler = 1;
    public float YScaler = 1;
    float ZScaler = 1;
    float Xscale;
    Coroutine lerper;

    public void SetNormalizedXScale(float normalizedXScale)
    {
        if (Xscale == normalizedXScale)
            return;

        Xscale = Mathf.Min(normalizedXScale, 1);
        float newXScaler = Mathf.Max(minBlockScale, maxBlockScale * Xscale);
        XLerper(newXScaler);
    }

    void XLerper(float newXScaler)
    {
        if (lerper != null) StopCoroutine(lerper);
        lerper = StartCoroutine(Tools.LerpingCoroutine(XScaler, newXScaler, 1.5f, (i) => { XScaler = i; }));
    }

    public void SetDotProduct(float amount)
    {
        ZScaler = Mathf.Max(minBlockScale, maxBlockScale * (1 - Mathf.Abs(amount)));
        wavelength = Mathf.Max(minWavelength, initialWavelength * Mathf.Abs(amount)); 
    }
     
    public void PauseTrailSpawner()
    {
        spawnerEnabled = false;
    }

    void RestartAITrailSpawnerAfterDelay()
    {
        // Called on EndGame to restart only the trail spawners for the AI
        if (gameObject != GameObject.FindWithTag("Player_Ship"))
        {
            StartCoroutine(RestartSpawnerAfterDelayCoroutine(waitTime));
        }
    }

    public void RestartTrailSpawnerAfterDelay()
    {
        // Called when extending game play to resume spawning trails for player and AI
        RestartTrailSpawnerAfterDelay(waitTime);
    }
    public void RestartTrailSpawnerAfterDelay(float waitTime)
    {
        // Called when extending game play to resume spawning trails for player and AI
        StartCoroutine(RestartSpawnerAfterDelayCoroutine(waitTime));
    }
    IEnumerator RestartSpawnerAfterDelayCoroutine(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        spawnerEnabled = true;
    }

    void CreateBlock(float halfGap, Trail trail)
    {
        var Block = Instantiate(trailBlock);
        Block.TargetScale = new Vector3(trailBlock.transform.localScale.x * XScaler / 2f - Mathf.Abs(halfGap), trailBlock.transform.localScale.y * YScaler, trailBlock.transform.localScale.z * ZScaler);
        TargetScale = Block.TargetScale;
        Block.transform.SetPositionAndRotation(transform.position - shipData.Course * offset + ship.transform.right * (Block.TargetScale.x/2f + Mathf.Abs(halfGap)) * (halfGap / Mathf.Abs(halfGap)), shipData.blockRotation);
        Block.transform.parent = TrailContainer.transform;
        Block.ownerId = isCharmed ? tempShip.Player.PlayerUUID : ship.Player.PlayerUUID;
        Block.PlayerName = isCharmed ? tempShip.Player.PlayerName : ship.Player.PlayerName;
        Block.Team = isCharmed ? tempShip.Team : ship.Team;
        Block.warp = warp;
        if (waitTillOutsideSkimmer) 
            Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / ship.GetComponent<ShipStatus>().Speed;
        if (shielded)
        {
            Block.GetComponent<MeshRenderer>().material = shieldedBlockMaterial;
            Block.Shielded = true;
        }
        else Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.GetComponent<BoxCollider>().size = Vector3.one + VectorDivision((Vector3)blockMaterial.GetVector("_spread"), Block.TargetScale);
        Block.Trail = trail;

        trail.Add(Block);
        Block.Index = trail.TrailList.IndexOf(Block);
        Block.ID = ownerId + "::" + spawnedTrailCount++;
        
        if (Block.warp)
            wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

    }

    Vector3 VectorDivision(Vector3 Vector1, Vector3 Vector2) // TODO: move to tools
    {
        return new Vector3(Vector1.x / Vector2.x, Vector1.y / Vector2.y, Vector1.z / Vector2.z);
    }

    public void ForceStartSpawningTrail()
    {
        StopCoroutine(spawnTrailCoroutine);
        spawnTrailCoroutine = StartCoroutine(SpawnTrailCoroutine());
    }

    IEnumerator SpawnTrailCoroutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            if (Time.deltaTime < .1f && spawnerEnabled && !shipData.Attached && shipData.Speed > .01f)
            {
                if (gap == 0)
                {
                    var Block = Instantiate(trailBlock);
                    Block.TargetScale = new Vector3(trailBlock.transform.localScale.x * XScaler, trailBlock.transform.localScale.y * YScaler, trailBlock.transform.localScale.z * ZScaler);
                    TargetScale = Block.TargetScale;
                    Block.transform.SetPositionAndRotation(transform.position - shipData.Course * offset, shipData.blockRotation);
                    Block.transform.parent = TrailContainer.transform;
                    Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / ship.GetComponent<ShipStatus>().Speed;
                    Block.ownerId = isCharmed ? tempShip.Player.PlayerUUID : ship.Player.PlayerUUID;
                    Block.PlayerName = isCharmed ? tempShip.Player.PlayerName : ship.Player.PlayerName;
                    Block.Team = isCharmed ? tempShip.Team : ship.Team;
                    Block.warp = warp;
                    Block.GetComponent<MeshRenderer>().material = blockMaterial;
                    Block.Index = spawnedTrailCount;
                    Block.ID = ownerId + "::" + spawnedTrailCount++;
                    Block.Trail = trail;

                    if (Block.warp)
                        wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

                    trail.Add(Block);
                }
                else
                {
                    CreateBlock(gap / 2, trail);
                    CreateBlock(-gap / 2, trail2);
                } 
            }
            yield return new WaitForSeconds(Mathf.Clamp(wavelength / shipData.Speed,0,3f));
        }
    }

    bool isCharmed = false;
    Ship tempShip;
    
    public void Charm(Ship ship, float duration)
    {
        tempShip = ship;
        Debug.Log($"charming ship: {ship}");
        SetBlockMaterial(Hangar.Instance.GetTeamBlockMaterial(ship.Team));
        SetShieldedBlockMaterial(Hangar.Instance.GetTeamShieldedBlockMaterial(ship.Team));
        StartCoroutine(CharmCoroutine(duration));
    }

    IEnumerator CharmCoroutine(float duration) 
    {
        isCharmed = true;
        yield return new WaitForSeconds(duration);
        SetBlockMaterial(Hangar.Instance.GetTeamBlockMaterial(ship.Team));
        SetShieldedBlockMaterial(Hangar.Instance.GetTeamShieldedBlockMaterial(ship.Team));
        isCharmed = false;
    }


    public static void NukeTheTrails()
    {
        if (TrailContainer == null) return;

        foreach (Transform child in TrailContainer.transform)
            Destroy(child.gameObject);
    }
}