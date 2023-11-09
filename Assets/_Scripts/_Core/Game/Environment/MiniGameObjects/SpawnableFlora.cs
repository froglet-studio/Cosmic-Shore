using UnityEngine;

public class SpawnableFlora : SpawnableAbstractBase
{
    [SerializeField] Flora flora;
    static int ObjectsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Flora" + ObjectsSpawned++;

        var flora = Instantiate(this.flora);
        flora.transform.SetParent(container.transform);

        return container;
    }
}