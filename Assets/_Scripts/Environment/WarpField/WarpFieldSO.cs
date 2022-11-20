using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WarpFieldData", menuName = "ScriptableObjects/WarpfieldSO", order = 1)]
[System.Serializable] public class WarpFieldSO : ScriptableObject
{ 

    public int fieldThickness;
    public int fieldWidth;
    public int fieldHeight;
    public float fieldMax;

    public WarpFieldSO()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 200;
        fieldMax = 300f;
    }

    virtual public Vector3 HybridVector(Transform node) // direction is aligned with the gradient, but magnitude is the value of the scalar field
    {
        return Vector3.zero;
    }
}
