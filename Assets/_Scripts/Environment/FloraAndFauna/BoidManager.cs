using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [Header("Boid Settings")]
    public Boid boidPrefab; // The Boid prefab to be instantiated
    public int numberOfBoids = 100; // Number of boids to spawn
    public float spawnRadius = 50.0f; // Radius within which boids are randomly spawned

    [Header("Global Boid Settings")]
    public Transform globalGoal; // A global goal for all boids, if you want them to have a common goal
    public bool randomGoals = true; // If true, goals will be updated randomly. If false, the globalGoal will be used.
    public float goalUpdateInterval = 5f; // Update goal every 5 seconds

    private Vector3 goalPos;
    private float goalUpdateTimer = 0f;

    private void Start()
    {
        for (int i = 0; i < numberOfBoids; i++)
        {
            // Spawn boids within the spawn radius
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;

            Boid newBoid = Instantiate(boidPrefab, spawnPosition, Quaternion.identity);
            newBoid.transform.SetParent(transform); // Set the manager as the parent

            // If a global goal is set, use it
            if (globalGoal)
            {
                newBoid.goal = globalGoal;
            }

            newBoid.normalizedIndex = (float)i / numberOfBoids;
        }
    }

    private void Update()
    {
        // Update goal position if random goals are enabled
        if (randomGoals)
        {
            goalUpdateTimer += Time.deltaTime;
            if (goalUpdateTimer > goalUpdateInterval)
            {
                goalPos = transform.position + new Vector3(Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius));
                if (globalGoal)
                {
                    globalGoal.position = goalPos;
                }
                goalUpdateTimer = 0f;
            }
        }
        else if (globalGoal)
        {
            goalPos = globalGoal.position;
        }
    }
}
