using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using System.Collections.Generic;
using CosmicShore._Core.Ship;

public class BoidManager : MonoBehaviour
{
    [Header("Boid Settings")]
    public Boid boidPrefab;
    public int numberOfBoids = 100;
    public float spawnRadius = 50.0f;

    [Header("Global Boid Settings")]
    public Transform globalGoal;
    public bool randomGoals = true;
    public float goalUpdateInterval = 5f;

    private Vector3 goalPos;
    public List<float> Weights;

    Trail boidTrail = new();

    private void Start()
    {
        Weights = new List<float> { 1, 1, 1, 1 };
        for (int i = 0; i < numberOfBoids; i++)
        {
            Vector3 spawnPosition = transform.position + (spawnRadius * (Quaternion.AngleAxis(Random.Range(0, 360), Vector3.forward) * Vector3.right));
            Boid newBoid = Instantiate(boidPrefab, spawnPosition, Quaternion.LookRotation(Vector3.Cross(spawnPosition,Vector3.forward)));
            newBoid.transform.SetParent(transform);
            var block = newBoid.GetComponentInChildren<TrailBlock>();

            if (globalGoal)
            {
                newBoid.goal = globalGoal;
            }

            boidTrail.Add(block);
            block.Team = Teams.Blue;
            block.Trail = boidTrail;
            
            newBoid.normalizedIndex = (float)i / numberOfBoids;
        }

        StartCoroutine(UpdateGoal());
    }

    private void Update()
    {
        if (!randomGoals && globalGoal)
        {
            goalPos = globalGoal.position;
        }
    }

    private void CalculateTeamWeights()
    {
        Vector4 teamVolumes = StatsManager.Instance.GetTeamVolumes();
        float totalVolume = teamVolumes.x + teamVolumes.y + teamVolumes.z + teamVolumes.w;

        Weights = new List<float>
        {
        totalVolume / (teamVolumes.x + 1), // +1 to avoid division by zero
        totalVolume / (teamVolumes.y + 1),
        totalVolume / (teamVolumes.z + 1),
        totalVolume / (teamVolumes.w + 1)
        };
    }

    private IEnumerator UpdateGoal()
    {
        while (true)
        {
            yield return new WaitForSeconds(goalUpdateInterval);

            if (globalGoal && randomGoals)
            {
                goalPos = transform.position + new Vector3(Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius));
                globalGoal.position = goalPos;
            }

            CalculateTeamWeights();

        }
    }
}
