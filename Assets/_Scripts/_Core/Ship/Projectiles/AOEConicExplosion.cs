using System.Collections;
using StarWriter.Core;
using UnityEngine;

public class AOEConicExplosion : AOEExplosion
{
    [SerializeField] float heightMultiplier = 10;

   Vector3 startingPosition;

    protected override void Start()
    {
        base.Start();

        startingPosition = container.transform.position;
        MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale * heightMultiplier);
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);

        if (TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.material = Material;

        var elapsedTime = 0f;
        while (elapsedTime < ExplosionDuration)
        {
            elapsedTime += Time.deltaTime;
            container.transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO));
            container.transform.position = startingPosition;
            Material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - container.transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
            yield return null;
        }

        Destroy(gameObject);
    }
}