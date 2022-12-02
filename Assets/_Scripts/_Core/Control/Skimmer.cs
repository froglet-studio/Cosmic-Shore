using UnityEngine;

public class Skimmer : MonoBehaviour
{
    [SerializeField] Ship ship;
    [SerializeField] float fuelAmount;

    Vector3 direction;

    public delegate void Skim(string uuid, float amount);
    public static event Skim OnSkim;

    void Start()
    {
        direction = Vector3.zero;
    }

    void Update()
    {
        direction = Vector3.Lerp(direction, Vector3.zero, .02f);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("skim collider: " + other.name);
        OnSkim?.Invoke(ship.Player.PlayerUUID, fuelAmount);

        var trail = other.GetComponent<Trail>();
        if (trail != null) trail.InstantiateParticle(transform);
    }
}