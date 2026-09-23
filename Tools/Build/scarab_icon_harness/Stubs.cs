// UnityEngine math stubs for compiling ScarabHullForm (the pure hull generator) outside Unity:
// the regatta course harness stub plus Vector2, Mathf.Pow and the Vector3 min/max/infinity members.
// A harness, not Unity: Vector3/Quaternion/Mathf semantics re-implemented in double precision
// where Unity uses float - the harness measures GEOMETRY (lengths, radii, counts), which is
// insensitive to that, and asserts nothing bit-exact against the editor.
using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0,0,0);
        public static Vector3 one => new(1,1,1);
        public static Vector3 up => new(0,1,0);
        public static Vector3 forward => new(0,0,1);
        public static Vector3 right => new(1,0,0);
        public static Vector3 back => new(0,0,-1);
        public static Vector3 positiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector3 negativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        public static Vector3 Min(Vector3 a, Vector3 b) => new(Math.Min(a.x,b.x), Math.Min(a.y,b.y), Math.Min(a.z,b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new(Math.Max(a.x,b.x), Math.Max(a.y,b.y), Math.Max(a.z,b.z));
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        public void Normalize() { float m = magnitude; if (m > 1e-5f) { x/=m; y/=m; z/=m; } else { x=y=z=0f; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x, a.y+b.y, a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x, a.y-b.y, a.z-b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x*s, a.y*s, a.z*s);
        public static Vector3 operator *(float s, Vector3 a) => new(a.x*s, a.y*s, a.z*s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.x/s, a.y/s, a.z/s);
        public static bool operator ==(Vector3 a, Vector3 b) => (a-b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a==b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        public float sqrMagnitude => x*x+y*y+z*z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b-a)*t; }
        public static float Angle(Vector3 a, Vector3 b) { float d = Mathf.Sqrt(a.sqrMagnitude*b.sqrMagnitude); if (d < 1e-15f) return 0f; return Mathf.Acos(Mathf.Clamp(Dot(a,b)/d, -1f, 1f)) * Mathf.Rad2Deg; }
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) { float u = Angle(from, to); Vector3 c = Cross(from, to); float s = Mathf.Sign(Dot(axis, c)); return u * s; }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float nn = n.sqrMagnitude; if (nn < 1e-15f) return v; return v - n * (Dot(v,n)/nn); }
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new(0,0);
        public static Vector2 one => new(1,1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x+b.x, a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x, a.y-b.y);
        public static Vector2 operator *(Vector2 a, float s) => new(a.x*s, a.y*s);
        public float magnitude => (float)Math.Sqrt(x*x+y*y);
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x=x; this.y=y; this.z=z; this.w=w; }
        public static Quaternion identity => new(0,0,0,1);
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 f = forward.normalized; Vector3 r = Vector3.Cross(up, f).normalized; Vector3 u = Vector3.Cross(f, r);
            float m00=r.x,m01=u.x,m02=f.x,m10=r.y,m11=u.y,m12=f.y,m20=r.z,m21=u.z,m22=f.z;
            float tr = m00+m11+m22; Quaternion q;
            if (tr > 0) { float s = Mathf.Sqrt(tr+1f)*2f; q = new((m21-m12)/s,(m02-m20)/s,(m10-m01)/s,0.25f*s); }
            else if (m00 > m11 && m00 > m22) { float s = Mathf.Sqrt(1f+m00-m11-m22)*2f; q = new(0.25f*s,(m01+m10)/s,(m02+m20)/s,(m21-m12)/s); }
            else if (m11 > m22) { float s = Mathf.Sqrt(1f+m11-m00-m22)*2f; q = new((m01+m10)/s,0.25f*s,(m12+m21)/s,(m02-m20)/s); }
            else { float s = Mathf.Sqrt(1f+m22-m00-m11)*2f; q = new((m02+m20)/s,(m12+m21)/s,0.25f*s,(m10-m01)/s); }
            return q;
        }
        public static Quaternion AngleAxis(float deg, Vector3 axis) { axis = axis.normalized; float h = deg*Mathf.Deg2Rad*0.5f; float s = Mathf.Sin(h); return new(axis.x*s, axis.y*s, axis.z*s, Mathf.Cos(h)); }
        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            Vector3 a = from.normalized, b = to.normalized; float d = Vector3.Dot(a,b);
            if (d > 0.999999f) return identity;
            if (d < -0.999999f) { Vector3 ax = Vector3.Cross(Vector3.right, a); if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(Vector3.up, a); return AngleAxis(180f, ax); }
            Vector3 c = Vector3.Cross(a,b); float s = Mathf.Sqrt((1f+d)*2f); return new(c.x/s, c.y/s, c.z/s, s*0.5f);
        }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x=q.x*2f,y=q.y*2f,z=q.z*2f,xx=q.x*x,yy=q.y*y,zz=q.z*z,xy=q.x*y,xz=q.x*z,yz=q.y*z,wx=q.w*x,wy=q.w*y,wz=q.w*z;
            return new((1f-(yy+zz))*v.x+(xy-wz)*v.y+(xz+wy)*v.z,(xy+wz)*v.x+(1f-(xx+zz))*v.y+(yz-wx)*v.z,(xz-wy)*v.x+(yz+wx)*v.y+(1f-(xx+yy))*v.z);
        }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y, a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x, a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w, a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z);
    }
    public static class Mathf
    {
        public const float PI = (float)Math.PI; public const float Deg2Rad = PI/180f; public const float Rad2Deg = 180f/PI;
        public static float Sin(float v) => (float)Math.Sin(v); public static float Cos(float v) => (float)Math.Cos(v); public static float Tan(float v) => (float)Math.Tan(v);
        public static float Acos(float v) => (float)Math.Acos(v); public static float Sqrt(float v) => (float)Math.Sqrt(v); public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => Math.Max(a,b); public static int Max(int a, int b) => Math.Max(a,b);
        public static float Min(float a, float b) => Math.Min(a,b); public static int Min(int a, int b) => Math.Min(a,b);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v); public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f); public static float Lerp(float a, float b, float t) => a + (b-a)*Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b-a)*t;
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven); public static int CeilToInt(float v) => (int)Math.Ceiling(v); public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sign(float v) => v >= 0f ? 1f : -1f; public static bool Approximately(float a, float b) => Math.Abs(a-b) < 1e-6f;
    }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) {} }
    public class SerializeField : Attribute {}
}
