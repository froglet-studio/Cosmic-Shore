using System.Collections;
using UnityEngine;

public class TrailSpawner : MonoBehaviour
{
    [SerializeField]
    GameObject trail;


    public float offset = 0f;
    public float trailPeriod = .1f;
    public float lifeTime = 20;
    public float waitTime = .5f;
    public bool useRandom = true;

    //private Vector3 scale;
    private static GameObject TrailContainer;
    private IEnumerator trailCoroutine;

    IEnumerator SpawnTrailCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(trailPeriod);

            var Block = Instantiate(trail);
            Block.transform.SetPositionAndRotation(transform.position - transform.forward*offset, transform.rotation);
            //Block.transform.localScale = new Vector3(scale.x,scale.y,scale.z);
            Block.transform.parent = TrailContainer.transform;

            Trail trailScript = trail.GetComponent<Trail>();
            trailScript.lifeTime = lifeTime;
            trailScript.waitTime = waitTime;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (TrailContainer == null)
        {
            TrailContainer = new GameObject();
            TrailContainer.name = "TrailContainer";
        }

        //if (useRandom == true)
        //{
        //    scale = new Vector3(Random.Range(3, 50), Random.Range(.5f, 4), Random.Range(.5f, 2));
        //}
        //else 
        //{ 
        //    scale = new Vector3(3,.03f,.3f);
        //}

        trailCoroutine = SpawnTrailCoroutine();
        StartCoroutine(trailCoroutine);
    }
}
