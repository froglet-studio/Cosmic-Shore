namespace CosmicShore.Engine
{
    /// <summary>A half-line: origin + t·direction (UnityEngine.Ray; direction is normalized).</summary>
    public struct Ray
    {
        Vector3 m_Origin;
        Vector3 m_Direction;

        public Ray(Vector3 origin, Vector3 direction) { m_Origin = origin; m_Direction = direction.normalized; }

        public Vector3 origin { get => m_Origin; set => m_Origin = value; }
        public Vector3 direction { get => m_Direction; set => m_Direction = value.normalized; }
        public Vector3 GetPoint(float distance) => m_Origin + m_Direction * distance;
        public override string ToString() => $"Origin: {m_Origin}, Dir: {m_Direction}";
    }

    /// <summary>A 2D half-line (UnityEngine.Ray2D).</summary>
    public struct Ray2D
    {
        Vector2 m_Origin;
        Vector2 m_Direction;
        public Ray2D(Vector2 origin, Vector2 direction) { m_Origin = origin; m_Direction = direction.normalized; }
        public Vector2 origin { get => m_Origin; set => m_Origin = value; }
        public Vector2 direction { get => m_Direction; set => m_Direction = value.normalized; }
        public Vector2 GetPoint(float distance) => m_Origin + m_Direction * distance;
    }
}
