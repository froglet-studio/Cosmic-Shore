using System.Collections;
using UnityEngine;
using StarWriter.Core.Input;
using System.Linq;
using UnityEngine.SceneManagement;
using StarWriter.Core;

public class Trail : MonoBehaviour, ICollidable
{
    [SerializeField]
    private float intensityChange = -3f;

    [SerializeField]
    GameObject FossilBlock;

    [SerializeField]
    Material material;
    Material tempMaterial;

    public float waitTime = .6f;
    public float lifeTime = 20;
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
        yield return new WaitForSeconds(waitTime);
        meshRenderer.enabled = true;
        blockCollider.enabled = true;

        // TODO: use a queue of trail blocks instead of a expiration time
        // Using expiration timer will create variable length tails depending on clock speed
        yield return new WaitForSeconds(lifeTime);
        meshRenderer.enabled = false;
        blockCollider.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        Collide(other);
    }

    public void Collide(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            Debug.Log("tagplayer" + GameObject.FindGameObjectsWithTag("Player").Count());
            
            // Play SFX sound
            // TODO

            //// Create Fossil
            //tempMaterial = new Material(material);
            //var fossilBlock = Instantiate(FossilBlock);
            //fossilBlock.transform.localScale = transform.localScale;
            //fossilBlock.transform.position = transform.position;
            //fossilBlock.transform.localEulerAngles = transform.localEulerAngles;
            //fossilBlock.transform.parent = container.transform;
            //fossilBlock.GetComponent<Renderer>().material = tempMaterial;

            //// Do Impact Stuff
            var ship = other.transform.parent.parent.gameObject;
            //var blockImpact = fossilBlock.GetComponent<BlockImpact>();
            
            if (GameObject.FindGameObjectsWithTag("Player").Contains(ship))
            {
                //// Player Hit
                //var impactVector = ship.transform.forward * ship.GetComponent<InputController>().speed;
                //StartCoroutine(blockImpact.ImpactCoroutine(impactVector, tempMaterial, "Player"));
                OnTrailCollision?.Invoke(ship.GetComponent<Player>().PlayerUUID, intensityChange);
                HapticController.PlayBlockCollisionHaptics();
            }
            //else
            //{
            //    // AI Hit
            //    var impactVector = ship.transform.forward * ship.GetComponent<AiShipController>().speed;
            //    if (ship == GameObject.FindWithTag("red"))
            //    {
            //        StartCoroutine(blockImpact.ImpactCoroutine(impactVector, tempMaterial, "red"));
            //    }
            //    else
            //    {
            //        StartCoroutine(blockImpact.ImpactCoroutine(impactVector, tempMaterial, "blue"));
            //    }
            //}
            Destroy(gameObject);
        }
    }

    // TODO: Use tags to identify player instead
    private bool IsPlayer(GameObject go)
    {
        return go.transform.parent.parent.GetComponent<Player>() != null;
    }
}
