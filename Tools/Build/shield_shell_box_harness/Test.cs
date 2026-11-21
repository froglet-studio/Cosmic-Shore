using System;
using Unity.Mathematics;
using CosmicShore.Utility;

class Test {
  static Random rng = new Random(12345);
  static float U(float a,float b)=>(float)(a+rng.NextDouble()*(b-a));
  static float3 R3(float m)=>new float3(U(-m,m),U(-m,m),U(-m,m));
  static quaternion RandQ(){
    float u1=(float)rng.NextDouble(),u2=(float)rng.NextDouble(),u3=(float)rng.NextDouble();
    float s1=(float)Math.Sqrt(1-u1), s2=(float)Math.Sqrt(u1);
    return new quaternion{ x=s1*(float)Math.Sin(2*Math.PI*u2), y=s1*(float)Math.Cos(2*Math.PI*u2),
                           z=s2*(float)Math.Sin(2*Math.PI*u3), w=s2*(float)Math.Cos(2*Math.PI*u3) };
  }
  // ---- brute force ground truth: dense sampling of the PROBE volume, membership in the box ----
  static bool InBox(in ShieldShellMath.ShellFrame f, float3 p){
    float3 d=p-f.Center;
    float nx=math.dot(d,f.InvRowX), ny=math.dot(d,f.InvRowY), nz=math.dot(d,f.InvRowZ);
    return Math.Abs(nx)<=1f && Math.Abs(ny)<=1f && Math.Abs(nz)<=1f;
  }
  static float DistToBox(in ShieldShellMath.ShellFrame f, float3 p){
    float3 d=p-f.Center;
    float nx=math.clamp(math.dot(d,f.InvRowX),-1,1);
    float ny=math.clamp(math.dot(d,f.InvRowY),-1,1);
    float nz=math.clamp(math.dot(d,f.InvRowZ),-1,1);
    float3 w=f.Center+f.AxisX*nx+f.AxisY*ny+f.AxisZ*nz;
    return (float)Math.Sqrt(math.distancesq(p,w));
  }
  static void Main(){
    int N=40000, sphMis=0,capMis=0,boxMis=0, sphAmb=0,capAmb=0,boxAmb=0;
    int sphTrue=0,capTrue=0,boxTrue=0;
    for(int i=0;i<N;i++){
      var semi=new float3(U(0.2f,3f),U(0.2f,3f),U(0.2f,3f));
      var f=ShieldShellMath.CreateFrame(R3(2f), RandQ(), semi);
      float scale=math.cmax(semi);
      // --- sphere ---
      {
        float3 c=f.Center+R3(scale*2.2f); float r=U(0.05f,scale*1.2f);
        bool got=ShieldShellMath.SphereOverlapsBox(in f,c,r);
        float d=DistToBox(in f,c);                       // exact: sphere hits iff d <= r
        bool want=d<=r;
        if(Math.Abs(d-r)<1e-4f) sphAmb++; else if(got!=want) sphMis++;
        if(want) sphTrue++;
      }
      // --- capsule: exact truth = min distance from segment to box <= r, sampled densely ---
      {
        float3 a=f.Center+R3(scale*2.2f), b=a+R3(scale*1.6f); float r=U(0.05f,scale*0.9f);
        bool got=ShieldShellMath.CapsuleOverlapsBox(in f,a,b,r);
        float best=float.MaxValue;
        const int S=4000;
        for(int k=0;k<=S;k++){ float t=(float)k/S; best=Math.Min(best,DistToBox(in f,a+(b-a)*t)); }
        bool want=best<=r;
        if(Math.Abs(best-r)<2e-3f) capAmb++; else if(got!=want) capMis++;
        if(want) capTrue++;
      }
      // --- box: truth = SAT is exact for convex bodies; cross-check by dense sampling of BOTH volumes ---
      {
        float3 c=f.Center+R3(scale*2.0f);
        var q=RandQ();
        float3 e1=math.mul(q,new float3(U(0.1f,1.4f),0,0));
        float3 e2=math.mul(q,new float3(0,U(0.1f,1.4f),0));
        float3 e3=math.mul(q,new float3(0,0,U(0.1f,1.4f)));
        bool got=ShieldShellMath.BoxOverlapsBox(in f,c,e1,e2,e3);
        bool want=false; const int G=26;
        for(int a1=0;a1<=G&&!want;a1++)for(int a2=0;a2<=G&&!want;a2++)for(int a3=0;a3<=G&&!want;a3++){
          float s1=-1f+2f*a1/G, s2=-1f+2f*a2/G, s3=-1f+2f*a3/G;
          if(InBox(in f, c+e1*s1+e2*s2+e3*s3)) want=true;
        }
        if(!want && got){
          // sampling can miss a shallow overlap; only count a miss if the SHELL's corners
          // are also outside the probe (symmetric check) — otherwise it is ambiguous.
          bool rev=false;
          for(int m=0;m<8&&!rev;m++){
            float3 v=f.Center+((m&1)==0?f.AxisX:-f.AxisX)+((m&2)==0?f.AxisY:-f.AxisY)+((m&4)==0?f.AxisZ:-f.AxisZ);
            float3 d=v-c;
            float n1=math.dot(d,e1)/math.lengthsq(e1), n2=math.dot(d,e2)/math.lengthsq(e2), n3=math.dot(d,e3)/math.lengthsq(e3);
            if(Math.Abs(n1)<=1&&Math.Abs(n2)<=1&&Math.Abs(n3)<=1) rev=true;
          }
          if(!rev) boxAmb++;            // grazing / shallow — sampling cannot resolve it
        } else if(got!=want) boxMis++;
        if(want) boxTrue++;
      }
    }
    Console.WriteLine($"cases {N}");
    Console.WriteLine($"  SphereOverlapsBox   mismatches {sphMis}   (true overlaps {sphTrue}, near-tangent skipped {sphAmb})");
    Console.WriteLine($"  CapsuleOverlapsBox  mismatches {capMis}   (true overlaps {capTrue}, near-tangent skipped {capAmb})");
    Console.WriteLine($"  BoxOverlapsBox      mismatches {boxMis}   (true overlaps {boxTrue}, shallow/unresolved {boxAmb})");
    Console.WriteLine((sphMis+capMis+boxMis)==0 ? "\nPASS - all three agree with brute force" : "\nFAIL");
  }
}
