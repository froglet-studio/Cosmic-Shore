using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ShipAnimation : MonoBehaviour

{ 
    public abstract void PerformShipAnimations(float Xsum, float Ysum, float Xdiff, float Ydiff);
}
