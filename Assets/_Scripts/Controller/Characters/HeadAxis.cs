using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CONTINUOUS blend space every head — procedural or authored — is driven through.
    /// Each axis is a signed [-1, 1] dial about the neutral human; the base head decides what a
    /// unit of travel looks like. Adding an axis is a code change here AND in every
    /// <see cref="IBaseHead"/> implementation, which is deliberate: an axis is a promise about
    /// what the rig can express.
    /// </summary>
    public enum HeadAxis
    {
        CranialHeight = 0,
        CranialWidth = 1,
        CranialLength = 2,
        ForeheadBulge = 3,
        BrowRidge = 4,
        OrbitalSpacing = 5,
        OrbitalSize = 6,
        OrbitalDepth = 7,
        CheekboneWidth = 8,
        MuzzleLength = 9,
        MuzzleWidth = 10,
        NoseProjection = 11,
        NoseWidth = 12,
        LipFullness = 13,
        MouthWidth = 14,
        JawWidth = 15,
        ChinProjection = 16,
        LowerFaceHeight = 17,
        NeckThickness = 18,
        NeckLength = 19,
    }

    /// <summary>
    /// One point in the blend space. A plain float per axis, serializable, comparable — a
    /// genome carries one of these for the human base, and the resolver produces one for the
    /// finished character. Values outside [-1, 1] are clamped at evaluation, never here, so a
    /// hand-edited genome is kept as written.
    /// </summary>
    [Serializable]
    public struct HeadShape
    {
        public const int AxisCount = 20;

        [SerializeField] float[] _values;

        public static HeadShape Neutral => new HeadShape { _values = new float[AxisCount] };

        public bool IsInitialized => _values != null && _values.Length == AxisCount;

        public float this[HeadAxis axis]
        {
            get => _values == null || _values.Length != AxisCount ? 0f : _values[(int)axis];
            set
            {
                if (_values == null || _values.Length != AxisCount) _values = new float[AxisCount];
                _values[(int)axis] = value;
            }
        }

        /// <summary>Clamped read — the only read the geometry ever performs.</summary>
        public float Clamped(HeadAxis axis)
        {
            float v = this[axis];
            return v < -1f ? -1f : (v > 1f ? 1f : v);
        }

        public HeadShape With(HeadAxis axis, float value)
        {
            var copy = Copy();
            copy[axis] = value;
            return copy;
        }

        public HeadShape Copy()
        {
            var c = Neutral;
            if (_values != null)
                for (int i = 0; i < AxisCount && i < _values.Length; i++) c._values[i] = _values[i];
            return c;
        }

        public static HeadShape Lerp(HeadShape a, HeadShape b, float t)
        {
            var r = Neutral;
            for (int i = 0; i < AxisCount; i++)
            {
                var axis = (HeadAxis)i;
                r._values[i] = a[axis] + (b[axis] - a[axis]) * t;
            }
            return r;
        }

        public float MaxAbsDifference(HeadShape other)
        {
            float m = 0f;
            for (int i = 0; i < AxisCount; i++)
            {
                float d = Mathf.Abs(this[(HeadAxis)i] - other[(HeadAxis)i]);
                if (d > m) m = d;
            }
            return m;
        }
    }

    /// <summary>
    /// The INTEGER settings that decide a procedural head's topology. Never touched by a
    /// morph or an axis: two heads generated at the same detail have identical vertex counts
    /// and triangle lists whatever their <see cref="HeadShape"/>, which is what lets a rig,
    /// a bake and a test compare them vertex for vertex.
    /// </summary>
    [Serializable]
    public struct HeadDetail
    {
        [Tooltip("Rings from crown to neck base.")] public int Rings;
        [Tooltip("Segments around the head. Even, so the face centre line lands on a column.")] public int Segments;

        /// <summary>The portrait bake budget — model high, bake down.</summary>
        public static HeadDetail Portrait => new HeadDetail { Rings = 96, Segments = 128 };
        /// <summary>An in-world budget the next game can decimate to; same generator, fewer ints.</summary>
        public static HeadDetail Runtime => new HeadDetail { Rings = 40, Segments = 56 };

        public HeadDetail Sanitized()
        {
            var d = this;
            if (d.Rings < 8) d.Rings = 8;
            if (d.Segments < 12) d.Segments = 12;
            if ((d.Segments & 1) == 1) d.Segments++;
            return d;
        }
    }
}
