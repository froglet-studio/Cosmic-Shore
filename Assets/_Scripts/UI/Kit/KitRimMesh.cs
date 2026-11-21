using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Feeds the elemental rim shader (<c>CosmicShore/UI/ElementRim</c>, Docs/MENU_REDESIGN_B.md §45.3) where each
    /// vertex sits on its rect: UV1 = (x, y) normalised over the rect, (z, w) = the rect's size. A sliced border's own
    /// UVs are sprite coordinates, so the shader cannot place the comet without this. Runs only when the mesh is
    /// rebuilt; the rim itself animates in the shader, so it costs nothing per frame.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Rim Mesh")]
    public class KitRimMesh : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            // UV1 only reaches the shader when the root canvas outputs that channel (Menu_Main's already does).
            var canvas = graphic.canvas ? graphic.canvas.rootCanvas : null;
            if (canvas && (canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;

            Rect rect = graphic.rectTransform.rect;
            float w = Mathf.Max(rect.width, 1f), h = Mathf.Max(rect.height, 1f);
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4((vertex.position.x - rect.xMin) / w, (vertex.position.y - rect.yMin) / h, w, h);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
