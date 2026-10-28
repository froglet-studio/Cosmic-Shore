// MeerkatWormholeLens.shader — the bent space round a Meerkat's tunnels, drawn in ONE full-screen pass: the Vessel Studio's
// lensMat (Docs/Studios/VesselStudio/meerkat.html), ported line for line (R_VesselActions/MEERKAT.md). Drawn only by
// MeerkatWormholeLensPass.cs after the transparents, from its copy of the camera colour (_MeerkatSceneColor):
//   - each tunnel's two mouths dent space, a graded dent exactly as wide as the mouth: in at the way in, out at the way out
//     (a vessel or other opaque mass in front of a mouth is not bent);
//   - the tubes' warp (_MeerkatWarpTex, drawn by the same pass from MeerkatWormholeWarp.shader) bends the world behind a
//     tube where its wall runs;
//   - a collapse ripples the screen's EDGES only: a ring running from rippleEdge out past the corners; nothing inside it
//     moves (Yash, 2026-10-27: no distortion in the player's eyesight).
//   - the contour field (Garrett, 2026-10-27, the Tube Lens Lab, Docs/Studios/StoatTubeLensLab.html): each pixel's ray is
//     marched through a thin shell on every nearby tunnel's wall, graded to zero at the shell's edge, and turned there by a
//     field whose lines on the wall are the tube's own contours (along it at pitch 0, round its rings at 90); the pixel
//     shows where the turned ray lands. The lab's GLSL (tube, prof, ribF, fieldDir, trace), ported line for line, with
//     the tube measured over its LIVE window [s0, s1] (MeerkatWormholeMath.Radius) so the field forms and collapses with it.
// A ray bent off the screen shows the screen mirrored at its edge, as in the studio.
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

            #define MEERKAT_LENS_MAX_WELLS 8
            #define MEERKAT_LENS_MAX_TUBES 4
            #define MEERKAT_FIELD_STEPS 256
            #define MW_PI 3.14159265

            // Per camera, with the draw (MeerkatWormholeLensPass.ScreenWells): c xy the mouth's centre on screen (screen
            // heights from the centre, y up), z its depth, w 1; p x 3 (way in) or 4 (way out), y its angular radius, z the
            // dent's strength, w the foreground margin.
            float4 _MWWellC[MEERKAT_LENS_MAX_WELLS];
            float4 _MWWellP[MEERKAT_LENS_MAX_WELLS];
            // x strength, y its front (share of the half diagonal), z where it starts
            float4 _MWRipple;
            // x the camera's aspect, y the tubes' warp is drawn (1) or not, z mouths in the bank, w contour tubes in the bank
            float4 _MWView;

            // The contour field (MeerkatWormholeLensPass.ContourTubesFor): per tube, world to its own frame (the way in at the
            // origin, its first heading +z, its bend toward +x), its shape (shaft S, bend radius R, theta, throat), its live
            // window (s0, s1, fade, mouth) and a bounding sphere (world centre, radius). Round 7 (the umbrella handle): a tube
            // runs straight along +z for S, then bends round (R, 0, S) through theta, so it is S + R theta long.
            float4x4 _MWTubeW2L[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeShape[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeWin[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeBound[MEERKAT_LENS_MAX_TUBES];
            float4 _MWField0;   // strength (rad per wall crossing), pitch (rad), drag, twist
            float4 _MWField1;   // glass, glass index, grazing, flow (0 even, 1 in then out, 2 throat)
            float4 _MWField2;   // inner, outer (x local radius), profile (0 smooth, 1 smoother, 2 linear, 3 bell), ribs
            float4 _MWField3;   // rib sharpness, rib phase, rib rings, rib lines
            float4 _MWCamPos;   // xyz the eye, w tan(fov / 2)
            float4 _MWCamR;
            float4 _MWCamU;
            float4 _MWCamF;

            TEXTURE2D(_MeerkatSceneColor);
            TEXTURE2D(_MeerkatWarpTex);

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            // ---- the contour field ----
            struct MWTQ { float d; float u; float phi; float r; float drds; float scale; float3 n; float3 T; };

            // The wall of tube i near world point pw: signed distance (- inside, + outside; past a live mouth, the distance to
            // its lip), the way along its live window u, the angle round it, its radius and slope, and its frame (local).
            MWTQ MWTube(int i, float3 pw)
            {
                MWTQ q;
                float3 p = mul(_MWTubeW2L[i], float4(pw, 1.0)).xyz;
                float S = _MWTubeShape[i].x, R = max(_MWTubeShape[i].y, 1e-3), theta = _MWTubeShape[i].z;
                float b0 = _MWTubeShape[i].w, m0 = _MWTubeWin[i].w, L = S + R * theta;
                float s0 = _MWTubeWin[i].x, s1 = _MWTubeWin[i].y;
                float s, sc, invR = 0.0;
                float3 c, T, N;
                // the shaft: the straight run [0, S] along +z (MeerkatWormholeMath.At before the bend)
                bool shaftLive = s0 <= S || theta < 1e-3;
                float sS = p.z, scS = clamp(sS, s0, theta < 1e-3 ? s1 : min(s1, S));
                float3 cS = float3(0.0, 0.0, scS);
                float dS = shaftLive ? dot(p - cS, p - cS) : 1e30;
                // the bend: round (R, 0, S) through theta, s = S + R a
                float sB = 0.0, scB = 0.0, dB = 1e30;
                float3 cB = 0.0;
                if (theta >= 1e-3 && s1 >= S)
                {
                    float3 v = p - float3(R, 0.0, S);
                    float a = atan2(v.z, -v.x);
                    if (a < 0.5 * theta - MW_PI) a += 2.0 * MW_PI;
                    sB = S + R * a; scB = clamp(sB, max(s0, S), s1);
                    float ac = (scB - S) / R;
                    cB = float3(R - R * cos(ac), 0.0, S + R * sin(ac));
                    dB = dot(p - cB, p - cB);
                }
                if (dS <= dB)
                {
                    s = sS; sc = scS; c = cS; T = float3(0.0, 0.0, 1.0); N = float3(1.0, 0.0, 0.0);
                }
                else
                {
                    float ac = (scB - S) / R;
                    s = sB; sc = scB; c = cB; invR = 1.0 / R;
                    T = float3(sin(ac), 0.0, cos(ac)); N = float3(cos(ac), 0.0, -sin(ac));
                }
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
            float2 MWContour(float2 p, float sceneZ)
            {
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
                        }
                        else stp = min(stp, max((ad - th) * 0.85, hmin));
                    }
                    if (inShell)
                    {
                        if (entry < 0.0) entry = travel * dot(rd, camF);
                        dir = normalize(dir + dd * stp);
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
                if (entry < 0.0 || sceneZ < entry - 0.5) return float2(0.0, 0.0);   // no shell met, or something opaque in front of it
                float fz = max(dot(dir, camF), 1e-3);
                float2 landed = float2(dot(dir, _MWCamR.xyz), dot(dir, _MWCamU.xyz)) / (fz * th2);
                float2 off = landed - p;
                float len = length(off);
                return len > 2.0 ? off * (2.0 / len) : off;
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
                float2 disp = float2(0.0, 0.0);
                for (int i = 0; i < MEERKAT_LENS_MAX_WELLS; i++)
                {
                    if (i >= (int)_MWView.z) break;
                    float4 c = _MWWellC[i];
                    float4 w = _MWWellP[i];
                    if (c.w < 0.5) continue;
                    if (sceneZ < c.z - w.w) continue;   // a foreground object is not bent
                    float2 d = p - c.xy;
                    float th = max(length(d), 1e-6);
                    float2 dir = d / th;
                    float rc = max(w.y, 1e-6), u = th / rc, a = w.z * rc * u * exp(-0.5 * u * u);
                    disp += (w.x < 3.5 ? -1.0 : 1.0) * dir * a;
                }
                if (_MWView.y > 0.5) disp += SAMPLE_TEXTURE2D_LOD(_MeerkatWarpTex, sampler_LinearClamp, uv, 0).xy;
                if (_MWView.w > 0.5) disp += MWContour(p, sceneZ);
                if (_MWRipple.x > 0.0)
                {
                    float hd = 0.5 * length(float2(aspect, 1.0)), r = length(p) / hd;
                    float mask = smoothstep(_MWRipple.z, min(1.0, _MWRipple.z + 0.25), r), x = (r - _MWRipple.y) / 0.07;
                    disp += p / max(length(p), 1e-4) * _MWRipple.x * mask * exp(-x * x) * sin(x * 3.14159);
                }
                if (dot(disp, disp) < 1e-12) discard;   // nothing bends this pixel: it keeps what the camera drew
                float2 s = p + disp;
                float2 suv = s / float2(aspect, 1.0) + 0.5;
                suv = 1.0 - abs(1.0 - abs(suv));
                return half4(SAMPLE_TEXTURE2D_LOD(_MeerkatSceneColor, sampler_LinearClamp, suv, 0).rgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
