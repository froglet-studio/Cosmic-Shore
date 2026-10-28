// SwarmMemberInstanced.hlsl - the pose and look of one swarm member from the per-tick instance data
// (Docs/SWARM_FAUNA.md §14). Included by SwarmMemberInstanced.shader (SM 4.5, the instance buffer) and by
// SwarmMemberInstancedFallback.shader (SM 3.5, per-instance constants - WebGL2 / GLES 3.0 / any device without
// vertex-stage structured buffers, Docs/SWARM_FAUNA.md §14.8). Also compiled as C++ by
// Tools/Shaders/verify_swarm_member_pose.py, which runs it against the C# glue's own pose rules
// (the proxy's transform: root at the interpolated position, LookRotation(face, up), body prism at
// (0, 0, PrismZ) x Scale under it, heart at the root x its world scale). Keep it portable: no
// intrinsics beyond the HLSL core set the verifier shims.
//
// TWO HALVES. Everything down to SwarmMemberShade is the CORE: the pose and look of one member from ONE
// SwarmInstance, whoever fetched it - both member shaders run exactly this code, so the two paths cannot drift.
// The last block is the SM 4.5 half: the StructuredBuffers and the fetch by instance id. The fallback shader
// defines SWARM_MEMBER_INSTANCED_INCLUDED before including this file, which skips exactly that half (a
// StructuredBuffer does not exist at SM 3.5). The include guard does this job, rather than a macro of its own,
// because the verifier compiles this file as C++ after stripping only that guard - a second conditional here
// would break the proof. So this file has no guard around the core: include it once per shader.

// SwarmTickJob.SwarmInstance, byte for byte (80 bytes)
struct SwarmInstance
{
    float3 PrevPos; float BirthTick;
    float3 CurPos;  uint Flags;
    float3 PrevFace; float PrevMolt;
    float3 CurFace;  float CurMolt;
    float3 Scale;    float PrismZ;
};

// per draw (MaterialPropertyBlock). The member's HEART is not drawn here: it is the elemental crystal's own models and
// materials (CrystalHeartDraw.cs, Docs/ECOSYSTEM.md §65.7), posed by SwarmHeartPose (SwarmPrismSync.cs).
float4x4 _SwarmMeshLocal;    // the body mesh's own transform under the member root
// per swarm, per frame
float _SwarmAlpha;           // display alpha between Prev and Cur (0..1)
float _SwarmClock;           // display time in TICKS (pair index + alpha)
float _SwarmBloomTicks;      // a newborn grows in over this many ticks
float4 _SwarmUp;             // the body's up (BY) ...
float4 _SwarmUpAlt;          // ... and the fallback when the facing is parallel to it (BZ)
// the body palette, per TIER and per DOMAIN SLOT (Docs/SWARM_FAUNA.md §16.4): [tier * 3 + slot], tier 0 plain,
// 1 danger, 2 shielded; slot = SwarmInstance.Flags bits 7-8. A one-colour swarm writes the same domain into all
// three slots. Base face (dark) and fresnel rim (bright), linear HDR. (New names: a shader property ARRAY's
// length is pinned per editor session by NAME, so the round-7 scalar names are not reused.)
float4 _SwarmTierDark[9];
float4 _SwarmTierBright[9];
float _SwarmRimPower;        // body rim falloff
// per swarm, at bind: each body tier's prism SPREAD - xyz = that tier material's _Spread, w = its _SqrDistance.
// This is what OPENS a prism: BlockGraph's DistanceSpreadAndColors -> SpreadSubGraph -> TangentSlider push
// every face out along its own normal by an amount that grows with camera distance (SwarmPrismSpread).
float4 _SwarmSpreadPlain, _SwarmSpreadDanger, _SwarmSpreadShield;

struct SwarmMemberVertex
{
    float3 positionWS;
    float3 normalWS;
    float4 dark;
    float4 bright;
    bool visible;
};

float SwarmSmooth01(float x) { x = saturate(x); return x * x * (3.0 - 2.0 * x); }

// Quaternion.LookRotation(forward, up) as a basis: z = forward, x = up x z, y = z x x.
void SwarmBasis(float3 face, float3 up, float3 upAlt, out float3 bx, out float3 by, out float3 bz)
{
    float l = length(face);
    bz = l > 1e-5 ? face / l : float3(0, 0, 1);
    if (abs(dot(bz, up)) > 0.98) up = upAlt;
    bx = cross(up, bz);
    float lx = length(bx);
    bx = lx > 1e-6 ? bx / lx : float3(1, 0, 0);
    by = cross(bz, bx);
}

// The prism look's face spread, transcribed from the graphs every live prism draws with (BlockGraph's vertex
// stage): DistanceSpreadAndColors.shadersubgraph -> SpreadSubGraph.shadersubgraph -> TangentSlider.shadersubgraph.
//   far      = Spread * (50, 35, 20)
//   eff      = SqrDistance > MaxSqrDistance ? far : lerp(-7, far, SqrDistance / MaxSqrDistance)
//   eff      = max(eff, Spread)                                       (never closer than the authored spread)
//   position = positionOS + (eff / objectScale) * normalOS            (SpreadSubGraph: a WORLD-size offset)
//            + ((eff - Spread) / objectScale) * tangentOS * 0.5       (TangentSlider: the faces slide as they part)
// SqrDistance is the prism's own centre to the camera (PrismFlightSqrDistance with no flight). objectScale is
// the prism's TRANSFORM scale (the body's Scale); the bloom is the prism's grow, which multiplies AFTER.
float3 SwarmPrismSpread(float3 positionOS, float3 normalOS, float3 tangentOS, float3 objectScale, float sqrDistance,
                        float4 spread)
{
    float3 sp = spread.xyz;
    float3 far = sp * float3(50.0, 35.0, 20.0);
    float maxSqr = max(spread.w, 1e-6);
    float3 eff = sqrDistance > maxSqr ? far : lerp(float3(-7.0, -7.0, -7.0), far, sqrDistance / maxSqr);
    eff = max(eff, sp);
    float3 scl = max(objectScale, float3(1e-4, 1e-4, 1e-4));
    return positionOS + (eff / scl) * normalOS + ((eff - sp) / scl) * tangentOS * 0.5;
}

// One SwarmInstance from the five float4 rows the SM 3.5 path carries per instance (SwarmMemberFallbackDraw.cs
// packs them): (PrevPos, BirthTick) (CurPos, Flags) (PrevFace, PrevMolt) (CurFace, CurMolt) (Scale, PrismZ). Flags
// travel as a float VALUE (every bit lies below 2^10, exact in a float), never as raw bits: a small integer
// reinterpreted as a float is a denormal, which a GPU may flush to zero.
SwarmInstance SwarmInstanceFromRows(float4 r0, float4 r1, float4 r2, float4 r3, float4 r4)
{
    SwarmInstance s;
    s.PrevPos = r0.xyz;  s.BirthTick = r0.w;
    s.CurPos = r1.xyz;   s.Flags = (uint)(max(r1.w, 0.0) + 0.5);
    s.PrevFace = r2.xyz; s.PrevMolt = r2.w;
    s.CurFace = r3.xyz;  s.CurMolt = r3.w;
    s.Scale = r4.xyz;    s.PrismZ = r4.w;
    return s;
}

// The pose and palette of ONE member's body prism, from its instance data.
SwarmMemberVertex SwarmMemberPoseOf(SwarmInstance s, float3 positionOS, float3 normalOS, float3 tangentOS)
{
    SwarmMemberVertex o;
    o.visible = false;
    o.positionWS = float3(0, 0, 0);
    o.normalWS = float3(0, 0, 1);
    o.dark = float4(0, 0, 0, 1);
    o.bright = float4(0, 0, 0, 1);

    if ((s.Flags & 1u) == 0u) return o;

    float a = saturate(_SwarmAlpha);
    float3 p = lerp(s.PrevPos, s.CurPos, a);
    float3 bx, by, bz;
    float3 face = lerp(s.PrevFace, s.CurFace, a);
    if (length(face) < 1e-5) face = s.CurFace;   // a half-turn between ticks (SwarmTickJob.FaceAt's rule)
    SwarmBasis(face, _SwarmUp.xyz, _SwarmUpAlt.xyz, bx, by, bz);

    // continuity of existence: a newborn grows in from nothing over the bloom
    float bloom = max(0.001, SwarmSmooth01((_SwarmClock - s.BirthTick) / max(_SwarmBloomTicks, 1e-3)));

    // the prism opens with distance, exactly as a live prism does (SwarmPrismSpread)
    int tier = (int)((s.Flags >> 1) & 3u);
    float4 spread = tier == 1 ? _SwarmSpreadDanger : tier == 2 ? _SwarmSpreadShield : _SwarmSpreadPlain;
    float3 centre = p + bz * (s.PrismZ * bloom);
    float3 dc = centre - _WorldSpaceCameraPos;
    positionOS = SwarmPrismSpread(positionOS, normalOS, tangentOS, s.Scale, PrismDepthSqrDistance(centre, dot(dc, dc)), spread);
    float3 lp = mul(_SwarmMeshLocal, float4(positionOS, 1.0)).xyz;
    float3 ln = mul((float3x3)_SwarmMeshLocal, normalOS);

    // the body prism: local scale = Scale (x wide, y thin, z long), seated at z = PrismZ behind the heart
    lp = float3(lp.x * s.Scale.x, lp.y * s.Scale.y, lp.z * s.Scale.z + s.PrismZ);
    ln = float3(ln.x / max(s.Scale.x, 1e-4), ln.y / max(s.Scale.y, 1e-4), ln.z / max(s.Scale.z, 1e-4));
    int slot = (int)((s.Flags >> 7) & 3u);
    int pal = (tier == 1 ? 1 : tier == 2 ? 2 : 0) * 3 + (slot > 2 ? 0 : slot);
    o.dark = _SwarmTierDark[pal];
    o.bright = _SwarmTierBright[pal];

    o.positionWS = p + (bx * lp.x + by * lp.y + bz * lp.z) * bloom;
    float3 n = bx * ln.x + by * ln.y + bz * ln.z;
    float nl = length(n);
    o.normalWS = nl > 1e-6 ? n / nl : bz;
    o.visible = true;
    return o;
}

// The prism / crystal look: base face to fresnel rim (Docs/PALETTE.md §2: the rim is the bright one).
float3 SwarmMemberShade(float3 positionWS, float3 normalWS, float3 dark, float3 bright)
{
    float3 v = normalize(_WorldSpaceCameraPos.xyz - positionWS);
    float ndv = saturate(abs(dot(normalize(normalWS), v)));
    float power = max(_SwarmRimPower, 0.5);
    return lerp(dark, bright, pow(1.0 - ndv, power));
}

// ---- the SM 4.5 half: the per-tick instance buffer -----------------------------------------------------------------
#ifndef SWARM_MEMBER_INSTANCED_INCLUDED
#define SWARM_MEMBER_INSTANCED_INCLUDED

StructuredBuffer<SwarmInstance> _SwarmInstances;

SwarmMemberVertex SwarmMemberPose(uint instanceID, float3 positionOS, float3 normalOS, float3 tangentOS)
{
    return SwarmMemberPoseOf(_SwarmInstances[instanceID], positionOS, normalOS, tangentOS);
}

#endif
