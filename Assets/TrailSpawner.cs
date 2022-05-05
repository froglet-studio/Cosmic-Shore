using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrailSpawner : MonoBehaviour
{
    public GameObject trail;
    public Transform head;
    public float offset = 0f;
    public float tailPeriod = .1f;
    public float lifeTime = 20;
    public float waitTime = .5f;

    public bool useRandom = true;

    Vector3 randomScale;

    bool hasTail = true;
    private IEnumerator trailCoroutine;

    IEnumerator SpawnTrailCoroutine()
    {
        while (true)
        {
            
            yield return new WaitForSeconds(tailPeriod);
            var Block = Instantiate<GameObject>(trail);
            Block.transform.position = head.transform.position - head.transform.forward*offset;
            Block.transform.rotation = head.transform.rotation;
            Block.transform.localScale = new Vector3(randomScale.x,randomScale.y,randomScale.z);

            Trail trailScript = trail.GetComponent<Trail>();
            trailScript.lifeTime = lifeTime;
            trailScript.waitTime = waitTime;

            Instantiate<GameObject>(Block);
        }
    }


    // Start is called before the first frame update
    void Start()
    {   
        if (useRandom == true)
        {
            randomScale = new Vector3(Random.Range(3, 50), Random.Range(.5f, 4), Random.Range(.5f, 2));
        }
        else { randomScale = new Vector3(3,.03f,.3f); }
        if (trailCoroutine != null)
        {
            StopCoroutine(trailCoroutine);
        }
        trailCoroutine = SpawnTrailCoroutine();
        StartCoroutine(trailCoroutine);

    }

    //// Update is called once per frame
    //void Update()
    //{
        
    //    if (Input.GetKeyDown(KeyCode.Space))
    //    {
    //        if (hasTail)
    //        {
    //            StopCoroutine(trailCoroutine);
    //            hasTail = false;
    //        }
    //        else if (!hasTail)
    //        {
    //            StartCoroutine(trailCoroutine);
    //            hasTail = true;
    //        }
            
    //    }
        
    //}
}
