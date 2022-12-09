using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "TardisWarpData", menuName = "ScriptableObjects/TardisWarp", order = 2)]
[System.Serializable] public class TardisWarp : WarpFieldSO
{

    public TardisWarp()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 200;
        fieldMax = 500f;
    }

    float minRadius = 10;

    override public Vector3 HybridVector(Transform node)
    {
        return new Vector3(sigmoid(node.position.x),
                           sigmoid(node.position.y),
                           sigmoid(node.position.z));
    }

    float Gaussian(float input, float sigma, float distance)
    {
        float twoSigmaSquared = 2 * Mathf.Pow(sigma, 2);
        return Mathf.Exp(-Mathf.Pow(input - distance, 2) / twoSigmaSquared);
    }

    float sigmoid(float position)
    {
        if (position > 0) return 3 * Mathf.Atan(position - minRadius) / fieldMax;
        else return 3 * Mathf.Atan(position + minRadius) / fieldMax;
    }
}
