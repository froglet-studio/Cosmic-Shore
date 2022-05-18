using System.Collections;
using UnityEngine;

public class TrailSpawner : MonoBehaviour
{
    [SerializeField]
    GameObject trail;

    public float offset = 0f;
    public float trailPeriod = .1f;
    public float lifeTime = 20;
    public float waitTime = .5f;            // Time until the trail block appears - camera dependent
    public bool useRandom = true;

    [SerializeField]
    private static GameObject TrailContainer;
    private IEnumerator trailCoroutine;

    // Start is called before the first frame update
    void Start()
    {
        if (TrailContainer == null)
        {
            TrailContainer = new GameObject();
            TrailContainer.name = "TrailContainer";
            DontDestroyOnLoad(TrailContainer);
        }

        trailCoroutine = SpawnTrailCoroutine();
        StartCoroutine(trailCoroutine);
    }

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
}
