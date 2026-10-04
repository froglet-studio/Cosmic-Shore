// Minimal Unity.Mathematics stand-in: just enough to compile the SHIPPED
// ShieldShellMath.cs unchanged and run it against brute force.
using System;
namespace Unity.Mathematics {
  public struct float3 {
    public float x,y,z;
    public float3(float a,float b,float c){x=a;y=b;z=c;}
    public float3(float a){x=y=z=a;}
    public static float3 operator+(float3 a,float3 b)=>new float3(a.x+b.x,a.y+b.y,a.z+b.z);
    public static float3 operator-(float3 a,float3 b)=>new float3(a.x-b.x,a.y-b.y,a.z-b.z);
    public static float3 operator-(float3 a)=>new float3(-a.x,-a.y,-a.z);
    public static float3 operator*(float3 a,float s)=>new float3(a.x*s,a.y*s,a.z*s);
    public static float3 operator*(float s,float3 a)=>new float3(a.x*s,a.y*s,a.z*s);
    public static float3 operator*(float3 a,float3 b)=>new float3(a.x*b.x,a.y*b.y,a.z*b.z);
    public static float3 operator/(float3 a,float s)=>new float3(a.x/s,a.y/s,a.z/s);
    public override string ToString()=>$"({x:F4},{y:F4},{z:F4})";
  }
  public struct float4 {
    public float x,y,z,w;
    public float4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}
    public static float4 operator+(float4 a,float4 b)=>new float4(a.x+b.x,a.y+b.y,a.z+b.z,a.w+b.w);
    public static float4 operator-(float4 a,float4 b)=>new float4(a.x-b.x,a.y-b.y,a.z-b.z,a.w-b.w);
    public static float4 operator*(float4 a,float s)=>new float4(a.x*s,a.y*s,a.z*s,a.w*s);
  }
  public struct quaternion { public float x,y,z,w;
    public static quaternion identity => new quaternion{x=0,y=0,z=0,w=1}; }
  public static class math {
    public static float3 abs(float3 a)=>new float3(Math.Abs(a.x),Math.Abs(a.y),Math.Abs(a.z));
    public static float abs(float a)=>Math.Abs(a);
    public static float cmax(float3 a)=>Math.Max(a.x,Math.Max(a.y,a.z));
    public static float cmin(float3 a)=>Math.Min(a.x,Math.Min(a.y,a.z));
    public static float dot(float3 a,float3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
    public static float3 cross(float3 a,float3 b)=>new float3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    public static float lengthsq(float3 a)=>dot(a,a);
    public static float distancesq(float3 a,float3 b)=>lengthsq(a-b);
    public static float max(float a,float b)=>Math.Max(a,b);
    public static float min(float a,float b)=>Math.Min(a,b);
    public static float3 max(float3 a,float3 b)=>new float3(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z));
    public static float3 min(float3 a,float3 b)=>new float3(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Min(a.z,b.z));
    public static float clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));
    public static float3 clamp(float3 v,float a,float b)=>new float3(clamp(v.x,a,b),clamp(v.y,a,b),clamp(v.z,a,b));
    public static float cmax(float4 a)=>Math.Max(Math.Max(a.x,a.y),Math.Max(a.z,a.w));
    public static float cmin(float4 a)=>Math.Min(Math.Min(a.x,a.y),Math.Min(a.z,a.w));
    public static float4 abs(float4 a)=>new float4(Math.Abs(a.x),Math.Abs(a.y),Math.Abs(a.z),Math.Abs(a.w));
    public static float4 max(float4 a,float4 b)=>new float4(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z),Math.Max(a.w,b.w));
    public static float4 min(float4 a,float4 b)=>new float4(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Min(a.z,b.z),Math.Min(a.w,b.w));
    public static float3 mul(quaternion q,float3 v){
      float3 u=new float3(q.x,q.y,q.z);
      return u*(2f*dot(u,v)) + v*(q.w*q.w-dot(u,u)) + cross(u,v)*(2f*q.w);
    }
  }
}
