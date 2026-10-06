namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Uniform-cell grid (original contract): every child gets <see cref="cellSize"/>;
    /// cell counts come from the constraint (or, when Flexible, from how many cells fit
    /// the group's rect); fill order runs along <see cref="startAxis"/> from
    /// <see cref="startCorner"/>, and the whole block aligns via childAlignment.
    /// </summary>
    public class GridLayoutGroup : LayoutGroup
    {
        public enum Corner { UpperLeft = 0, UpperRight = 1, LowerLeft = 2, LowerRight = 3 }
        public enum Axis { Horizontal = 0, Vertical = 1 }
        public enum Constraint { Flexible = 0, FixedColumnCount = 1, FixedRowCount = 2 }

        [SerializeField] protected Corner m_StartCorner = Corner.UpperLeft;
        [SerializeField] protected Axis m_StartAxis = Axis.Horizontal;
        [SerializeField] protected Vector2 m_CellSize = new(100f, 100f);
        [SerializeField] protected Vector2 m_Spacing = Vector2.zero;
        [SerializeField] protected Constraint m_Constraint = Constraint.Flexible;
        [SerializeField] protected int m_ConstraintCount = 2;

        public Corner startCorner { get => m_StartCorner; set => SetProperty(ref m_StartCorner, value); }
        public Axis startAxis { get => m_StartAxis; set => SetProperty(ref m_StartAxis, value); }
        public Vector2 cellSize { get => m_CellSize; set => SetProperty(ref m_CellSize, value); }
        public Vector2 spacing { get => m_Spacing; set => SetProperty(ref m_Spacing, value); }
        public Constraint constraint { get => m_Constraint; set => SetProperty(ref m_Constraint, value); }
        public int constraintCount { get => m_ConstraintCount; set => SetProperty(ref m_ConstraintCount, Mathf.Max(1, value)); }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            int minColumns, preferredColumns;
            if (m_Constraint == Constraint.FixedColumnCount)
            {
                minColumns = preferredColumns = m_ConstraintCount;
            }
            else if (m_Constraint == Constraint.FixedRowCount)
            {
                minColumns = preferredColumns =
                    Mathf.CeilToInt(rectChildren.Count / (float)m_ConstraintCount - 0.001f);
            }
            else
            {
                minColumns = 1;
                preferredColumns = Mathf.CeilToInt(Mathf.Sqrt(rectChildren.Count));
            }

            SetLayoutInputForAxis(
                padding.horizontal + (cellSize.x + spacing.x) * minColumns - spacing.x,
                padding.horizontal + (cellSize.x + spacing.x) * preferredColumns - spacing.x,
                -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            int minRows;
            if (m_Constraint == Constraint.FixedColumnCount)
            {
                minRows = Mathf.CeilToInt(rectChildren.Count / (float)m_ConstraintCount - 0.001f);
            }
            else if (m_Constraint == Constraint.FixedRowCount)
            {
                minRows = m_ConstraintCount;
            }
            else
            {
                float width = rectTransform.rect.width;
                int cellCountX = Mathf.Max(1,
                    Mathf.FloorToInt((width - padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
                minRows = Mathf.CeilToInt(rectChildren.Count / (float)cellCountX);
            }

            float minSpace = padding.vertical + (cellSize.y + spacing.y) * minRows - spacing.y;
            SetLayoutInputForAxis(minSpace, minSpace, -1f, 1);
        }

        public override void SetLayoutHorizontal() => SetCellsAlongAxis(0);
        public override void SetLayoutVertical() => SetCellsAlongAxis(1);

        void SetCellsAlongAxis(int axis)
        {
            // Axis 0 only pins sizes/anchors; positions land in the axis-1 pass, once
            // BOTH the width and height needed for corner mirroring are known.
            if (axis == 0)
            {
                foreach (var rect in rectChildren)
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.sizeDelta = cellSize;
                }
                return;
            }

            int count = rectChildren.Count;
            if (count == 0) return;

            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;

            int cellCountX = 1;
            int cellCountY = 1;
            if (m_Constraint == Constraint.FixedColumnCount)
            {
                cellCountX = m_ConstraintCount;
                cellCountY = Mathf.CeilToInt(count / (float)cellCountX - 0.001f);
            }
            else if (m_Constraint == Constraint.FixedRowCount)
            {
                cellCountY = m_ConstraintCount;
                cellCountX = Mathf.CeilToInt(count / (float)cellCountY - 0.001f);
            }
            else
            {
                cellCountX = cellSize.x + spacing.x <= 0f
                    ? int.MaxValue
                    : Mathf.Max(1, Mathf.FloorToInt((width - padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
                cellCountY = cellSize.y + spacing.y <= 0f
                    ? int.MaxValue
                    : Mathf.Max(1, Mathf.FloorToInt((height - padding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y)));
            }

            int cornerX = (int)startCorner % 2;
            int cornerY = (int)startCorner / 2;

            int cellsPerMainAxis, actualCellCountX, actualCellCountY;
            if (startAxis == Axis.Horizontal)
            {
                cellsPerMainAxis = cellCountX;
                actualCellCountX = Mathf.Clamp(cellCountX, 1, count);
                actualCellCountY = Mathf.Clamp(cellCountY, 1, Mathf.CeilToInt(count / (float)cellsPerMainAxis));
            }
            else
            {
                cellsPerMainAxis = cellCountY;
                actualCellCountY = Mathf.Clamp(cellCountY, 1, count);
                actualCellCountX = Mathf.Clamp(cellCountX, 1, Mathf.CeilToInt(count / (float)cellsPerMainAxis));
            }

            var requiredSpace = new Vector2(
                actualCellCountX * cellSize.x + (actualCellCountX - 1) * spacing.x,
                actualCellCountY * cellSize.y + (actualCellCountY - 1) * spacing.y);
            var startOffset = new Vector2(
                GetStartOffset(0, requiredSpace.x),
                GetStartOffset(1, requiredSpace.y));

            for (int i = 0; i < count; i++)
            {
                int positionX, positionY;
                if (startAxis == Axis.Horizontal)
                {
                    positionX = i % cellsPerMainAxis;
                    positionY = i / cellsPerMainAxis;
                }
                else
                {
                    positionX = i / cellsPerMainAxis;
                    positionY = i % cellsPerMainAxis;
                }

                if (cornerX == 1) positionX = actualCellCountX - 1 - positionX;
                if (cornerY == 1) positionY = actualCellCountY - 1 - positionY;

                SetChildAlongAxis(rectChildren[i], 0, startOffset.x + (cellSize.x + spacing.x) * positionX, cellSize.x);
                SetChildAlongAxis(rectChildren[i], 1, startOffset.y + (cellSize.y + spacing.y) * positionY, cellSize.y);
            }
        }
    }
}
