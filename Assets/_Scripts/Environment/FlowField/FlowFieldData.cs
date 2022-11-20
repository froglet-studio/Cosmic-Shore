using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class FlowFieldData : MonoBehaviour 
{
    [SerializeField] FlowFieldSO field;

    public int fieldWidth;
    public int fieldHeight;
    public int fieldThickness;
    public float fieldMax;

    private bool initialized = false;

    public void Init(bool forceReinitialization=false)
    {
        if (initialized && !forceReinitialization)
            return;

        initialized = true;
        fieldThickness = field.fieldThickness;
        fieldWidth = field.fieldWidth;
        fieldHeight = field.fieldHeight;
        fieldMax = field.fieldMax;
    }

    public void UpdateScriptableObjectValues()
    {
#if UNITY_EDITOR
        field.fieldThickness = fieldThickness;
        field.fieldWidth = fieldWidth;
        field.fieldHeight = fieldHeight;
        field.fieldMax = fieldMax;
#endif
    }

    public Vector3 FlowVector(Transform node)
    {
        return field.FlowVector(node);
    }

    //public Vector3 HybridVector(Transform node)
    //{
    //    if ((Mathf.Pow(node.position.x, 2) / Mathf.Pow(fieldWidth, 2)) + (Mathf.Pow(node.position.y, 2) / Mathf.Pow(fieldHeight, 2)) < 1)
    //    {
    //        return new Vector3(
    //                     -(Mathf.Sin(node.position.y * 3.14f / fieldHeight) * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1))),
    //                     Mathf.Sin(node.position.x * 3.14f / fieldWidth) * fieldMax * (1 - Mathf.Clamp(Mathf.Abs(node.position.z / fieldThickness), 0, 1)),
    //                     0);
    //    }
    //    else return Vector3.zero;
    //}
}
