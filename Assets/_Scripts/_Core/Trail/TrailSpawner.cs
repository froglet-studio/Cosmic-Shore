using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class TrailSpawner : MonoBehaviour
{
    [SerializeField] Trail trail;

    public delegate void OnDropIncreaseScore(string uuid, int amount);
    public static event OnDropIncreaseScore AddToScore;

    public float offset = 0f;

    public float initialWavelength = 4f;
    float wavelength;

    public float trailLength = 20;
    public float waitTime = .5f;  // Time until the trail block appears - camera dependent
    public float startDelay = 2.1f;

    private Player player;
    ShipData shipData;

    [SerializeField] bool warp = false;
    GameObject shards;

    static GameObject TrailContainer;

    readonly Queue<Trail> trailQueue = new();
    readonly public List<Trail> trailList = new();
    bool spawnerEnabled = true;

    float volume;
    float volumeScoreScaler = .01f;
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

        player = GetComponent<Player>();
        shipData = GetComponent<ShipData>();

        StartCoroutine(SpawnTrailCoroutine());

        volume = trail.transform.localScale.x *
                     trail.transform.localScale.y *
                     trail.transform.localScale.z;

        ownerId = GetComponent<Player>().PlayerUUID;

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
                var Block = Instantiate(trail);

                score += shipData.boost ? volume * volumeScoreScaler*8 : volume * volumeScoreScaler; //if ship is boosted the blocks gets 8 times bigger (2^dimensionality)
                
                if (score > 1)
                {
                    AddToScore?.Invoke(ownerId, (int)score); //TODO have the trail script control the scoring instead
                    score = score % 1;
                }
                
                Block.ownerId = player.PlayerUUID;
                Block.transform.SetPositionAndRotation(transform.position - shipData.velocityDirection * offset, shipData.blockRotation);
                Block.transform.parent = TrailContainer.transform;
                Block.GetComponent<Trail>().waitTime = waitTime;

                if (warp)
                {
                    Block.warp = true;
                    wavelength = shards.GetComponent<WarpFieldData>().HybridVector(Block.transform).magnitude * initialWavelength;
                }

                trailQueue.Enqueue(Block);
                trailList.Add(Block);
                //if (trailQueue.Count > trailLength / initialWavelength)
                //{
                //    StartCoroutine(ShrinkTrailCoroutine());
                //}
                if (shipData.boost)
                    Block.GetComponent<Trail>().embiggen = true;
                else
                    Block.GetComponent<Trail>().embiggen = false;

            }
            if (shipData.boost)
                yield return new WaitForSeconds(wavelength / shipData.speed);
            else
                yield return new WaitForSeconds(wavelength / shipData.speed);
        }
    }

    IEnumerator ShrinkTrailCoroutine()
    {
        var size = 1f;
        var Block = trailQueue.Dequeue();
        var initialTransformSize = Block.transform.localScale;
        var initialColliderSize = Block.GetComponent<BoxCollider>().size;

        while (size > 0.01)
        {
            size -= .5f * Time.deltaTime;
            Block.transform.localScale = initialTransformSize * size;
            Block.GetComponent<BoxCollider>().size = initialColliderSize * size;
            yield return null;
        }

        Destroy(Block);
    }
}