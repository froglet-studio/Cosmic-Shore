using UnityEngine;
using System.Collections.Generic;
using CosmicShore;

public class LightFaunaManager : Population
{
    [Header("Spawn Settings")]
    [SerializeField] LightFauna lightFaunaPrefab;
    [SerializeField] int spawnCount = 10;
    [SerializeField] float spawnRadius = 10f;
    
    [Header("Formation Settings")]
    [SerializeField] float formationSpread = 5f;
    [SerializeField] float PhaseIncrease;


    private List<LightFauna> activeFauna = new List<LightFauna>();

    protected override void Start()
    {
        base.Start();
        SpawnGroup();
    }

    void SpawnGroup()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
            randomOffset.y = 0; // Keep spawns on same plane
            
            Vector3 spawnPosition = transform.position + randomOffset;
            
            LightFauna fauna = Instantiate(lightFaunaPrefab, spawnPosition, Random.rotation, transform);
            fauna.Team = Team;
            fauna.Population = this;
            fauna.Phase = PhaseIncrease*i;
            
            activeFauna.Add(fauna);
        }

        // Set initial formation positions
        for (int i = 0; i < activeFauna.Count; i++)
        {
            float angle = (i * 360f / activeFauna.Count) * Mathf.Deg2Rad;
            Vector3 formationOffset = new Vector3(
                Mathf.Cos(angle) * formationSpread,
                0,
                Mathf.Sin(angle) * formationSpread
            );
            activeFauna[i].transform.position = transform.position + formationOffset;
        }
    }

    public void RemoveFauna(LightFauna fauna)
    {
        if (activeFauna.Contains(fauna))
        {
            activeFauna.Remove(fauna);
            Destroy(fauna.gameObject);
        }

        // Optional: Respawn if count gets too low
        if (activeFauna.Count < spawnCount / 2)
        {
            SpawnGroup();
        }
    }
}
