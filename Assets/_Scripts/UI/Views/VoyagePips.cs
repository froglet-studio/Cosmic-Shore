using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A voyage's difficulty as a row of pips (Frogma Rev 100 and Rev 111, Docs/MENU_REDESIGN_B.md sections 201-203): nine
    /// small squares, the first <see cref="Lit"/> in <see cref="Graphic.color"/>, the rest in <see cref="Off"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyagePips : MaskableGraphic
    {
        public int Count = 9;
        public Color Off = new(0.263f, 0.298f, 0.537f, 1f);
        int _lit;

        /// <summary>How many pips are lit.</summary>
        public int Lit
        {
            get => _lit;
            set
            {
                if (_lit == value) return;
                _lit = value;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (Count <= 0 || r.width < 1f) return;
            float step = r.width / Count, gap = Mathf.Min(2f, step * 0.2f);
            for (int i = 0; i < Count; i++)
            {
                var c = i < _lit ? color : ColorTrace.Retrace(ColorTrace.Ui, Off);
                float x0 = r.xMin + i * step, x1 = x0 + step - gap;
                int v = vh.currentVertCount;
                vh.AddVert(new Vector2(x0, r.yMin), c, Vector2.zero);
                vh.AddVert(new Vector2(x0, r.yMax), c, Vector2.zero);
                vh.AddVert(new Vector2(x1, r.yMax), c, Vector2.zero);
                vh.AddVert(new Vector2(x1, r.yMin), c, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}
