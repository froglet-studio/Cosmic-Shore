using System;
namespace UnityEngine {
  public struct Vector3 {
    public float x,y,z;
    public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
    public static Vector3 zero => new Vector3(0,0,0);
    public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    public static Vector3 operator-(Vector3 a)=>new Vector3(-a.x,-a.y,-a.z);
    public static Vector3 operator*(Vector3 a,float s)=>new Vector3(a.x*s,a.y*s,a.z*s);
    public static Vector3 operator*(float s,Vector3 a)=>a*s;
    public static Vector3 operator/(Vector3 a,float s)=>new Vector3(a.x/s,a.y/s,a.z/s);
    public float magnitude => (float)Math.Sqrt(x*x+y*y+z*z);
    public float sqrMagnitude => x*x+y*y+z*z;
    public Vector3 normalized { get { float m=magnitude; return m<1e-9f?zero:this/m; } }
    public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
    public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    public override string ToString()=>$"({x:F2},{y:F2},{z:F2})";
  }
  public static class Mathf {
    public const float Epsilon=1e-7f;
    public static float Min(float a,float b)=>a<b?a:b;
    public static float Max(float a,float b)=>a>b?a:b;
    public static float Clamp01(float v)=>v<0?0:(v>1?1:v);
    public static float Clamp(float v,float a,float b)=>v<a?a:(v>b?b:v);
    public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
    public static float Exp(float v)=>(float)Math.Exp(v);
    public static float Sqrt(float v)=>(float)Math.Sqrt(v);
    public static float Abs(float v)=>Math.Abs(v);
  }
}
