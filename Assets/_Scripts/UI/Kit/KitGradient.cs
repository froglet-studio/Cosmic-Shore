using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The opaque Play button's left-to-right Jade gradient (Docs/STYLE_FOUNDATION.md §1.4: the
    /// PLAY button gradients because it is the primary action; nothing else does).
    ///
    /// Reads the graphic's OWN colour as the left end and lightens it toward white for the right
    /// end, so it follows whatever <see cref="KitButton"/> tints the fill to (hover, press) and
    /// needs no second token: controlActive <c>00D4FF</c> halfway to white is <c>80EAFF</c>, the
    /// approved mockup's right end. Writes vertex colours over a sliced Image's quads, so the slivered
    /// corners are untouched. <see cref="KitButton"/> enables it only for an opaque Play button.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Gradient")]
    public class KitGradient : BaseMeshEffect
    {
        /// <summary>How far toward white the right end goes; 0.5 turns 00D4FF into 80EAFF.</summary>
        public const float DefaultLighten = 0.5f;

        [Tooltip("How far toward white the RIGHT end of the gradient is, 0..1. 0.5 makes controlActive 00D4FF into 80EAFF.")]
        [Range(0f, 1f)]
        [SerializeField] private float lightenRight = DefaultLighten;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Rect rect = graphic.rectTransform.rect;
            Color left = graphic.color;
            Color right = Color.Lerp(left, Color.white, lightenRight);
            right.a = left.a;

            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = rect.width > 0f ? Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x) : 0f;
                vertex.color = Color.Lerp(left, right, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
