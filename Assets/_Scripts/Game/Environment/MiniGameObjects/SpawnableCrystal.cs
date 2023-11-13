using CosmicShore.Environment.FlowField;
using UnityEngine;

public class SpawnableCrystal : SpawnableAbstractBase
{
    [SerializeField] Crystal Crystal;
    static int ObjectsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Crystal" + ObjectsSpawned++;

        var crystal = Instantiate(Crystal);
        crystal.transform.SetParent(container.transform);

        return container;
    }
}