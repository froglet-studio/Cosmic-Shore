using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turns a head's named <see cref="HeadSiteSpec"/> into a posed <see cref="AttachmentSite"/>
    /// on a given surface: samples the surface point, estimates the outward normal by finite
    /// differences (the ray direction is NOT the normal on a flat face — at the eye it is ~20°
    /// off), builds the tangent frame, and samples the seam ring in contract order.
    /// </summary>
    public static class HeadSiteResolver
    {
        const float NormalProbeRad = 0.02f;

        public static Vector3 SurfaceNormal(IHeadSurface surface, Vector3 dir)
        {
            dir = dir.normalized;
            GeometryKit.Frame(dir, Vector3.up, out var e1, out var e2);
            Vector3 p0 = surface.Sample(dir);
            Vector3 p1 = surface.Sample(GeometryKit.Rotate(dir, e2, NormalProbeRad));   // toward +e1
            Vector3 p2 = surface.Sample(GeometryKit.Rotate(dir, e1, -NormalProbeRad));  // toward +e2
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.sqrMagnitude < 1e-14f) return dir;
            n.Normalize();
            return Vector3.Dot(n, dir) < 0f ? -n : n;
        }

        /// <summary>
        /// Resolve one instance of a site. <paramref name="mirrored"/> selects the right-hand
        /// instance of a bilateral site: its direction and frame are the mirror image across
        /// x = 0, and its <see cref="AttachmentSite.Right"/> points the OTHER way so a feature
        /// generated once places as its own reflection.
        /// </summary>
        public static AttachmentSite Resolve(IBaseHead head, IHeadSurface surface, HeadShape shape,
                                             HeadSiteSpec spec, bool mirrored, float tiltPitchDeg = 0f, float tiltYawDeg = 0f)
        {
            Vector3 dir = head.SiteDirection(spec, shape);
            if (mirrored) dir.x = -dir.x;

            Vector3 upHint = spec.ThetaDeg < 25f ? Vector3.forward : Vector3.up;
            Vector3 normal = SurfaceNormal(surface, dir);
            GeometryKit.Frame(normal, upHint, out var right, out var up);

            // Authored tilt: pitch about Right (forward = toward +Z), yaw about Up.
            if (tiltPitchDeg != 0f)
            {
                float a = tiltPitchDeg * Mathf.Deg2Rad;
                normal = GeometryKit.Rotate(normal, right, a);
                up = GeometryKit.Rotate(up, right, a);
            }
            if (tiltYawDeg != 0f)
            {
                float a = tiltYawDeg * Mathf.Deg2Rad * (mirrored ? -1f : 1f);
                normal = GeometryKit.Rotate(normal, up, a);
                right = GeometryKit.Rotate(right, up, a);
            }
            if (mirrored) right = -right;

            var site = new AttachmentSite
            {
                Name = spec.Name + (spec.Bilateral ? (mirrored ? ".R" : ".L") : string.Empty),
                Mirrored = mirrored,
                Position = surface.Sample(dir),
                Normal = normal,
                Right = right,
                Up = up,
            };

            // Seam ring: surface samples at the ring's angular radius around the site direction,
            // in the contract's order (CCW from +x in the feature's own frame — which on the
            // mirrored side is the reflected frame, so the ring order reflects with it).
            float rho = spec.RingDeg * Mathf.Deg2Rad;
            GeometryKit.Frame(dir, upHint, out var t1, out var t2);
            if (mirrored) t1 = -t1;
            var ring = new Vector3[AttachmentContract.RingCount];
            float radiusSum = 0f;
            for (int k = 0; k < ring.Length; k++)
            {
                float a = GeometryKit.Tau * k / ring.Length;
                Vector3 dk = (dir * Mathf.Cos(rho) + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * Mathf.Sin(rho)).normalized;
                ring[k] = surface.Sample(dk);
                radiusSum += (ring[k] - site.Position).magnitude;
            }
            site.Ring = ring;
            site.Radius = Mathf.Max(1e-3f, radiusSum / ring.Length);
            return site;
        }

        public static bool TryFindSpec(IBaseHead head, string name, out HeadSiteSpec spec)
        {
            var sites = head.Sites;
            for (int i = 0; i < sites.Count; i++)
                if (string.Equals(sites[i].Name, name, StringComparison.Ordinal)) { spec = sites[i]; return true; }
            spec = default;
            return false;
        }
    }
}
