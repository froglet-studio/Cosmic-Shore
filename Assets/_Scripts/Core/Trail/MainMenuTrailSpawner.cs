using System.Collections;
using UnityEngine;

// TODO: This is a direct copy of "/Assets/TrainSpawner" it needs to not be
public class MainMenuTrailSpawner : MonoBehaviour
{
    public GameObject trail;
    public Transform head;
    public float offset = 0f;
    public float trailPeriod = .1f;
    public float lifeTime = 20;
    public float waitTime = .5f;
    public bool useRandom = true;

    private Vector3 scale;
    private static GameObject TrailContainer;
    private IEnumerator trailCoroutine;

    IEnumerator SpawnTrailCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(trailPeriod);

            var Block = Instantiate(trail);
            Block.transform.position = head.transform.position - head.transform.forward * offset;
            Block.transform.rotation = head.transform.rotation;
            Block.transform.localScale = new Vector3(scale.x, scale.y, scale.z);
            Block.transform.parent = TrailContainer.transform;

            MainMenuTrail trailScript = trail.GetComponent<MainMenuTrail>();
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

        if (useRandom == true)
        {
            scale = new Vector3(Random.Range(3, 50), Random.Range(.5f, 4), Random.Range(.5f, 2));
        }
        else
        {
            scale = new Vector3(3, .03f, .3f);
        }

        trailCoroutine = SpawnTrailCoroutine();
        StartCoroutine(trailCoroutine);
    }
}


