using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Utility;

public class BoidManager : Population
{
    [Header("Boid Settings")]
    public Boid boidPrefab;
    public int numberOfBoids = 100;
    public float spawnRadius = 50.0f;

    [Header("Global Boid Settings")]
    public Transform Mound;

    public List<Boid> Boids;


    public Trail boidTrail = new();

    protected override void Start()
    {
        //Weights = new List<float> { 1, 1, 1, 1 };
        for (int i = 0; i < numberOfBoids; i++)
        {
            Vector3 spawnPosition = transform.position + (spawnRadius * (Quaternion.AngleAxis(Random.Range(0, 360), Vector3.forward) * Vector3.right));
            SafeLookRotation.TryGet(Vector3.Cross(spawnPosition,Vector3.forward), out var initialRotation, boidPrefab);
            Boid newBoid = Instantiate(boidPrefab, spawnPosition, initialRotation);
            newBoid.transform.SetParent(transform);
            newBoid.Population = this;
            newBoid.domain = domain;
            var block = newBoid.GetComponentInChildren<Prism>();

            if (Mound)
            {
                newBoid.Mound = Mound;
            }

            //newBoid.DefaultGoal = node.GetClosestItem(spawnPosition).transform;

            boidTrail.Add(block);
            block.ChangeTeam(domain);
            block.Trail = boidTrail;
            
            newBoid.normalizedIndex = (float)i / numberOfBoids;
            Boids.Add(newBoid);
        }

        base.Start();
    }

}