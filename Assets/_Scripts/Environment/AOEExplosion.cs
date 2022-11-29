using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: namespace
// TODO: add IBlockImpact interface
public class AOEExplosion : MonoBehaviour
{
    [SerializeField] float MaxScale = 200f;
    [SerializeField] float ExplosionDuration = 2f;
    [SerializeField] float ExplosionDelay = .2f;
    [SerializeField] GameObject Geometry;

    void Start()
    {
        StartCoroutine(ExplodeCoroutine());
    }

    IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);
        // TODO: leverage explosion duration to derive the scale increment factor
        // TODO: apply some kind of easing
        //while (MaxScale > Geometry.transform.localScale.x)
        while (MaxScale > transform.localScale.x)
        {
            transform.localScale += new Vector3(.5f, .5f, .5f);
            yield return null;
        }

        transform.localScale = new Vector3(.1f, .1f, .1f);

        Destroy(gameObject);
    }
}