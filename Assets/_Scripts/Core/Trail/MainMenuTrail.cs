using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuTrail : MonoBehaviour, ICollidable
{
    [SerializeField]
    public float intensityChange = -3f;

    //public delegate void TrailCollision(float amount, string uuid);
    //public static event TrailCollision OnTrailCollision;

    [SerializeField]
    GameObject FossilBlock;

    private MeshRenderer meshRenderer;
    private Collider blockCollider;

    public float waitTime = .6f;
    public float lifeTime = 20;

    // Start is called before the first frame update
    void Start()
    {
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
        //TODO play SFX sound, break apart or float away
        //OnTrailCollision(intensityChange, other.gameObject.GetComponent<Player>().PlayerUUID);
        FossilBlock.transform.localScale = transform.localScale;
        FossilBlock.transform.position = transform.position;
        FossilBlock.transform.localEulerAngles = transform.localEulerAngles;

        Instantiate<GameObject>(FossilBlock);
        Destroy(this.gameObject);

    }
}
