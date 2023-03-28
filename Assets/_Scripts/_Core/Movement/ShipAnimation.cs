using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

[RequireComponent(typeof(Ship))]
public abstract class ShipAnimation : MonoBehaviour
{
    InputController inputController;
    void Start()
    {
        inputController = GetComponent<Ship>().inputController;
    }

    void Update()
    {
        if (inputController == null) inputController = GetComponent<Ship>().inputController; 
        if (inputController != null) // the line above makes this run the moment it has the handle
        {
            if (inputController.Idle)
                Idle();
            else
                PerformShipAnimations(inputController.YSum, inputController.XSum, inputController.YDiff, inputController.XDiff);
        }
    }

    public abstract void PerformShipAnimations(float YSum, float XSum, float YDiff, float XDiff);
    public abstract void Idle();
}
