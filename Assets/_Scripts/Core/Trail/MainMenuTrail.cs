using System.Collections;
using UnityEngine;

public class MainMenuTrail : MonoBehaviour, ICollidable
{
    [SerializeField]
    GameObject FossilBlock;

    private GameObject container;
    private MeshRenderer meshRenderer;
    private Collider blockCollider;

    public float waitTime = .6f;
    public float lifeTime = 20;

    // Start is called before the first frame update
    void Start()
    {
        container = new GameObject();
        container.name = "FossilBlockContainer";

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
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        Collide(other);
    }

    public void Collide(Collider other)
    {
        //TODO play SFX sound, break apart or float away
        var fossilBlock = Instantiate<GameObject>(FossilBlock);
        fossilBlock.transform.localScale = transform.localScale;
        fossilBlock.transform.position = transform.position;
        fossilBlock.transform.localEulerAngles = transform.localEulerAngles;
        fossilBlock.transform.parent = container.transform;

        Destroy(gameObject);
    }
}
