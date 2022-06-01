using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TrailSpawner : MonoBehaviour
{
    [SerializeField]
    GameObject trail;

    public float offset = 0f;
    public float trailPeriod = .1f;

    public float trailLength = 20;
    //public float trailLength = 20;

    public float waitTime = .5f;            // Time until the trail block appears - camera dependent
    public float startDelay = 2;
    public bool useRandom = true;


    [SerializeField]
    private static GameObject TrailContainer;
    private IEnumerator trailCoroutine;

    private readonly Queue<GameObject> trailList = new();

    public static void ResetTrailContainer()
    {
        for (var i = TrailContainer.transform.childCount-1; i >= 0; i--)
        {
            var child = TrailContainer.transform.GetChild(i).gameObject;
            Destroy(child);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (TrailContainer == null)
        {
            TrailContainer = new GameObject();
            TrailContainer.name = "TrailContainer";
            GameManager.onPlayGame += ResetTrailContainer;
            DontDestroyOnLoad(TrailContainer);
        }



        trailCoroutine = SpawnTrailCoroutine();
        StartCoroutine(trailCoroutine);
    }

    IEnumerator SpawnTrailCoroutine()
    {
        yield return new WaitForSeconds(startDelay);
        while (true)
        {
            yield return new WaitForSeconds(trailPeriod);

            var Block = Instantiate(trail);
            Block.transform.SetPositionAndRotation(transform.position - transform.forward*offset, transform.rotation);
            //Block.transform.localScale = new Vector3(scale.x,scale.y,scale.z);
            Block.transform.parent = TrailContainer.transform;

            Trail trailScript = trail.GetComponent<Trail>();
            trailScript.waitTime = waitTime;

            
            trailList.Enqueue(Block);
            if (trailList.Count > trailLength/trailPeriod)
            {
                Destroy(trailList.Dequeue()); 
            }

        }
    }
}
