using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The geometry of pinning a held camera's OWN X AXIS to a world axis — the Scarab grapple's
    /// "align the camera to the axis the hull is spinning about" (SCARAB.md §4.7).
    ///
    /// It is pure and separate because the claim it makes is a HANDEDNESS claim, and handedness is
    /// exactly what cannot be checked by reading a camera on screen: get the sign wrong and the
    /// view is upside down, get the cross product wrong and the axis lands on the camera's Y and
    /// the swing reads as a circle instead of the clean side-on arc it is meant to be. Here it can
    /// be run through the shipped <see cref="Quaternion.LookRotation"/> and asserted.
    ///
    /// THE ONE IDENTITY EVERYTHING RESTS ON: a camera's right is <c>cross(up, forward)</c> and its
    /// forward points at the anchor, so "right is parallel to the axis" is the SAME STATEMENT as
    /// "the camera sits in the plane the anchor is spinning in". Aligning the axis is therefore a
    /// constraint on WHERE THE CAMERA IS, not only on its roll — which is why the hold has to move
    /// the camera, and why rotating about that axis is the one motion that preserves the alignment.
    /// </summary>
    public static class AnchorAlignmentMath
    {
        /// <summary>
        /// The direction (anchor → camera) nearest <paramref name="dir"/> that satisfies the
        /// alignment: <paramref name="dir"/> projected into the plane perpendicular to
        /// <paramref name="axis"/>. Nearest, so easing toward it is the shortest way in and the
        /// vantage the pilot arrived on is preserved as far as the constraint allows.
        ///
        /// A camera sitting exactly ON the axis has no nearest in-plane direction (every one is
        /// equally far), so <paramref name="hint"/> picks one rather than the caller getting a
        /// zero vector — the degenerate case is a legal place to be, not an error.
        /// </summary>
        public static Vector3 InPlaneDirection(Vector3 dir, Vector3 axis, Vector3 hint)
        {
            Vector3 inPlane = Vector3.ProjectOnPlane(dir, axis);
            if (inPlane.sqrMagnitude < 1e-6f) inPlane = AnyPerpendicular(axis, hint);
            return inPlane.normalized;
        }

        /// <summary>
        /// The up vector that makes <see cref="Quaternion.LookRotation"/> put the camera's right on
        /// <paramref name="axis"/>, for a camera at <paramref name="dir"/> × distance from the
        /// anchor and looking back at it.
        ///
        /// BOTH SIGNS SATISFY "the x axis is aligned to the axis" — one puts right on +axis, the
        /// other on −axis — so it takes the one nearer <paramref name="previousUp"/>. That is not
        /// cosmetic: without it, entering the hold can turn the world upside down, and an axis the
        /// pilot is slowly rolling would snap over as it passed the halfway point. Continuity is
        /// the whole reason the choice exists.
        /// </summary>
        public static Vector3 UpFor(Vector3 dir, Vector3 axis, Vector3 previousUp)
        {
            Vector3 forward = -dir;
            Vector3 up = Vector3.Cross(forward, axis);
            if (up.sqrMagnitude < 1e-6f) return previousUp;
            if (Vector3.Dot(up, previousUp) < 0f) up = -up;
            return up.normalized;
        }

        /// <summary>Any unit vector perpendicular to <paramref name="unit"/>, preferring one that
        /// leans toward <paramref name="hint"/>.</summary>
        public static Vector3 AnyPerpendicular(Vector3 unit, Vector3 hint)
        {
            Vector3 candidate = Vector3.Cross(unit, hint);
            if (candidate.sqrMagnitude < 1e-6f) candidate = Vector3.Cross(unit, Vector3.up);
            if (candidate.sqrMagnitude < 1e-6f) candidate = Vector3.Cross(unit, Vector3.right);
            return candidate.normalized;
        }
    }
}
