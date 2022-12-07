using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ShipAnimation : MonoBehaviour

{ 
    public abstract void PerformShipAnimations(float ySum, float xSum, float yDiff, float xDiff);
    public abstract void Idle();
}
