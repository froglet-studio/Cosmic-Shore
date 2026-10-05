using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// 2D rectangle defined by min corner (x, y) + size (width, height), matching the
    /// original engine's Rect contract. The UI geometry core (RectTransform.rect) expresses
    /// rects in pivot-relative local space: min = -pivot * size.
    /// </summary>
    [Serializable]
    public struct Rect : IEquatable<Rect>
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public Rect(Vector2 position, Vector2 size)
        {
            x = position.x;
            y = position.y;
            width = size.x;
            height = size.y;
        }

        /// <summary>Original contract: build from edges rather than min + size.</summary>
        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
            => new(xmin, ymin, xmax - xmin, ymax - ymin);

        public static Rect zero => new(0f, 0f, 0f, 0f);

        public float xMin { get => x; set { float oldMax = xMax; x = value; width = oldMax - x; } }
        public float yMin { get => y; set { float oldMax = yMax; y = value; height = oldMax - y; } }
        public float xMax { get => x + width; set => width = value - x; }
        public float yMax { get => y + height; set => height = value - y; }

        public Vector2 position
        {
            get => new(x, y);
            set { x = value.x; y = value.y; }
        }

        public Vector2 size
        {
            get => new(width, height);
            set { width = value.x; height = value.y; }
        }

        public Vector2 min
        {
            get => new(xMin, yMin);
            set { xMin = value.x; yMin = value.y; }
        }

        public Vector2 max
        {
            get => new(xMax, yMax);
            set { xMax = value.x; yMax = value.y; }
        }

        public Vector2 center
        {
            get => new(x + width * 0.5f, y + height * 0.5f);
            set { x = value.x - width * 0.5f; y = value.y - height * 0.5f; }
        }

        public bool Contains(Vector2 point)
            => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;

        public bool Contains(Vector3 point) => Contains(new Vector2(point.x, point.y));

        public bool Overlaps(Rect other)
            => other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;

        /// <summary>Normalized [0,1]² coordinates → a point inside the rect.</summary>
        public static Vector2 NormalizedToPoint(Rect rect, Vector2 normalized)
            => new(Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                   Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));

        /// <summary>A point → normalized [0,1]² coordinates within the rect.</summary>
        public static Vector2 PointToNormalized(Rect rect, Vector2 point)
            => new(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                   Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));

        public bool Equals(Rect other)
            => x == other.x && y == other.y && width == other.width && height == other.height;

        public override bool Equals(object obj) => obj is Rect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(x, y, width, height);

        public static bool operator ==(Rect a, Rect b) => a.Equals(b);
        public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

        public override string ToString() => $"(x:{x:F2}, y:{y:F2}, width:{width:F2}, height:{height:F2})";
    }
}
