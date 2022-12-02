using UnityEngine;

public class Skimmer : MonoBehaviour
{
    [SerializeField] Ship ship;
    [SerializeField] float fuelAmount;

    // TODO: move this away from using an event
    public delegate void Skim(string uuid, float amount);
    public static event Skim OnSkim;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("skim collider: " + other.name);
        OnSkim?.Invoke(ship.Player.PlayerUUID, fuelAmount);

        var trail = other.GetComponent<Trail>();
        if (trail != null) trail.InstantiateParticle(transform);
    }
}