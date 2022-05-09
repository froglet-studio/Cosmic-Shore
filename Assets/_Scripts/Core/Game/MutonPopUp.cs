using UnityEngine;
using Random = UnityEngine.Random;
using TMPro;
using System.Collections.Generic;

public class MutonPopUp : MonoBehaviour//, ICollidable
{
    [SerializeField]
    float sphereRadius = 100;

    [SerializeField]
    GameObject aiShip;

    [SerializeField]
    GameObject spentMutonPrefab;

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

    [SerializeField]
    GameObject Muton;

    [SerializeField]
    Material material;

    Material tempMaterial;

    private List<Material> materials; 

    //private List<Coroutine> impactRoutines;

    int score = 0;

    public delegate void PopUpCollision(float amount, string uuid);
    public static event PopUpCollision OnMutonPopUpCollision;

    void Start()
    {
        transform.position = Random.insideUnitSphere * sphereRadius;
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
        tempMaterial = new Material(material);
        //tempMaterial.CopyPropertiesFromMaterial(material);
        //materials.Add(tempMaterial);
        spentMuton.GetComponent<Renderer>().material = tempMaterial;

        //impactRoutines.Add();
        
        StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(spentMuton.transform.rotation*other.transform.forward, tempMaterial));

        //move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
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
