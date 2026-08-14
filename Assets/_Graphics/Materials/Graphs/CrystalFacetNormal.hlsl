// CrystalFacetNormal.hlsl — the crystal's shading normal, derived from the surface
// itself rather than read from the mesh.
//
// PURPOSE. A crystal's whole look is its fresnel: facets that face you stay bright,
// facets turned away darken. That term is a dot product against a NORMAL, so whatever
// the normal does, the shading does too.
//
// THE BUG THIS EXISTS TO KILL. The time crystal's faces flip. The animation drives
// them a half turn and then returns to its initial conditions — geometrically the SAME
// crystal, but every authored normal has been carried 180 degrees around and now points
// the other way. Skinned normals ride the bone matrices, so at the loop seam the shading
// inverted between one frame and the next: bright facets went black. Identical geometry,
// completely different picture, and the eye reads it as a hard cut.
//
// THE FIX. Derive the normal from the rasterized surface — the screen-space derivatives
// of world position are two vectors lying IN the triangle, so their cross product is
// that triangle's true normal at this instant. It is a pure function of the geometry
// being drawn, so identical geometry gives an identical normal BY CONSTRUCTION and the
// loop seam cannot pop. No baked data, no bone dependence, no per-mesh authoring — it
// survives a re-export, a re-rig, or a swap back to blend shapes with nothing to update.
//
// TWO PROPERTIES WORTH NAMING:
//   • It is a FACE normal, so every fragment of a facet shares one value. That is the
//     faceted-gem read (each plate a flat plane of colour) rather than the soft rim
//     gradient smooth normals give — which is the "edge effect" this crystal is meant
//     to do without.
//   • It is oriented toward the viewer, so a plate seen from behind shades exactly like
//     the same plate seen from the front. The crystal renders two-sided and its plates
//     turn through edge-on constantly; without this they would invert as they pass 90
//     degrees. This is also the second, independent reason the flip seam is invisible.
//
// COST. Two derivative instructions and a cross product per fragment, on a handful of
// crystals. No CPU work, no extra draw call, nothing to keep in sync.

#ifndef CRYSTAL_FACET_NORMAL_INCLUDED
#define CRYSTAL_FACET_NORMAL_INCLUDED

// PositionWS — interpolated world position (Position node, World space).
// ViewWS     — world-space vector from the fragment toward the camera (View Vector node,
//              World space). Need not be normalized.
// Out        — unit face normal in world space, hemisphere-aligned with ViewWS.
void CrystalFacetNormal_float(float3 PositionWS, float3 ViewWS, out float3 Out)
{
    float3 view = ViewWS;
    float viewLen = length(view);
    view = viewLen > 1e-8 ? view / viewLen : float3(0.0, 0.0, 1.0);

    // ddy x ddx are two in-plane tangents of the triangle under this fragment.
    float3 faceNormal = cross(ddy(PositionWS), ddx(PositionWS));
    float faceLen = length(faceNormal);

    // A silhouette sliver or a degenerate quad can hand us a zero-area derivative pair.
    // Fall back to the view direction: that yields dot(N,V) = 1, i.e. fresnel 0 and no
    // darkening, which reads as a normal front-facing facet instead of a NaN speckle.
    faceNormal = faceLen > 1e-8 ? faceNormal / faceLen : view;

    // Flip toward the viewer so front and back of a plate shade identically.
    Out = dot(faceNormal, view) < 0.0 ? -faceNormal : faceNormal;
}

#endif // CRYSTAL_FACET_NORMAL_INCLUDED
