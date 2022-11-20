using System.Collections;
using UnityEngine;
using System.Linq;

public class Trail : MonoBehaviour
{


    [SerializeField] float fuelChange = -3f;
    [SerializeField] GameObject FossilBlock;
    [SerializeField] GameObject ParticleEffect;
    [SerializeField] Material material;
    //[SerializeField] Vector3 ParticleEffectScale = new Vector3(1.5f, 1.5f, 1.5f);

    public string ownerId;
    public float waitTime = .6f;
    public bool embiggen;

    public bool warp = false;
    GameObject shards;

    public delegate void TrailCollision(string uuid, float amount);
    public static event TrailCollision OnTrailCollision;

    public delegate void OnCollisionIncreaseScore(string uuid, int amount);
    public static event OnCollisionIncreaseScore AddToScore;

    private int scoreChange = 1;
    private static GameObject container;
    private MeshRenderer meshRenderer;
    private BoxCollider blockCollider;

    void Start()
    {
        if (warp) shards = GameObject.FindGameObjectWithTag("field");

        if (container == null)
        {
            container = new GameObject();
            container.name = "FossilBlockContainer";
        }

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false;

        blockCollider = GetComponent<BoxCollider>();
        blockCollider.enabled = false;

        if (embiggen) StartCoroutine(ToggleBlockCoroutine(2f));
        else StartCoroutine(ToggleBlockCoroutine(1f));


    }

    IEnumerator ToggleBlockCoroutine(float finalSize)
    {
        var finalTransformScale = transform.localScale;

        if (warp) finalTransformScale *= shards.GetComponent<WarpFieldData>().HybridVector(transform).magnitude;

        var size = 0.01f;

        yield return new WaitForSeconds(waitTime);

        transform.localScale = finalTransformScale * size;

        meshRenderer.enabled = true;
        blockCollider.enabled = true;

        while (size < finalSize)
        {
            size += .5f*Time.deltaTime;
            transform.localScale = finalTransformScale * size;
            yield return null;
        }
    }

    public void InstantiateParticle(Transform skimmer)
    {
        Debug.Log($"{this.name}, Instantiating Particle Emitter");
        var particle = Instantiate(ParticleEffect);
        particle.transform.parent = transform;
        //particle.transform.localPosition = Vector3.zero;
        StartCoroutine(UpdateParticleCoroutine(particle, skimmer));

        // TODO: expose scale as a parameter or base it off of distance between block and skimmer, or both
        // TODO: rotate particle using skimmer forward and block forward?
        // TODO: experiment with multiple instantiations when super close
    }

    IEnumerator UpdateParticleCoroutine(GameObject particle, Transform skimmer)
    {
        var time = 50;
        var timer = 0;
        while (timer < time)
        {
            yield return null;
            var distance =  transform.position - skimmer.position;
            particle.transform.localScale = new Vector3(1, 1, distance.magnitude);
            particle.transform.rotation = Quaternion.LookRotation(distance, transform.up);
            particle.transform.position = skimmer.position;
            timer++;
        }
        Destroy(particle);
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

    private bool IsPlayer(GameObject go)
    {
        //return go.transform.parent.parent.GetComponent<Player>() != null;
        
        return go.layer == LayerMask.NameToLayer("Ships");
    }
}
