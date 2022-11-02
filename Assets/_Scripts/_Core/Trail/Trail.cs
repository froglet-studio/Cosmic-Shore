using System.Collections;
using UnityEngine;
using System.Linq;

public class Trail : MonoBehaviour
{
    [SerializeField]
    private float fuelChange = -3f;
    private int scoreChange = 1;
    public string ownerId;

    [SerializeField] GameObject FossilBlock;
    [SerializeField] GameObject ParticleEffect;


    [SerializeField] Material material;

    public float waitTime = .6f;
    public delegate void TrailCollision(string uuid, float amount);
    public static event TrailCollision OnTrailCollision;

    public delegate void OnCollisionIncreaseScore(string uuid, int amount);
    public static event OnCollisionIncreaseScore AddToScore;

    private static GameObject container;
    private MeshRenderer meshRenderer;
    private BoxCollider blockCollider;

    // TODO: why are we doing this? The scene is getting reloaded, so shouldn't the container get voided out that way...
    // Wait a minute... is this to account for the 'DontDestroyOnLoad(container) line further down?
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
        }

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false;

        blockCollider = GetComponent<BoxCollider>();
        blockCollider.enabled = false;

        StartCoroutine(ToggleBlockCoroutine());
    }

    IEnumerator ToggleBlockCoroutine()
    {
        var finalTransformScale = transform.localScale;
        var finalColliderScale = blockCollider.size;
        var size = 0.01f;

        yield return new WaitForSeconds(waitTime);

        transform.localScale = finalTransformScale * size;
        blockCollider.size = finalColliderScale * size;

        meshRenderer.enabled = true;
        blockCollider.enabled = true;

        while (size < 1)
        {
            size += .5f*Time.deltaTime;
            transform.localScale = finalTransformScale * size;
            blockCollider.size = finalColliderScale * size;
            yield return null;
        }
    }

    public void InstantiateParticle()
    {
        Debug.Log($"{this.name}, Instantiating Particle Emitter");
        var particle = Instantiate(ParticleEffect);
        particle.transform.parent = transform;
        particle.transform.localPosition = Vector3.zero;
        particle.transform.localScale = new Vector3(3, 3, 3);
    }
    void OnTriggerEnter(Collider other)
    {
        if (gameObject != null && other.isTrigger == false) //don't want to catch the skimmer collider
        {
            // We used to destroy the object, but we were throwing null pointers later in the code when Destroying blocks that expired
            // Instead, let's just disable the collider and renderer and let the trailspawner clean up the object lazily
            //Destroy(gameObject);

            gameObject.GetComponent<BoxCollider>().enabled = false;
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            Collide(other);
        }
    }

    public void Collide(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            Debug.Log("tagplayer" + GameObject.FindGameObjectsWithTag("Player").Count());

            //// Do Impact Stuff
            var ship = other.transform.parent.parent.gameObject;

            if (ownerId == ship.GetComponent<Player>().PlayerUUID)
            {
                Debug.Log($"You hit you're own tail: {ownerId}");
            }
            else
            {
                Debug.Log($"Player ({ship.GetComponent<Player>().PlayerUUID}) just gave player({ownerId}) a point via tail collision");
                AddToScore?.Invoke(ownerId, scoreChange);
            }

            // TODO: null pointers thrown here
            var impactVector = ship.transform.forward * ship.GetComponent<ShipData>().speed;

            // Make exploding block
            var explodingBlock = Instantiate(FossilBlock);
            explodingBlock.transform.position = transform.position;
            explodingBlock.transform.localEulerAngles = transform.localEulerAngles;
            explodingBlock.transform.localScale = transform.localScale;
            explodingBlock.GetComponent<Renderer>().material = new Material(material);
            explodingBlock.GetComponent<BlockImpact>().HandleImpact(impactVector, "Player");

            //// Player Hit
            if (ship == GameObject.FindWithTag("Player"))
            {
                // TODO: for now, we're only turning off collision on the player. In the future, we want AI ships to explode and all that too
                // TODO: turned off collision toggling for now - need to reintroduce into death sequence somewhere else
                //other.transform.parent.parent.GetComponent<Player>().ToggleCollision(false);

                // TODO: currently AI fuel levels are not impacted when they collide with a trail
                OnTrailCollision?.Invoke(ownerId, fuelChange);
                
                HapticController.PlayBlockCollisionHaptics();
            }
        }
    }

    // TODO: Use tags to identify player instead
    private bool IsPlayer(GameObject go)
    {
        return go.transform.parent.parent.GetComponent<Player>() != null;
    }
}
