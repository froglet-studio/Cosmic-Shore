using UnityEngine;
using Random = UnityEngine.Random;

public class MutonPopUp : MonoBehaviour//, ICollidable
{
    [SerializeField]
    float sphereRadius = 100;

    [SerializeField]
    GameObject aiShip;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    Vector3 displacement = Vector3.zero;

    [SerializeField]
    float intensityAmount = 10f;

    [SerializeField]
    float lifeTimeIncrease = 20;

    public delegate void PopUpCollision(float amount, string uuid);
    public static event PopUpCollision OnMutonPopUpCollision;

    void Start()
    {
        transform.position = Random.insideUnitSphere * sphereRadius + displacement;
    }

    private void OnTriggerEnter(Collider other)
    {
        //TODO Decay brokenSphere and clean up
        Collide(other);
    }

    public void Collide(Collider other)
    {
        var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
        spentMuton.transform.position = transform.position;
        spentMuton.transform.localEulerAngles = transform.localEulerAngles;
        
        transform.position = UnityEngine.Random.insideUnitSphere * sphereRadius;
        OnMutonPopUpCollision(intensityAmount, other.gameObject.GetComponent<Player>().PlayerUUID);

        // Grow tail
        TrailSpawner trailScript = other.GetComponent<TrailSpawner>();
        trailScript.lifeTime += lifeTimeIncrease;

        // Make ai harder
        if (other.gameObject.GetComponent<Player>().PlayerUUID == "admin")
        {
            StarWriter.Core.Input.AiShipController aiControllerScript = aiShip.GetComponent<StarWriter.Core.Input.AiShipController>();
            aiControllerScript.lerpAmount += .005f;
            aiControllerScript.defaultThrottle += .05f;
        }
        
        //TODO play SFX sound
        //TODO Respawn
    }
}
