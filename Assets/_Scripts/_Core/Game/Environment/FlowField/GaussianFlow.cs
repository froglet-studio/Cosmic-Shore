using UnityEngine;

[CreateAssetMenu(fileName = "GaussianFlowData", menuName = "CosmicShore/Flow/GaussianFlow", order = 30)]
[System.Serializable]
public class GaussianFlow : FlowFieldSO
{
    public float sigma = 100;

    public GaussianFlow()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 700;
        fieldMax = .7f;
    }

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