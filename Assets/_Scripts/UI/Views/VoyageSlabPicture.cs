using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A still picture as the Sea table's top (<see cref="VoyageSlab"/>, Rev 90, Docs/MENU_REDESIGN_B.md section 172): it
    /// fills its rect (cropped to the rect's shape, about its middle, <see cref="Zoom"/> times closer), lies back in
    /// perspective and fades at its far edge. The Saga, Quests and Episodes tables are one of these (the Voyages tab's
    /// map is flat since Rev 98, Docs/MENU_REDESIGN_B.md section 184).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageSlabPicture : MaskableGraphic
    {
        Texture _texture;
        Rect _uv = new(0f, 0f, 1f, 1f);
        float _zoom = 1f;

        public override Texture mainTexture => _texture ? _texture : s_WhiteTexture;

        /// <summary>How much closer than a plain fill the picture is seen (1: the whole picture's fitting part).</summary>
        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Mathf.Max(1f, value);
                SetVerticesDirty();
            }
        }

        /// <summary>Show <paramref name="sprite"/>; nothing is drawn for none.</summary>
        public void Show(Sprite sprite)
        {
            if (!sprite || !sprite.texture)
            {
                Show(null, default);
                return;
            }
            var tex = sprite.texture;
            var r = sprite.textureRect;
            Show(tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height),
                r.width / Mathf.Max(1f, r.height));
        }

        /// <summary>Show the part <paramref name="uv"/> of <paramref name="texture"/>, which is
        /// <paramref name="aspect"/> wide for its height (0: the texture's own shape under that part).</summary>
        public void Show(Texture texture, Rect uv, float aspect = 0f)
        {
            _texture = texture;
            _uv = uv;
            _aspect = aspect > 0f ? aspect
                : texture ? uv.width * texture.width / Mathf.Max(1f, uv.height * texture.height) : 1f;
            raycastTarget = false;
            SetMaterialDirty();
            SetVerticesDirty();
        }
        float _aspect = 1f;

        /// <summary>The part of the picture a rect <paramref name="rectAspect"/> wide for its height shows: the picture
        /// cropped to that shape about its middle, <paramref name="zoom"/> times closer.</summary>
        public static Rect Crop(Rect uv, float pictureAspect, float rectAspect, float zoom)
        {
            float w = 1f, h = 1f;
            if (pictureAspect > rectAspect) w = rectAspect / pictureAspect;
            else h = pictureAspect / rectAspect;
            w /= Mathf.Max(1f, zoom);
            h /= Mathf.Max(1f, zoom);
            return new Rect(uv.x + uv.width * (1f - w) * 0.5f, uv.y + uv.height * (1f - h) * 0.5f, uv.width * w, uv.height * h);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_texture) return;
            var r = rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return;
            var part = Crop(_uv, _aspect, r.width / r.height, _zoom);
            VoyageSlab.Grid(vh, r, color, part.min, part.max);
            VoyageSlab.Apply(vh, r);
        }
    }
}
