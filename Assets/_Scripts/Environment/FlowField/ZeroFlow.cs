using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ZeroFlowData", menuName = "ScriptableObjects/ZeroFlow", order = 2)]
[System.Serializable] public class ZeroFlow : FlowFieldSO
{

    public ZeroFlow()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 700;
        fieldMax = .7f;
    }

    override public Vector3 FlowVector(Transform node)
    {
        return Vector3.zero;
    }
}

// v.x = 5.*exp(-pow(p.y/2.-10.,2.)/10.)-5.*exp(-pow(p.y/2.+10.,2.)/10.);
//v.y = -5.* exp(-pow(p.x - 10., 2.) / 10.) + 5.* exp(-pow(p.x + 10., 2.) / 10.);
