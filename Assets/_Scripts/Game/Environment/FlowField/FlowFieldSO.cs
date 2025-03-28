﻿using UnityEngine;

[CreateAssetMenu(fileName = "DefaultFlowData", menuName = "CosmicShore/Flow/EllipticalFlow", order = 30)]
[System.Serializable] 
public class FlowFieldSO : ScriptableObject 
{
    public int fieldThickness;
    public int fieldWidth;
    public int fieldHeight;
    public float fieldMax;

    public FlowFieldSO()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 700;
        fieldMax = .7f;
    }

    virtual public Vector3 FlowVector(Transform node)
    {
        if ((Mathf.Pow(node.position.x, 2) / Mathf.Pow(fieldWidth, 2)) + (Mathf.Pow(node.position.y, 2) / Mathf.Pow(fieldHeight, 2)) < 1)
        {
            return new Vector3(
                         -(Mathf.Sin(node.position.y * 3.14f / fieldHeight) * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1))),
                         Mathf.Sin(node.position.x * 3.14f / fieldWidth) * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1)),
                         0);
        }
        else return Vector3.zero;
    }
}