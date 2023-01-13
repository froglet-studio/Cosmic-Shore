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
    }

    public void ToggleBlockWaitTime(bool state)
    {
        waitTime = state ? 1.5f : 0.5f;
    }

    [Tooltip("Number of proximal blocks before trail block size reaches minimum")]
    [SerializeField] int SaturatedBlockDensity = 10; 

    public void SetNearbyBlockCount(int blockCount)
    {

        // TODO WIP Here
        /*
        var trail = other.GetComponent<Trail>();
        if (trail != null)
        {
            // start with a baseline fuel amount the ranges from 0-1 depending on proximity of the skimmer to the trail block
            var fuel = fuelAmount * (1 - (Vector3.Magnitude(transform.position - other.transform.position) / transform.localScale.x));

            // apply multiskim multiplier
            fuel += (activelySkimmingBlockCount * MultiSkimMultiplier);
        }
        */
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
                Block.Dimensions = trail.transform.localScale;

                if (Block.warp)
                    wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

                trailQueue.Enqueue(Block);
                trailList.Add(Block);
            }

            yield return new WaitForSeconds(wavelength / shipData.Speed);
        }
    }
}