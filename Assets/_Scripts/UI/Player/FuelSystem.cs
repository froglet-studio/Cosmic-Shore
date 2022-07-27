using System;
using UnityEngine;

public class FuelSystem : MonoBehaviour
{
    #region Events

    public delegate void OnFuelChangeEvent(string uuid, float amount);
    public static event OnFuelChangeEvent onFuelChange;

    public delegate void OnFuelZeroEvent();
    public static event OnFuelZeroEvent zeroFuel;

    #endregion
    #region Floats
    [Tooltip("Initial and Max fuel level from 0-1")]
    [SerializeField]
    [Range(0, 1)]
    static float maxFuel = 1f;
    static float currentFuel;

    [SerializeField]
    float rateOfFuelChange = -0.02f;

    #endregion

    [SerializeField] string uuidOfPlayer = "";
    [SerializeField] bool verboseLogging;

    public static float CurrentFuel { get => currentFuel; }

    public static void ResetZeroFuel()
    {
        foreach (Delegate d in zeroFuel.GetInvocationList())
        {
            zeroFuel -= (OnFuelZeroEvent)d;
        }
    }

    private void OnEnable()
    {
        Trail.OnTrailCollision += ChangeFuelAmount;
        MutonPopUp.OnMutonPopUpCollision += ChangeFuelAmount;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= ChangeFuelAmount;
        MutonPopUp.OnMutonPopUpCollision -= ChangeFuelAmount;
    }

    void Start()
    {
        ResetFuel();
    }


    void Update()
    {
        if (currentFuel > 0)
            ChangeFuelAmount("admin", rateOfFuelChange * Time.deltaTime); //Only effects current player
    }

    public static void ResetFuel()
    {
        currentFuel = maxFuel;
    }

    private void ChangeFuelAmount(string uuid, float amount)
    {
        uuidOfPlayer = uuid;  //Recieves uuid of from Collision Events
        
        currentFuel = Mathf.Clamp(currentFuel + amount, 0, 1);
        UpdateCurrentFuelAmount(uuidOfPlayer, currentFuel);
        UpdateFuelBar(uuid, currentFuel);
        if (currentFuel <= 0)
            zeroFuel?.Invoke();
    }

    private void UpdateFuelBar(string uuidOfPlayer, float currentFuel)
    {
        if (verboseLogging)
            Debug.Log("FuelSystem reading is " + currentFuel);
        
        onFuelChange?.Invoke(uuidOfPlayer, currentFuel);
    }

    private void UpdateCurrentFuelAmount(string uuid, float amount)
    {
        if (uuid == "admin")
            currentFuel = amount;
    }
}
