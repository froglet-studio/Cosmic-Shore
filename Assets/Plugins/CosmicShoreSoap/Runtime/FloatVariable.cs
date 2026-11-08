using UnityEngine;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(fileName = "Variable_Float", menuName = "ScriptableObjects/Variables/Float")]
    public class FloatVariable : ScriptableVariable<float>
    {
        [Tooltip("Keep the value between Min Max (x = min, y = max).")]
        [SerializeField] bool _isClamped;
        [SerializeField] Vector2 _minMax = new(float.MinValue, float.MaxValue);

        public override float Value
        {
            get => base.Value;
            set => base.Value = _isClamped ? Mathf.Clamp(value, _minMax.x, _minMax.y) : value;
        }
    }
}
