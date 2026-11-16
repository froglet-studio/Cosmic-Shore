// SolitonTunnelStandin.hlsl - the Soliton wall bend WITHOUT the march (lens worker, 2026-11-10). Included by
// SolitonTunnel.shader under the _MW_STANDIN keyword, after the shipped declarations and helpers (MWWobble, the uniforms).
//
// THE IDEA. The march integrates, along the pixel's ray, a pull along the tube (BEND), the shell's gradient (LENS) and
// the layer's weights through a thin shell graded round the tube's centreline. For a ray that crosses a locally straight
// tube those integrals depend only on two numbers: beta = (the ray's closest approach to the centreline) / (the tube's
// radius there), and sin(gamma), the sine of the angle between the ray and the tube. They are baked ONCE into a 64x32
// RGBA LUT (_MWShellLut, SolitonTunnelShellLut.cs, from the look's WallInside / WallOutside / RimTightness):
//   R  the pull along the tube, both walls      G  the flare's pull out from the axis
//   B  how much shell (glow / tint weight)      A  the rim weight             (all x sin(gamma))
// Per pixel and tube: one ray-to-centreline closest approach (24 segments, no march), one LUT fetch, the shipped wobble,
// fade, flare and glint. The lens term cancels across a thin shell (in through the outer half, out through the inner)
// and is left out; the measured residual is in the README (lens/).
//
// LIMITS. A ray running along a tube (looking down its throat from the mouth, or riding inside) is not a "crossing":
// the stand-in fades itself out as sin(gamma) falls below 0.25 and the shipped mouth lens / throat stretch carry that
// view; riding a tube (_MWView.y > 0) should keep the march (the C# switches the keyword off while riding).

#ifndef MW_SHELL_LUT
TEXTURE2D(_MWShellLut);
#define MW_SHELL_LUT(uv) SAMPLE_TEXTURE2D_LOD(_MWShellLut, sampler_LinearClamp, uv, 0)
#endif
// beta range of the LUT (SolitonTunnelShellLut.BetaMax); past it a ray misses the shell.
#define MW_LUT_BMAX 1.3
// past a mouth the shipped shell wraps round the lip: the walls fade out over this share of the radius
#ifndef MW_LIP
#define MW_LIP 0.2
#endif

// The ray o + u d (local frame) against tube i's centreline: closest approach. Out: s along it, the centreline point
// c, its heading T, and u along the ray. A tunnel's points live in the x-z plane (x, z = path.x, path.y; s = path.z);
// a network channel's are xyz with s at even steps.
float MWRayNearest(int i, float3 o, float3 d, out float s, out float3 c, out float3 T, out float u)
{
    int base0 = i * MEERKAT_LENS_PATH;
    bool chan = _MWTubeShape[i].y > 0.5;
    float seg = max(_MWTubeShape[i].x, 1e-3) / (MEERKAT_LENS_PATH - 1);
    float best = 1e30;
    s = 0.0; c = float3(0.0, 0.0, 0.0); T = float3(0.0, 0.0, 1.0); u = 0.0;
    float4 pa = _MWTubePath[base0];
    float3 a3 = chan ? pa.xyz : float3(pa.x, 0.0, pa.y);
    for (int j = 0; j < MEERKAT_LENS_PATH - 1; j++)
    {
        float4 pb = _MWTubePath[base0 + j + 1];
        float3 b3 = chan ? pb.xyz : float3(pb.x, 0.0, pb.y);
        float3 ab = b3 - a3, w0 = a3 - o;
        float bb = dot(d, ab), cc = max(dot(ab, ab), 1e-8), dd = dot(d, w0), e = dot(ab, w0);
        float den = cc - bb * bb;
        // past the way in or out the centreline runs on along the end's heading (as the shipped MWTube)
        float lo = j == 0 ? -4.0 : 0.0, hi = j == MEERKAT_LENS_PATH - 2 ? 5.0 : 1.0;
        float t = den > 1e-6 * cc ? clamp((dd * bb - e) / den, lo, hi) : 0.0;
        float uu = max(t * bb + dd, 0.0);
        t = clamp((dot(o + d * uu - a3, ab)) / cc, lo, hi);
        float3 q = a3 + ab * t;
        float3 r = o + d * uu - q;
        float dist2 = dot(r, r);
        if (dist2 < best)
        {
            best = dist2;
            s = chan ? (j + t) * seg : lerp(pa.z, pb.z, t);
            c = q; T = ab * rsqrt(cc); u = uu;
        }
        pa = pb; a3 = b3;
    }
    return sqrt(best);
}

// The tube's radius at s and its slope (the shipped hyperboloid; a channel's from its points).
float MWRadiusAt(int i, float s, out float drds)
{
    float L = max(_MWTubeShape[i].x, 1e-3), s0 = _MWTubeWin[i].x, s1 = _MWTubeWin[i].y;
    float span = max(1e-3, s1 - s0), sc = clamp(s, s0, s1);
    if (_MWTubeShape[i].y > 0.5)
    {
        float fj = saturate(sc / L) * (MEERKAT_LENS_PATH - 1);
        int jj = min((int)fj, MEERKAT_LENS_PATH - 2);
        float4 qa = _MWTubePath[i * MEERKAT_LENS_PATH + jj], qb = _MWTubePath[i * MEERKAT_LENS_PATH + jj + 1];
        drds = (qb.w - qa.w) / max(L / (MEERKAT_LENS_PATH - 1), 1e-3);
        return lerp(qa.w, qb.w, fj - jj);
    }
    float k = min(1.0, span / L), bt = _MWTubeShape[i].w * k, M = max(bt, _MWTubeWin[i].w * k), w = 2.0 * (sc - s0) / span - 1.0;
    float r = sqrt(bt * bt + (M * M - bt * bt) * w * w);
    drds = (M * M - bt * bt) * w * 2.0 / (span * max(r, 1e-4));
    return r;
}

float2 MWContourStandin(float2 p, float sceneZ)
{
    g_mwLayer.a = float4(0.0, 0.0, 0.0, 0.0); g_mwLayer.u = 0.0; g_mwLayer.phi = 0.0;
    g_mwLayer.tint = float4(0.0, 0.0, 0.0, 0.0);
    int n = (int)_MWView.w;
    float3 camF = _MWCamF.xyz, ro = _MWCamPos.xyz;
    float th2 = 2.0 * _MWCamPos.w;
    float3 rd = normalize(camF + _MWCamR.xyz * (p.x * th2) + _MWCamU.xyz * (p.y * th2));
    float3 bend = float3(0.0, 0.0, 0.0);
    float entry = 1e20;
    float3 hv = normalize(_MWLightDir.xyz - rd);
    for (int i = 0; i < MEERKAT_LENS_MAX_TUBES; i++)
    {
        if (i >= n) break;
        float3 oc = ro - _MWTubeBound[i].xyz;
        float bq = dot(oc, rd), cq = dot(oc, oc) - _MWTubeBound[i].w * _MWTubeBound[i].w;
        if (bq * bq - cq < 0.0 || (cq > 0.0 && bq > 0.0)) continue;
        float3 o = mul(_MWTubeW2L[i], float4(ro, 1.0)).xyz;
        float3 d = mul(_MWTubeW2L[i], float4(rd, 0.0)).xyz;
        float s, u; float3 c, T;
        float b = MWRayNearest(i, o, d, s, c, T, u);
        float s0 = _MWTubeWin[i].x, s1 = _MWTubeWin[i].y;
        float sg = length(cross(d, T));
        float along = smoothstep(0.15, 0.3, sg);   // a ray along the tube is not a crossing (see LIMITS)
        if (along <= 0.0) continue;
        float cot = dot(d, T) / max(sg, 0.03);
        float3 rad = o + d * u - c;
        float rl = length(rad);
        float3 eb = rl > 1e-5 ? rad / rl : float3(1.0, 0.0, 0.0);
        float3 dp = d - dot(d, T) * T; float dpl = length(dp); dp = dpl > 1e-5 ? dp / dpl : float3(0.0, 0.0, 0.0);
        int kk = MWKind(i);
        float4 l0 = _MWLook[kk], l1 = _MWLook[kk + 1];
        float drds0;
        float r0 = MWRadiusAt(i, s, drds0);
        // the two walls the ray crosses, near (k = 0) and far (k = 1): where along the tube, the radius there, and
        // whether that point is inside the live window (past a mouth the ray crosses no wall)
        for (int k = 0; k < 2; k++)
        {
            float sgn = k == 0 ? -1.0 : 1.0, drds;
            float sk = s + sgn * sqrt(max(r0 * r0 - b * b, 0.0)) * cot;
            float rk = MWRadiusAt(i, sk, drds);
            sk = s + sgn * sqrt(max(rk * rk - b * b, 0.0)) * cot;
            rk = MWRadiusAt(i, sk, drds);
            float beta = b / max(rk, 1e-4);
            float win = (1.0 - smoothstep(s1, s1 + MW_LIP * rk, sk)) * smoothstep(s0 - MW_LIP * rk, s0, sk);
            if (beta >= MW_LUT_BMAX || win <= 0.0) continue;
            float4 lut = MW_SHELL_LUT(float2(beta / MW_LUT_BMAX, sg)) * (0.5 / max(sg, 0.03));
            float lc = sqrt(max(0.0, 1.0 - beta * beta));
            float3 nl = eb * min(beta, 1.0) + dp * (sgn * lc);   // the wall's normal where the ray crosses it
            MWTQ q;
            q.u = (clamp(sk, s0, s1) - s0) / max(1e-3, s1 - s0);
            q.phi = atan2(nl.y, dot(nl, float3(T.z, 0.0, -T.x)));
            float shape = MWWobble(q, l1, l1.x) * _MWTubeWin[i].z * along * win;
            // the pull: along the tube and out from the axis by the flare, as the shipped MWFieldDir
            float3 Fl = (T * lut.x + eb * (drds * lut.y)) * (rsqrt(1.0 + drds * drds) * l0.x * shape);
            float3 Fw = MWToWorld(i, Fl);
            bend += Fw - dot(Fw, rd) * rd;
            float lw = lut.z * shape;
            float3 nW = MWToWorld(i, nl);
            float glint = pow(saturate(abs(dot(nW, hv))), 48.0);
            float network = kk > 0 ? 1.0 : 0.0;
            g_mwLayer.a += float4(lw, lut.w * shape, lw * glint, lw * network);
            float4 tt = _MWTubeTint[i];
            g_mwLayer.tint += lw * float4(tt.rgb * tt.a, tt.a);
            if (k == 0 || entry > 1e19) entry = min(entry, (u + sgn * lc * rk / max(sg, 0.03)) * dot(rd, camF));
        }
    }
    if (entry > 1e19 || sceneZ < entry - 0.5)
    {
        g_mwLayer.a = float4(0.0, 0.0, 0.0, 0.0);
        g_mwLayer.tint = float4(0.0, 0.0, 0.0, 0.0);
        return float2(0.0, 0.0);
    }
    float3 dir = normalize(rd + bend);
    float fz = max(dot(dir, camF), 1e-3);
    float2 landed = float2(dot(dir, _MWCamR.xyz), dot(dir, _MWCamU.xyz)) / (fz * th2);
    float2 off = landed - p;
    float len = length(off);
    return len > 2.0 ? off * (2.0 / len) : off;
}
