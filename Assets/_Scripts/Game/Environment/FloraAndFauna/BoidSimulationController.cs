using UnityEngine;
using CosmicShore.Core;
using System.Collections.Generic;
using System.Collections;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
public struct Entity
{
    public int type;
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 goalDirection;
    public int explodeFlag;
    public int team; // Team of the TrailBlock
    public Vector4 teamWeights; // Weights for each team

    // Constants for entity types
    public const int ENTITY_TYPE_BOID = 0;
    public const int ENTITY_TYPE_BLOCK = 1;
    public const int ENTITY_TYPE_FAUNA = 2;
}

public class BoidSImulationController : MonoBehaviour
{
    public ComputeShader boidSimulationShader;
    public TrailBlock boidPrefab;
    public int numberOfBoids = 100;
    public float spawnRadius = 50.0f;
    public Transform globalGoal;

    private ComputeBuffer readBuffer;
    private ComputeBuffer writeBuffer;
    public List<Entity> entities = new List<Entity>();
    private Entity[] entityArray;
    private TrailBlock[] boids; // Array to keep track of boid game objects

    int kernel;

    private void Start()
    {
        kernel = boidSimulationShader.FindKernel("CSMain");

        InitializeEntities();
        entityArray = entities.ToArray();

        InitializeTrailBlocks();
        InitializeComputeShader();
        StartCoroutine(UpdateWeightsCoroutine());
    }

    private void InitializeTrailBlocks()
    {
        // Assuming you have a method or a way to get all non-boid trail blocks in the scene
        TrailBlock[] trailBlocks = FindObjectsOfType<TrailBlock>();
        foreach (var block in trailBlocks)
        {
            if (!block.CompareTag("Fauna")) // Assuming "Fauna" is the tag for boid trail blocks
            {
                Entity blockEntity = new Entity
                {
                    type = Entity.ENTITY_TYPE_BLOCK,
                    position = block.transform.position,
                    velocity = Vector3.zero, // Trail blocks don't move on their own
                    goalDirection = Vector3.zero,
                    team = (int)block.Team
                };
                Debug.Log($"team {(int)block.Team}");

                // Add blockEntity to the entities list
                entities.Add(blockEntity);
            }
        }
    }


    private void InitializeEntities()
    {
        entities = new List<Entity>(numberOfBoids);

        boids = new TrailBlock[numberOfBoids]; // Initialize the boids array

        // Initialize boids
        for (int i = 0; i < numberOfBoids; i++)
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            Debug.Log("Instantiating boid number: " + i);
            TrailBlock newBoid = Instantiate(boidPrefab, spawnPosition, Quaternion.identity);
            newBoid.transform.SetParent(transform);

            boids[i] = newBoid; // Store the boid game object reference

            Entity newEntity = new Entity
            {
                type = Entity.ENTITY_TYPE_BOID,
                position = spawnPosition,
                velocity = newBoid.transform.forward,
                goalDirection = globalGoal.position - spawnPosition,
                teamWeights = new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // Example weights for 4 teams
                team = (int)Teams.Blue
            };

            entities.Add(newEntity);

        }
        entityArray = entities.ToArray();
        // TODO: Initialize trail blocks in the entities array
    }

    private void InitializeComputeShader()
    {
        readBuffer = new ComputeBuffer(entities.Count, 64);
        writeBuffer = new ComputeBuffer(entities.Count, 64);

        readBuffer.SetData(entities);

        boidSimulationShader.SetBuffer(kernel, "entityBufferRead", readBuffer);
        boidSimulationShader.SetBuffer(kernel, "entityBufferWrite", writeBuffer);
    }

    private void Update()
    {
        int groups = Mathf.CeilToInt((float)entityArray.Length / 32);
        boidSimulationShader.Dispatch(kernel, groups, 1, 1);

        SwapBuffers();

        readBuffer.GetData(entityArray);

        for (int i = 0; i < entityArray.Length; i++)
        {
            Entity entity = entityArray[i];

            if (entity.position == new Vector3(9999.0f, 9999.0f, 9999.0f))
            {
                Debug.LogError("NaN detected by shader for entity at index: " + i);
            }

            if (entity.type == Entity.ENTITY_TYPE_BOID)
            {
                boids[i].transform.position = entity.position;
                boids[i].transform.LookAt(entity.velocity.normalized);
            }
        }
    }

    private void SwapBuffers()
    {
        var temp = readBuffer;
        readBuffer = writeBuffer;
        writeBuffer = temp;

        boidSimulationShader.SetBuffer(kernel, "entityBufferRead", readBuffer);
        boidSimulationShader.SetBuffer(kernel, "entityBufferWrite", writeBuffer);
    }



    private IEnumerator UpdateWeightsCoroutine()
    {
        while (true)
        {
            Vector4 currentWeights = CalculateTeamWeights();
            for (int i = 0; i < entityArray.Length; i++)
            {
                if (entityArray[i].type == Entity.ENTITY_TYPE_BOID)
                {
                    Entity entity = entityArray[i];
                    entity.teamWeights = currentWeights;
                    entityArray[i] = entity;
                    Debug.Log($"BoidManager.entityArray[{i}].position {entityArray[i].position}");
                }
            }

            readBuffer.SetData(entityArray);

            yield return new WaitForSeconds(.05f);
        }
    }


    private Vector4 CalculateTeamWeights()
    {
        Vector4 teamVolumes = StatsManager.Instance.GetTeamVolumes();
        float totalVolume = teamVolumes.x + teamVolumes.y + teamVolumes.z + teamVolumes.w;
        return new Vector4(
            totalVolume / (teamVolumes.x + 1), // +1 to avoid division by zero
            totalVolume / (teamVolumes.y + 1),
            totalVolume / (teamVolumes.z + 1),
            totalVolume / (teamVolumes.w + 1)
        );
    }

    private TrailBlock FindBlockByPosition(Vector3 position)
    {
        // Get the node that contains the position
        Node containingNode = NodeControlManager.Instance.GetNodeByPosition(position);

        // If there's no node that contains the position, return null
        if (containingNode == null)
        {
            return null;
        }

        // Iterate through all NodeItems in the node to find the closest TrailBlock
        Dictionary<int, NodeItem> nodeItems = containingNode.GetItems();
        TrailBlock closestBlock = null;
        float closestDistance = float.MaxValue;

        foreach (var itemPair in nodeItems)
        {
            NodeItem item = itemPair.Value;
            if (item.GetComponent<TrailBlock>() && !item.CompareTag("Fauna"))
            {
                float distance = Vector3.Distance(position, item.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBlock = item.GetComponent<TrailBlock>();
                }
            }
        }

        return closestBlock;
    }


    public void CreateBoid(Vector3 position, Vector3 direction)
    {
        TrailBlock newBoid = Instantiate(boidPrefab, position, Quaternion.LookRotation(direction));
        newBoid.transform.SetParent(transform);

        Entity newEntity = new Entity
        {
            type = Entity.ENTITY_TYPE_BOID,
            position = position,
            velocity = direction,
            goalDirection = globalGoal.position - position,
            team = (int)Teams.None
        };

        entities.Add(newEntity);
        boids = new List<TrailBlock>(boids) { newBoid }.ToArray();

        readBuffer.Release();
        writeBuffer.Release();
        readBuffer = new ComputeBuffer(entities.Count, 64);
        writeBuffer = new ComputeBuffer(entities.Count, 64);
        readBuffer.SetData(entities);
    }

    private void OnDestroy()
    {
        readBuffer.Release();
        writeBuffer.Release();
    }
}
