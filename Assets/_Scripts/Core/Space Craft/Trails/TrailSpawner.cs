using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrailSpawner : MonoBehaviour
{
    public GameObject trail;
    public Transform head;
    public float offset = 0.0f;
    public Vector3 offsetVector;

    bool hasTail = true;
    private IEnumerator trailCoroutine;

    IEnumerator SpawnTrailCoroutine()
    {
        while (true)
        {
            trail.transform.position = head.transform.position - Vector3.Scale(head.transform.forward,offsetVector);
            trail.transform.rotation = head.transform.rotation;
            Instantiate<GameObject>(trail);
            yield return new WaitForSeconds(.1f);
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        offsetVector = new Vector3(offset, offset, offset);
        if(trailCoroutine != null)
        {
            StopCoroutine(trailCoroutine);
        }
        trailCoroutine = SpawnTrailCoroutine();
        StartCoroutine(trailCoroutine);

    }

    // Update is called once per frame
    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (hasTail)
            {
                StopCoroutine(trailCoroutine);
                hasTail = false;
            }
            else if (!hasTail)
            {
                StartCoroutine(trailCoroutine);
                hasTail = true;
            }
            
        }
        
    }
}
