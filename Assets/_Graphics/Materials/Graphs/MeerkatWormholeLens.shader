// MeerkatWormholeLens.shader - THE wormhole look (2026-10-29, Yash: "just keep one high quality look"), drawn in ONE
// full-screen pass by MeerkatWormholeLensPass.cs after the transparents, from its copy of the camera colour
// (_MeerkatSceneColor). Every formed tunnel and every channel of an omnicrystal's network is only bent space:
//   - the contour field (Garrett, 2026-10-27, the Tube Lens Lab, Docs/Studios/StoatTubeLensLab.html, with the water-pipe
//     numbers chosen in the artifact): each pixel's ray is marched through a thin shell on every nearby tube's wall, graded
//     to zero at the shell's edge, and turned there by a field whose lines on the wall are the tube's own contours (along
//     it at pitch 0, round its rings at 90); the pixel shows where the turned ray lands. The tube is measured over its LIVE
//     window [s0, s1] (MeerkatWormholeMath.Radius) so the field forms and collapses with it;
//   - the OUTER LAYER: the shell's optical depth along the bent ray (graded by the profile, faintly by the ribs) tints the
//     world behind it a watery white and glows faintly, so the wall reads as a thin layer of water-glass (_MWHQ0.x/.w);
//   - a Fresnel RIM where the ray grazes the shell, and a GLINT of the main light off it (_MWHQ0.yz, _MWHQ1.w);
//   - DISPERSION: red, green and blue land at slightly different places, as through real glass (_MWHQ2.x);
//   - a NETWORK channel (_MWTubeShape.y 1) adds a thin-film iridescence round it, pulses of light running along it and a
//     stronger dispersion and glow (_MWHQ2.yzw, _MWHQ3).
// A ray bent off the screen shows the screen mirrored at its edge. A vessel in front of a tube is neither bent nor tinted.
// Every other wormhole shader (the warp tube, the mouth dents, the collapse ripple, the calm wall, the water tubes, the
// tree, the roots, the bridge's glass) was removed on 2026-10-29: Docs/Shaders/WORMHOLE_SHADERS.md §12 restores any of them.
Shader "CosmicShore/MeerkatWormholeLens"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "MeerkatWormholeLens"
            Tags { "LightMode" = "MeerkatWormholeLens" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MeerkatLensVert
            #pragma fragment MeerkatLensFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MEERKAT_LENS_MAX_TUBES 4
            #define MEERKAT_LENS_PATH 25
            #define MEERKAT_FIELD_STEPS 256
            #define MW_PI 3.14159265

            // x the camera's aspect, w contour tubes in the bank (y, z unused)
            float4 _MWView;

            // The contour field (MeerkatWormholeLensPass.ContourTubesFor): per tube, world to its own frame (the way in at the
            // origin, its first heading +z, its bend toward +x), its shape (L, -, theta, throat), its live window (s0, s1,
            // fade, mouth), a bounding sphere (world centre, radius), and (round 22: one conic arc bending round its middle,
            // MeerkatWormholeMath.Conic) its centreline as MEERKAT_LENS_PATH points at even steps of s: (x across, z ahead,
            // s, how far it has turned). Measured against the exact arc (a brute-force search over 400 random tubes and
            // points): within 0.07 u.
            float4x4 _MWTubeW2L[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeShape[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeWin[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeBound[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubePath[MEERKAT_LENS_MAX_TUBES * MEERKAT_LENS_PATH];
            float4 _MWTubeNorm[MEERKAT_LENS_MAX_TUBES * MEERKAT_LENS_PATH];   // round 26: a channel's carried normal per point
            float4 _MWField0;   // strength (rad per wall crossing), pitch (rad), drag, twist
            float4 _MWField1;   // glass, glass index, grazing, flow (0 even, 1 in then out, 2 throat)
            float4 _MWField2;   // inner, outer (x local radius), profile (0 smooth, 1 smoother, 2 linear, 3 bell), ribs
            float4 _MWField3;   // rib sharpness, rib phase, rib rings, rib lines
            float4 _MWCamPos;   // xyz the eye, w tan(fov / 2)
            float4 _MWCamR;
            float4 _MWCamU;
            float4 _MWCamF;
            // The look (MeerkatWormholeHQ.Look): 0 outer layer, rim, rim power, absorb; 1 tint rgb, glint; 2 dispersion, network
            // dispersion, iridescence, pulses; 3 iridescence scale, pulses along a channel, pulse speed, network glow
            float4 _MWHQ0;
            float4 _MWHQ1;
            float4 _MWHQ2;
            float4 _MWHQ3;
            float4 _MWLightDir;   // xyz toward the main light, w its strength (0: no glint)

            TEXTURE2D(_MeerkatSceneColor);

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            // ---- the contour field ----
            struct MWTQ { float d; float u; float phi; float r; float drds; float scale; float3 n; float3 T; };

            // The wall of tube i near world point pw: signed distance (- inside, + outside; past a live mouth, the distance to
            // its lip), the way along its live window u, the angle round it, its radius and slope, and its frame (local).
            // Round 26: a channel of an omnicrystal's network (Shape.y 1) - a 3D polyline at even steps of s, each point
            // (x, y, z, radius) in its own frame (the world's, moved to its source), its carried normal in _MWTubeNorm.
            MWTQ MWChannel(int i, float3 p, float L, float s0, float s1)
            {
                MWTQ q;
                int base0 = i * MEERKAT_LENS_PATH;
                float seg = L / (MEERKAT_LENS_PATH - 1);
                float best = 1e30, s = 0.0;
                for (int j = 0; j < MEERKAT_LENS_PATH - 1; j++)
                {
                    float4 pa = _MWTubePath[base0 + j], pb = _MWTubePath[base0 + j + 1];
                    float3 ab = pb.xyz - pa.xyz;
                    float ll = max(dot(ab, ab), 1e-6), t = dot(p - pa.xyz, ab) / ll;
                    float tc = saturate(t);
                    float3 cc = pa.xyz + ab * tc;
                    float dd = dot(p - cc, p - cc);
                    if (dd < best)
                    {
                        best = dd;
                        float tu = (j == 0 && t < 0.0) || (j == MEERKAT_LENS_PATH - 2 && t > 1.0) ? t : tc;
                        s = (j + tu) * seg;
                    }
                }
                float sc = clamp(s, s0, s1), fj = saturate(sc / L) * (MEERKAT_LENS_PATH - 1);
                int jj = min((int)fj, MEERKAT_LENS_PATH - 2);
                float tj = fj - jj;
                float4 qa = _MWTubePath[base0 + jj], qb = _MWTubePath[base0 + jj + 1];
                float3 c = lerp(qa.xyz, qb.xyz, tj), dT = qb.xyz - qa.xyz;
                float3 T = dot(dT, dT) > 1e-10 ? normalize(dT) : float3(0.0, 0.0, 1.0);
                float3 N = lerp(_MWTubeNorm[base0 + jj].xyz, _MWTubeNorm[base0 + jj + 1].xyz, tj);
                N = N - dot(N, T) * T;
                N = dot(N, N) > 1e-10 ? normalize(N) : normalize(cross(T, abs(T.y) < 0.9 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0)));
                float3 Bn = cross(T, N);
                float span = max(1e-3, s1 - s0);
                q.r = lerp(qa.w, qb.w, tj);
                q.drds = (qb.w - qa.w) / max(seg, 1e-3);
                q.u = (sc - s0) / span;
                float3 vv = p - c; float h = dot(vv, T); float3 vr = vv - h * T; float rho = length(vr);
                q.n = rho > 1e-5 ? vr / rho : N;
                q.d = (s >= s0 && s <= s1) ? rho - q.r : length(float2(h, rho - q.r));
                q.T = T;
                q.phi = atan2(dot(q.n, Bn), dot(q.n, N));
                q.scale = 1.0;   // the polyline carries no bend radius: the inside of a bend is not shortened
                return q;
            }

            MWTQ MWTube(int i, float3 pw)
            {
                MWTQ q;
                float3 p = mul(_MWTubeW2L[i], float4(pw, 1.0)).xyz;
                float L = max(_MWTubeShape[i].x, 1e-3), theta = _MWTubeShape[i].z;
                float b0 = _MWTubeShape[i].w, m0 = _MWTubeWin[i].w;
                float s0 = _MWTubeWin[i].x, s1 = _MWTubeWin[i].y;
                if (_MWTubeShape[i].y > 0.5) return MWChannel(i, p, L, s0, s1);
                // round 22: the nearest point on the centreline (the arc's polyline, in the x-z plane); past the way in or out,
                // s runs on along the end's heading
                int base0 = i * MEERKAT_LENS_PATH;
                float best = 1e30, s = 0.0;
                for (int j = 0; j < MEERKAT_LENS_PATH - 1; j++)
                {
                    float4 pa = _MWTubePath[base0 + j], pb = _MWTubePath[base0 + j + 1];
                    float3 a3 = float3(pa.x, 0.0, pa.y), ab = float3(pb.x - pa.x, 0.0, pb.y - pa.y);
                    float ll = max(dot(ab, ab), 1e-6), t = dot(p - a3, ab) / ll;
                    float tc = saturate(t);
                    float3 cc = a3 + ab * tc;
                    float dd = dot(p - cc, p - cc);
                    if (dd < best)
                    {
                        best = dd;
                        float tu = (j == 0 && t < 0.0) || (j == MEERKAT_LENS_PATH - 2 && t > 1.0) ? t : tc;
                        s = lerp(pa.z, pb.z, tu);
                    }
                }
                // the point at s clamped to the live window, its heading and how sharply it turns there
                float sc = clamp(s, s0, s1), fj = saturate(sc / L) * (MEERKAT_LENS_PATH - 1);
                int jj = min((int)fj, MEERKAT_LENS_PATH - 2);
                float tj = fj - jj;
                float4 qa = _MWTubePath[base0 + jj], qb = _MWTubePath[base0 + jj + 1];
                float dTurn = qb.w - qa.w, ac = qa.w + dTurn * tj;
                float invR = dTurn / max(qb.z - qa.z, 1e-3);   // the turn per unit length (1 / radius)
                // between two points the centreline is a circle's arc from the first's heading to the second's (the chord
                // alone would cut inside the curve)
                float3 c = abs(dTurn) > 1e-5
                    ? float3(qa.x + (cos(qa.w) - cos(ac)) / invR, 0.0, qa.y + (sin(ac) - sin(qa.w)) / invR)
                    : float3(lerp(qa.x, qb.x, tj), 0.0, lerp(qa.y, qb.y, tj));
                float3 T = float3(sin(ac), 0.0, cos(ac)), N = float3(cos(ac), 0.0, -sin(ac));
                // MeerkatWormholeMath.Radius: a hyperboloid over the live window, narrower while the window is short
                float span = max(1e-3, s1 - s0), k = min(1.0, span / L), b = b0 * k, M = max(b, m0 * k), w = 2.0 * (sc - s0) / span - 1.0;
                q.r = sqrt(b * b + (M * M - b * b) * w * w);
                q.drds = (M * M - b * b) * w * 2.0 / (span * max(q.r, 1e-4));
                q.u = (sc - s0) / span;
                float3 vv = p - c; float h = dot(vv, T); float3 vr = vv - h * T; float rho = length(vr);
                q.n = rho > 1e-5 ? vr / rho : N;
                q.d = (s >= s0 && s <= s1) ? rho - q.r : length(float2(h, rho - q.r));
                q.T = T;
                float cn = dot(q.n, N);
                q.phi = atan2(q.n.y, cn);
                q.scale = 1.0 - q.r * cn * invR;   // the inside of a bend is shorter than the outside
                return q;
            }
            float3 MWToWorld(int i, float3 v) { return mul(v, (float3x3)_MWTubeW2L[i]); }
            float MWShellTh(MWTQ q) { return (q.d > 0.0 ? _MWField2.y : _MWField2.x) * q.r; }
            float MWProf(float t)
            {
                t = saturate(t);
                if (_MWField2.z < 0.5) return t * t * (3.0 - 2.0 * t);
                if (_MWField2.z < 1.5) return t * t * t * (t * (6.0 * t - 15.0) + 10.0);
                if (_MWField2.z < 2.5) return t;
                float e = exp(-9.0);
                return (exp(-9.0 * (1.0 - t) * (1.0 - t)) - e) / (1.0 - e);
            }
            float MWShellW(MWTQ q) { float th = MWShellTh(q); return th <= 1e-4 ? 0.0 : MWProf(1.0 - abs(q.d) / th); }
            float MWFlow(float u) { return _MWField1.w < 0.5 ? 1.0 : (_MWField1.w < 1.5 ? cos(MW_PI * u) : sin(MW_PI * u)); }
            float MWLineP(float x) { return pow(0.5 + 0.5 * cos(2.0 * MW_PI * x), _MWField3.x); }
            float MWRib(MWTQ q)
            {
                if (_MWField2.w <= 0.0) return 1.0;
                float a = MWLineP(q.u * (_MWField3.z - 1.0) - _MWField3.y), b = MWLineP(q.phi / (2.0 * MW_PI) * _MWField3.w);
                return lerp(1.0, max(a, b), _MWField2.w);
            }
            float MWN(int i, float3 pw) { MWTQ q = MWTube(i, pw); return MWShellW(q) * MWRib(q) * MWFlow(q.u) * _MWTubeWin[i].z; }
            // The field on the wall (local): T the long grid line's own tangent (tilted by the flare and the bend), Phi the ring's.
            float3 MWFieldDir(MWTQ q)
            {
                float3 ts = normalize(q.T * q.scale + q.drds * q.n);
                float3 ph = normalize(cross(ts, q.n));
                return cos(_MWField0.y) * ts + sin(_MWField0.y) * ph;
            }

            // The screen offset the contour field gives this pixel (p: screen heights from the centre, y up), zero when its ray
            // meets no shell or an opaque thing stands in front of the first shell it meets.
            // The layer: what the bent ray met on its way through the shells: x optical depth (shell thicknesses crossed, graded),
            // y the rim (the same, weighted by how much it grazed), z the glint, w how much of it was a network channel;
            // u the channel's way along (weighted), phi its angle round.
            struct MWLayer { float4 a; float u; float phi; };
            static MWLayer g_mwLayer;

            float2 MWContour(float2 p, float sceneZ)
            {
                g_mwLayer.a = float4(0.0, 0.0, 0.0, 0.0); g_mwLayer.u = 0.0; g_mwLayer.phi = 0.0;
                int n = (int)_MWView.w;
                float3 camF = _MWCamF.xyz, ro = _MWCamPos.xyz;
                float th2 = 2.0 * _MWCamPos.w;
                float3 rd = normalize(camF + _MWCamR.xyz * (p.x * th2) + _MWCamU.xyz * (p.y * th2));
                float tStart = 1e20;
                for (int i = 0; i < MEERKAT_LENS_MAX_TUBES; i++)
                {
                    if (i >= n) break;
                    float3 oc = ro - _MWTubeBound[i].xyz;
                    float b = dot(oc, rd), cc = dot(oc, oc) - _MWTubeBound[i].w * _MWTubeBound[i].w, disc = b * b - cc;
                    if (disc < 0.0 || (cc > 0.0 && b > 0.0)) continue;
                    tStart = min(tStart, max(0.0, -b - sqrt(disc)));
                }
                if (tStart > 1e19) return float2(0.0, 0.0);
                float3 pos = ro + rd * tStart, dir = rd;
                float travel = tStart, entry = -1.0;
                float inner = _MWField2.x, outer = _MWField2.y, graze = _MWField1.z;
                [loop]
                for (int it = 0; it < MEERKAT_FIELD_STEPS; it++)
                {
                    float stp = 1e20;
                    float3 dd = float3(0.0, 0.0, 0.0);
                    bool inShell = false;
                    float4 la = float4(0.0, 0.0, 0.0, 0.0); float lu = 0.0, lphi = 0.0;
                    for (int j = 0; j < MEERKAT_LENS_MAX_TUBES; j++)
                    {
                        if (j >= n) break;
                        MWTQ q = MWTube(j, pos);
                        float th = MWShellTh(q), ad = abs(q.d), hmin = max(max(0.02, 0.008 * q.r), 0.2 * th);
                        if (ad < th)
                        {
                            inShell = true;
                            float3 nW = MWToWorld(j, q.n);
                            float across = max(abs(dot(dir, nW)), 0.12);   // grazing: the field barely changes along the wall
                            float sj = clamp(th / (7.0 * across), 0.02, max(th * 1.2, 0.02));
                            if (_MWField2.w > 0.0)
                                sj = min(sj, (_MWTubeWin[j].y - _MWTubeWin[j].x) / (_MWField3.z - 1.0) / (2.0 * _MWField3.x + 4.0));
                            stp = min(stp, sj);
                            float wgt = MWShellW(q) * MWRib(q) * MWFlow(q.u) * _MWTubeWin[j].z;
                            float3 F = MWToWorld(j, MWFieldDir(q));
                            float kk = _MWField0.x / max(0.5 * (inner + outer) * q.r, 1e-3) * lerp(1.0, across, graze);
                            dd += (_MWField0.z * (F - dot(F, dir) * dir) + _MWField0.w * cross(dir, F)) * kk * wgt;
                            if (_MWField1.x > 0.0 && _MWField1.y > 0.0)
                            {
                                float e = max(0.15 * th, 0.02);
                                float2 kv = float2(1.0, -1.0);
                                float3 g = kv.xyy * MWN(j, pos + kv.xyy * e) + kv.yyx * MWN(j, pos + kv.yyx * e) +
                                           kv.yxy * MWN(j, pos + kv.yxy * e) + kv.xxx * MWN(j, pos + kv.xxx * e);
                                g /= 4.0 * e;
                                dd += _MWField1.x * _MWField1.y * lerp(1.0, across, graze) * (g - dot(g, dir) * dir);
                            }
                            {
                                // the layer: graded like the field, the ribs only faintly (the water, not the grid)
                                float lw = MWShellW(q) * (0.6 + 0.4 * MWRib(q)) * _MWTubeWin[j].z / max(th, 1e-3);
                                float gr = 1.0 - abs(dot(dir, nW));
                                float3 hv = normalize(_MWLightDir.xyz - dir);
                                float ch = _MWTubeShape[j].y > 0.5 ? 1.0 : 0.0;
                                la += lw * float4(1.0, pow(max(gr, 0.0), _MWHQ0.z), pow(saturate(abs(dot(nW, hv))), 48.0), ch);
                                lu += lw * ch * q.u; lphi += lw * ch * q.phi;
                            }
                        }
                        else stp = min(stp, max((ad - th) * 0.85, hmin));
                    }
                    if (inShell)
                    {
                        if (entry < 0.0) entry = travel * dot(rd, camF);
                        dir = normalize(dir + dd * stp);
                        if (travel * dot(rd, camF) < sceneZ)   // the layer behind an opaque thing is hidden by it
                        {
                            g_mwLayer.a += la * stp; g_mwLayer.u += lu * stp; g_mwLayer.phi += lphi * stp;
                        }
                    }
                    pos += dir * stp; travel += stp;
                    bool done = true;   // outside every tube's bounds, heading away from each
                    for (int m = 0; m < MEERKAT_LENS_MAX_TUBES; m++)
                    {
                        if (m >= n) break;
                        float3 rel = pos - _MWTubeBound[m].xyz;
                        if (dot(rel, rel) <= _MWTubeBound[m].w * _MWTubeBound[m].w || dot(rel, dir) <= 0.0) done = false;
                    }
                    if (done) break;
                }
                if (entry < 0.0 || sceneZ < entry - 0.5)   // no shell met, or something opaque in front of it
                {
                    g_mwLayer.a = float4(0.0, 0.0, 0.0, 0.0);
                    return float2(0.0, 0.0);
                }
                float fz = max(dot(dir, camF), 1e-3);
                float2 landed = float2(dot(dir, _MWCamR.xyz), dot(dir, _MWCamU.xyz)) / (fz * th2);
                float2 off = landed - p;
                float len = length(off);
                return len > 2.0 ? off * (2.0 / len) : off;
            }

            float3 MWScene(float2 s, float aspect)
            {
                float2 suv = s / float2(aspect, 1.0) + 0.5;
                suv = 1.0 - abs(1.0 - abs(suv));
                return SAMPLE_TEXTURE2D_LOD(_MeerkatSceneColor, sampler_LinearClamp, suv, 0).rgb;
            }

            // The bent scene through glass that disperses, the outer layer's faint water tint and glow, its rim and glint,
            // and a network channel's iridescence and running light.
            half4 MWHighQuality(float2 p, float2 disp, float aspect)
            {
                float4 L = g_mwLayer.a;
                float od = L.x;
                if (dot(disp, disp) < 1e-12 && od < 1e-4) discard;
                float layer = 1.0 - exp(-od);                    // 0 clear .. 1 a thick crossing
                float net = od > 1e-4 ? saturate(L.w / od) : 0.0;  // the share of it that was a network channel
                float rim = 1.0 - exp(-L.y), glint = 1.0 - exp(-L.z);
                float k = lerp(_MWHQ2.x, _MWHQ2.y, net);
                float3 col;
                if (k > 1e-4)
                {
                    col.r = MWScene(p + disp * (1.0 + k), aspect).r;
                    col.g = MWScene(p + disp, aspect).g;
                    col.b = MWScene(p + disp * (1.0 - k), aspect).b;
                }
                else col = MWScene(p + disp, aspect);
                float3 tint = _MWHQ1.rgb;
                float glow = _MWHQ0.x;
                if (net > 0.0)
                {
                    float u = L.w > 1e-5 ? g_mwLayer.u / L.w : 0.0, phi = L.w > 1e-5 ? g_mwLayer.phi / L.w : 0.0;
                    float x = phi / (2.0 * MW_PI) * _MWHQ3.x + u + 0.05 * _Time.y;
                    float3 iri = 0.6 + 0.4 * cos(2.0 * MW_PI * (x + float3(0.0, 0.33, 0.67)));
                    tint = lerp(tint, iri, _MWHQ2.z * net);
                    float pulse = pow(saturate(0.5 + 0.5 * cos(2.0 * MW_PI * (u * _MWHQ3.y - _Time.y * _MWHQ3.z))), 12.0);
                    glow = lerp(glow, glow * _MWHQ3.w, net) + _MWHQ2.w * pulse * net;
                }
                col = lerp(col, col * tint, saturate(layer * _MWHQ0.w));             // the water's faint absorption
                col += tint * (glow * layer + _MWHQ0.y * rim);                        // the layer and its rim
                col += _MWLightDir.w * _MWHQ1.w * glint * float3(1.0, 0.98, 0.95);    // the light off the glass
                return half4(col, 1.0);
            }

            Varyings MeerkatLensVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            half4 MeerkatLensFrag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _MWView.x;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                if (_MWView.w < 0.5) discard;   // no tube in the bank: the pixel keeps what the camera drew
                float2 disp = MWContour(p, sceneZ);
                return MWHighQuality(p, disp, aspect);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
