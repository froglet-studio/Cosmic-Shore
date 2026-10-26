using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [System.Serializable] public class FlowFieldData : MonoBehaviour
    {
        [SerializeField] FlowFieldSO field;

        [SerializeField] int fieldWidth;
        public int FieldWidth => fieldWidth;
        [SerializeField] int fieldHeight;
        public int FieldHeight => fieldHeight;
        [SerializeField] int fieldThickness;
        public int FieldThickness => fieldThickness;
        [SerializeField] float fieldMax;
        public float FieldMax => fieldMax;

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
    }
}
