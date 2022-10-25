using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skimmer : MonoBehaviour
{
    public float fuelAmount;
    GameObject ship;

    public Vector3 direction;
    Material material;
    public delegate void Skim(string uuid, float amount);
    public static event Skim OnSkim;

    // Start is called before the first frame update
    void Start()
    {
        ship = GameObject.FindWithTag("Player");
        material = GetComponent<MeshRenderer>().material;
        direction = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        direction = Vector3.Lerp(direction, Vector3.zero, .01f);
        material.SetVector("_direction", direction);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("skim collider: " + other.name);
        OnSkim?.Invoke(ship.GetComponent<Player>().PlayerUUID, fuelAmount);

        direction = Vector3.Lerp(direction, (transform.worldToLocalMatrix * (other.transform.position - transform.position)).normalized * 19,.1f);
        material.SetVector("_direction", direction);
    }
}
