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

    private Material blockMaterial;
    private Ship ship;
    ShipData shipData;

    [SerializeField] bool warp = false;
    GameObject shards;

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    static GameObject TrailContainer;

    readonly Queue<Trail> trailQueue = new();
    readonly public List<Trail> trailList = new();
    bool spawnerEnabled = true;

    float volume;
    float volumeScoreScaler = .01f;
    float boostedVolumeScoreScaler = .08f;  // if ship is boosted the blocks gets 8 times bigger (2^dimensionality)
    float score = 0f;
    string ownerId;

    private void OnEnable()
    {
        GameManager.onPhoneFlip += OnPhoneFlip;
        GameManager.onDeath += PauseTrailSpawner;
        GameManager.onGameOver += RestartAITrailSpawnerAfterDelay;
        GameManager.onExtendGamePlay += RestartTrailSpawnerAfterDelay;
    }

    private void OnDisable()
    {
        GameManager.onPhoneFlip -= OnPhoneFlip;
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

        volume = trail.transform.localScale.x *
                 trail.transform.localScale.y *
                 trail.transform.localScale.z;

        ownerId = ship.Player.PlayerUUID;
    }

    private void OnPhoneFlip(bool state)
    {
        if (gameObject == GameObject.FindWithTag("Player"))
        {
            waitTime = state ? 1.5f : 0.5f;
        }
    }
    void PauseTrailSpawner()
    {
        spawnerEnabled = false;
    }

    void RestartAITrailSpawnerAfterDelay()
    {
        // Called on GameOver to restart only the trail spawners for the AI
        if (gameObject != GameObject.FindWithTag("Player"))
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
                score += shipData.boost ? volume * boostedVolumeScoreScaler : volume * volumeScoreScaler;

                if (score > 1 && ScoringManager.Instance.nodeGame)
                {
                    ScoringManager.Instance.UpdateScore(ownerId, (int)score);
                    score = score % 1;
                }

                var Block = Instantiate(trail);
                Block.transform.SetPositionAndRotation(transform.position - shipData.velocityDirection * offset, shipData.blockRotation);
                Block.transform.parent = TrailContainer.transform;
                Block.waitTime = waitTime;
                Block.embiggen = shipData.boost;
                Block.ownerId = ship.Player.PlayerUUID;
                Block.PlayerName = ship.Player.PlayerName;
                Block.Team = ship.Team;
                Block.warp = warp;
                Block.GetComponent<MeshRenderer>().material = blockMaterial;

                if (Block.warp)
                    wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;

                trailQueue.Enqueue(Block);
                trailList.Add(Block);
            }

            yield return new WaitForSeconds(wavelength / shipData.speed);
        }
    }
}