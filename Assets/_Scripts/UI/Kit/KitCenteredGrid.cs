using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A fixed-size grid whose rows are each CENTRED, so a short last row sits in the middle under
    /// the full ones (the Toy Box's four over three, Rev 145 H, Docs/MENU_REDESIGN_B.md section
    /// 235.3). Unity's GridLayoutGroup aligns the whole block and leaves a short row hanging to one
    /// side. Children are laid out in sibling order, row by row, top to bottom.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Centered Grid")]
    public class KitCenteredGrid : LayoutGroup
    {
        [Tooltip("Every child's size.")]
        [SerializeField] Vector2 cellSize = new(252, 302);

        [Tooltip("The gap between cells, x across and y down.")]
        [SerializeField] Vector2 spacing = new(34, 18);

        [Tooltip("How many cells fit a full row.")]
        [Min(1)]
        [SerializeField] int columns = 4;

        public Vector2 CellSize => cellSize;
        public int Columns => columns;

        int Rows => rectChildren.Count == 0 ? 0 : (rectChildren.Count + columns - 1) / columns;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            int across = Mathf.Min(columns, Mathf.Max(1, rectChildren.Count));
            float w = padding.horizontal + across * cellSize.x + (across - 1) * spacing.x;
            SetLayoutInputForAxis(w, w, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            int rows = Rows;
            float h = padding.vertical + rows * cellSize.y + Mathf.Max(0, rows - 1) * spacing.y;
            SetLayoutInputForAxis(h, h, -1, 1);
        }

        public override void SetLayoutHorizontal() => Place();

        public override void SetLayoutVertical() => Place();

        void Place()
        {
            float width = rectTransform.rect.width;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                int row = i / columns;
                int inRow = Mathf.Min(columns, rectChildren.Count - row * columns);
                int col = i % columns;
                float rowWidth = inRow * cellSize.x + (inRow - 1) * spacing.x;
                float x = padding.left + (width - padding.horizontal - rowWidth) * 0.5f + col * (cellSize.x + spacing.x);
                float y = padding.top + row * (cellSize.y + spacing.y);
                SetChildAlongAxis(rectChildren[i], 0, x, cellSize.x);
                SetChildAlongAxis(rectChildren[i], 1, y, cellSize.y);
            }
        }
    }
}
