#!/usr/bin/env python3
"""
Prove the SHIPPED PrismShieldMorph_float - the shield shatter's velocity terms.

Docs/PRISM_ANIMATION.md 4.8.1. The shatter takes the prism explosion's own initial
condition (a WORLD-space velocity) and applies it per face: the shards DRIFT along the
impulse and each face TUMBLES about normalize(cross(velocity, faceNormal)). None of that
is observable from the graph wiring - wire_prism_shield_morph.py proves the node is
connected to the right things, and the edit-mode PrismShieldMorphTests proves the node's
slots still describe the function's parameters, but neither can say the ARITHMETIC is
right. This does, by compiling the shipped .hlsl through a thin HLSL->C++ shim and
running it.

The properties it asserts are the ones a regression here would break silently:

  1. Duration <= 0 passes the position through (every prism not mid-morph)
  2. a ZERO velocity is byte-identical to the pre-velocity shatter, at every t - which is
     what makes every direction-less disengage (a shield timer expiring, an arena
     teardown, a domain change, a herbivore stripping armour) unchanged
  3. the engage bloom ignores a stamped velocity entirely
  4. drift == Velocity * Duration in world terms
  5. the tumble is exactly Rodrigues about normalize(cross(v, n)) through the face
     CENTROID, at angle PRISM_SHIELD_SHATTER_SPIN * |Velocity| * t
  6. the rotation runs in the locally-ISOTROPIC frame, so a long thin trail slab's faces
     do not shear (the 4.9 correction, easy to drop and invisible on a cube)
  7. a face struck dead-on (cross ~ 0) is pushed rather than tumbled
  8. a degenerate object frame (a prism still at localScale 0) bails to the plain puff
  9. a stamp that outlives its scheduled retirement freezes instead of flying away
 10. a NaN velocity falls back to the plain puff rather than poisoning the shard

Requires clang++ (no Unity, no GPU). Read-only: it copies the shipped file into a temp
dir and never writes to the repo.

Two transforms are applied to that COPY, and both are mechanical translations of HLSL
into C++ rather than edits to the logic: `.yzx` becomes `.yzx()` (a C++ member cannot be
both a field and a swizzle) and `out T x` becomes `T& x` (which is what HLSL `out` means).

Usage:  python3 Tools/Shaders/verify_prism_shield_shatter.py
        exit 0 = every property holds.
"""

SHIM = r"""
// Minimal HLSL->C++ shim: enough of the language + intrinsic surface to type-check
// PrismClockAnimation.hlsl with clang++. Not a renderer — a compiler front end.
#pragma once
#include <cmath>
#include <algorithm>

struct float3;
struct float4;

struct float2 {
    float x=0,y=0;
    float2()=default; float2(float a):x(a),y(a){} float2(float a,float b):x(a),y(b){}
};

struct float3 {
    float x=0,y=0,z=0;
    float3()=default; float3(float a):x(a),y(a),z(a){} float3(float a,float b,float c):x(a),y(b),z(c){}
    float3 yzx() const { return float3(y,z,x); }
    float3& operator+=(const float3&o){x+=o.x;y+=o.y;z+=o.z;return *this;}
    float3& operator-=(const float3&o){x-=o.x;y-=o.y;z-=o.z;return *this;}
    float3& operator*=(const float3&o){x*=o.x;y*=o.y;z*=o.z;return *this;}
    float3& operator*=(float s){x*=s;y*=s;z*=s;return *this;}
};
inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);} 
inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);} 
inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);} 
inline float3 operator*(float3 a,float3 b){return float3(a.x*b.x,a.y*b.y,a.z*b.z);} 
inline float3 operator*(float3 a,float s){return float3(a.x*s,a.y*s,a.z*s);} 
inline float3 operator*(float s,float3 a){return float3(a.x*s,a.y*s,a.z*s);} 
inline float3 operator/(float3 a,float3 b){return float3(a.x/b.x,a.y/b.y,a.z/b.z);} 
inline float3 operator/(float3 a,float s){return float3(a.x/s,a.y/s,a.z/s);} 
struct bool3 { bool x,y,z; };
inline bool3 operator>(float3 a,float s){return bool3{a.x>s,a.y>s,a.z>s};}
inline bool all(bool3 b){return b.x&&b.y&&b.z;}
inline bool all(bool b){return b;}

struct float4 {
    float x=0,y=0,z=0,w=0;
    float4()=default; float4(float a):x(a),y(a),z(a),w(a){}
    float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
};

struct float3x3 {
    float _m00=0,_m01=0,_m02=0,_m10=0,_m11=0,_m12=0,_m20=0,_m21=0,_m22=0;
};
struct float4x4 {
    float _m00=0,_m01=0,_m02=0,_m03=0,_m10=0,_m11=0,_m12=0,_m13=0,
          _m20=0,_m21=0,_m22=0,_m23=0,_m30=0,_m31=0,_m32=0,_m33=0;
    explicit operator float3x3() const {
        return float3x3{_m00,_m01,_m02,_m10,_m11,_m12,_m20,_m21,_m22};
    }
};

inline float3 mul(const float3x3&m,const float3&v){
    return float3(m._m00*v.x+m._m01*v.y+m._m02*v.z,
                  m._m10*v.x+m._m11*v.y+m._m12*v.z,
                  m._m20*v.x+m._m21*v.y+m._m22*v.z);
}

inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
inline float3 saturate(float3 v){return float3(saturate(v.x),saturate(v.y),saturate(v.z));}
inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
inline float3 cross(float3 a,float3 b){return float3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
inline float length(float3 v){return std::sqrt(dot(v,v));}
inline float3 normalize(float3 v){return v/length(v);}
inline float rsqrt(float v){return 1.0f/std::sqrt(v);}
inline float lerp(float a,float b,float t){return a+(b-a)*t;}
inline float3 lerp(float3 a,float3 b,float t){return a+(b-a)*t;}
inline float smoothstep(float e0,float e1,float v){float t=saturate((v-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
inline float frac(float v){return v-std::floor(v);}
inline float3 frac(float3 v){return float3(frac(v.x),frac(v.y),frac(v.z));}
inline void sincos(float a,float&s,float&c){s=std::sin(a);c=std::cos(a);}
inline float clamp(float v,float a,float b){return std::min(b,std::max(a,v));}
inline float3 abs(float3 v){return float3(std::fabs(v.x),std::fabs(v.y),std::fabs(v.z));}
using std::max; using std::min; using std::exp; using std::sin; using std::cos; using std::sqrt;
inline float max(float a,float b){return a>b?a:b;}
inline float min(float a,float b){return a<b?a:b;}

// URP space helpers the file calls.
// Settable object frame so the numeric harness can exercise non-uniform scale.
inline float4x4& O2W(){ static float4x4 m{1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1}; return m; }
inline float4x4& W2O(){ static float4x4 m{1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1}; return m; }
inline void SetObjectScale(float sx,float sy,float sz){
    O2W() = float4x4{sx,0,0,0, 0,sy,0,0, 0,0,sz,0, 0,0,0,1};
    W2O() = float4x4{1/sx,0,0,0, 0,1/sy,0,0, 0,0,1/sz,0, 0,0,0,1};
}
inline float4x4 GetWorldToObjectMatrix(){ return W2O(); }
inline float4x4 GetObjectToWorldMatrix(){ return O2W(); }

// float4 arithmetic + remaining intrinsic overloads.
inline float4 operator+(float4 a,float4 b){return float4(a.x+b.x,a.y+b.y,a.z+b.z,a.w+b.w);}
inline float4 operator-(float4 a,float4 b){return float4(a.x-b.x,a.y-b.y,a.z-b.z,a.w-b.w);}
inline float4 operator*(float4 a,float s){return float4(a.x*s,a.y*s,a.z*s,a.w*s);}
inline float4 operator*(float s,float4 a){return a*s;}
inline float4 lerp(float4 a,float4 b,float t){return a+(b-a)*t;}
inline float3 max(float3 a,float3 b){return float3(max(a.x,b.x),max(a.y,b.y),max(a.z,b.z));}
inline float3 min(float3 a,float3 b){return float3(min(a.x,b.x),min(a.y,b.y),min(a.z,b.z));}
inline float3 exp(float3 v){return float3(std::exp(v.x),std::exp(v.y),std::exp(v.z));}
inline float3 operator+(float3 a,float s){return float3(a.x+s,a.y+s,a.z+s);}
inline float3 operator+(float s,float3 a){return a+s;}
inline float3 operator-(float3 a,float s){return float3(a.x-s,a.y-s,a.z-s);}
inline float3 operator-(float s,float3 a){return float3(s-a.x,s-a.y,s-a.z);}
"""

HARNESS = r"""
#include "hlsl_shim.hpp"
#include <cstdio>
#include "PrismClockAnimation.hlsl"

// The pre-velocity shatter, transcribed from the shipped expression before this pass.
// Every zero-velocity case must reproduce it exactly.
static float3 OldShatter(float clock,float t0,float dur,float dir,float off,
                         float3 pos,float3 nrm,float3 cent){
    if(dur<=0.0f) return pos;
    float p=saturate((clock-t0)/dur); float t=smoothstep(0.0f,1.0f,p);
    float sh=dir<0.0f?1.0f:0.0f;
    float fs=lerp(t,1.0f-t,sh); float o=sh*t*off;
    return cent+fs*(pos-cent)+o*nrm;
}
static bool near(float3 a,float3 b,float eps=1e-5f){
    return std::fabs(a.x-b.x)<eps&&std::fabs(a.y-b.y)<eps&&std::fabs(a.z-b.z)<eps;
}
int fails=0;
static void check(const char*what,bool ok){ printf("%-58s %s\n",what,ok?"ok":"FAIL"); if(!ok)fails++; }

int main(){
    SetObjectScale(1,1,1);
    float3 pos(0.7f,0.2f,-0.4f), nrm(0,0,1), cent(0.3f,0.1f,0.0f);
    float3 P;

    // 1. unstamped -> identity
    PrismShieldMorph_float(5,0,0,-1,3, float3(9,9,9), pos,nrm,cent, P);
    check("Duration<=0 passes the position through", near(P,pos));

    // 2. zero velocity shatter == the pre-change formula, at every t
    bool same=true;
    for(float c=0.0f;c<=0.6f;c+=0.05f){
        PrismShieldMorph_float(c,0,0.6f,-1,3, float3(0,0,0), pos,nrm,cent, P);
        same &= near(P, OldShatter(c,0,0.6f,-1,3,pos,nrm,cent));
    }
    check("zero-velocity shatter is byte-identical to the old puff", same);

    // 3. bloom ignores velocity entirely
    bool bloom=true;
    for(float c=0.0f;c<=0.35f;c+=0.05f){
        PrismShieldMorph_float(c,0,0.35f,1,0, float3(40,0,0), pos,nrm,cent, P);
        bloom &= near(P, OldShatter(c,0,0.35f,1,0,pos,nrm,cent));
    }
    check("engage bloom is unaffected by a stamped velocity", bloom);

    // 4. drift: at t=Duration the shard has moved exactly Velocity*Duration past the
    //    zero-velocity result, when the impulse is along the face normal (no tumble).
    float3 vAlongN(0,0,20);
    PrismShieldMorph_float(0.6f,0,0.6f,-1,3, vAlongN, pos,nrm,cent, P);
    float3 baseline = OldShatter(0.6f,0,0.6f,-1,3,pos,nrm,cent);
    check("drift == Velocity*Duration when impulse is along the normal",
          near(P, baseline + vAlongN*0.6f, 1e-4f));

    // 5. tumble: a cross-wise impulse rotates the face about normalize(cross(v,n))
    //    THROUGH ITS OWN CENTROID, by SPIN*|v|*t.
    float3 vCross(20,0,0);
    PrismShieldMorph_float(0.3f,0,0.6f,-1,3, vCross, pos,nrm,cent, P);
    float ang = PRISM_SHIELD_SHATTER_SPIN*20.0f*0.3f;
    float3 axis = normalize(cross(vCross,nrm));
    float3 expectRel = PrismJiggleRotate(
        (OldShatter(0.3f,0,0.6f,-1,3,pos,nrm,cent) + vCross*0.3f) - cent, axis, ang);
    check("tumble rotates position about the face centroid", near(P, cent+expectRel, 1e-4f));

    // 6. non-uniform scale: rotation runs in the isotropic frame, so a face on a long
    //    thin trail slab must not shear.
    SetObjectScale(1,1,8);
    PrismShieldMorph_float(0.3f,0,0.6f,-1,3, vCross, pos,nrm,cent, P);
    float3 s(1,1,8);
    float3 vObj = float3(vCross.x/1, vCross.y/1, vCross.z/8);
    float3 axis2 = normalize(cross(vObj,nrm));
    float3 pre = OldShatter(0.3f,0,0.6f,-1,3,pos,nrm,cent) + vObj*0.3f;
    float3 expect2 = cent + PrismJiggleRotate((pre-cent)*s, axis2, ang)/s;
    check("non-uniform scale rotates in the isotropic frame", near(P,expect2,1e-4f));

    // 7. an impulse straight along the face normal must not tumble that face (cross ~ 0)
    SetObjectScale(1,1,1);
    PrismShieldMorph_float(0.3f,0,0.6f,-1,3, vAlongN, pos,nrm,cent, P);
    check("a face struck dead-on is pushed, not tumbled",
          near(P, OldShatter(0.3f,0,0.6f,-1,3,pos,nrm,cent) + vAlongN*0.3f, 1e-4f));

    // 8. degenerate object frame (a prism still at localScale 0) bails safely
    SetObjectScale(1e-9f,1e-9f,1e-9f);
    PrismShieldMorph_float(0.3f,0,0.6f,-1,3, vCross, pos,nrm,cent, P);
    check("degenerate object scale leaves the plain puff",
          near(P, OldShatter(0.3f,0,0.6f,-1,3,pos,nrm,cent)));

    // 9. clock past the end freezes rather than flying away forever
    SetObjectScale(1,1,1);
    float3 A,B;
    PrismShieldMorph_float(0.6f,0,0.6f,-1,3, vCross, pos,nrm,cent, A);
    PrismShieldMorph_float(60.0f,0,0.6f,-1,3, vCross, pos,nrm,cent, B);
    check("a stamp that outlives its retirement freezes at Duration", near(A,B));

    // 10. a NaN velocity must not poison the shard
    SetObjectScale(1,1,1);
    float nan = std::nanf("");
    PrismShieldMorph_float(0.3f,0,0.6f,-1,3, float3(nan,nan,nan), pos,nrm,cent, P);
    check("a NaN velocity falls back to the plain puff",
          near(P, OldShatter(0.3f,0,0.6f,-1,3,pos,nrm,cent)));

    printf("\n%s\n", fails? "FAILURES" : "all checks passed");
    return fails?1:0;
}
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl"


def main():
    compiler = shutil.which("clang++") or shutil.which("g++")
    if not compiler:
        print("clang++/g++ not found - cannot verify the shipped HLSL numerically.")
        return 2

    src = os.path.join(REPO, HLSL)
    if not os.path.exists(src):
        print("missing " + HLSL)
        return 1

    text = open(src, encoding="utf-8").read()
    # HLSL -> C++, mechanically (see the module docstring).
    text = text.replace(".yzx", ".yzx()")
    text = re.sub(r"\bout (float[34]?) ", r"\1& ", text)

    with tempfile.TemporaryDirectory() as tmp:
        open(os.path.join(tmp, "hlsl_shim.hpp"), "w", encoding="utf-8").write(SHIM)
        open(os.path.join(tmp, "PrismClockAnimation.hlsl"), "w", encoding="utf-8").write(text)
        open(os.path.join(tmp, "harness.cpp"), "w", encoding="utf-8").write(HARNESS)

        exe = os.path.join(tmp, "harness")
        build = subprocess.run([compiler, "-std=c++17", "-O0", "-I", tmp,
                                os.path.join(tmp, "harness.cpp"), "-o", exe],
                               capture_output=True, text=True)
        if build.returncode != 0:
            print("the shipped HLSL does not compile:")
            print(build.stderr[:4000])
            return 1

        run = subprocess.run([exe], capture_output=True, text=True)
        print(run.stdout, end="")
        if run.stderr:
            print(run.stderr, end="")
        return run.returncode


if __name__ == "__main__":
    sys.exit(main())
