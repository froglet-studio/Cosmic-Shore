using System;

namespace CosmicShore.Engine
{
    /// <summary>An infinite plane: normal·p + distance = 0 (UnityEngine.Plane).</summary>
    public struct Plane
    {
        Vector3 m_Normal;
        float m_Distance;

        public Vector3 normal { get => m_Normal; set => m_Normal = value; }
        public float distance { get => m_Distance; set => m_Distance = value; }

        public Plane(Vector3 inNormal, Vector3 inPoint)
        {
            m_Normal = inNormal.normalized;
            m_Distance = -Vector3.Dot(m_Normal, inPoint);
        }

        public Plane(Vector3 inNormal, float d)
        {
            m_Normal = inNormal.normalized;
            m_Distance = d;
        }

        public Plane(Vector3 a, Vector3 b, Vector3 c)
        {
            m_Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            m_Distance = -Vector3.Dot(m_Normal, a);
        }

        public void SetNormalAndPosition(Vector3 inNormal, Vector3 inPoint)
        {
            m_Normal = inNormal.normalized;
            m_Distance = -Vector3.Dot(m_Normal, inPoint);
        }

        public void Set3Points(Vector3 a, Vector3 b, Vector3 c) => this = new Plane(a, b, c);
        public void Flip() { m_Normal = -m_Normal; m_Distance = -m_Distance; }
        public Plane flipped => new(-m_Normal, -m_Distance);
        public void Translate(Vector3 translation) => m_Distance += Vector3.Dot(m_Normal, translation);
        public static Plane Translate(Plane plane, Vector3 translation) { plane.Translate(translation); return plane; }

        public Vector3 ClosestPointOnPlane(Vector3 point) => point - m_Normal * (Vector3.Dot(m_Normal, point) + m_Distance);
        public float GetDistanceToPoint(Vector3 point) => Vector3.Dot(m_Normal, point) + m_Distance;
        public bool GetSide(Vector3 point) => Vector3.Dot(m_Normal, point) + m_Distance > 0f;
        public bool SameSide(Vector3 a, Vector3 b) => GetSide(a) == GetSide(b);

        public bool Raycast(Ray ray, out float enter)
        {
            float vdot = Vector3.Dot(ray.direction, m_Normal);
            float ndot = -Vector3.Dot(ray.origin, m_Normal) - m_Distance;
            if (MathF.Abs(vdot) < 1e-6f) { enter = 0f; return false; }
            enter = ndot / vdot;
            return enter > 0f;
        }

        public override string ToString() => $"(normal:{m_Normal}, distance:{m_Distance})";
    }
}
