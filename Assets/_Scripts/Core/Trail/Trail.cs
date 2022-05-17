using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core.Input;
using System.Linq;


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

    List<Collider> collisions;

    // Start is called before the first frame update
    void Start()
    {
        collisions = new List<Collider>();

        if (container == null)
        {
            container = new GameObject();
            container.name = "FossilBlockContainer";
            DontDestroyOnLoad(container);
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
            //TODO play SFX sound,
            //OnTrailCollision?.Invoke(other.GetComponentInParent<Transform>().GetComponentInParent<Player>().PlayerUUID, intensityChange);
            GameObject ship;
            ship = other.transform.parent.parent.gameObject;
            var fossilBlock = Instantiate(FossilBlock);
            fossilBlock.transform.localScale = transform.localScale;
            fossilBlock.transform.position = transform.position;
            fossilBlock.transform.localEulerAngles = transform.localEulerAngles;
            fossilBlock.transform.parent = container.transform;
            tempMaterial = new Material(material);
            fossilBlock.GetComponent<Renderer>().material = tempMaterial;

           
            Debug.Log("tagplayer" + GameObject.FindGameObjectsWithTag("Player").Count());

            if (GameObject.FindGameObjectsWithTag("Player").Contains(ship))
            {
                //trail animation and haptics
                StartCoroutine(fossilBlock.GetComponent<BlockImpact>().ImpactCoroutine(
                    ship.transform.forward * ship.GetComponent<InputController>().speed, tempMaterial, "Player"));
                HapticController.PlayBlockCollisionHaptics();
                //update intensity bar and score
                OnTrailCollision?.Invoke(ship.GetComponent<Player>().PlayerUUID, intensityChange); 
            }
            //animate when ai hit
            else
            {
                if (ship == GameObject.FindWithTag("red"))
                {
                    StartCoroutine(fossilBlock.GetComponent<BlockImpact>().ImpactCoroutine(
                        ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "red"));

                }
                else
                {
                    StartCoroutine(fossilBlock.GetComponent<BlockImpact>().ImpactCoroutine(
                         ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "blue"));
                }
            }
            Destroy(gameObject);
        }
    }

    // TODO: Use tags to identify player instead
    private bool IsPlayer(GameObject go)
    {
        return go.transform.parent.parent.GetComponent<Player>() != null;
    }
}
