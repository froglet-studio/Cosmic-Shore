// SwarmMemberInstanced.hlsl - the pose and look of one swarm member from the per-tick instance buffer
// (Docs/SWARM_FAUNA.md §14). Included by SwarmMemberInstanced.shader; also compiled as C++ by
// Tools/Shaders/verify_swarm_member_pose.py, which runs it against the C# glue's own pose rules
// (the proxy's transform: root at the interpolated position, LookRotation(face, up), body prism at
// (0, 0, PrismZ) x Scale under it, heart at the root x its world scale). Keep it portable: no
// intrinsics beyond the HLSL core set the verifier shims.
#ifndef SWARM_MEMBER_INSTANCED_INCLUDED
#define SWARM_MEMBER_INSTANCED_INCLUDED

// SwarmTickJob.SwarmInstance, byte for byte (80 bytes)
struct SwarmInstance
{
    float3 PrevPos; float BirthTick;
    float3 CurPos;  uint Flags;
    float3 PrevFace; float PrevMolt;
    float3 CurFace;  float CurMolt;
    float3 Scale;    float PrismZ;
};

StructuredBuffer<SwarmInstance> _SwarmInstances;
StructuredBuffer<uint> _SwarmHeartIdx;

// per draw (MaterialPropertyBlock)
float _SwarmPart;            // 0 body prism, 1 heart
float _SwarmBase;            // heart draws: where this element's list starts in _SwarmHeartIdx
float _SwarmHeartElement;    // heart draws: which element this mesh is (0 Charge .. 3 Time)
float4x4 _SwarmMeshLocal;    // the mesh's own transform under the member root (heart: per crystal model)
// per swarm, per frame
float _SwarmAlpha;           // display alpha between Prev and Cur (0..1)
float _SwarmClock;           // display time in TICKS (pair index + alpha)
float _SwarmBloomTicks;      // a newborn grows in over this many ticks
float4 _SwarmUp;             // the body's up (BY) ...
float4 _SwarmUpAlt;          // ... and the fallback when the facing is parallel to it (BZ)
float4 _SwarmHeartScale;     // heart world scale per element (x Charge, y Mass, z Space, w Time)
float4 _SwarmBodyDark, _SwarmBodyBright;       // plain tier (base face, fresnel rim) - linear HDR
float4 _SwarmDangerDark, _SwarmDangerBright;   // danger tier
float4 _SwarmShieldDark, _SwarmShieldBright;   // shielded tier
float4 _SwarmHeartDull, _SwarmHeartBright;     // a living heart's neutral tint
float _SwarmRimPower;        // body rim falloff (heart uses 4: the crystal convention)
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

// The member's heart element as drawn now, and its display factor (the molt: shrink away, re-form).
void SwarmHeart(SwarmInstance s, float a, out int element, out float factor)
{
    float m = lerp(s.PrevMolt, s.CurMolt, a);
    int from = (int)((s.Flags >> 3) & 3u), to = (int)((s.Flags >> 5) & 3u);
    element = (m >= 0.5 && to != from) ? to : from;
    // a molt that finished this tick has from == to == the new element and runs m to 1
    factor = m > 0.0 ? abs(1.0 - 2.0 * m) : 1.0;
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

SwarmMemberVertex SwarmMemberPose(uint instanceID, float3 positionOS, float3 normalOS, float3 tangentOS)
{
    SwarmMemberVertex o;
    o.visible = false;
    o.positionWS = float3(0, 0, 0);
    o.normalWS = float3(0, 0, 1);
    o.dark = float4(0, 0, 0, 1);
    o.bright = float4(0, 0, 0, 1);

    bool heart = _SwarmPart > 0.5;
    uint slot = heart ? _SwarmHeartIdx[(uint)_SwarmBase + instanceID] : instanceID;
    SwarmInstance s = _SwarmInstances[slot];
    if ((s.Flags & 1u) == 0u) return o;

    float a = saturate(_SwarmAlpha);
    float3 p = lerp(s.PrevPos, s.CurPos, a);
    float3 bx, by, bz;
    float3 face = lerp(s.PrevFace, s.CurFace, a);
    if (length(face) < 1e-5) face = s.CurFace;   // a half-turn between ticks (SwarmTickJob.FaceAt's rule)
    SwarmBasis(face, _SwarmUp.xyz, _SwarmUpAlt.xyz, bx, by, bz);

    // continuity of existence: a newborn grows in from nothing over the bloom
    float bloom = max(0.001, SwarmSmooth01((_SwarmClock - s.BirthTick) / max(_SwarmBloomTicks, 1e-3)));

    if (!heart)
    {
        // the prism opens with distance, exactly as a live prism does (SwarmPrismSpread)
        int bodyTier = (int)((s.Flags >> 1) & 3u);
        float4 spread = bodyTier == 1 ? _SwarmSpreadDanger : bodyTier == 2 ? _SwarmSpreadShield : _SwarmSpreadPlain;
        float3 centre = p + bz * (s.PrismZ * bloom);
        float3 dc = centre - _WorldSpaceCameraPos;
        positionOS = SwarmPrismSpread(positionOS, normalOS, tangentOS, s.Scale, dot(dc, dc), spread);
    }
    float3 lp = mul(_SwarmMeshLocal, float4(positionOS, 1.0)).xyz;
    float3 ln = mul((float3x3)_SwarmMeshLocal, normalOS);
    if (heart)
    {
        int element; float factor;
        SwarmHeart(s, a, element, factor);
        if (element != (int)(_SwarmHeartElement + 0.5)) return o;   // drawn by the other element's list
        float hs = element == 0 ? _SwarmHeartScale.x : element == 1 ? _SwarmHeartScale.y
                 : element == 2 ? _SwarmHeartScale.z : _SwarmHeartScale.w;
        lp *= hs * max(factor, 0.001);
        o.dark = _SwarmHeartDull;
        o.bright = _SwarmHeartBright;
    }
    else
    {
        // the body prism: local scale = Scale (x wide, y thin, z long), seated at z = PrismZ behind the heart
        lp = float3(lp.x * s.Scale.x, lp.y * s.Scale.y, lp.z * s.Scale.z + s.PrismZ);
        ln = float3(ln.x / max(s.Scale.x, 1e-4), ln.y / max(s.Scale.y, 1e-4), ln.z / max(s.Scale.z, 1e-4));
        int tier = (int)((s.Flags >> 1) & 3u);
        o.dark = tier == 1 ? _SwarmDangerDark : tier == 2 ? _SwarmShieldDark : _SwarmBodyDark;
        o.bright = tier == 1 ? _SwarmDangerBright : tier == 2 ? _SwarmShieldBright : _SwarmBodyBright;
    }

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
    float power = _SwarmPart > 0.5 ? 4.0 : max(_SwarmRimPower, 0.5);
    return lerp(dark, bright, pow(1.0 - ndv, power));
}

#endif
