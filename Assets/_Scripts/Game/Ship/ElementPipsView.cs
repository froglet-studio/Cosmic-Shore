using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;

namespace CosmicShore
{
    /// <summary>
    /// Renders 4 vertical columns of pips above the silhouette, one per element.
    /// Each column has N pips (default 20) representing levels -5 through +15.
    /// Subscribes to ResourceSystem.OnElementLevelChange to stay in sync.
    /// </summary>
    public class ElementPipsView : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ElementPipsConfigSO config;

        [Header("Container")]
        [Tooltip("RectTransform that holds all pip columns. Should be anchored above the silhouette.")]
        [SerializeField] private RectTransform container;

        // Element column order (left to right)
        static readonly Element[] ColumnOrder = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Runtime state
        private Image[,] _pips;        // [columnIndex, pipIndex] bottom-to-top
        private Image[] _labels;        // one label per column
        private RectTransform[] _zeroLines;
        private int[] _currentLevels;   // integer level per column (-5 to 15)
        private bool _built;

        const int MinLevel = -5;
        const int MaxLevel = 15;

        /// <summary>
        /// Build the pip UI. Call once after the config is assigned.
        /// </summary>
        public void Build()
        {
            if (_built || !config || !container) return;

            int cols = ColumnOrder.Length;
            int rows = config.pipsPerColumn;

            _pips = new Image[cols, rows];
            _labels = new Image[cols];
            _zeroLines = new RectTransform[cols];
            _currentLevels = new int[cols];

            float totalWidth = (cols - 1) * config.columnSpacing;
            float startX = -totalWidth * 0.5f;

            for (int c = 0; c < cols; c++)
            {
                var element = ColumnOrder[c];
                float xPos = startX + c * config.columnSpacing;

                // Column parent
                var colGO = new GameObject($"ElementCol_{element}", typeof(RectTransform));
                var colRT = (RectTransform)colGO.transform;
                colRT.SetParent(container, false);
                colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0f);
                colRT.pivot = new Vector2(0.5f, 0f);
                colRT.anchoredPosition = new Vector2(xPos, 0f);
                colRT.sizeDelta = new Vector2(config.pipSize.x, 0f);

                // Label icon at the bottom of the column
                var labelGO = new GameObject($"Label_{element}", typeof(RectTransform), typeof(Image));
                var labelRT = (RectTransform)labelGO.transform;
                labelRT.SetParent(colRT, false);
                labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0f);
                labelRT.pivot = new Vector2(0.5f, 0f);
                labelRT.anchoredPosition = Vector2.zero;
                labelRT.sizeDelta = config.labelIconSize;

                var labelImg = labelGO.GetComponent<Image>();
                labelImg.sprite = config.GetLabelSprite(element);
                labelImg.color = config.filledColor;
                labelImg.preserveAspect = true;
                _labels[c] = labelImg;

                float pipBaseY = config.labelIconSize.y + config.labelGap;

                // Pips (bottom to top)
                Sprite pipSprite = config.GetPipSprite(element);
                for (int p = 0; p < rows; p++)
                {
                    var pipGO = new GameObject($"Pip_{element}_{p}", typeof(RectTransform), typeof(Image));
                    var pipRT = (RectTransform)pipGO.transform;
                    pipRT.SetParent(colRT, false);
                    pipRT.anchorMin = pipRT.anchorMax = new Vector2(0.5f, 0f);
                    pipRT.pivot = new Vector2(0.5f, 0.5f);
                    pipRT.anchoredPosition = new Vector2(0f, pipBaseY + p * config.pipSpacing + config.pipSize.y * 0.5f);
                    pipRT.sizeDelta = config.pipSize;

                    var img = pipGO.GetComponent<Image>();
                    img.sprite = pipSprite;
                    img.color = config.emptyColor;
                    img.preserveAspect = true;
                    _pips[c, p] = img;
                }

                // Zero-line marker
                var zeroGO = new GameObject($"ZeroLine_{element}", typeof(RectTransform), typeof(Image));
                var zeroRT = (RectTransform)zeroGO.transform;
                zeroRT.SetParent(colRT, false);
                zeroRT.anchorMin = zeroRT.anchorMax = new Vector2(0.5f, 0f);
                zeroRT.pivot = new Vector2(0.5f, 0.5f);

                // Position the zero line between pip[zeroLineIndex-1] and pip[zeroLineIndex]
                float zeroY = pipBaseY + config.zeroLineIndex * config.pipSpacing;
                zeroRT.anchoredPosition = new Vector2(0f, zeroY);
                zeroRT.sizeDelta = new Vector2(config.pipSize.x + 4f, config.zeroLineHeight);

                var zeroImg = zeroGO.GetComponent<Image>();
                zeroImg.color = config.zeroLineColor;
                _zeroLines[c] = zeroRT;

                // Initial level = 0
                _currentLevels[c] = 0;
            }

            _built = true;
            RefreshAllColumns();
        }

        /// <summary>
        /// Called by controller when an element level changes.
        /// </summary>
        public void SetLevel(Element element, int level)
        {
            int col = GetColumnIndex(element);
            if (col < 0 || !_built) return;

            _currentLevels[col] = Mathf.Clamp(level, MinLevel, MaxLevel);
            RefreshColumn(col);
        }

        /// <summary>
        /// Refresh all 4 columns (e.g. on init).
        /// </summary>
        public void RefreshAllColumns()
        {
            if (!_built) return;
            for (int c = 0; c < ColumnOrder.Length; c++)
                RefreshColumn(c);
        }

        void RefreshColumn(int col)
        {
            int level = _currentLevels[col];
            int rows = config.pipsPerColumn;

            // Each pip index 0..19 maps to level (MinLevel + 1 + index)
            // pip 0 → level -4, pip 4 → level 0, pip 19 → level 15
            // A pip is filled when the current level >= its represented level
            for (int p = 0; p < rows; p++)
            {
                int pipLevel = MinLevel + 1 + p; // -4, -3, ..., 0, ..., 15
                bool filled = level >= pipLevel;
                bool isNegative = pipLevel < 0;

                var img = _pips[col, p];
                if (!img) continue;

                if (filled)
                    img.color = isNegative ? config.negativeFillColor : config.filledColor;
                else
                    img.color = config.emptyColor;
            }
        }

        static int GetColumnIndex(Element element) => element switch
        {
            Element.Charge => 0,
            Element.Mass   => 1,
            Element.Space  => 2,
            Element.Time   => 3,
            _              => -1
        };
    }
}
