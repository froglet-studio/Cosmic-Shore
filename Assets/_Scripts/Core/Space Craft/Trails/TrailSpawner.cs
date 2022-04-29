using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrailSpawner : MonoBehaviour
{
    public GameObject trail;
    public Transform head;
    public float offset = 1.5f;
    

    bool hasTail = true;
    private IEnumerator trailCoroutine;

    IEnumerator SpawnTrailCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(.1f);
            trail.transform.position = head.transform.position - head.transform.forward*offset;
            trail.transform.rotation = head.transform.rotation;
            Instantiate<GameObject>(trail);
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        
        if(trailCoroutine != null)
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
