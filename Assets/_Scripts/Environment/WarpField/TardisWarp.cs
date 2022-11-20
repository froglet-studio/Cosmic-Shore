using System.Collections.Generic;
using UnityEngine;

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

    override public Vector3 HybridVector(Transform node)
    {
        return new Vector3(3*Mathf.Atan(node.position.x / fieldMax),
                           3*Mathf.Atan(node.position.y/ fieldMax),
                           3*Mathf.Atan(node.position.z/ fieldMax));
    }

    float Gaussian(float input, float sigma, float distance)
    {
        float twoSigmaSquared = 2 * Mathf.Pow(sigma, 2);
        return Mathf.Exp(-Mathf.Pow(input - distance, 2) / twoSigmaSquared);
    }
}
