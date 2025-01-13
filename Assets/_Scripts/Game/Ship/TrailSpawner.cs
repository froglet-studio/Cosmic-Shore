﻿using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;

[RequireComponent(typeof(Ship))]
[RequireComponent(typeof(ShipStatus))]
public class TrailSpawner : MonoBehaviour
{
    public delegate void BlockCreationHandler(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ);
    public event BlockCreationHandler OnBlockCreated;

    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Skimmer skimmer;
    [SerializeField] bool waitTillOutsideSkimmer = true;
    [SerializeField] bool shielded = false;
    [SerializeField] float initialWavelength = 4f;
    [SerializeField] float minWavelength = 1f;
    public float MinWaveLength {get { return minWavelength; } }

    float wavelength;
    public float offset = 0f;
    public float Gap;
    public float MinimumGap = 1;
    public Vector3 TargetScale;

    public ushort TrailLength { get { return (ushort)Trail.TrailList.Count; } }
    [SerializeField] float defaultWaitTime = .5f;
    
    [HideInInspector] public float waitTime = .5f;  // Time until the trail block appears - camera dependent
    public float startDelay = 2.1f;

    ushort spawnedTrailCount;

    public Trail Trail = new();
    Trail Trail2 = new();

    IShip ship;
    ShipStatus shipData;
    Coroutine spawnTrailCoroutine;

    public List<TrailBlock> GetLastTwoBlocks() 
    {
        if (Trail2.TrailList.Count > 0)
        {
            return new List<TrailBlock>
            {
                // ^1 is the hat operator for index of last element
                Trail.TrailList[^1],
                Trail2.TrailList[^1]
            };
        }
        
        return null;
    }

    public float TrailZScale => trailBlock.transform.localScale.z;

    public static GameObject TrailContainer;

    [Tooltip("This is serialized for debug visibility")]
    [SerializeField] bool spawnerEnabled = true;
    string ownerId;

    private void OnEnable()
    {
        GameManager.OnGameOver += RestartAITrailSpawnerAfterDelay;
    }

    private void OnDisable()
    {
        GameManager.OnGameOver -= RestartAITrailSpawnerAfterDelay;
    }

    public void Initialize(IShip ship)
    {
        this.ship = ship;

        waitTime = defaultWaitTime;
        wavelength = initialWavelength;
        if (TrailContainer == null)
        {
            TrailContainer = new GameObject();
            TrailContainer.name = "TrailContainer";
        }

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
    public float ZScaler = 1;
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
        lerper = StartCoroutine(LerpUtilities.LerpingCoroutine(XScaler, newXScaler, 1.5f, (i) => { XScaler = i; }));
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
        var targetScale = new Vector3(trailBlock.transform.localScale.x * XScaler / 2f - Mathf.Abs(halfGap), trailBlock.transform.localScale.y * YScaler, trailBlock.transform.localScale.z * ZScaler);
        Block.TargetScale = TargetScale = targetScale;
        float xShift = (targetScale.x / 2f + Mathf.Abs(halfGap)) * (halfGap / Mathf.Abs(halfGap));
        Block.transform.SetPositionAndRotation(transform.position - shipData.Course * offset + ship.Transform.right * xShift, shipData.blockRotation);
        Block.transform.parent = TrailContainer.transform;
        Block.ownerID = isCharmed ? tempShip.Player.PlayerUUID : ship.Player.PlayerUUID;
        Block.Player = isCharmed ? tempShip.Player : ship.Player;
        Block.ChangeTeam(isCharmed ? tempShip.Team : ship.Team);
        if (waitTillOutsideSkimmer) 
            Block.waitTime = (skimmer.transform.localScale.z + TrailZScale) / shipData.Speed;            
        if (shielded)
        {
            Block.TrailBlockProperties.IsShielded = true;
        }
        Block.Trail = trail;

        OnBlockCreated?.Invoke(xShift, wavelength, Block.TargetScale.x, Block.TargetScale.y, Block.TargetScale.z);
        trail.Add(Block);
        Block.TrailBlockProperties.Index = (ushort) trail.TrailList.IndexOf(Block);
        Block.ownerID = ownerId + ":" + spawnedTrailCount++;
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
            if (spawnerEnabled && !shipData.Attached && shipData.Speed > 3f)
            {
                if (Gap == 0)
                {
                    var Block = Instantiate(trailBlock);
                    var targetScale = new Vector3(trailBlock.transform.localScale.x * XScaler, trailBlock.transform.localScale.y * YScaler, trailBlock.transform.localScale.z * ZScaler);
                    Block.TargetScale = TargetScale = targetScale;
                    Block.transform.SetPositionAndRotation(transform.position - shipData.Course * offset, shipData.blockRotation);
                    Block.transform.parent = TrailContainer.transform;
                    Block.waitTime = waitTillOutsideSkimmer ? (skimmer.transform.localScale.z + TrailZScale) / shipData.Speed : waitTime;
                    Block.ownerID = isCharmed ? tempShip.Player.PlayerUUID : ship.Player.PlayerUUID;
                    Block.Player = isCharmed ? tempShip.Player : ship.Player;
                    Block.ChangeTeam(isCharmed ? tempShip.Team : ship.Team);
                    Block.TrailBlockProperties.Index = spawnedTrailCount;
                    Block.ownerID = ownerId + ":" + spawnedTrailCount++;
                    Block.Trail = Trail;

                    Trail.Add(Block);
                }
                else
                {
                    CreateBlock(Gap / 2, Trail);
                    CreateBlock(-Gap / 2, Trail2);
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
        StartCoroutine(CharmCoroutine(duration));
    }

    IEnumerator CharmCoroutine(float duration) 
    {
        isCharmed = true;
        yield return new WaitForSeconds(duration);
        isCharmed = false;
    }


    public static void NukeTheTrails()
    {
        if (TrailContainer == null) return;

        foreach (Transform child in TrailContainer.transform)
            Destroy(child.gameObject);
    }
}
