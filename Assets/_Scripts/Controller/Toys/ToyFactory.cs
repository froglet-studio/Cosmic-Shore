using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds the runtime GameObject for a <see cref="Toy"/>: a trigger collider on the root,
    /// a tinted body, and a world-space label. Visuals are deliberately simple/procedural so
    /// toys work with zero prefab authoring; an art prefab can replace the body later by
    /// extending the toy's <see cref="ToyDefinitionSO"/>.
    ///
    /// Continuity-of-existence law: the toy is created at full size here, but the <see cref="Toy"/>
    /// base scales it from zero (bloom-in) on <see cref="Toy.Initialize"/>.
    /// </summary>
    public static class ToyFactory
    {
        public static GameObject CreateRoot(string toyName, Transform parent, ToyPlacement placement, Color accent, string label)
        {
            var root = new GameObject($"Toy_{toyName}");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.Position;

            // Face the cell centre so the label reads toward the play area.
            Vector3 toCenter = placement.LookTarget - placement.Position;
            if (toCenter.sqrMagnitude > 0.0001f)
                root.transform.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);

            // Trigger collider on the root — this is the activation volume.
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = Mathf.Max(0.01f, placement.TriggerRadius);

            // Visible body. CreatePrimitive adds its own collider; strip it so only the
            // root trigger drives activation.
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            if (body.TryGetComponent(out Collider bodyCol)) Object.Destroy(bodyCol);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * (placement.BodyRadius * 2f);
            ApplyTint(body, accent);

            CreateLabel(root.transform, label, accent, placement.BodyRadius);
            return root;
        }

        static void ApplyTint(GameObject body, Color accent)
        {
            if (!body.TryGetComponent(out MeshRenderer renderer)) return;

            // URP-safe procedural material. Mirror the LineRenderer fallback used by
            // ShapeDrawingManager so this never renders as the magenta error shader.
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

        static void CreateLabel(Transform parent, string text, Color color, float bodyRadius)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 3D TextMeshPro uses a RectTransform — create it up front so AddComponent is safe.
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * (bodyRadius * 1.9f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = Mathf.Max(8f, bodyRadius * 1.4f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
        }
    }
}
