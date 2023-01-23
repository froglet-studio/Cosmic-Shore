using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Ship))]
public class TrailSpawner : MonoBehaviour
{
    [SerializeField] Trail trail;

    public float offset = 0f;

    public float initialWavelength = 4f;
    float wavelength;

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

    static GameObject TrailContainer;

    readonly Queue<Trail> trailQueue = new();
    readonly public List<Trail> trailList = new();
    bool spawnerEnabled = true;
    string ownerId;

    private void OnEnable()
    {
        GameManager.onDeath += PauseTrailSpawner;
        GameManager.onGameOver += RestartAITrailSpawnerAfterDelay;
        GameManager.onExtendGamePlay += RestartTrailSpawnerAfterDelay;
    }

    private void OnDisable()
    {
        GameManager.onDeath -= PauseTrailSpawner;
        GameManager.onGameOver -= RestartAITrailSpawnerAfterDelay;
        GameManager.onExtendGamePlay -= RestartTrailSpawnerAfterDelay;
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
        blockScale = minBlockScale;
    }

    public void ToggleBlockWaitTime(bool state)
    {
        waitTime = state ? defaultWaitTime*3 : defaultWaitTime;
    }

    [Tooltip("Number of proximal blocks before trail block size reaches minimum")]
    [SerializeField] int MaxNearbyBlockCount = 10;
    [SerializeField] float minBlockScale = 1;
    [SerializeField] float maxBlockScale = 1;
    float blockScale;

    public void SetNearbyBlockCount(int blockCount)
    {
        blockCount = Mathf.Min(blockCount, MaxNearbyBlockCount);
        blockScale = Mathf.Max(minBlockScale, maxBlockScale * (1  - (blockCount / (float)MaxNearbyBlockCount)));
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

    IEnumerator SpawnTrailCoroutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            if (Time.deltaTime < .1f && spawnerEnabled)
            {
                var Block = Instantiate(trail);
                Block.transform.SetPositionAndRotation(transform.position - shipData.velocityDirection * offset, shipData.blockRotation);
                Block.transform.parent = TrailContainer.transform;
                Block.waitTime = waitTime;
                Block.ownerId = ship.Player.PlayerUUID;
                Block.PlayerName = ship.Player.PlayerName;
                Block.Team = ship.Team;
                Block.warp = warp;
                Block.GetComponent<MeshRenderer>().material = blockMaterial;
                Block.ID = ownerId + "::" + spawnedTrailCount++;
                Block.Dimensions = new Vector3(trail.transform.localScale.x * blockScale, trail.transform.localScale.y, trail.transform.localScale.z);

                if (Block.warp)
                    wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

                trailQueue.Enqueue(Block);
                trailList.Add(Block);
            }

            yield return new WaitForSeconds(wavelength / shipData.Speed);
        }
    }
}