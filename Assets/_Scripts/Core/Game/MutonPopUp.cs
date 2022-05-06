using UnityEngine;
using Random = UnityEngine.Random;
using TMPro;

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
    IntensityBar IntensityBar;

    [SerializeField]
    float MutonIntensityBoost = .1f;


    [SerializeField]
    float lifeTimeIncrease = 20;

    [SerializeField]
    TextMeshProUGUI outputText;

    int score = 0;

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
        //make some dandruff
        var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
        spentMuton.transform.position = transform.position;
        spentMuton.transform.localEulerAngles = transform.localEulerAngles;

        //move the muton
        transform.position = UnityEngine.Random.insideUnitSphere * sphereRadius;
        transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);
        OnMutonPopUpCollision(intensityAmount, other.GetComponentInParent<Transform>().GetComponentInParent<Player>().PlayerUUID);

        //update intensity bar and score
        IntensityBar.IncreaseIntensity(MutonIntensityBoost); // TODO: use events instead
        score++;
        outputText.text = score.ToString("D3");

        // Grow tail
        TrailSpawner trailScript = other.GetComponent<TrailSpawner>();
        trailScript.lifeTime += lifeTimeIncrease;

        // Make ai harder
        if (other.gameObject.GetComponent<Player>().PlayerUUID == "admin")
        {
            StarWriter.Core.Input.AiShipController aiControllerScript = aiShip.GetComponent<StarWriter.Core.Input.AiShipController>();
            aiControllerScript.lerpAmount += .001f;
            aiControllerScript.defaultThrottle += .01f;
        }
        
        //TODO play SFX sound
        //TODO Respawn
    }
}
