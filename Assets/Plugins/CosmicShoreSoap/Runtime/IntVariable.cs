using UnityEngine;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(fileName = "Variable_Int", menuName = "ScriptableObjects/Variables/Int")]
    public class IntVariable : ScriptableVariable<int>
    {
        [Tooltip("Keep the value between Min Max (x = min, y = max).")]
        [SerializeField] bool _isClamped;
        [SerializeField] Vector2Int _minMax = new(int.MinValue, int.MaxValue);

        public override int Value
        {
            get => base.Value;
            set => base.Value = _isClamped ? Mathf.Clamp(value, _minMax.x, _minMax.y) : value;
        }
    }
}
