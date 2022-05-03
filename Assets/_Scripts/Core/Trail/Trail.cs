using System;
using System.Collections.Generic;
using UnityEngine;

public class Trail : MonoBehaviour, ICollidable
{
    [SerializeField]
    public float intensityAmountloss = -3f;

    public static event Action<float, string> OnTrailCollision;

    // Start is called before the first frame update
    void Start()
    {
        
    }


    private void OnTriggerEnter(Collider other)
    {
        OnTrailCollision(intensityAmountloss, other.gameObject.GetComponent<Player>().PlayerUUID);
        Collide();
    }

    public void Collide()
    {
        //TODO play SFX sound, break apart or float away
        Destroy(this);
    }
}
