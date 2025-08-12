using UnityEngine;

namespace Obvious.Soap
{
    /// <summary>
    /// Binds a color variable to a renderer
    /// </summary>
    [AddComponentMenu("Soap/Bindings/BindRendererColor")]
    [RequireComponent(typeof(Renderer))]
    public class BindRendererColor : CacheComponent<Renderer>
    {
        [SerializeField] private ColorVariable _colorVariable = null;

        protected override void Awake()
        {
            base.Awake();
            Refresh(_colorVariable);
            _colorVariable.OnValueChanged += Refresh;
        }

        private void OnDestroy()
        {
            _colorVariable.OnValueChanged -= Refresh;
        }

        private void Refresh(Color color)
        {
            _component.material.color = color;
        }
    }
}