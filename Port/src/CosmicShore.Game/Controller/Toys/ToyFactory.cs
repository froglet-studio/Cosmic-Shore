// Ported verbatim from _Scripts/Controller/Toys/ToyFactory.cs (painting-toy drift-sync
// 2026-07-09). Mechanical substitutions (README): UnityEngine → CosmicShore.Engine (+
// .Rendering for Mesh/LineRenderer/MeshRenderer/Material/Shader/PrimitiveType/ShadowCastingMode);
// TMPro → CosmicShore.Engine.UI; Cysharp.Threading.Tasks → System.Threading.Tasks +
// CosmicShore.Engine.Tasks (async UniTaskVoid ScaleOutAndDestroy → async Task, callers .Forget();
// UniTask.Yield(PlayerLoopTiming.Update, ct) → GameTask.Yield(ct)). The one carried surface is
// the 3D-TMP label's RectTransform (UI-shell deviation, restore when RectTransform ports) — the
// same deviation the pre-rework port ToyFactory carried; everything else (cone/jack/ring meshes,
// gate builder, material cache, domain colour/material) ports LIVE.
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using System.Threading.Tasks;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.UI;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;

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

        /// <summary>
        /// Unit crystal spike: a SIX-sided, FLAT-SHADED cone (base radius 0.5 at z=-0.5, apex at
        /// z=+0.5). Hexagonal facets with hard edges echo the game's crystals — under the prism
        /// material each facet catches the light separately as the body slowly spins, instead of
        /// reading as a smooth traffic cone.
        /// </summary>
        static Mesh ConeMesh
        {
            get
            {
                if (s_coneMesh) return s_coneMesh;

                const int segs = 6;
                var apex = new Vector3(0f, 0f, 0.5f);
                var baseCenter = new Vector3(0f, 0f, -0.5f);
                var ring = new Vector3[segs];
                for (int i = 0; i < segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    ring[i] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, -0.5f);
                }

                // Flat shading needs unshared vertices — every triangle owns its three.
                var verts = new Vector3[segs * 6];
                var tris = new int[segs * 6];
                int v = 0;
                for (int i = 0; i < segs; i++)
                {
                    int j = (i + 1) % segs;
                    verts[v] = apex; tris[v] = v; v++;          // side facet
                    verts[v] = ring[i]; tris[v] = v; v++;
                    verts[v] = ring[j]; tris[v] = v; v++;
                    verts[v] = baseCenter; tris[v] = v; v++;    // base facet
                    verts[v] = ring[j]; tris[v] = v; v++;
                    verts[v] = ring[i]; tris[v] = v; v++;
                }

                s_coneMesh = new Mesh { name = "ToyCrystalSpike", vertices = verts, triangles = tris };
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

            // Slow spin about the pointing axis — the facets glint as they turn.
            body.AddComponent<ToyIdleSpin>().Configure(Vector3.forward, 45f);
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

            // A lazy tumble — omnidirectional, like the old toy rolling to rest.
            body.AddComponent<ToyIdleSpin>().Configure(new Vector3(0.35f, 1f, 0.2f).normalized, 22f);
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

        /// <summary>
        /// A fly-through gate: trigger root facing <paramref name="flightDirection"/>, ring, hub,
        /// label, and a <see cref="SwapToy"/> that raises <paramref name="onActivated"/> (inheriting
        /// the standard bloom-in + local-user + freestyle gating + re-arm). Shape vocabulary: pass
        /// <paramref name="hubPrismMaterial"/> (or just true-ish intent via <paramref name="hubIsCone"/>)
        /// for gates that turn the trail ON — the hub becomes the shared trail-changer cone; choice
        /// gates keep a neutral sphere hub, because crossing them commits a choice, not a trail state.
        /// </summary>
        public static GameObject CreateGate(string gateName, Transform parent, Vector3 position,
            Vector3 flightDirection, float ringRadius, Color color, string label,
            bool hubIsCone, Material hubPrismMaterial, ToyDefinitionSO definition, ToyContext context,
            System.Action<SwapToy> onActivated)
        {
            var root = CreateBareRoot(gateName, parent, position, position + flightDirection, ringRadius);
            AddRingBody(root.transform, ringRadius, color);
            if (hubIsCone)
                AddConeBody(root.transform, ringRadius * 0.22f, ringRadius * 0.66f, color, hubPrismMaterial);
            else
                AddSphereBody(root.transform, ringRadius * 0.16f, color);
            AddLabel(root.transform, label, color, ringRadius * 1.5f);

            var toy = root.AddComponent<SwapToy>();
            if (onActivated != null) toy.Activated += onActivated;
            toy.Initialize(definition, context, default);
            return root;
        }

        /// <summary>Continuity-law teardown: shrink to zero over <paramref name="seconds"/>, then destroy.</summary>
        public static async Task ScaleOutAndDestroy(GameObject go, float seconds)
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
                await GameTask.Yield(ct);
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
                var mat = AccentMaterial(accent); // cached per colour — rebuilds don't orphan Materials
                if (mat) renderer.sharedMaterial = mat;
            }
            renderer.shadowCastingMode = CosmicShore.Engine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // One material per accent colour, shared by every tinted body. Toys rebuild visuals on
        // flips / stroke changes — per-body materials would orphan a Material each rebuild
        // (UnityEngine.Objects are never GC'd). Nothing mutates these after creation.
        static readonly System.Collections.Generic.Dictionary<int, Material> s_accentMaterials = new();

        static Material AccentMaterial(Color accent)
        {
            Color32 c = accent;
            int key = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
            if (s_accentMaterials.TryGetValue(key, out var mat) && mat) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default");
            mat = shader ? new Material(shader) { color = accent } : null;
            s_accentMaterials[key] = mat;
            return mat;
        }

        static void Tint(GameObject body, Color accent)
        {
            if (!body.TryGetComponent(out MeshRenderer renderer)) return;
            var mat = AccentMaterial(accent);
            if (mat) renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = CosmicShore.Engine.Rendering.ShadowCastingMode.Off;
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
            lr.shadowCastingMode = CosmicShore.Engine.Rendering.ShadowCastingMode.Off;
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
            // PORT Deviation (UI shell, restore when RectTransform ports): var go = new GameObject("Label", typeof(RectTransform));
            var go = new GameObject("Label");
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
