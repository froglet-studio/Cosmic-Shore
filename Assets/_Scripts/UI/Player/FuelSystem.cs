using StarWriter.Core;
using System;
using UnityEngine;

public class FuelSystem : MonoBehaviour
{
    #region Events
    public delegate void OnFuelChangeEvent(float amount);
    public static event OnFuelChangeEvent OnFuelChange;

    public delegate void OnFuelEmptyEvent();
    public static event OnFuelEmptyEvent OnFuelEmpty;
    #endregion

    #region Floats
    [Tooltip("Initial and Max fuel level from 0-1")]
    [SerializeField]
    [Range(0, 1)]
    static float maxFuel = 1f;
    static float currentFuel;

    [SerializeField]float rateOfFuelChange = -0.02f;
    #endregion

    [SerializeField] bool verboseLogging;

    public static float CurrentFuel { 
        get => currentFuel; 
        private set 
        { 
            currentFuel = value; 
            OnFuelChange?.Invoke(currentFuel);
            if (currentFuel <= 0) OnFuelEmpty?.Invoke();
        }
    }

    public static void ResetFuelEmptyListeners()
    {
        foreach (Delegate d in OnFuelEmpty.GetInvocationList())
        {
            OnFuelEmpty -= (OnFuelEmptyEvent)d;
        }
    }

    private void OnEnable()
    {
        Trail.OnTrailCollision += ChangeFuelAmount;
        MutonPopUp.OnMutonPopUpCollision += ChangeFuelAmount;
        GameManager.onExtendGamePlay += ResetFuel;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= ChangeFuelAmount;
        MutonPopUp.OnMutonPopUpCollision -= ChangeFuelAmount;
        GameManager.onExtendGamePlay -= ResetFuel;
    }

    void Start()
    {
        ResetFuel();
    }

    void Update()
    {
        // TODO: we need to get "admin" out of the codebase
        if (currentFuel > 0)
            ChangeFuelAmount("admin", rateOfFuelChange * Time.deltaTime); // Only effects current player
    }

    public static void ResetFuel()
    {
        CurrentFuel = maxFuel;
    }

    private void ChangeFuelAmount(string uuid, float amount)
    {
        if (uuid == "admin")
        {
            CurrentFuel = Mathf.Clamp(currentFuel + amount, 0, 1);

            Debug.Log("FuelSystem reading is " + currentFuel);
        }
    }
}