using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GaussianFlowData", menuName = "ScriptableObjects/GaussianFlow", order = 2)]
[System.Serializable] public class GaussianFlow : FlowFieldSO
{

    public GaussianFlow()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 700;
        fieldMax = .7f;
        
    }

    public float sigma = 100;
    


    override public Vector3 FlowVector(Transform node)
    {
        //if ((Mathf.Pow(node.position.x, 2) / Mathf.Pow(fieldWidth, 2)) + (Mathf.Pow(node.position.y, 2) / Mathf.Pow(fieldHeight, 2)) < 1)
        //{
        float twoSigmaSquared = 2 * Mathf.Pow(sigma, 2);
        float twoSigma = 2 * sigma;
        return new Vector3(
                         (-Mathf.Exp(-Mathf.Pow(node.position.y - (fieldWidth - twoSigma), 2) / twoSigmaSquared) + Mathf.Exp(-Mathf.Pow(node.position.y + (fieldWidth - twoSigma), 2)/ twoSigmaSquared))
                                * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1)),
                         (Mathf.Exp(-Mathf.Pow(node.position.x - (fieldHeight - twoSigma), 2) / twoSigmaSquared) - Mathf.Exp(-Mathf.Pow(node.position.x + (fieldHeight - twoSigma), 2) / twoSigmaSquared))
                                * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1)),
                         0);
        //}
        //else return Vector3.zero;
    }
}

// v.x = 5.*exp(-pow(p.y/2.-10.,2.)/10.)-5.*exp(-pow(p.y/2.+10.,2.)/10.);
//v.y = -5.* exp(-pow(p.x - 10., 2.) / 10.) + 5.* exp(-pow(p.x + 10., 2.) / 10.);
