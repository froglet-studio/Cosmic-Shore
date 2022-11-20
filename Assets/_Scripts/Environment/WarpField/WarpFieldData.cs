using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class WarpFieldData : MonoBehaviour
{
    [SerializeField] WarpFieldSO field;

    public int fieldWidth;
    public int fieldHeight;
    public int fieldThickness;
    public float fieldMax;

    private bool initialized = false;

    public void Init(bool forceReinitialization = false)
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

    public Vector3 HybridVector(Transform node)
    {
        return field.HybridVector(node);
    }
}