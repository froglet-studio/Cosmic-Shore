using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The cytoplasm flow a <see cref="CellGenome"/> describes: the sum of up to three analytic terms (a banded swirl
    /// round an axis, an Arnold-Beltrami-Childress cell, radial rings), evaluated in the cell's own frame with
    /// positions measured in membrane radii. Implements <see cref="SnowChanger.IFlow"/>: cheap, allocation-free,
    /// always finite, and bounded by <see cref="MaxMagnitude"/>. Visual only - it decides which way a shard points
    /// and how long it is drawn, nothing else.
    /// </summary>
    public sealed class CellGenomeFlow : SnowChanger.IFlow
    {
        // Abc coefficients (A, B, C); a classic chaotic-streamline set
        const float AbcA = 1f, AbcB = 0.7f, AbcC = 0.5f;
        /// <summary>Largest |velocity| one unit-strength term can produce (the Abc term's bound).</summary>
        public const float TermBound = AbcA + AbcB + AbcC + 1f;
        const float MinMagnitude = 1e-4f;

        readonly CellGenome.FlowKind[] _kind = new CellGenome.FlowKind[CellGenome.FlowTermCount];
        readonly Quaternion[] _toTerm = new Quaternion[CellGenome.FlowTermCount];
        readonly Vector3[] _axis = new Vector3[CellGenome.FlowTermCount];
        readonly float[] _k = new float[CellGenome.FlowTermCount];
        readonly float[] _strength = new float[CellGenome.FlowTermCount];
        readonly float[] _phase = new float[CellGenome.FlowTermCount];
        readonly float _baseStretch;
        readonly float _invMagnitude;
        Vector3 _centre;
        float _invRadius;

        /// <summary>Upper bound on |sum of terms| for this genome (sum of |strength| x <see cref="TermBound"/>).</summary>
        public float MaxMagnitude { get; }

        public CellGenomeFlow(in CellGenome genome, Vector3 centre, float radius)
        {
            float bound = 0f;
            for (int i = 0; i < CellGenome.FlowTermCount; i++)
            {
                var t = genome.GetFlowTerm(i);
                _kind[i] = t.Kind;
                _axis[i] = t.Axis;
                _toTerm[i] = Quaternion.Inverse(Quaternion.LookRotation(_axis[i], Mathf.Abs(_axis[i].y) > 0.99f ? Vector3.right : Vector3.up));
                _k[i] = Mathf.Clamp(t.Frequency, 0f, CellGenome.MaxFlowFrequency) * Mathf.PI;
                _strength[i] = t.Kind == CellGenome.FlowKind.Off ? 0f : Mathf.Clamp(t.Strength, -1f, 1f);
                _phase[i] = t.Phase * 2f * Mathf.PI;
                bound += Mathf.Abs(_strength[i]) * TermBound;
            }
            MaxMagnitude = bound;
            _invMagnitude = bound > 0f ? 1f / bound : 0f;
            _baseStretch = Mathf.Clamp01(genome.FlowBaseStretch);
            SetFrame(centre, radius);
        }

        /// <summary>Where the cell is and how big (world units); the field is laid out in membrane radii.</summary>
        public void SetFrame(Vector3 centre, float radius)
        {
            _centre = centre;
            _invRadius = 1f / Mathf.Max(1e-3f, radius);
        }

        /// <summary>The flow's velocity at a world position (bounded by <see cref="MaxMagnitude"/>).</summary>
        public Vector3 Evaluate(Vector3 position)
        {
            Vector3 q = (position - _centre) * _invRadius;
            // outside the membrane the field is clamped to the unit ball, so a far shard still gets a finite answer
            float r2 = q.sqrMagnitude;
            if (r2 > 1f) q *= 1f / Mathf.Sqrt(r2);
            Vector3 v = Vector3.zero;
            for (int i = 0; i < CellGenome.FlowTermCount; i++)
            {
                float s = _strength[i];
                if (s == 0f) continue;
                v += s * Term(i, q);
            }
            return v;
        }

        Vector3 Term(int i, Vector3 q)
        {
            float k = _k[i], ph = _phase[i];
            switch (_kind[i])
            {
                case CellGenome.FlowKind.Swirl:
                {
                    // rotation round the axis, its speed banded along it: |cross(a, q)| <= 1 within the unit ball
                    Vector3 a = _axis[i];
                    float band = 0.5f + 0.5f * Mathf.Sin(k * Vector3.Dot(q, a) + ph);
                    return Vector3.Cross(a, q) * (0.5f + band);
                }
                case CellGenome.FlowKind.Abc:
                {
                    Vector3 p = _toTerm[i] * q;
                    var local = new Vector3(
                        AbcA * Mathf.Sin(k * p.z + ph) + AbcC * Mathf.Cos(k * p.y + ph),
                        AbcB * Mathf.Sin(k * p.x + ph) + AbcA * Mathf.Cos(k * p.z + ph),
                        AbcC * Mathf.Sin(k * p.y + ph) + AbcB * Mathf.Cos(k * p.x + ph));
                    return Quaternion.Inverse(_toTerm[i]) * local;
                }
                case CellGenome.FlowKind.Rings:
                {
                    float r = q.magnitude;
                    if (r < 1e-5f) return Vector3.zero;
                    return q * (Mathf.Sin(k * r * 2f + ph) / r);
                }
                default:
                    return Vector3.zero;
            }
        }

        public bool TryFlow(Vector3 position, out Vector3 direction, out float stretch01)
        {
            direction = Evaluate(position);
            float m = direction.magnitude;
            if (!(m > MinMagnitude) || float.IsInfinity(m))
            {
                stretch01 = 0f;
                return false;   // a still point: leave this shard to the shipped law
            }
            stretch01 = Mathf.Clamp01(_baseStretch + (1f - _baseStretch) * m * _invMagnitude);
            return true;
        }
    }
}
