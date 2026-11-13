// VesselHullLighting.hlsl - the GPU half of the HULL LIGHT (Docs/VesselModels/COLOR.md section 2a).
//
// PURPOSE. VesselGraph is an UNLIT graph: every hull slot is a two-tone ramp from its rim colour to
// its face colour, keyed on how squarely the surface faces the camera. That is a good read at the
// chase camera, and it is why a close, slow shot of a ship looks like grey plastic pasted onto the
// sea: nothing on the hull answers to the world around it. The footage critique (2026-11-13, weak
// link 4) asked for hulls that "sit in the sea's light". This is that light, and it is the WORLD's
// light, read off the sky the player is looking at, never a colour of its own:
//
//   - the KEY is the galactic core, from the direction the sky draws it (_CoreDirection), in the
//     sky's own core colour. The side of the hull that faces the core is lit and the far side falls
//     into shade, so a hull has a FORM, and it changes as the ship rolls;
//   - a SPECULAR highlight from that key, which rolls off instead of clipping;
//   - REFLECTIONS of the sky, weighted by a Fresnel term so they live on the grazing surfaces the way
//     real reflections do: the core as a hot point, and the galactic band as a soft streak that
//     slides across the shell as the ship turns (the sky's band normal and colour);
//   - a RIM on the silhouette in the pilot's DOMAIN colour (_VesselHullDomain, stamped per renderer),
//     falling back to the sky's own colour where nothing was stamped.
//
// Every colour here is a uniform the C# publisher (VesselHullLighting.cs) reads off the live
// skybox material, or the domain signal colour ThemeManager already publishes. WorldColorTrace
// repaints the skybox from the worn colour set, so the hull light follows the colour set with no
// colour of its own (the LOCKED colour-set rule, CLAUDE.md).
//
// WHERE IT SITS. Between the graph's final Multiply (Blend x _ColorMultiplier) and the vessel
// vision band. After the multiply, so the engine's x5 boost flare and the Echo Sight still drive the
// slot they always drove; before the vision band, so at range the cel mark still wins and the
// pilot-identification aid is untouched (the band is zero inside 150 units, where this matters).
//
// STRENGTH 0 IS THE OLD HULL, BIT FOR BIT. _VesselHullKey.w is the master strength; at 0 the
// function returns BaseColor before touching anything else. That is the A/B switch for the footage
// (VesselHullLightingConfigSO.strength), and it is what a scene with no publisher reads (a global
// nobody set is zero).

#ifndef VESSEL_HULL_LIGHTING_INCLUDED
#define VESSEL_HULL_LIGHTING_INCLUDED

// File scope, outside every CBUFFER, so Shader.SetGlobal* drives them (the VesselVisionShading.hlsl
// shape). Published once per frame by VesselHullLighting.cs; O(1) in the number of vessels.
float4 _VesselHullKey;        // xyz: unit direction TO the key (the galactic core), world space. w: master strength 0..1
float4 _VesselHullKeyColor;   // rgb: the sky's core colour x its brightness (linear, HDR)
float4 _VesselHullSkyColor;   // rgb: the sky's fill (its cell-membrane glow), linear
float4 _VesselHullBand;       // xyz: the galactic plane's unit normal. w: band half-width (in |dot(R, n)|)
float4 _VesselHullBandColor;  // rgb: the galactic band's emission colour, linear
float4 _VesselHullShape;      // x: diffuse wrap, y: shade floor, z: key gain, w: sky fill gain
float4 _VesselHullSpecular;   // x: highlight exponent, y: highlight gain, z: core reflection exponent, w: core reflection gain
float4 _VesselHullReflect;    // x: Fresnel F0, y: band reflection gain, z: sky reflection gain, w: rim gain
float4 _VesselHullRim;        // x: rim inner (1 - N.V where it starts), y: rim outer, z: rim key bias, w: unused

/// The Shader Graph entry point. Spliced between the graph's final Multiply and VesselVisionShade
/// by Tools/Shaders/wire_vessel_hull_lighting.py.
///
/// Gloss is the per-material _HullGloss: how much of the sky the slot reflects (1 the body, more
/// for the glass). SelfLit is _HullSelfLit: 1 for a slot that glows with its own light (the engine
/// ember), which keeps its authored colour on the shade side instead of falling into shadow.
void VesselHullLight_float(float3 PositionWS, float3 NormalWS, float4 Domain, float Gloss,
    float SelfLit, float3 BaseColor, out float3 Color)
{
    Color = BaseColor;

    float strength = saturate(_VesselHullKey.w);
    if (strength <= 0.0) return;

#if defined(SHADERGRAPH_PREVIEW)
    // No camera in the preview thumbnail: the unlit hull is the honest preview.
    return;
#else
    float3 N = normalize(NormalWS);
    float3 V = normalize(_WorldSpaceCameraPos.xyz - PositionWS);
    float3 L = normalize(_VesselHullKey.xyz);
    float  ndv = saturate(dot(N, V));
    float  gloss = max(Gloss, 0.0);

    // ---- the form: the authored colour, lit by the core and shaded away from it ------------------
    // Wrapped Lambert, so the terminator is soft (a hard terminator on a low-poly hull shows every
    // facet) and the shade side keeps a floor (a black side is a hole in the ship).
    float wrap = _VesselHullShape.x;
    float diffuse = saturate((dot(N, L) + wrap) / (1.0 + wrap));
    float shade = lerp(saturate(_VesselHullShape.y), 1.0, diffuse);
    float3 keyLight = _VesselHullKeyColor.rgb * (_VesselHullShape.z * diffuse);
    // Sky fill from every direction, a touch stronger on the side facing away from the core, where
    // the key does not reach: the sea's own blue in the shadows.
    float3 skyFill = _VesselHullSkyColor.rgb * (_VesselHullShape.w * (1.0 - 0.5 * diffuse));
    float3 lit = BaseColor * (shade + keyLight + skyFill);
    // A glowing slot is never darker than its own glow.
    lit = max(lit, BaseColor * saturate(SelfLit));

    // ---- the highlight: Blinn-Phong off the key, gated by gloss ------------------------------------
    float3 H = normalize(L + V);
    float specPower = max(_VesselHullSpecular.x, 1.0);
    float highlight = pow(saturate(dot(N, H)), specPower) * _VesselHullSpecular.y;
    // Energy-ish normalisation so a tighter highlight is not also a dimmer one.
    highlight *= (specPower + 8.0) / (8.0 * 3.14159265) * 0.25;
    float3 specular = _VesselHullKeyColor.rgb * (highlight * gloss);

    // ---- reflections of the sky, on the grazing surfaces -------------------------------------------
    float fresnel = _VesselHullReflect.x + (1.0 - _VesselHullReflect.x) * pow(1.0 - ndv, 5.0);
    float3 R = reflect(-V, N);
    // The core: a hot point that slides over the hull as it turns.
    float core = pow(saturate(dot(R, L)), max(_VesselHullSpecular.z, 1.0)) * _VesselHullSpecular.w;
    // The galactic band: a great circle on the sky, so a reflection vector near the galactic plane
    // sees it. A soft streak across the shell that moves with every roll.
    float bandWidth = max(_VesselHullBand.w, 1e-3);
    float bandDot = dot(R, normalize(_VesselHullBand.xyz)) / bandWidth;
    float band = exp(-bandDot * bandDot) * _VesselHullReflect.y;
    float3 reflection = (_VesselHullKeyColor.rgb * core
                       + _VesselHullBandColor.rgb * band
                       + _VesselHullSkyColor.rgb * _VesselHullReflect.z) * (fresnel * gloss);

    // ---- the rim: the pilot's domain on the silhouette ----------------------------------------------
    // Domain.a is the marker (VesselHullLighting.StampDomain sets it to 1). Nothing stamped: the
    // sky's own colour draws the rim instead, so every hull is separated from the sea.
    float3 rimColour = Domain.a > 0.0 ? Domain.rgb : _VesselHullSkyColor.rgb;
    float rim = smoothstep(_VesselHullRim.x, _VesselHullRim.y, 1.0 - ndv);
    // Brighter where the core is behind the ship (a back-light), never zero where it is not.
    float backLit = lerp(1.0, saturate(dot(-V, L)) * 0.5 + 0.75, saturate(_VesselHullRim.z));
    float3 rimLight = rimColour * (rim * backLit * _VesselHullReflect.w);

    float3 litHull = lit + specular + reflection + rimLight;
    Color = lerp(BaseColor, litHull, strength);
#endif
}

#endif // VESSEL_HULL_LIGHTING_INCLUDED
