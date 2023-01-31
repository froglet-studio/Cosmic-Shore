using System.Collections;
using StarWriter.Core;
using UnityEngine;

public class AOEConicExplosion : AOEExplosion
{
    //public float speed = 5f; // TODO: use the easing of the explosion to change this over time
    //const float PI_OVER_TWO = Mathf.PI / 2;
    //Vector3 MaxScaleVector;

    //[SerializeField] public float MaxScale = 200f;
    //[SerializeField] float ExplosionDuration = 2f;
    [SerializeField] float heightMultiplier = 10;
    [SerializeField] GameObject cone;
    //[SerializeField] protected float ExplosionDelay = .2f;

    //Material material;
    //[HideInInspector] public Material Material { get { return material; } set { material = new Material(value); Debug.LogWarning($"Setting AOEExplosion material: {material}"); } }

    //Teams team;
    //[HideInInspector] public Teams Team { get => team; set => team = value; }

    //Ship ship;
    //[HideInInspector] public Ship Ship { get => ship; set => ship = value; }

   Vector3 startingPosition;


    void Start()
    {
        MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale * heightMultiplier);
        //transform.Rotate(-90,0,0);
        startingPosition = transform.position;
        //transform.position += ship.transform.forward * MaxScale * heightMultiplier / 2f;
        StartCoroutine(ExplodeCoroutine());
    }

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);

        if (cone.TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.material = material;

        var elapsedTime = 0f;
        while (elapsedTime < ExplosionDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO));
            transform.position = startingPosition;// + (ship.transform.forward * transform.localScale.y/2f);
            material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
            yield return null;
        }

        Destroy(gameObject);
    }
}