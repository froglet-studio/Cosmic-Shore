using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Core;
using System.Collections;

public class TeamColorPersistentPool : PoolManagerBase
{
    private static TeamColorPersistentPool instance;
    public static TeamColorPersistentPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("TeamColorPersistentPool");
                instance = go.AddComponent<TeamColorPersistentPool>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [SerializeField] private GameObject fossilBlockPrefab;
    [SerializeField] private int poolSizePerTeam = 750; // 750 per team = 3000 total

    protected override void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            base.Awake();

            // Initialize all team pools
            StartCoroutine(WaitForThemeManagerInitialization());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected override GameObject CreatePoolObject(GameObject prefab)
    {
        GameObject obj = base.CreatePoolObject(prefab);
        
        // Set the material based on the pool's tag
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Teams team = Teams.Jade; // Default
            
            // Determine team from tag
            if (prefab.tag.Contains("Ruby")) team = Teams.Ruby;
            else if (prefab.tag.Contains("Gold")) team = Teams.Gold;
            else if (prefab.tag.Contains("Blue")) team = Teams.Blue;
            
            // Create and set the material
            Material teamMaterial = new Material(ThemeManager.Instance.GetTeamExplodingBlockMaterial(team));
            renderer.material = teamMaterial;
        }
        
        return obj;
    }

    //the following coroutine waits for the thememanager to initialize before creating the team pools
    IEnumerator WaitForThemeManagerInitialization()
    {
        while (ThemeManager.Instance == null)
        {
            yield return new WaitForEndOfFrame();
        }
        
        // Initialize all team pools
        InitializeTeamPools(fossilBlockPrefab, poolSizePerTeam);
    }

    private void InitializeTeamPools(GameObject prefab, int sizePerTeam)
    {
        // Initialize default pool
        InitializePool(prefab, sizePerTeam);

        // Temporarily modify the tag for each team
        string originalTag = prefab.tag;

        prefab.tag = "FossilPrism_Ruby";
        InitializePool(prefab, sizePerTeam);

        prefab.tag = "FossilPrism_Gold";
        InitializePool(prefab, sizePerTeam);

        prefab.tag = "FossilPrism_Blue";
        InitializePool(prefab, sizePerTeam);

        // Restore original tag
        prefab.tag = originalTag;
    }

    public GameObject SpawnFromTeamPool(Teams team, Vector3 position, Quaternion rotation)
    {
        string tag = "FossilPrism";
        
        // Get the appropriate pool based on team
        switch (team)
        {
            case Teams.Ruby:
                tag = "FossilPrism_Ruby";
                break;
            case Teams.Gold:
                tag = "FossilPrism_Gold";
                break;
            case Teams.Blue:
                tag = "FossilPrism_Blue";
                break;
        }
        
        return SpawnFromPool(tag, position, rotation);
    }
}