using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds the runtime pieces of a <see cref="Toy"/>: a trigger-collider root, a tinted sphere
    /// body or a mini vessel model, and a world-space label. Visuals are procedural so toys work
    /// with zero prefab authoring.
    ///
    /// Continuity-of-existence law: pieces are created at full size here; the <see cref="Toy"/> base
    /// scales the root from zero (bloom-in) on <see cref="Toy.Initialize"/>.
    /// </summary>
    public static class ToyFactory
    {
        /// <summary>Composite: bare root + sphere body + label. Used by single toys (e.g. painting).</summary>
        public static GameObject CreateRoot(string toyName, Transform parent, ToyPlacement placement, Color accent, string label)
        {
            var root = CreateBareRoot(toyName, parent, placement.Position, placement.LookTarget, placement.TriggerRadius);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            AddSphereBody(body.transform, placement.BodyRadius, accent);
            AddLabel(root.transform, label, accent, placement.BodyRadius * 1.9f);
            return root;
        }

        /// <summary>A positioned, forward-facing GameObject with a trigger SphereCollider — no visuals.</summary>
        public static GameObject CreateBareRoot(string toyName, Transform parent, Vector3 position, Vector3 lookTarget, float triggerRadius)
        {
            var root = new GameObject($"Toy_{toyName}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            Vector3 toCenter = lookTarget - position;
            if (toCenter.sqrMagnitude > 0.0001f)
                root.transform.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = Mathf.Max(0.01f, triggerRadius);
            return root;
        }

        /// <summary>Adds a tinted sphere under <paramref name="parent"/> (no collider).</summary>
        public static GameObject AddSphereBody(Transform parent, float radius, Color accent)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Sphere";
            if (body.TryGetComponent(out Collider bodyCol)) Object.Destroy(bodyCol);
            body.transform.SetParent(parent, false);
            body.transform.localScale = Vector3.one * (radius * 2f);
            Tint(body, accent);
            return body;
        }

        // ── Shared shape language ────────────────────────────────────────────
        //
        // Objects that TURN THE TRAIL ON are cones whose apex points where you go next (the
        // painting's stroke-start hubs and intermediate points, and the domain-changer bodies —
        // both change your trail, so both wear the same shape). Objects that TURN THE TRAIL OFF
        // (a stroke's final point) are jacks — three rods through a common centre, like the old
        // toy. One shape vocabulary across toys so each teaches the other.

        static Mesh s_coneMesh;

        /// <summary>Unit cone: base circle (radius 0.5) at z=-0.5, apex at z=+0.5. Scale to size.</summary>
        static Mesh ConeMesh
        {
            get
            {
                if (s_coneMesh) return s_coneMesh;

                const int segs = 20;
                var verts = new Vector3[segs + 2];
                verts[0] = new Vector3(0f, 0f, 0.5f);   // apex
                verts[1] = new Vector3(0f, 0f, -0.5f);  // base centre
                for (int i = 0; i < segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    verts[2 + i] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, -0.5f);
                }

                var tris = new int[segs * 6];
                for (int i = 0; i < segs; i++)
                {
                    int a = 2 + i, b = 2 + (i + 1) % segs;
                    int t = i * 6;
                    tris[t] = 0; tris[t + 1] = a; tris[t + 2] = b;      // side
                    tris[t + 3] = 1; tris[t + 4] = b; tris[t + 5] = a;  // base cap
                }

                s_coneMesh = new Mesh { name = "ToyCone", vertices = verts, triangles = tris };
                s_coneMesh.RecalculateNormals();
                s_coneMesh.RecalculateBounds();
                return s_coneMesh;
            }
        }

        /// <summary>
        /// A cone pointing along the parent's local +Z ("this way next"). Pass the domain's prism
        /// material to speak the prism visual language; falls back to an unlit accent tint.
        /// </summary>
        public static GameObject AddConeBody(Transform parent, float baseRadius, float length,
            Color accent, Material prismMaterial = null)
        {
            var body = new GameObject("Cone");
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(baseRadius * 2f, baseRadius * 2f, length);

            var filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = ConeMesh;
            var renderer = body.AddComponent<MeshRenderer>();
            ApplyBodyMaterial(renderer, accent, prismMaterial);
            return body;
        }

        /// <summary>
        /// A jack — three orthogonal rods intersecting at the centre (lines run from opposite
        /// faces through the middle). <paramref name="radius"/> is the half-length of each rod.
        /// </summary>
        public static GameObject AddJackBody(Transform parent, float radius, Color accent,
            Material prismMaterial = null)
        {
            var body = new GameObject("Jack");
            body.transform.SetParent(parent, false);

            float thickness = radius * 0.28f;
            var axes = new[]
            {
                new Vector3(radius * 2f, thickness, thickness),
                new Vector3(thickness, radius * 2f, thickness),
                new Vector3(thickness, thickness, radius * 2f),
            };
            foreach (var scale in axes)
            {
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rod.name = "Rod";
                if (rod.TryGetComponent(out Collider rodCol)) Object.Destroy(rodCol);
                rod.transform.SetParent(body.transform, false);
                rod.transform.localScale = scale;
                if (rod.TryGetComponent(out MeshRenderer rodRenderer))
                    ApplyBodyMaterial(rodRenderer, accent, prismMaterial);
            }
            return body;
        }

        /// <summary>A flat fly-through ring in the parent's local XY plane (a portal the vessel crosses).</summary>
        public static LineRenderer AddRingBody(Transform parent, float radius, Color color,
            float width = 2.2f, int segments = 28)
        {
            var lr = CreateLine("Ring", parent, width, false);
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            lr.startColor = lr.endColor = color;
            return lr;
        }

        /// <summary>Continuity-law teardown: shrink to zero over <paramref name="seconds"/>, then destroy.</summary>
        public static async UniTaskVoid ScaleOutAndDestroy(GameObject go, float seconds)
        {
            if (!go) return;
            var ct = go.GetCancellationTokenOnDestroy();
            Vector3 start = go.transform.localScale;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!go) return;
                elapsed += Time.unscaledDeltaTime;
                go.transform.localScale = Vector3.LerpUnclamped(start, Vector3.zero,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (go) Object.Destroy(go);
        }

        /// <summary>
        /// The domain's live PRISM material (the same shader the painted trail wears), so
        /// trail-changing toys visually belong to the prism family. Null when theme material
        /// sets aren't available — callers fall back to an accent tint.
        /// </summary>
        public static Material DomainPrismMaterial(ToyContext context, Domains domain)
        {
            var themeData = context?.GameData ? context.GameData.ThemeManagerData : null;
            if (themeData?.TeamMaterialSets != null
                && themeData.TeamMaterialSets.TryGetValue(domain, out var set)
                && set && set.BlockMaterial)
                return set.BlockMaterial;
            return null;
        }

        static void ApplyBodyMaterial(MeshRenderer renderer, Color accent, Material prismMaterial)
        {
            if (prismMaterial)
            {
                renderer.sharedMaterial = prismMaterial; // shared theme asset — never mutate it
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader) renderer.sharedMaterial = new Material(shader) { color = accent };
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void Tint(GameObject body, Color accent)
        {
            if (!body.TryGetComponent(out MeshRenderer renderer)) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default");
            if (shader)
            {
                var mat = new Material(shader) { color = accent };
                renderer.sharedMaterial = mat; // unique per toy, intentional — not renderer.material clone-on-read
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material s_lineMaterial;

        /// <summary>
        /// One shared vertex-coloured material for every toy LineRenderer (ghost blueprints,
        /// guides, gate rings) — per-line tint comes from startColor/endColor, so dozens of
        /// lines don't each need a Shader.Find + Material allocation.
        /// </summary>
        static Material LineMaterial
        {
            get
            {
                if (!s_lineMaterial)
                {
                    var shader = Shader.Find("Sprites/Default")
                              ?? Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader) s_lineMaterial = new Material(shader);
                }
                return s_lineMaterial;
            }
        }

        /// <summary>A configured LineRenderer child (shared material; tint via vertex colours only).</summary>
        public static LineRenderer CreateLine(string name, Transform parent, float width, bool worldSpace)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = worldSpace;
            lr.positionCount = 0;
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (LineMaterial) lr.sharedMaterial = LineMaterial;
            return lr;
        }

        /// <summary>
        /// The one domain→accent-colour read for toys: the live theme's trail-highlight colour,
        /// with a fixed fallback palette when no theme data is wired.
        /// </summary>
        public static Color DomainAccentColor(ToyContext context, Domains domain)
        {
            var themeData = context?.GameData ? context.GameData.ThemeManagerData : null;
            if (themeData) return themeData.GetDomainUIColor(domain);
            return domain switch
            {
                Domains.Jade => new Color(0.15f, 0.95f, 0.55f),
                Domains.Ruby => new Color(1.00f, 0.20f, 0.45f),
                Domains.Gold => new Color(1.00f, 0.80f, 0.15f),
                _ => Color.gray,
            };
        }

        /// <summary>Adds a world-space TMP label above the body. Returns the text so callers can recolor/retext it.</summary>
        public static TMP_Text AddLabel(Transform parent, string text, Color color, float upOffset)
        {
            // 3D TextMeshPro uses a RectTransform — create it up front so AddComponent is safe.
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * upOffset;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = Mathf.Max(8f, upOffset * 0.75f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }
    }
}
