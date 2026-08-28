using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// Draws a <b>Toy</b>'s codex portrait. Every other kingdom is photographed from an authored
    /// prefab; a toy has none — it is built at runtime by <see cref="ToyFactory"/> from its
    /// definition — so its page has to be drawn from the same vocabulary the toy itself is made
    /// of, rather than harvested.
    ///
    /// <para>That vocabulary is the toy's <see cref="ToyEmblem"/>: a <b>core</b> (what you are
    /// right now) ringed by <b>satellites</b> (what a pass would offer you), inside the
    /// <b>switch ring</b> that is the platform's one word for "fly through this and something
    /// happens". Every proportion is read from <see cref="ToyEmblem"/>'s own published constants,
    /// so a retune of the emblem retunes the portraits with it and the two can never drift into
    /// disagreeing about what a toy looks like.</para>
    ///
    /// <para><b>Nothing from the runtime is instantiated, deliberately.</b> The obvious shortcut —
    /// call <see cref="ToyFactory"/>'s builders — is wrong twice over in an editor pass:
    /// <c>AddSphereBody</c> discards its collider with <c>Object.Destroy</c>, which is illegal in
    /// edit mode and logs per bake, and <c>AddRingBody</c> attaches a live <c>ToyIdleSpin</c> and
    /// hands out a static mesh nobody owns. The codex's rule is that a bake wakes no gameplay
    /// component, so the geometry here is built and owned outright.</para>
    /// </summary>
    public static class ToyPortraitBuilder
    {
        /// <summary>
        /// Satellites past this read as a smear at icon size rather than as a count — the painting
        /// gallery offers sixteen. The portrait says "several", the stats say how many; a picture
        /// is a bad place to state a number.
        /// </summary>
        const int MaxSatellites = 12;

        /// <summary>
        /// How far outside the emblem's own outer extent the switch ring sits. Derived rather than
        /// copied from Menu_Main's 42u trigger over 22u body (1.909): the emblem is sized so its
        /// satellites clear the trigger volume, so expressing the ring against the emblem's extent
        /// lands on the same proportion and stays right if either is retuned.
        /// </summary>
        const float SwitchRingClearance = 1.25f;

        /// <summary>Ring tube thickness as a fraction of ring radius — the toy's own ring value.</summary>
        const float TubeFraction = ToyFactory.RingTubeFraction;

        /// <summary>
        /// The portrait, un-normalised and un-framed: the caller scales and frames it exactly like
        /// a harvested model. <paramref name="temporaries"/> collects every mesh and material this
        /// creates, because a bake that leaks one per run leaks one per toy per run.
        /// </summary>
        public static GameObject Build(Color accent, int offerCount, bool flat,
            List<Object> temporaries)
        {
            var root = new GameObject("CodexToolPortrait") { hideFlags = HideFlags.HideAndDontSave };

            var material = flat
                ? CodexImageBaker.BuildFlatMaterial()
                : CodexImageBaker.BuildTintedMaterial(accent);
            temporaries.Add(material);

            // Body radius is 1 by construction: the emblem's constants are all expressed in body
            // radii, and the whole portrait is normalised afterwards, so any other unit would just
            // be a number to divide back out.
            const float body = 1f;

            AddSphere(root.transform, Vector3.zero, body * ToyEmblem.CoreRadiusBodies, material);

            int satellites = Mathf.Clamp(offerCount, 0, MaxSatellites);
            float orbit = body * ToyEmblem.OrbitRadiusBodies;
            var tilt = Quaternion.Euler(ToyEmblem.HaloTiltDegrees, 0f, 0f);

            for (int i = 0; i < satellites; i++)
            {
                float angle = i / (float)satellites * Mathf.PI * 2f;
                var onRing = new Vector3(Mathf.Cos(angle) * orbit, Mathf.Sin(angle) * orbit, 0f);
                AddSphere(root.transform, tilt * onRing,
                    body * ToyEmblem.SatelliteRadiusBodies, material);
            }

            float ringRadius =
                body * (ToyEmblem.OrbitRadiusBodies + ToyEmblem.SatelliteRadiusBodies) *
                SwitchRingClearance;
            AddRing(root.transform, ringRadius, material, temporaries);

            return root;
        }

        // ── Geometry ─────────────────────────────────────────────────────────────

        static void AddSphere(Transform parent, Vector3 position, float radius, Material material)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.hideFlags = HideFlags.HideAndDontSave;

            // CreatePrimitive always attaches one; DestroyImmediate because this runs in edit mode.
            if (sphere.TryGetComponent(out Collider collider)) Object.DestroyImmediate(collider);

            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = Vector3.one * (radius * 2f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// A flat fly-through ring in the local XY plane — the switch. Same low-poly torus the toy
        /// draws (12 major × 6 minor segments, tube 8% of the radius), rebuilt here so the mesh has
        /// an owner: the runtime's is a static cache with no hide flags, and borrowing it would
        /// leave a mesh behind in the editor after every bake.
        /// </summary>
        static void AddRing(Transform parent, float radius, Material material,
            List<Object> temporaries)
        {
            const int major = 12, minor = 6;

            var vertices = new Vector3[major * minor * 4];
            var triangles = new int[major * minor * 6];
            float tube = radius * TubeFraction;

            int v = 0, t = 0;
            for (int i = 0; i < major; i++)
            {
                float theta0 = i / (float)major * Mathf.PI * 2f;
                float theta1 = (i + 1) / (float)major * Mathf.PI * 2f;

                for (int j = 0; j < minor; j++)
                {
                    float phi0 = j / (float)minor * Mathf.PI * 2f;
                    float phi1 = (j + 1) / (float)minor * Mathf.PI * 2f;

                    Vector3 P(float theta, float phi) => new(
                        Mathf.Cos(theta) * (radius + tube * Mathf.Cos(phi)),
                        Mathf.Sin(theta) * (radius + tube * Mathf.Cos(phi)),
                        tube * Mathf.Sin(phi));

                    int b = v;
                    vertices[v++] = P(theta0, phi0);
                    vertices[v++] = P(theta1, phi0);
                    vertices[v++] = P(theta1, phi1);
                    vertices[v++] = P(theta0, phi1);

                    // Outward-facing winding, matching ToyFactory.RingMesh.
                    triangles[t++] = b; triangles[t++] = b + 1; triangles[t++] = b + 2;
                    triangles[t++] = b; triangles[t++] = b + 2; triangles[t++] = b + 3;
                }
            }

            var mesh = new Mesh { name = "CodexToolSwitchRing", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            temporaries.Add(mesh);

            var ring = new GameObject("SwitchRing") { hideFlags = HideFlags.HideAndDontSave };
            ring.transform.SetParent(parent, false);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            ring.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
