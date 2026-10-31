// SolitonTunnel.shader - THE Soliton look: a PLAIN WORMHOLE (2026-11-01, Yash: "plain worm hole with wider mouth looking
// cool from outside and inside, bending and warping of space time"), drawn in ONE full-screen pass by SolitonTunnelLensPass.cs
// after the transparents, from its copy of the camera colour (_MeerkatSceneColor). Every formed Soliton Tunnel
// (_MWTubeShape.y 0) and every channel of a Soliton Network (_MWTubeShape.y 1), made by a vessel or placed, is bent space:
//   - WALL BEND (the contour lens, 53b34e076 / 435ce058c): each pixel's ray is marched through a thin shell on every nearby
//     tube's wall, graded to zero at the shell's edges, and pulled along the tube there; the shell's gradient refracts it a
//     little (LENS) and the bend breathes slowly along the tube (WOBBLE). A faint clear RIM, GLINT and GLOW mark the wall.
//   - MOUTH LENS (from outside; the 435ce058c mouth wells, now a point-mass lens): each live mouth (_MWWell*) bends the
//     scene round it like a gravitational lens - the image at angle th from the mouth's centre shows the scene at
//     th - thE^2 / th, so the world round the flared mouth curls into an EINSTEIN RING and, looking straight in, turns over
//     and magnifies; a thin bright ring at thE and a hint of shadow in the throat's core make it read as a hole.
//   - THROAT STRETCH (from inside, _MWHole): riding a tube, the view stretches out from where you are heading and the
//     warp streams toward the way out (log-polar waves) with a slight twist; the middle of the screen stays still.
//   - its DOMAIN TINT (_MWTubeTint): a faint wash through the wall and the colour of its rim and ring. No fog.
// Comfort's Distortion intensity and every speed cap are folded in on the C# side. A vessel in front of a tube or a mouth
// is neither bent nor lit. Removed 2026-11-01 (Docs/Shaders/WORMHOLE_SHADERS.md §16, restore from this commit's parent):
// the trunk / root (network) shimmer and pulses, the spindle bridge over formed tubes. Earlier shaders: §12-§15.
Shader "CosmicShore/SolitonTunnel"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SolitonTunnel"
            Tags { "LightMode" = "SolitonTunnel" }
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
            #define MW_GRAZE 0.9      // a grazing crossing bends nearly as much as a square one (the Tube Lens Lab's 0.9)

            // x the camera's aspect, y how far the eye is inside a tube (0..1), z that tube's steady centre (screen heights),
            // w tubes in the bank
            float4 _MWView;

            // Per tube (SolitonTunnelLensPass.ContourTubesFor): world to its own frame (the way in at the origin, its first
            // heading +z, its bend toward +x), its shape (L, kind: 0 tunnel / 1 network channel, eye inside 0..1, throat), its live window
            // (s0, s1, fade, mouth), a bounding sphere (world centre, radius), and its centreline as MEERKAT_LENS_PATH points
            // at even steps of s: a tunnel's (x across, z ahead, s, how far it has turned), a channel's (x, y, z, radius) with its
            // carried normal in _MWTubeNorm; and its domain tint (rgb, a strength).
            float4x4 _MWTubeW2L[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeShape[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeWin[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubeBound[MEERKAT_LENS_MAX_TUBES];
            float4 _MWTubePath[MEERKAT_LENS_MAX_TUBES * MEERKAT_LENS_PATH];
            float4 _MWTubeNorm[MEERKAT_LENS_MAX_TUBES * MEERKAT_LENS_PATH];
            float4 _MWTubeTint[MEERKAT_LENS_MAX_TUBES];
            // The look per kind (tunnel 0..4, network 5..9; SolitonTunnelLensPass.PackLook):
            //   +0 bend (rad per crossing), lens, wall inside, wall outside (x local radius)
            //   +1 wobble, wobble waves, wobble phase, -
            //   +2 rim, rim tightness, glint, glow
            //   +3 -
            //   +4 inside bend, inside wobble, steady centre, -
            float4 _MWLook[10];
            float4 _MWCamPos;   // xyz the eye, w tan(fov / 2)
            float4 _MWCamR;
            float4 _MWCamU;
            float4 _MWCamF;
            float4 _MWLightDir;   // xyz toward the main light, w its strength (0: no glint)

            // The mouths (SolitonTunnelLensPass.ScreenWells): C (x, y screen heights from the centre, y up; z depth; w the
            // mouth's world radius, the foreground margin), P (x Einstein angle thE in screen heights, y lens strength,
            // z ring brightness, w throat shadow), T its tube's tint (rgb, a strength).
            #define MEERKAT_LENS_MAX_WELLS 8
            float4 _MWWellC[MEERKAT_LENS_MAX_WELLS];
            float4 _MWWellP[MEERKAT_LENS_MAX_WELLS];
            float4 _MWWellT[MEERKAT_LENS_MAX_WELLS];
            // x mouths in the bank, y the throat stretch (x how far inside), z its flow phase, w its twist
            float4 _MWHole;

            TEXTURE2D(_MeerkatSceneColor);

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            // ---- the tube's wall ----
            struct MWTQ { float d; float u; float phi; float r; float drds; float scale; float3 n; float3 T; };

            // The wall of tube i near world point pw: signed distance (- inside, + outside; past a live mouth, the distance to
            // its lip), the way along its live window u, the angle round it, its radius and slope, and its frame (local).
            // A network channel (Shape.y 1): a 3D polyline at even steps of s, each point (x, y, z, radius) in its own frame.
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
                float L = max(_MWTubeShape[i].x, 1e-3);
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
                // SolitonTunnelShapeMath.Radius: a hyperboloid over the live window, narrower while the window is short
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
            int MWKind(int i) { return _MWTubeShape[i].y > 0.5 ? 5 : 0; }
            // the shell's fall-off, quintic (1 at the wall, 0 at its edges), and its slope
            float MWProf(float t) { t = saturate(t); return t * t * t * (t * (6.0 * t - 15.0) + 10.0); }
            float MWProfD(float t) { t = saturate(t); float a = t * (1.0 - t); return 30.0 * a * a; }
            // the jelly breathing: slow swells running along the tube, a little uneven round it (never below 0)
            float MWWobble(MWTQ q, float4 l1, float amount)
            {
                return 1.0 + amount * sin(2.0 * MW_PI * (q.u * l1.y - l1.z) + 0.6 * sin(2.0 * q.phi));
            }
            // the field on the wall (local): along the tube, tilted by the flare and the bend
            float3 MWFieldDir(MWTQ q) { return normalize(q.T * q.scale + q.drds * q.n); }

            // The screen offset the tubes give this pixel (p: screen heights from the centre, y up), zero when its ray meets no
            // shell or an opaque thing stands in front of the first shell it meets.
            // The layer: what the bent ray met on its way through the shells: x how much shell (graded), y the rim (the same,
            // weighted by how much it grazed), z the glint, w how much of it was a network; u its way along (weighted), phi
            // round; tint the domain tint met (rgb x strength, a strength; weighted the same).
            struct MWLayer { float4 a; float u; float phi; float4 tint; };
            static MWLayer g_mwLayer;

            float2 MWContour(float2 p, float sceneZ)
            {
                g_mwLayer.a = float4(0.0, 0.0, 0.0, 0.0); g_mwLayer.u = 0.0; g_mwLayer.phi = 0.0;
                g_mwLayer.tint = float4(0.0, 0.0, 0.0, 0.0);
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
                [loop]
                for (int it = 0; it < MEERKAT_FIELD_STEPS; it++)
                {
                    float stp = 1e20;
                    float3 dd = float3(0.0, 0.0, 0.0);
                    bool inShell = false;
                    float4 la = float4(0.0, 0.0, 0.0, 0.0), lt = float4(0.0, 0.0, 0.0, 0.0); float lu = 0.0, lphi = 0.0;
                    for (int j = 0; j < MEERKAT_LENS_MAX_TUBES; j++)
                    {
                        if (j >= n) break;
                        MWTQ q = MWTube(j, pos);
                        int k = MWKind(j);
                        float4 l0 = _MWLook[k], l1 = _MWLook[k + 1], l4 = _MWLook[k + 4];
                        float th = (q.d > 0.0 ? l0.w : l0.z) * q.r, ad = abs(q.d), hmin = max(max(0.02, 0.008 * q.r), 0.2 * th);
                        if (ad < th)
                        {
                            inShell = true;
                            float eye = _MWTubeShape[j].z;   // riding this tube: its calmer inside numbers
                            float bendK = lerp(1.0, l4.x, eye), wob = l1.x * lerp(1.0, l4.y, eye);
                            float3 nW = MWToWorld(j, q.n);
                            float across = max(abs(dot(dir, nW)), 0.12);   // grazing: the field barely changes along the wall
                            float sj = clamp(th / (7.0 * across), 0.02, max(th * 1.2, 0.02));
                            if (wob > 0.0) sj = min(sj, (_MWTubeWin[j].y - _MWTubeWin[j].x) / max(l1.y, 0.5) / 16.0);
                            stp = min(stp, sj);
                            float t = 1.0 - ad / th;
                            float shape = MWWobble(q, l1, wob) * _MWTubeWin[j].z;
                            float wgt = MWProf(t) * shape;
                            float graze = lerp(1.0, across, MW_GRAZE) * bendK;
                            // bend: pull the ray along the tube
                            float3 F = MWToWorld(j, MWFieldDir(q));
                            dd += (F - dot(F, dir) * dir) * (l0.x / max(0.5 * (l0.z + l0.w) * q.r, 1e-3) * graze * wgt);
                            // lens: the shell's own gradient (its slope across the wall, along the normal)
                            float3 g = nW * (-MWProfD(t) * sign(q.d) / th * shape);
                            dd += l0.y * graze * (g - dot(g, dir) * dir);
                            // the layer
                            float lw = wgt / max(th, 1e-3);
                            float gr = 1.0 - abs(dot(dir, nW));
                            float3 hv = normalize(_MWLightDir.xyz - dir);
                            float network = k > 0 ? 1.0 : 0.0;
                            float4 l2 = _MWLook[k + 2];
                            la += lw * float4(1.0, pow(max(gr, 0.0), l2.y), pow(saturate(abs(dot(nW, hv))), 48.0), network);
                            lu += lw * q.u; lphi += lw * q.phi;
                            float4 tt = _MWTubeTint[j];
                            lt += lw * float4(tt.rgb * tt.a, tt.a);
                        }
                        else stp = min(stp, max((ad - th) * 0.85, hmin));
                    }
                    if (inShell)
                    {
                        if (entry < 0.0) entry = travel * dot(rd, camF);
                        dir = normalize(dir + dd * stp);
                        if (travel * dot(rd, camF) < sceneZ)   // the layer behind an opaque thing is hidden by it
                        {
                            g_mwLayer.a += la * stp; g_mwLayer.u += lu * stp; g_mwLayer.phi += lphi * stp; g_mwLayer.tint += lt * stp;
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
                    g_mwLayer.tint = float4(0.0, 0.0, 0.0, 0.0);
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

            // The mouths: a point-mass lens round each (the scene at th - thE^2 / th, softened at the centre, fading out by
            // 3 thE), the Einstein ring's light and the throat's shadow.
            float2 MWWells(float2 p, float sceneZ, inout float3 light, inout float shadow)
            {
                float2 disp = float2(0.0, 0.0);
                int n = (int)_MWHole.x;
                for (int i = 0; i < MEERKAT_LENS_MAX_WELLS; i++)
                {
                    if (i >= n) break;
                    float4 c = _MWWellC[i], w = _MWWellP[i];
                    if (sceneZ < c.z - c.w) continue;   // something in front of the mouth is not bent
                    float2 d = p - c.xy;
                    float r = length(d), te = max(w.x, 1e-5);
                    float x = r / (3.0 * te);
                    if (x >= 1.0) continue;
                    float m = (1.0 - x * x); m *= m;
                    float2 dir = d / max(r, 1e-6);
                    float soft = 0.15 * te;
                    disp -= dir * (w.y * te * te * r / (r * r + soft * soft)) * m;
                    float4 t = _MWWellT[i];
                    float3 tc = lerp(float3(1.0, 1.0, 1.0), t.rgb, saturate(t.a));
                    float k = (r - te) / (0.06 * te);
                    light += tc * (w.z * exp(-k * k) * m);
                    shadow = max(shadow, w.w * (1.0 - smoothstep(0.15 * te, 0.6 * te, r)));
                }
                return disp;
            }

            // Riding: the throat stretches out from where you are heading, streaming toward the way out, slightly twisted.
            float2 MWStretch(float2 p)
            {
                if (_MWHole.y <= 0.0) return float2(0.0, 0.0);
                float r = length(p);
                float wave = 0.6 + 0.4 * sin(2.0 * MW_PI * (log(max(r, 1e-3)) * 1.6 - _MWHole.z));
                float k = smoothstep(0.05, 0.6, r) * wave;
                return (-p * _MWHole.y + float2(-p.y, p.x) * _MWHole.w) * k;
            }

            // The bent scene (one sample), darkened in a throat, washed by the domain tint through the wall, then the light:
            // the wall's rim, glint and glow, and the mouths' Einstein rings.
            half4 MWShade(float2 p, float2 disp, float aspect, float3 ring, float shadow)
            {
                float4 L = g_mwLayer.a;
                float od = L.x;
                if (dot(disp, disp) < 1e-12 && od < 1e-4 && dot(ring, ring) < 1e-8) discard;
                // riding a tube: the middle of the screen stays still (comfort)
                if (_MWView.y > 0.0 && _MWView.z > 0.0)
                    disp *= lerp(1.0, smoothstep(0.4 * _MWView.z, _MWView.z, length(p)), _MWView.y);
                float3 col = MWScene(p + disp, aspect) * (1.0 - shadow) + ring;
                if (od < 1e-4) return half4(col, 1.0);
                float layer = 1.0 - exp(-od);
                float net = saturate(L.w / od);              // the share of it that was a network channel (its own numbers)
                float rim = 1.0 - exp(-L.y), glint = 1.0 - exp(-L.z);
                float4 l2 = lerp(_MWLook[2], _MWLook[7], net);
                // the domain tint: its colour (strength-weighted) and how strongly it was met
                float tintK = saturate(g_mwLayer.tint.a / od);
                float3 tintC = g_mwLayer.tint.a > 1e-5 ? g_mwLayer.tint.rgb / g_mwLayer.tint.a : float3(1.0, 1.0, 1.0);
                col = lerp(col, col * lerp(float3(1.0, 1.0, 1.0), tintC * 1.25, 0.6) + tintC * 0.03, tintK * layer);
                float3 film = lerp(float3(1.0, 1.0, 1.0), tintC, tintK);
                col += film * (l2.w * layer + l2.x * rim);                             // the wall's glow and rim
                col += _MWLightDir.w * l2.z * glint * float3(1.0, 0.98, 0.95);          // the light off the wall
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
                if (_MWView.w < 0.5) discard;   // no tube in the bank: the pixel keeps what the camera drew
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _MWView.x;
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float2 disp = MWContour(p, sceneZ);
                float3 ring = float3(0.0, 0.0, 0.0);
                float shadow = 0.0;
                disp += MWWells(p, sceneZ, ring, shadow);
                disp += MWStretch(p);
                return MWShade(p, disp, aspect, ring, shadow);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
