using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ship : MonoBehaviour
{
    [SerializeField] string shipName;
    [SerializeField] string shipUUID;

    [SerializeField] SO_Ship_Base ship_SO;

    public string ShipName { get => shipName; }
    public string ShipUUID { get => shipUUID; }
    public SO_Ship_Base ShipSO { get => ship_SO; }


    // Start is called before the first frame update
    void Start()
    {
        InitializeShip();
    }

    // Update is called once per frame
    void Update()
    {

    }

    //Sets Ship Fields from the assigned Scriptable Object 
    void InitializeShip()
    {
        //TODO
        shipName = ship_SO.ShipName;
        //shipUUID = ship_SO.

    }

    public void ChangeShip(SO_Ship_Base ship)
    {
        ship_SO = ship;
        InitializeShip();
    }
}
