using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skimmer : MonoBehaviour
{
    public float fuelAmount;
    GameObject ship;

    public delegate void Skim(string uuid, float amount);
    public static event Skim OnSkim;

    // Start is called before the first frame update
    void Start()
    {
        ship = GameObject.FindWithTag("Player");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("skim collider: " + other.name);
        OnSkim?.Invoke(ship.GetComponent<Player>().PlayerUUID, fuelAmount);
        
    }
}
