// VesselVisionShading.hlsl — the GPU half of the VESSEL VISION BAND (Docs/VESSEL_VISION.md).
//
// PURPOSE. A pilot has to be able to find another pilot. At gameplay range a Cosmic Shore
// vessel is a dark, glassy, few-pixel shape against a nebula that is itself dark, glassy and
// colourful, and the hull's authored two-tone read — the thing that makes a ship beautiful up
// close — is exactly what makes it disappear at 900 units. So every vessel is progressively
// re-shaded into a FLAT, BANDED (cel) silhouette in its own DOMAIN colour, with a bright rim on
// the silhouette, as a function of its distance from the camera that is drawing it.
//
// THE BAND, and why it has two graded edges rather than one cutoff:
//
//     amount
//       1 |          ______________________
//         |         /                      \
//         |        /                        \
//       0 |_______/                          \___________
//         0    nearStart  nearFull      farFull  farEnd     distance
//
//   - BELOW nearStart the effect is exactly zero. Close up the ship fills the screen and its
//     own art is the better read; an aid there would be vandalism. This is also what silently
//     and correctly excludes the pilot's OWN vessel, which rides 10-40 units from its camera
//     (CameraSettingsSO dynamicMin/MaxDistance) — an order of magnitude inside the floor. There
//     is deliberately no "is this me" test anywhere in the law: "close things do not need help"
//     is the rule, and your own ship is the closest thing there is. It falls out; it is not
//     special-cased. A broadcast/replay camera parked far from every ship therefore marks them
//     ALL, including the local one, which is what a broadcast view wants.
//   - ABOVE farEnd it is zero again. Past that the ship subtends a few pixels and a saturated
//     dot is not a ship any more, it is one more bright speck in an arena full of crystals and
//     prisms — a signal that cannot be told from noise is worse than no signal.
//   - BOTH edges are GRADED, over a real distance rather than a threshold, because a mark that
//     pops on is a mark the eye reads as a new object appearing. Continuity of existence is a
//     platform law about things entering and leaving the world (CLAUDE.md); this is the same
//     rule applied to a thing entering and leaving VISIBILITY.
//
// WHY IT LIVES HERE AND NOT ON THE CPU. Distance-to-camera is per-CAMERA live data: the answer
// differs between the game view, the scene view, a replay camera and (one day) a split screen,
// and it changes every frame. Computing it on the CPU would pick ONE camera and be wrong in
// every other, and would cost a per-vessel-per-frame write for a number the GPU already has.
// The fragment stage knows where it is and knows where the camera is, so the band costs a
// subtract and two smoothsteps and there is NOTHING per vessel per frame anywhere in the law.
//
// THE UNIFORMS (published by VesselVisionShading.cs, three SetGlobalVector calls per frame,
// O(1) in the number of vessels — see that file for why they are re-published rather than set
// once):
//   float4 _VesselVisionBand   — (nearStart, nearFull, farFull, farEnd) in world units.
//                                farEnd <= 0 is the law's OFF sentinel.
//   float4 _VesselVisionShape  — (strength, celSteps, shadeFloor, gain)
//   float4 _VesselVisionRim    — (rimInner, rimOuter, rimGain, unused)
//
// THE PER-VESSEL DATUM is _VesselVisionTint, an EXPOSED Color property on VesselGraph, stamped
// per renderer by VesselVisionShading.Stamp from VesselHelper.SetShipProperties. Its ALPHA is a
// marker, not an opacity: alpha 0 means "nothing published for this object", and the function
// returns the base colour untouched. That is what keeps the law to VESSELS even though
// VesselGraph is also worn by a projectile material — an object nobody stamped is not a vessel,
// and the effect declines rather than guessing. It is also why the property must be EXPOSED:
// an unexposed ShaderGraph property is declared outside UnityPerMaterial, so a
// MaterialPropertyBlock cannot reach it and Material.HasColor can never see it — the trap
// PrismOcclusionDiagnostics records for the corridor's own globals.
//
// COLOUR IS THE DOMAIN'S, ALWAYS, and never the hull's own. The domain hull materials are
// authored as stylised two-tone glass — Ruby's is (0.27, 0, 0.75), a purple — so deriving the
// mark from the material would answer "purple" to the question "whose ship is that". The mark
// carries SO_ColorSet.GetDomainSignalColor, the same accessor the HUD and the Echo Sight read,
// so the aid speaks the palette every other domain surface speaks (Docs/PALETTE.md).

#ifndef VESSEL_VISION_SHADING_INCLUDED
#define VESSEL_VISION_SHADING_INCLUDED

// File scope, OUTSIDE every CBUFFER — this is what makes Shader.SetGlobalVector the driver and
// keeps them off the per-material constant buffer (the shape PrismOcclusionCorridor.hlsl uses
// for its dither globals).
float4 _VesselVisionBand;
float4 _VesselVisionShape;
float4 _VesselVisionRim;

// Hermite smoothstep, written out rather than calling the intrinsic so the C# transcription in
// VesselVisionShadingConfigSO.Effect01 can be proven identical — the law is asked about in three
// places (the shader, the edit-mode test, the FrogletTools validator) and they must agree.
float VesselVisionSmooth(float edge0, float edge1, float x)
{
    float t = saturate((x - edge0) / max(edge1 - edge0, 1e-5));
    return t * t * (3.0 - 2.0 * t);
}

/// The law itself: camera distance -> mark strength in [0, 1]. Pure, absolute, and identical for
/// every vessel and every camera. Rising edge and falling edge, nothing between them.
float VesselVisionBand01(float distanceToCamera)
{
    float nearStart = _VesselVisionBand.x;
    float nearFull  = _VesselVisionBand.y;
    float farFull   = _VesselVisionBand.z;
    float farEnd    = _VesselVisionBand.w;

    if (farEnd <= 0.0) return 0.0;                       // law off

    float rise = VesselVisionSmooth(nearStart, nearFull, distanceToCamera);
    float fall = 1.0 - VesselVisionSmooth(farFull, farEnd, distanceToCamera);
    return saturate(min(rise, fall));
}

/// Flat, quantized domain tone plus a rim on the silhouette. The quantization is what makes it
/// read as CEL shading rather than as a tint: a smooth ramp over a hull is just the hull again in
/// a different colour, whereas two or three flat tones with hard borders is a shape, and a shape
/// survives being 30 pixels tall.
float3 VesselVisionCel(float3 normalWS, float3 viewWS, float3 tint)
{
    float steps      = max(_VesselVisionShape.y, 1.0);
    float shadeFloor = saturate(_VesselVisionShape.z);
    float gain       = max(_VesselVisionShape.w, 0.0);
    float rimInner   = _VesselVisionRim.x;
    float rimOuter   = _VesselVisionRim.y;
    float rimGain    = max(_VesselVisionRim.z, 0.0);

    float ndv = saturate(dot(normalWS, viewWS));         // 1 head-on, 0 at the silhouette

    // min() before the divide: at ndv == 1 the floor lands on `steps` itself, which would push
    // the top band past 1 and blow the brightest tone out by 1/(steps-1). Costs one instruction
    // and is the difference between N bands and N bands plus a wrong one.
    float band = min(floor(ndv * steps), steps - 1.0) / max(steps - 1.0, 1.0);
    float tone = lerp(shadeFloor, 1.0, band);

    // The rim is measured on the RAW facing term, not the quantized one — a rim quantized by the
    // same ladder would land on a band edge and thicken in jumps as the ship turns.
    float rim = VesselVisionSmooth(rimInner, rimOuter, 1.0 - ndv);

    return tint * (tone + rim * rimGain) * gain;
}

/// Shader Graph entry point. Spliced between the graph's own final colour and
/// SurfaceDescription.BaseColor by Tools/Shaders/wire_vessel_vision_shading.py.
void VesselVisionShade_float(float3 PositionWS, float3 NormalWS, float4 Tint, float3 BaseColor,
    out float3 Color)
{
    Color = BaseColor;

    // Not a vessel (nothing stamped this object), or the mark is authored transparent. Either
    // way there is no domain to speak for and the hull renders exactly as it always did.
    if (Tint.a <= 0.0) return;

    float strength = saturate(_VesselVisionShape.x);
    if (strength <= 0.0) return;

#if defined(SHADERGRAPH_PREVIEW)
    // No camera and no object matrix in the preview thumbnail; showing the base colour is the
    // honest preview of a vessel at conversational range.
    return;
#else
    float3 cameraWS = _WorldSpaceCameraPos.xyz;

    // The band is measured from the OBJECT'S ORIGIN, not from this fragment. A vessel is a
    // SIGNAL, and a signal must switch on as one object: metering per fragment would let a long
    // hull have its nose inside the band and its tail outside it, and would draw the falling
    // edge as a gradient across the ship. The idiom (reading the translation column of
    // UNITY_MATRIX_M) is the one PrismClockAnimation.hlsl and PrismDestructionSight.hlsl already
    // use, and it is per-draw data, so it survives instancing and the SRP batcher.
    float3 originWS = float3(GetObjectToWorldMatrix()._m03,
                             GetObjectToWorldMatrix()._m13,
                             GetObjectToWorldMatrix()._m23);

    float amount = VesselVisionBand01(distance(cameraWS, originWS)) * strength;
    if (amount <= 0.0) return;

    // The view vector, by contrast, IS per fragment — it is what curves the cel bands around the
    // hull, and a per-object view direction would flatten every ship into one facing tone.
    float3 viewWS = normalize(cameraWS - PositionWS);
    float3 normal = normalize(NormalWS);

    Color = lerp(BaseColor, VesselVisionCel(normal, viewWS, Tint.rgb), amount);
#endif
}

#endif // VESSEL_VISION_SHADING_INCLUDED
