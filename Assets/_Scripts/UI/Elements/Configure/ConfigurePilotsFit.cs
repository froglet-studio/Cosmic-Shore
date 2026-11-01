using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Keeps the configure window's settings column fitted to its PILOTS (Docs/MENU_REDESIGN_B.md §43).
    /// A domain column grows by one seat row per pilot, but the column was authored for one seat and the
    /// open seat: the second pilot in a domain ran into the "AI pilots" row and the third sat behind its
    /// Easy / Medium / Hard buttons.
    ///
    /// <para>So the tallest column decides the layout. The Pilots section grows to hold it, the AI row
    /// drops under it, and everything authored below the columns (the next section, the launch row)
    /// moves down by the same amount, up to <see cref="maxGrowth"/> - the room the generator measured
    /// above the screen's bottom edge. Past that the seat rows and the open seat tighten (smaller gaps, then
    /// shorter rows, never under <see cref="minSeatHeight"/>), which only a crowded Arena column needs.</para>
    ///
    /// <para>Positions are written as absolute offsets from the authored ones, read through
    /// <see cref="KitEntrance.RestPosition"/>, so an entrance playing when this wakes or writes is not
    /// mistaken for the layout. Authored by Tools/Build/author_menu_configure.py; it writes only on a
    /// change.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ConfigurePilotsFit : MonoBehaviour
    {
        [Header("Pilots")]
        [SerializeField, Tooltip("The Pilots section. It grows downward to hold its tallest domain column.")]
        RectTransform pilotsSection;

        [SerializeField, Tooltip("The domain columns, each a domain tile over the seat strip the modal parents chips under.")]
        DomainInfoData[] columns = System.Array.Empty<DomainInfoData>();

        [SerializeField, Tooltip("The AI difficulty row under the columns. Optional (the Maelstrom has none). " +
                                 "While it is shown the columns stop above it; while hidden they may use its space.")]
        CanvasGroup aiRow;

        [SerializeField, Min(0f), Tooltip("Canvas pixels between the tallest column and the AI row.")]
        float rowGap = 8f;

        [Header("Below the pilots")]
        [SerializeField, Tooltip("Everything authored under the columns that moves down with them: the next " +
                                 "section of this column and the launch row (status, summary chips, Start).")]
        RectTransform[] below = System.Array.Empty<RectTransform>();

        [SerializeField, Min(0f), Tooltip("How far the column may grow before the launch row reaches the " +
                                          "screen's bottom edge. The generator measures it per window.")]
        float maxGrowth;

        [Header("Crowded columns")]
        [SerializeField, Min(1f), Tooltip("A seat row's authored height (ConfigureSeatChip).")]
        float seatHeight = 40f;

        [SerializeField, Min(1f), Tooltip("The shortest a seat row gets when the column still does not fit. " +
                                          "Never under its kick button.")]
        float minSeatHeight = 32f;

        [SerializeField, Min(0f), Tooltip("The gap between rows while the column is tightened.")]
        float compactGap = 2f;

        RectTransform _steps;
        float _stepsHeight, _pilotsHeight;
        Vector2 _rowRest;
        Vector2[] _belowRest = System.Array.Empty<Vector2>();
        float _stripGap, _columnGap;
        bool _captured;

        // What is applied now.
        float _growth;
        float _seat;
        bool _compact;

        void Awake() => Capture();

        void Capture()
        {
            if (_captured) return;
            _captured = true;
            _steps = transform as RectTransform;
            _stepsHeight = _steps ? _steps.sizeDelta.y : 0f;
            _pilotsHeight = pilotsSection ? pilotsSection.sizeDelta.y : 0f;
            _rowRest = aiRow ? RestOf(aiRow.transform as RectTransform) : default;
            _belowRest = new Vector2[below.Length];
            for (int i = 0; i < below.Length; i++) _belowRest[i] = RestOf(below[i]);

            _stripGap = _columnGap = 0f;
            foreach (var column in columns)
            {
                if (!column) continue;
                if (column.TryGetComponent(out HorizontalOrVerticalLayoutGroup own)) _columnGap = own.spacing;
                var strip = column.AvatarStripTransform;
                if (strip && strip.TryGetComponent(out HorizontalOrVerticalLayoutGroup seats)) _stripGap = seats.spacing;
                break;
            }
            _seat = seatHeight;
        }

        static Vector2 RestOf(RectTransform rect)
        {
            if (!rect) return default;
            return rect.TryGetComponent(out KitEntrance entrance) ? entrance.RestPosition : rect.anchoredPosition;
        }

        void LateUpdate()
        {
            Capture();

            // The tallest column, as it would stand with full-size rows (the measured height is taken at the
            // rows applied now, so the decision never feeds back on itself).
            float natural = 0f, top = 0f;
            int rows = 0, stripGaps = 0, columnGaps = 0;
            for (int i = 0; i < columns.Length; i++)
            {
                var column = columns[i];
                if (!column || !column.gameObject.activeInHierarchy) continue;
                var rect = (RectTransform)column.transform;
                int seats = CountSeats(column.AvatarStripTransform);
                int n = seats + (InLayout(OpenSeatOf(column)) ? 1 : 0);
                int s = Mathf.Max(0, seats - 1);
                int k = Mathf.Max(0, CountLayoutChildren(rect) - 1);
                float full = rect.rect.height
                             + n * (seatHeight - _seat)
                             + s * (_stripGap - (_compact ? compactGap : _stripGap))
                             + k * (_columnGap - (_compact ? compactGap : _columnGap));
                if (full <= natural) continue;
                natural = full;
                rows = n;
                stripGaps = s;
                columnGaps = k;
                top = -RestOf(rect).y;
            }
            if (natural <= 0f) return;

            // KitVisibility's own flag, not alpha: the row's entrance holds alpha at 0 for a moment after a show.
            bool rowShown = aiRow && aiRow.blocksRaycasts;
            float room = rowShown ? -_rowRest.y - rowGap - top : _pilotsHeight - top;
            var fit = Solve(natural, room, maxGrowth, rows, stripGaps, columnGaps, seatHeight, _stripGap, _columnGap,
                            compactGap, minSeatHeight);

            ApplySeats(fit);
            if (Mathf.Abs(fit.Growth - _growth) > 0.01f) ApplyGrowth(fit.Growth);
        }

        /// <summary>
        /// How much the column grows, and whether and how far its rows tighten, for a tallest column of
        /// <paramref name="natural"/> pixels (at full-size rows) in <paramref name="room"/> pixels.
        /// <paramref name="rows"/> are the rows that can shorten (its seats and its open seat),
        /// <paramref name="stripGaps"/> the gaps between seats and <paramref name="columnGaps"/> the gaps
        /// between the tile, the seats and the open seat.
        /// </summary>
        public static Fit Solve(float natural, float room, float maxGrowth, int rows, int stripGaps, int columnGaps,
                                float seatHeight, float stripGap, float columnGap, float compactGap, float minSeatHeight)
        {
            float need = natural - room;
            float growth = Mathf.Clamp(need, 0f, Mathf.Max(0f, maxGrowth));
            float overflow = need - growth;
            if (overflow <= 0.01f) return new Fit(growth, false, seatHeight);

            float gapSaving = stripGaps * Mathf.Max(0f, stripGap - compactGap)
                              + columnGaps * Mathf.Max(0f, columnGap - compactGap);
            float rest = overflow - gapSaving;
            float seat = seatHeight;
            if (rest > 0f && rows > 0)
                seat = Mathf.Clamp(seatHeight - rest / rows, Mathf.Min(minSeatHeight, seatHeight), seatHeight);
            // Rows at their floor and still too tall (a lobby that cannot launch, e.g. every Arena pilot in
            // one domain): the column grows past maxGrowth rather than cover the AI row.
            float residual = rest - rows * (seatHeight - seat);
            if (residual > 0f) growth += residual;
            return new Fit(growth, true, seat);
        }

        public readonly struct Fit
        {
            public readonly float Growth;
            public readonly bool Compact;
            public readonly float SeatHeight;

            public Fit(float growth, bool compact, float seatHeight)
            {
                Growth = growth;
                Compact = compact;
                SeatHeight = seatHeight;
            }
        }

        void ApplySeats(Fit fit)
        {
            bool changed = fit.Compact != _compact || Mathf.Abs(fit.SeatHeight - _seat) > 0.01f;
            _compact = fit.Compact;
            _seat = fit.SeatHeight;

            for (int i = 0; i < columns.Length; i++)
            {
                var column = columns[i];
                if (!column) continue;
                var strip = column.AvatarStripTransform;
                if (changed)
                {
                    if (column.TryGetComponent(out HorizontalOrVerticalLayoutGroup own))
                        own.spacing = _compact ? compactGap : _columnGap;
                    if (strip && strip.TryGetComponent(out HorizontalOrVerticalLayoutGroup seats))
                        seats.spacing = _compact ? compactGap : _stripGap;
                }
                SetRowHeight(OpenSeatOf(column));
                // Every frame, cheaply: a chip the modal spawned since arrives at the prefab's height.
                if (!strip) continue;
                for (int c = 0; c < strip.childCount; c++) SetRowHeight(strip.GetChild(c));
            }
        }

        void SetRowHeight(Transform row)
        {
            if (!row || !row.TryGetComponent(out LayoutElement layout)) return;
            if (Mathf.Abs(layout.preferredHeight - _seat) > 0.01f) layout.preferredHeight = _seat;
        }

        static RectTransform OpenSeatOf(DomainInfoData column) => column is KitDomainTile kit ? kit.OpenSeatRect : null;

        static bool InLayout(Transform child) =>
            child && child.gameObject.activeSelf &&
            !(child.TryGetComponent(out LayoutElement layout) && layout.ignoreLayout);

        void ApplyGrowth(float growth)
        {
            _growth = growth;
            if (_steps) _steps.sizeDelta = new Vector2(_steps.sizeDelta.x, _stepsHeight + growth);
            if (pilotsSection) pilotsSection.sizeDelta = new Vector2(pilotsSection.sizeDelta.x, _pilotsHeight + growth);
            if (aiRow && aiRow.transform is RectTransform row)
                row.anchoredPosition = new Vector2(_rowRest.x, _rowRest.y - growth);
            for (int i = 0; i < below.Length; i++)
                if (below[i]) below[i].anchoredPosition = new Vector2(_belowRest[i].x, _belowRest[i].y - growth);
        }

        static int CountSeats(Transform strip)
        {
            if (!strip) return 0;
            int count = 0;
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i);
                if (child.gameObject.activeSelf && child.TryGetComponent<DomainAvatarChip>(out _)) count++;
            }
            return count;
        }

        static int CountLayoutChildren(Transform column)
        {
            int count = 0;
            for (int i = 0; i < column.childCount; i++)
                if (InLayout(column.GetChild(i))) count++;
            return count;
        }
    }
}
