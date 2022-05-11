using System.Collections;
using UnityEngine;

public class Trail : MonoBehaviour, ICollidable
{
    [SerializeField]
    private float intensityChange = -3f;

    [SerializeField]
    GameObject FossilBlock;

    public float waitTime = .6f;
    public float lifeTime = 20;
    public delegate void TrailCollision(string uuid, float amount);
    public static event TrailCollision OnTrailCollision;

    private static GameObject container;
    private MeshRenderer meshRenderer;
    private Collider blockCollider;

    // Start is called before the first frame update
    void Start()
    {
        if (container == null)
        {
            container = new GameObject();
            container.name = "FossilBlockContainer";
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

    private void OnTriggerEnter(Collider other)
    {
        Collide(other);
    }

    public void Collide(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            //TODO play SFX sound, break apart or float away
            OnTrailCollision?.Invoke(other.GetComponentInParent<Transform>().GetComponentInParent<Player>().PlayerUUID, intensityChange);
            var fossilBlock = Instantiate(FossilBlock);
            fossilBlock.transform.localScale = transform.localScale;
            fossilBlock.transform.position = transform.position;
            fossilBlock.transform.localEulerAngles = transform.localEulerAngles;
            fossilBlock.transform.parent = container.transform;

            Destroy(this.gameObject);
        }
    }

    // TODO: Use tags to identify player instead
    private bool IsPlayer(GameObject go)
    {
        return go.GetComponent<Player>() != null;
    }
}
