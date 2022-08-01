using System.Collections;
using UnityEngine;
using StarWriter.Core.Input;
using System.Linq;
using UnityEngine.SceneManagement;
using StarWriter.Core;

public class Trail : MonoBehaviour, ICollidable
{
    [SerializeField]
    private float fuelChange = -3f;

    [SerializeField] GameObject FossilBlock;

    [SerializeField] Material material;
    Material tempMaterial;

    public float waitTime = .6f;
    public delegate void TrailCollision(string uuid, float amount);
    public static event TrailCollision OnTrailCollision;

    private static GameObject container;
    private MeshRenderer meshRenderer;
    private Collider blockCollider;

    public static void ResetTrailContainer()
    {
        for (var i=0; i<container.transform.childCount; i++)
        {
            var child = container.transform.GetChild(i).gameObject;
            Destroy(child);
        }
    }

    void Start()
    {
        if (container == null)
        {
            container = new GameObject();
            container.name = "FossilBlockContainer";
            GameManager.onPlayGame += ResetTrailContainer;
            DontDestroyOnLoad(container);   // TODO: this is probably not awesome ¯\_(ツ)_/¯
        }

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false;

        blockCollider = GetComponent<Collider>();
        blockCollider.enabled = false;

        StartCoroutine(ToggleBlockCoroutine());
    }

    IEnumerator ToggleBlockCoroutine()
    {
        var finalScale = transform.localScale;
        var size = 0f;
        yield return new WaitForSeconds(waitTime);
        transform.localScale = finalScale * size;
        meshRenderer.enabled = true;
        while (size < 1)
        {
            yield return null;
            size += .5f*Time.deltaTime;
            transform.localScale = finalScale * size;
        }
        blockCollider.enabled = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (gameObject != null)
        {
            Destroy(gameObject);
            Collide(other);
        }
        
        
    }

    public void Collide(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            other.transform.parent.parent.GetComponent<Player>().ToggleCollision(false);

            Debug.Log("tagplayer" + GameObject.FindGameObjectsWithTag("Player").Count());

            //// Do Impact Stuff
            var ship = other.transform.parent.parent.gameObject;

            //make exploding block
            var explodingBlock = Instantiate<GameObject>(FossilBlock);
            explodingBlock.transform.position = transform.position;
            explodingBlock.transform.localEulerAngles = transform.localEulerAngles;
            tempMaterial = new Material(material);
            explodingBlock.GetComponent<Renderer>().material = tempMaterial;


            if (ship == GameObject.FindWithTag("Player"))
            //if (GameObject.FindGameObjectsWithTag("Player").Contains(ship))
            {
                //// Player Hit
                var impactVector = ship.transform.forward * ship.GetComponent<InputController>().speed;
                StartCoroutine(explodingBlock.GetComponent<BlockImpact>().ImpactCoroutine(impactVector, tempMaterial, "Player"));
                OnTrailCollision?.Invoke(ship.GetComponent<Player>().PlayerUUID, fuelChange);
                HapticController.PlayBlockCollisionHaptics();
            }

            
            other.transform.parent.parent.GetComponent<Player>().ToggleCollision(true);
        }
    }

    // TODO: Use tags to identify player instead
    private bool IsPlayer(GameObject go)
    {
        return go.transform.parent.parent.GetComponent<Player>() != null;
    }
}
