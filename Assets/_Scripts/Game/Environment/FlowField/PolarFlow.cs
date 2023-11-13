using UnityEngine;

[CreateAssetMenu(fileName = "PolarFlowData", menuName = "CosmicShore/Flow/PolarFlow", order = 30)]
[System.Serializable] 
public class PolarFlow : FlowFieldSO
{
    public PolarFlow()
    {
        fieldThickness = 200;
        fieldWidth = 200;
        fieldHeight = 700;
        fieldMax = .7f;
    }

    override public Vector3 FlowVector(Transform node)
    {
        float AspectRatio = (float)fieldHeight / (float)fieldWidth;

        float Pr = Mathf.Sqrt(Mathf.Pow(node.position.x, 2)+ Mathf.Pow(node.position.y / AspectRatio, 2)); //divide y by aspect to fit curve in bounding box
        float Ptheta = Mathf.Atan2(node.position.y / Mathf.Pow(AspectRatio, 2), node.position.x); //divide y by aspect twice to get the flow pointed with the tangent.

        float Vr = Gaussian(Pr, fieldThickness, (fieldWidth - 3 * fieldThickness));
        float Vtheta = Ptheta + Mathf.PI/2 ;

        float Zdecay = Gaussian(node.position.z, fieldThickness/4,0);

        return new Vector3(
                        Vr*Mathf.Cos(Vtheta) * fieldMax * Zdecay,
                        Vr*Mathf.Sin(Vtheta) * fieldMax * Zdecay,
                        0);
    }

    float Gaussian(float input, float sigma, float distance)
    {
        float twoSigmaSquared = 2 * Mathf.Pow(sigma, 2);
        return Mathf.Exp(-Mathf.Pow(input - distance, 2) / twoSigmaSquared);
    }
}