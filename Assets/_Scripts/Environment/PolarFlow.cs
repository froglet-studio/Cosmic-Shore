using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PolarFlowData", menuName = "ScriptableObjects/PolarFlow", order = 2)]
[System.Serializable] public class PolarFlow : FlowFieldSO
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
        //if ((Mathf.Pow(node.position.x, 2) / Mathf.Pow(fieldWidth, 2)) + (Mathf.Pow(node.position.y, 2) / Mathf.Pow(fieldHeight, 2)) < 1)
        //{
        float twoSigmaSquared = 2 * Mathf.Pow(fieldThickness, 2);
        float twoSigma = 2 * fieldThickness;

        float AspectRatio = (float)fieldHeight / (float)fieldWidth;

        float Pr = Mathf.Sqrt(Mathf.Pow(node.position.x, 2)+ Mathf.Pow(node.position.y / AspectRatio, 2));
        float Ptheta = Mathf.Atan2(node.position.y / Mathf.Pow(AspectRatio, 2), node.position.x);

        float newTheta = Ptheta;
        //if (((Ptheta+Mathf.PI) % (Mathf.PI * 2)) > (Mathf.PI/2) && ((Ptheta + Mathf.PI) % (Mathf.PI * 2)) < (Mathf.PI))//|| ((Ptheta + Mathf.PI) % (Mathf.PI * 2)) > (Mathf.PI))
        //{
        //    newTheta = Ptheta * (1 - .6f * Mathf.Sin(Ptheta * 2)); //empirical correction not derived
        //}
        //else
        //{
        //    newTheta = Ptheta * (1 + .6f * Mathf.Sin(Ptheta * 2));
        //}

        float Vr = Mathf.Exp(-Mathf.Pow(Pr - (fieldWidth - twoSigma), 2) / twoSigmaSquared);
        float Vtheta = newTheta + Mathf.PI/2 ;



        return new Vector3(Vr*Mathf.Cos(Vtheta)
                                * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1)),
                           Vr*Mathf.Sin(Vtheta)
                                * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1)),
                           0);
        //}
        //else return Vector3.zero;
    }
}

// v.x = 5.*exp(-pow(p.y/2.-10.,2.)/10.)-5.*exp(-pow(p.y/2.+10.,2.)/10.);
//v.y = -5.* exp(-pow(p.x - 10., 2.) / 10.) + 5.* exp(-pow(p.x + 10., 2.) / 10.);
