using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// UnityEngine.GeometryUtility. Frustum planes are extracted from the
    /// view-projection matrix (Gribb/Hartmann) in the engine's order — left, right,
    /// bottom, top, near, far — with normals pointing INTO the frustum, so
    /// <see cref="TestPlanesAABB"/> is "every plane has the box on its positive side".
    /// </summary>
    public static class GeometryUtility
    {
        public static Plane[] CalculateFrustumPlanes(Camera camera)
        {
            var planes = new Plane[6];
            CalculateFrustumPlanes(camera, planes);
            return planes;
        }

        public static void CalculateFrustumPlanes(Camera camera, Plane[] planes)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            CalculateFrustumPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, planes);
        }

        public static Plane[] CalculateFrustumPlanes(Matrix4x4 worldToProjectionMatrix)
        {
            var planes = new Plane[6];
            CalculateFrustumPlanes(worldToProjectionMatrix, planes);
            return planes;
        }

        public static void CalculateFrustumPlanes(Matrix4x4 m, Plane[] planes)
        {
            if (planes == null) throw new ArgumentNullException(nameof(planes));
            if (planes.Length != 6) throw new ArgumentException("Planes array must be of length 6.", nameof(planes));

            var r0 = new Vector4(m.m00, m.m01, m.m02, m.m03);
            var r1 = new Vector4(m.m10, m.m11, m.m12, m.m13);
            var r2 = new Vector4(m.m20, m.m21, m.m22, m.m23);
            var r3 = new Vector4(m.m30, m.m31, m.m32, m.m33);

            planes[0] = FromRow(r3 + r0); // left
            planes[1] = FromRow(r3 - r0); // right
            planes[2] = FromRow(r3 + r1); // bottom
            planes[3] = FromRow(r3 - r1); // top
            planes[4] = FromRow(r3 + r2); // near (OpenGL clip: -w <= z)
            planes[5] = FromRow(r3 - r2); // far
        }

        static Plane FromRow(Vector4 row)
        {
            var n = new Vector3(row.x, row.y, row.z);
            float len = n.magnitude;
            if (len < 1e-12f) return new Plane(Vector3.up, 0f);
            var p = new Plane { normal = n / len, distance = row.w / len };
            return p;
        }

        /// <summary>True when the box is inside or intersects every plane (conservative, as the original).</summary>
        public static bool TestPlanesAABB(Plane[] planes, Bounds bounds)
        {
            var c = bounds.center;
            var e = bounds.extents;
            for (int i = 0; i < planes.Length; i++)
            {
                var n = planes[i].normal;
                float r = e.x * MathF.Abs(n.x) + e.y * MathF.Abs(n.y) + e.z * MathF.Abs(n.z);
                float s = Vector3.Dot(n, c) + planes[i].distance;
                if (s + r < 0f) return false;
            }
            return true;
        }

        public static Bounds CalculateBounds(Vector3[] positions, Matrix4x4 transform)
        {
            if (positions == null || positions.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            var b = new Bounds(transform.MultiplyPoint3x4(positions[0]), Vector3.zero);
            for (int i = 1; i < positions.Length; i++) b.Encapsulate(transform.MultiplyPoint3x4(positions[i]));
            return b;
        }

        public static bool TryCreatePlaneFromPolygon(Vector3[] vertices, out Plane plane)
        {
            plane = default;
            if (vertices == null || vertices.Length < 3) return false;
            // Newell's method: robust for non-convex and slightly non-planar polygons.
            var normal = Vector3.zero;
            var centroid = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Length];
                normal.x += (a.y - b.y) * (a.z + b.z);
                normal.y += (a.z - b.z) * (a.x + b.x);
                normal.z += (a.x - b.x) * (a.y + b.y);
                centroid += a;
            }
            if (normal.sqrMagnitude < 1e-20f) return false;
            plane = new Plane(normal, centroid / vertices.Length);
            return true;
        }
    }
}
