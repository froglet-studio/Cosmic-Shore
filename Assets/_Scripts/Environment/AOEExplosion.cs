using System.Collections;
using UnityEngine;

public enum EntityType
{
    Ship,
    TrailBlock,
    Explosion,
    Skimmer,
    Crystal,
}

// TODO: namespace
// TODO: add IBlockImpact interface
public class AOEExplosion : MonoBehaviour
{
    [SerializeField] float MaxScale = 200f;
    [SerializeField] float ExplosionDuration = 2f;
    [SerializeField] float ExplosionDelay = .2f;
    [SerializeField] GameObject Geometry;

    public float speed = 5f; // TODO: use the easing of the explosion to change this over time
    
    const float PI_OVER_TWO = Mathf.PI / 2;

    Vector3 MaxScaleVector;

    Teams team;
    public Teams Team { get => team; set => team = value; }

    void Start()
    {
        MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
        StartCoroutine(ExplodeCoroutine());
    }

    IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);
        
        var elapsedTime = 0f;
        while (elapsedTime < ExplosionDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO));
            yield return null;
        }

        Destroy(gameObject);
    }
}