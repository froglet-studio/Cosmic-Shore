using System.Collections;
using StarWriter.Core;
using UnityEngine;

public class AOEConicExplosion : AOEExplosion
{

    [SerializeField] float heightMultiplier = 10;
    [SerializeField] GameObject cone;


   Vector3 startingPosition;


    void Start()
    {
        MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale * heightMultiplier);
        startingPosition = transform.position;
        StartCoroutine(ExplodeCoroutine());
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);

        if (cone.TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.material = Material;

        var elapsedTime = 0f;
        while (elapsedTime < ExplosionDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO));
            transform.position = startingPosition;
            Material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
            yield return null;
        }

        Destroy(gameObject);
    }
}