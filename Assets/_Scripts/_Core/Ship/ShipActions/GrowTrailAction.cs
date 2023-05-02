using System.Collections;
using UnityEngine;
public class GrowTrailAction : GrowActionBase
{
    protected override IEnumerator GrowCoroutine()
    {
        var spawner = target.GetComponent<TrailSpawner>();
        while (growing && spawner.YScaler < maxSize)
        {
            spawner.YScaler += Time.deltaTime * growRate;
            spawner.XScaler += Time.deltaTime * growRate;
            spawner.gap += Time.deltaTime * growRate * 2;
            yield return null;
        }
    }

    protected override IEnumerator ReturnToNeutralCoroutine()
    {
        var spawner = target.GetComponent<TrailSpawner>();
        while (ship.TrailSpawner.YScaler > minSize)
        {
            spawner.YScaler -= Time.deltaTime * shrinkRate;
            spawner.XScaler -= Time.deltaTime * shrinkRate;
            spawner.gap -= Time.deltaTime * shrinkRate * 2;
            yield return null;
        }
    }
}