// PrismDepthPlane.hlsl — a view that measures prism depth from a PLANE instead of from its lens.
//
// The prism look's only distance cue is the face spread (DistanceSpreadAndColors -> SpreadSubGraph:
// faces part as the prism gets farther away), fed by the squared distance to _WorldSpaceCameraPos.
// A top-down orthographic view of a plane (the Kite's navigator board,
// Controller/Vessel/Crew/KiteNavigatorView.cs) wants "far" to mean far OUT OF THAT PLANE, so a prism
// on the board reads near and crisp and one above or below it opens up, the way distant mass does in
// the pilot's view.
//
// The view publishes the plane for its own render only (PrismDepthPlane.cs: beginCameraRendering
// sets it, endCameraRendering zeroes it), so every other camera reads the all-zero default and
// renders bit-identically. Declared here, a file-scope global like _PrismOcclusionNearRadius, so it
// reaches every graph that includes it with no node edit.
//   _PrismDepthPlane       xyz = the plane's unit normal, w = dot(normal, a point on the plane)
//   _PrismDepthPlaneScale  world units out of the plane -> the spread's distance units

#ifndef PRISM_DEPTH_PLANE_INCLUDED
#define PRISM_DEPTH_PLANE_INCLUDED

float4 _PrismDepthPlane;
float _PrismDepthPlaneScale;

// The squared depth the spread reads: the camera distance, or the scaled plane distance while a view
// publishes a plane (an unpublished normal is zero length, which is "off").
float PrismDepthSqrDistance(float3 positionWS, float sqrCameraDistance)
{
    if (dot(_PrismDepthPlane.xyz, _PrismDepthPlane.xyz) < 0.5) return sqrCameraDistance;
    float h = (dot(positionWS, _PrismDepthPlane.xyz) - _PrismDepthPlane.w) * _PrismDepthPlaneScale;
    return h * h;
}

#endif
