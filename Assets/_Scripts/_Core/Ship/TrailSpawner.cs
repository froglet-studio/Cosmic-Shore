using StarWriter.Core;
using System.Collections;
using UnityEngine;


[RequireComponent(typeof(Ship))]
public class TrailSpawner : MonoBehaviour
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Skimmer skimmer;

    public float offset = 0f;

    [SerializeField] float initialWavelength = 4f;
    [SerializeField] float minWavelength = 1f;

    float wavelength;

    public float gap;

    public float trailLength = 20;
    [SerializeField] float defaultWaitTime = .5f;
    
    public float waitTime = .5f;  // Time until the trail block appears - camera dependent
    public float startDelay = 2.1f;

    int spawnedTrailCount;

    Trail trail = new();
    Trail trail2 = new();
    Material blockMaterial;
    Ship ship;
    ShipData shipData;

    [SerializeField] bool warp = false;
    GameObject shards;

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    public Material GetBlockMaterial()
    {
        return blockMaterial;
    }

    public float TrailZScale => trailBlock.transform.localScale.z;

    public static GameObject TrailContainer;

    [Tooltip("This is serialized for debug visibility")]
    [SerializeField] bool spawnerEnabled = true;
    string ownerId;

    private void OnEnable()
    {
        GameManager.onDeath += PauseTrailSpawner;
        GameManager.onGameOver += RestartAITrailSpawnerAfterDelay;
    }

    private void OnDisable()
    {
        GameManager.onDeath -= PauseTrailSpawner;
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
        shipData = GetComponent<ShipData>();

        StartCoroutine(SpawnTrailCoroutine());
        

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

    Coroutine lerper;

    public void SetNearbyBlockCount(int blockCount)
    {
        blockCount = Mathf.Min(blockCount, MaxNearbyBlockCount);
        float newXScaler = Mathf.Max(minBlockScale, maxBlockScale * (1 - (blockCount / (float)MaxNearbyBlockCount)));
        XLerper(newXScaler);
    }

    void XLerper(float newXScaler)
    {
        if (lerper != null) StopCoroutine(lerper);
        lerper = StartCoroutine(Lerper((i) => { XScaler = i; }, () => XScaler ,newXScaler, 2, 10));
    }

    IEnumerator Lerper(System.Action<float> replacementMethod, System.Func<float> getCurrent, float newValue, float duration, int steps)
    {
        float elapsedTime = 0;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            replacementMethod(Mathf.Lerp(getCurrent(), newValue, elapsedTime / duration));
            yield return new WaitForSeconds(duration / (float)steps);
        } 
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
        Block.InnerDimensions = new Vector3(trailBlock.transform.localScale.x * XScaler / 2f - Mathf.Abs(halfGap), trailBlock.transform.localScale.y * YScaler, trailBlock.transform.localScale.z * ZScaler);
        Block.transform.SetPositionAndRotation(transform.position - shipData.Course * offset + ship.transform.right * ((trailBlock.transform.localScale.x * XScaler )/ 4f + Mathf.Abs(halfGap)/2)*(halfGap/ Mathf.Abs(halfGap)), shipData.blockRotation);
        Block.transform.parent = TrailContainer.transform;
        Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / ship.GetComponent<ShipData>().Speed;
        Block.ownerId = ship.Player.PlayerUUID;
        Block.PlayerName = ship.Player.PlayerName;
        Block.Team = ship.Team;
        Block.warp = warp;
        Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.GetComponent<BoxCollider>().size = Vector3.one + VectorDivision((Vector3)blockMaterial.GetVector("_spread"), Block.InnerDimensions);
        Block.Trail = trail;

        Block.Index = spawnedTrailCount;
        Block.ID = ownerId + "::" + spawnedTrailCount++;
        

        if (Block.warp)
            wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

        
        trail.Add(Block);

    }

    Vector3 VectorDivision(Vector3 Vector1, Vector3 Vector2) // TODO: move to tools
    {
        return new Vector3(Vector1.x / Vector2.x, Vector1.y / Vector2.y, Vector1.z / Vector2.z);
    }

    public void ForceStartSpawningTrail()
    {
        StartCoroutine(SpawnTrailCoroutine());
    }

    IEnumerator SpawnTrailCoroutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            if (Time.deltaTime < .1f && spawnerEnabled && !shipData.Attached)
            {
                if (gap == 0)
                {
                    var Block = Instantiate(trailBlock);
                    Block.InnerDimensions = new Vector3(trailBlock.transform.localScale.x * XScaler, trailBlock.transform.localScale.y * YScaler, trailBlock.transform.localScale.z * ZScaler);
                    Block.transform.SetPositionAndRotation(transform.position - shipData.Course * offset, shipData.blockRotation);
                    Block.transform.parent = TrailContainer.transform;
                    Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / ship.GetComponent<ShipData>().Speed;
                    Block.ownerId = ship.Player.PlayerUUID;
                    Block.PlayerName = ship.Player.PlayerName;
                    Block.Team = ship.Team;
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
    
    public static void NukeTheTrails()
    {
        if (TrailContainer == null) return;

        foreach (Transform child in TrailContainer.transform)
            Destroy(child.gameObject);
    }
}