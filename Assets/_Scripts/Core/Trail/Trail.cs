using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trail : MonoBehaviour//, ICollidable
{
    //[SerializeField]
    //public float intensityAmountloss = -3f;

    public static event Action<float, string> OnTrailCollision;

    [SerializeField]
    GameObject FossilBlock;

    private MeshRenderer meshRenderer;
    private Collider blockCollider;

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
        yield return new WaitForSeconds(.5f);
        meshRenderer.enabled = true;
        blockCollider.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        //OnTrailCollision(intensityAmountloss, other.gameObject.GetComponent<Player>().PlayerUUID);
        //Collide();
        FossilBlock.transform.localScale = transform.localScale;
        FossilBlock.transform.position = transform.position;
        FossilBlock.transform.localEulerAngles = transform.localEulerAngles;
        
        Instantiate<GameObject>(FossilBlock);
        Destroy(this.gameObject);
    }

    //public void Collide()
    //{
    //    //TODO play SFX sound, break apart or float away
    //    Destroy(this.gameObject);

    //}
}
