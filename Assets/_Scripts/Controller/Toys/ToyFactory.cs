using CosmicShore.Data;
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
