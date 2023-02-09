using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Ship))]
public class TrailSpawner : MonoBehaviour
{
    [SerializeField] Trail trail;
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

    public float TrailZScale => trail.transform.localScale.z;

    public static GameObject TrailContainer;

    readonly Queue<Trail> trailQueue = new(); // TODO: unused
    readonly public List<Trail> trailList = new();
    bool spawnerEnabled = true;
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

    [Tooltip("Number of proximal blocks before trail block size reaches minimum")]
    [SerializeField] public int MaxNearbyBlockCount = 10;
    [SerializeField] float minBlockScale = 1;
    [SerializeField] float maxBlockScale = 1;
    
    public float XScaler = 1;
    public float YScaler = 1;
    float ZScaler = 1;


    public void SetNearbyBlockCount(int blockCount)
    {
        blockCount = Mathf.Min(blockCount, MaxNearbyBlockCount);
        XScaler = Mathf.Max(minBlockScale, maxBlockScale * (1  - (blockCount / (float)MaxNearbyBlockCount)));
    }

    public void SetDotProduct(float amount)
    {
        ZScaler = Mathf.Max(minBlockScale, maxBlockScale * (1 - Mathf.Abs(amount)));
        wavelength = Mathf.Max(minWavelength, initialWavelength * Mathf.Abs(amount)); 
    }
     
    void PauseTrailSpawner()
    {
        spawnerEnabled = false;
    }

    void RestartAITrailSpawnerAfterDelay()
    {
        // Called on GameOver to restart only the trail spawners for the AI
        if (gameObject != GameObject.FindWithTag("Player_Ship"))
        {
            StartCoroutine(RestartSpawnerAfterDelayCoroutine());
        }
    }

    void RestartTrailSpawnerAfterDelay()
    {
        // Called when extending game play to resume spawning trails for player and AI
        StartCoroutine(RestartSpawnerAfterDelayCoroutine());
    }

    IEnumerator RestartSpawnerAfterDelayCoroutine()
    {
        yield return new WaitForSeconds(waitTime);
        spawnerEnabled = true;
    }

    void CreateBlock(float halfGap)
    {
        var Block = Instantiate(trail);
        Block.InnerDimensions = new Vector3(trail.transform.localScale.x * XScaler / 2f - Mathf.Abs(halfGap), trail.transform.localScale.y * YScaler, trail.transform.localScale.z * ZScaler);
        Block.transform.SetPositionAndRotation(transform.position - shipData.VelocityDirection * offset + ship.transform.right * ((trail.transform.localScale.x * XScaler )/ 4f + Mathf.Abs(halfGap)/2)*(halfGap/ Mathf.Abs(halfGap)), shipData.blockRotation);
        Block.transform.parent = TrailContainer.transform;
        Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / ship.GetComponent<ShipData>().Speed;
        Block.ownerId = ship.Player.PlayerUUID;
        Block.PlayerName = ship.Player.PlayerName;
        Block.Team = ship.Team;
        Block.warp = warp;
        Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.GetComponent<BoxCollider>().size = Vector3.one + (Vector3)blockMaterial.GetVector("_spread");

        Block.Index = spawnedTrailCount;
        Block.ID = ownerId + "::" + spawnedTrailCount++;
        

        if (Block.warp)
            wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

        trailQueue.Enqueue(Block);
        
    }

    IEnumerator SpawnTrailCoroutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            if (Time.deltaTime < .1f && spawnerEnabled)
            {
                if (gap == 0)
                {
                    var Block = Instantiate(trail);
                    Block.InnerDimensions = new Vector3(trail.transform.localScale.x * XScaler, trail.transform.localScale.y * YScaler, trail.transform.localScale.z * ZScaler);
                    Block.transform.SetPositionAndRotation(transform.position - shipData.VelocityDirection * offset, shipData.blockRotation);
                    Block.transform.parent = TrailContainer.transform;
                    Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / ship.GetComponent<ShipData>().Speed;
                    Block.ownerId = ship.Player.PlayerUUID;
                    Block.PlayerName = ship.Player.PlayerName;
                    Block.Team = ship.Team;
                    Block.warp = warp;
                    Block.GetComponent<MeshRenderer>().material = blockMaterial;
                    Block.Index = spawnedTrailCount;
                    Block.ID = ownerId + "::" + spawnedTrailCount++;
                    Block.TrailSpawner = this;
                    
                   

                    if (Block.warp)
                        wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

                    trailQueue.Enqueue(Block);
                    trailList.Add(Block);
                }
                else
                {
                    CreateBlock(gap / 2);
                    CreateBlock(-gap / 2);
                } 
            }
            yield return new WaitForSeconds(wavelength / shipData.Speed);
        }
    }
}