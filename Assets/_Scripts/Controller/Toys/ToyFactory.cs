using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
            // The switch ring itself is drawn by Toy.Initialize off the trigger collider (one
            // implementation, every toy) - the label only needs to know how big it will be so it
            // can hang clear above the rim.
            AddRingedLabel(root.transform, label, accent, placement.TriggerRadius, placement.BodyRadius);
            return root;
        }

        /// <summary>A positioned, forward-facing GameObject with a trigger SphereCollider - no visuals.</summary>
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

        /// <summary>
        /// Adds a sphere under <paramref name="parent"/> (no collider). Pass
        /// <paramref name="prismMaterial"/> to paint it in a domain's prism material instead of a
        /// flat accent tint - a hub sitting inside a switch ring wears the same material as the
        /// ring, so the two read as one object rather than as a ball behind a hoop.
        /// </summary>
        public static GameObject AddSphereBody(Transform parent, float radius, Color accent,
            Material prismMaterial = null)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Sphere";
            if (body.TryGetComponent(out Collider bodyCol)) Object.Destroy(bodyCol);
            body.transform.SetParent(parent, false);
            body.transform.localScale = Vector3.one * (radius * 2f);
            if (body.TryGetComponent(out MeshRenderer sphereRenderer))
                ApplyBodyMaterial(sphereRenderer, accent, prismMaterial);
            return body;
        }

        // ── Shared shape language ────────────────────────────────────────────
        //
        // Objects that TURN THE TRAIL ON are cones whose apex points where you go next (the
        // painting's stroke-start hubs). Objects that TURN THE TRAIL OFF (a stroke's final point)
        // are jacks - three rods through a common centre, like the old toy. One shape vocabulary
        // across toys so each teaches the other.
        //
        // The cone as a BODY - the thing you fly into, rather than a hub sitting inside a ring -
        // is RESERVED for a booster (prompter-directed). It used to be the Domain Changer's body;
        // that toy is now a switch, and its meaning moved from its SHAPE to its SHADER. Do not
        // give a new toy a cone body: a cone big enough to fly at is a chevron, and a chevron
        // pointing the way you are going is the one thing a booster can be.

        static Mesh s_coneMesh;

        /// <summary>
        /// Unit crystal spike: a SIX-sided, FLAT-SHADED cone (base radius 0.5 at z=-0.5, apex at
        /// z=+0.5). Hexagonal facets with hard edges echo the game's crystals - under the prism
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

                // Flat shading needs unshared vertices - every triangle owns its three.
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
        ///
        /// <para><b>Hub-scale only.</b> The cone at BODY scale - one you fly at rather than one
        /// sitting inside a switch ring - is reserved for a booster; see the note above.</para>
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

            // Slow spin about the pointing axis - the facets glint as they turn.
            body.AddComponent<ToyIdleSpin>().Configure(Vector3.forward, 45f);
            return body;
        }

        /// <summary>
        /// A jack - three orthogonal rods intersecting at the centre (lines run from opposite
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

            // A lazy tumble - omnidirectional, like the old toy rolling to rest.
            body.AddComponent<ToyIdleSpin>().Configure(new Vector3(0.35f, 1f, 0.2f).normalized, 22f);
            return body;
        }

        static Mesh s_ringMesh;

        /// <summary>
        /// Unit low-poly torus in the XY plane (ring radius 0.5, axis +Z): 12 major × 6 minor
        /// flat-shaded facets, hard edges like the crystal cone. Every quad owns its four
        /// vertices so RecalculateNormals keeps the facets crisp. Scale by ring diameter -
        /// tube thickness rides along (8% of the radius), so big rings read chunkier.
        /// </summary>
        static Mesh RingMesh
        {
            get
            {
                if (s_ringMesh) return s_ringMesh;

                const int major = 12, minor = 6;
                const float R = 0.5f, r = 0.04f;

                var verts = new Vector3[major * minor * 4];
                var tris = new int[major * minor * 6];
                int v = 0, t = 0;
                for (int i = 0; i < major; i++)
                {
                    float t0 = i / (float)major * Mathf.PI * 2f;
                    float t1 = (i + 1) / (float)major * Mathf.PI * 2f;
                    for (int j = 0; j < minor; j++)
                    {
                        float p0 = j / (float)minor * Mathf.PI * 2f;
                        float p1 = (j + 1) / (float)minor * Mathf.PI * 2f;

                        Vector3 P(float theta, float phi) => new(
                            Mathf.Cos(theta) * (R + r * Mathf.Cos(phi)),
                            Mathf.Sin(theta) * (R + r * Mathf.Cos(phi)),
                            r * Mathf.Sin(phi));

                        int b = v;
                        verts[v++] = P(t0, p0); // b+0
                        verts[v++] = P(t1, p0); // b+1
                        verts[v++] = P(t1, p1); // b+2
                        verts[v++] = P(t0, p1); // b+3
                        // Outward-facing winding (verified against the analytic torus normal).
                        tris[t++] = b; tris[t++] = b + 1; tris[t++] = b + 2;
                        tris[t++] = b; tris[t++] = b + 2; tris[t++] = b + 3;
                    }
                }

                s_ringMesh = new Mesh { name = "ToyLowPolyRing", vertices = verts, triangles = tris };
                s_ringMesh.RecalculateNormals();
                s_ringMesh.RecalculateBounds();
                return s_ringMesh;
            }
        }

        /// <summary>
        /// A flat fly-through ring in the parent's local XY plane (a portal the vessel crosses):
        /// a low-poly flat-shaded torus in the crystal shape language, slowly spinning about its
        /// axis so the facets glint. Pass the domain's prism material to speak the prism visual
        /// language; falls back to an unlit accent tint.
        /// </summary>
        public static GameObject AddRingBody(Transform parent, float radius, Color accent,
            Material prismMaterial = null)
        {
            var body = new GameObject("Ring");
            body.transform.SetParent(parent, false);
            body.transform.localScale = Vector3.one * (radius * 2f);

            var filter = body.AddComponent<MeshFilter>();
            filter.sharedMesh = RingMesh;
            var renderer = body.AddComponent<MeshRenderer>();
            ApplyBodyMaterial(renderer, accent, prismMaterial);

            body.AddComponent<ToyIdleSpin>().Configure(Vector3.forward, 15f);
            return body;
        }

        // ── The SWITCH ───────────────────────────────────────────────────────
        //
        // A ring you thread is the platform's one word for "this activates something" - the
        // Scarab's placed switches, Astro League's goals, the painting's stroke gates, and (since
        // this pass) every freestyle toy and every choice a toy unfolds into. What makes it
        // teachable rather than decorative is a single rule:
        //
        //     THE RING IS THE TRIGGER VOLUME, DRAWN AT ITS OWN RADIUS.
        //
        // so a ring can never advertise a volume the collider does not have, and "fly through the
        // ring" is a promise the code keeps. `Toy` therefore draws its own from its own collider
        // rather than each toy's builder remembering to - see Toy.ConfigureSwitchRing for the two
        // explicit opt-outs (resize, waive).

        /// <summary>Ring tube radius as a fraction of ring radius (<see cref="RingMesh"/>: r/R = 0.04/0.5).</summary>
        public const float RingTubeFraction = 0.08f;

        /// <summary>
        /// Widest a switch ring may be, as a fraction of the spacing between it and its
        /// neighbour. A station's TRIGGER can legitimately overrun half that gap (the vessel
        /// changer's does, and the domain changer's slots do on a small placement circle), but
        /// rings that interpenetrate read as noise instead of as a row of switches. Threading a
        /// ring smaller than its trigger still always fires it, so clamping never breaks the
        /// promise above.
        ///
        /// <para>Its callers are <see cref="StationRingRadius"/> for a matrix and
        /// <c>SwapToySetCoordinator.SlotRingRadius</c> for a flip-set, whose "spacing" is the
        /// chord between adjacent slots. (The lifeform variant station used to be the third tight
        /// case at level 5; lifeform levels are retired — Docs/ECOSYSTEM.md §40 — so every variant
        /// station is now the plain station radius and no longer exercises the clamp.)</para>
        /// </summary>
        public const float MaxRingSpacingFraction = 0.45f;

        // ── What a switch's SHADER says ──────────────────────────────────────
        //
        // EVERY switch is drawn in the PRISM shader - the same material family the painted trail
        // wears - so the ring is made of the same stuff as the world it acts on, and the one
        // channel left free to carry meaning is WHICH prism it is painted as. That channel is
        // `ToySwitchSignal`:
        //
        //     Domain  -> that domain's plain prism material. RESERVED: in the toybox, a switch
        //                wearing a playable domain's colour is one that HANDS you that domain
        //                (the Domain Changer's slots; the painting's stroke-start gates).
        //     Neutral -> Domains.Blue's plain prism material - the platform's "no team" sentinel.
        //                Everything else. Painted Blue whatever domain the caller passes, so a
        //                neutral switch cannot wear a playable domain even by mistake.
        //
        // The signal picks the colour, never the caller: that is what makes the reservation a
        // property of the code rather than of everybody remembering it.

        /// <summary>The theme a <see cref="ToyContext"/> resolves to, or null when none is wired.</summary>
        public static ThemeManagerDataContainerSO Theme(ToyContext context)
            => context?.GameData ? context.GameData.ThemeManagerData : null;

        /// <summary>
        /// The domain a switch is actually PAINTED as: the one the signal allows, never the one
        /// the caller happened to pass. <see cref="ToySwitchSignal.Neutral"/> always resolves
        /// <c>Domains.Blue</c>.
        /// </summary>
        public static Domains SwitchDomain(ToySwitchSignal signal, Domains domain)
            => signal == ToySwitchSignal.Domain && domain != Domains.Blue ? domain : Domains.Blue;

        /// <summary>
        /// The prism material a switch of this signal wears. Falls back to a prism-SHADER material
        /// tinted from the domain accent when no theme is wired (a switch is still a prism even
        /// before <c>ThemeManager</c> has built the per-domain sets), and to a flat accent only if
        /// the prism shader itself cannot be found.
        /// </summary>
        public static Material SwitchMaterial(ThemeManagerDataContainerSO theme, ToySwitchSignal signal,
            Domains domain)
        {
            var painted = SwitchDomain(signal, domain);
            // Unity's null is not C#'s, so this is an explicit truthiness test rather than `??`.
            var themed = DomainPrismMaterial(theme, painted);
            if (themed) return themed;
            // No per-domain set yet: clone the base prism material if the theme has one (it
            // carries the render state a minted material would have to guess at), else mint.
            var template = theme && theme.BaseMaterialSet ? theme.BaseMaterialSet.BlockMaterial : null;
            return PrismShaderMaterial(DomainAccentColor(theme, painted), template);
        }

        /// <summary>The colour a switch of this signal reads as (its label, its hub, its ring tint fallback).</summary>
        public static Color SwitchColor(ThemeManagerDataContainerSO theme, ToySwitchSignal signal, Domains domain)
            => DomainAccentColor(theme, SwitchDomain(signal, domain));

        /// <summary>
        /// A <b>switch ring</b>: one continuous ring square across the flight path, at the radius
        /// of the trigger volume it advertises, in the prism material its
        /// <paramref name="signal"/> resolves to. Named in the hierarchy so it is never confused
        /// with an emblem's tilted halo. Returns null for a waived (non-positive) radius.
        /// </summary>
        public static GameObject AddSwitchRing(Transform parent, float radius,
            ThemeManagerDataContainerSO theme, ToySwitchSignal signal = ToySwitchSignal.Neutral,
            Domains domain = Domains.Blue)
        {
            if (radius <= 0.01f) return null;
            var ring = AddRingBody(parent, radius, SwitchColor(theme, signal, domain),
                                   SwitchMaterial(theme, signal, domain));
            ring.name = "SwitchRing";
            return ring;
        }

        /// <summary>
        /// Repaint an existing switch ring for a new signal - the Domain Changer's slots flip to
        /// the domain you just left, and the ring IS what says which domain that is. Prism
        /// materials are shared theme assets, so this swaps the reference and never mutates one.
        /// </summary>
        public static void RepaintSwitchRing(GameObject ring, ThemeManagerDataContainerSO theme,
            ToySwitchSignal signal, Domains domain)
        {
            if (!ring || !ring.TryGetComponent(out MeshRenderer renderer)) return;
            ApplyBodyMaterial(renderer, SwitchColor(theme, signal, domain),
                              SwitchMaterial(theme, signal, domain));
        }

        /// <summary>Switch ring radius for a matrix station, clamped by <see cref="MaxRingSpacingFraction"/>.</summary>
        public static float StationRingRadius(float triggerRadius, float stationSpacing)
            => Mathf.Min(triggerRadius, Mathf.Max(1f, stationSpacing) * MaxRingSpacingFraction);

        /// <summary>
        /// Height at which a label clears a switch ring of <paramref name="ringRadius"/> for text
        /// of <paramref name="fontSize"/>. TMP anchors world text at its MIDDLE and toy labels run
        /// to two lines (the second at 60%), so half a block is 0.8 x fontSize - clearing that plus
        /// the ring's own tube is what keeps text off the rim.
        /// </summary>
        public static float SwitchRingLabelHeight(float ringRadius, float fontSize)
            => ringRadius * (1f + RingTubeFraction) + fontSize * 0.85f;

        /// <summary>
        /// A label sized for content <paramref name="contentRadius"/> across, hung clear above that
        /// station's switch ring. Font size is unchanged from the pre-ring layout
        /// (<c>contentRadius x 1.425</c>, i.e. the old <c>1.9 x radius</c> offset x 0.75); only the
        /// height moves, so the far read is exactly what it was.
        /// </summary>
        public static TMP_Text AddRingedLabel(Transform parent, string text, Color color,
            float ringRadius, float contentRadius)
        {
            float fontSize = Mathf.Max(8f, contentRadius * 1.425f);
            return AddLabel(parent, text, color, SwitchRingLabelHeight(ringRadius, fontSize), fontSize);
        }

        /// <summary>
        /// A fly-through gate: trigger root facing <paramref name="flightDirection"/>, ring, hub,
        /// label, and a <see cref="SwapToy"/> that raises <paramref name="onActivated"/> (inheriting
        /// the standard bloom-in + local-user + freestyle gating + re-arm).
        ///
        /// <para><paramref name="signal"/> is what the RING says (see <see cref="ToySwitchSignal"/>):
        /// a painting's stroke-start gate hands you the stroke's domain and so wears it; a choice
        /// gate is <see cref="ToySwitchSignal.Neutral"/>, because crossing it commits a choice, not
        /// a domain. <paramref name="hubIsCone"/> is what the HUB says - the trail-ON cone for a
        /// stroke gate, a neutral sphere for a choice gate - and stays separate from the signal so
        /// a gate can say "this changes your domain" without also claiming to start a stroke.</para>
        /// </summary>
        public static GameObject CreateGate(string gateName, Transform parent, Vector3 position,
            Vector3 flightDirection, float ringRadius, Color color, string label,
            bool hubIsCone, ToySwitchSignal signal, Domains domain,
            ToyDefinitionSO definition, ToyContext context, System.Action<SwapToy> onActivated)
        {
            var root = CreateBareRoot(gateName, parent, position, position + flightDirection, ringRadius);
            if (hubIsCone)
                AddConeBody(root.transform, ringRadius * 0.22f, ringRadius * 0.66f, color,
                            SwitchMaterial(Theme(context), signal, domain));
            else
                AddSphereBody(root.transform, ringRadius * 0.16f, color);
            // 0.79 x the ring reproduces the pre-ring font exactly (old offset 1.5R x 0.75).
            AddRingedLabel(root.transform, label, color, ringRadius, ringRadius * 0.79f);

            var toy = root.AddComponent<SwapToy>();
            if (onActivated != null) toy.Activated += onActivated;
            // A gate's ring IS its switch ring - same builder, same rule, same vocabulary.
            toy.ConfigureSwitchRing(ringRadius, signal, domain);
            toy.Initialize(definition, context, default);
            return root;
        }

        /// <summary>
        /// Render a built <see cref="CellMiniatureBuilder.Miniature"/> as a child of
        /// <paramref name="parent"/>: one material per domain submesh, so the model wears the
        /// world's REAL domain composition in the same prism materials the world itself is built
        /// from (accent tint only where no theme is available). Shared by the Cell Selector's
        /// mini-cells and the toy-root emblems that show a scale model.
        /// </summary>
        public static GameObject AddMiniatureBody(Transform parent, CellMiniatureBuilder.Miniature miniature,
            ToyContext context, string bodyName)
        {
            if (!miniature.IsValid) return null;

            var go = new GameObject(bodyName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = miniature.Mesh;

            var materials = new Material[miniature.SubmeshDomains.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var domain = miniature.SubmeshDomains[i];
                var material = DomainPrismMaterial(context, domain);
                materials[i] = material ? material : AccentMaterial(DomainAccentColor(context, domain));
            }

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>
        /// Continuity-law arrival: grow from zero to the transform's current scale over
        /// <paramref name="seconds"/>. Callers zero the scale BEFORE the first tick so nothing ever
        /// renders at full size for a frame first.
        /// </summary>
        public static async UniTaskVoid ScaleInFromZero(Transform target, float seconds)
        {
            if (!target) return;
            var ct = target.gameObject.GetCancellationTokenOnDestroy();
            target.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!target) return;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                target.localScale = Vector3.one * (t * t * (3f - 2f * t)); // smoothstep
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (target) target.localScale = Vector3.one;
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
        /// sets aren't available - callers fall back to an accent tint.
        /// </summary>
        public static Material DomainPrismMaterial(ToyContext context, Domains domain)
            => DomainPrismMaterial(Theme(context), domain);

        /// <inheritdoc cref="DomainPrismMaterial(ToyContext, Domains)"/>
        public static Material DomainPrismMaterial(ThemeManagerDataContainerSO theme, Domains domain)
        {
            if (theme?.TeamMaterialSets != null
                && theme.TeamMaterialSets.TryGetValue(domain, out var set)
                && set && set.BlockMaterial)
                return set.BlockMaterial;
            return null;
        }

        static void ApplyBodyMaterial(MeshRenderer renderer, Color accent, Material prismMaterial)
        {
            if (prismMaterial)
            {
                renderer.sharedMaterial = prismMaterial; // shared theme asset - never mutate it
            }
            else
            {
                var mat = AccentMaterial(accent); // cached per colour - rebuilds don't orphan Materials
                if (mat) renderer.sharedMaterial = mat;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // One material per accent colour, shared by every tinted body. Toys rebuild visuals on
        // flips / stroke changes - per-body materials would orphan a Material each rebuild
        // (UnityEngine.Objects are never GC'd). Nothing mutates these after creation.
        static readonly System.Collections.Generic.Dictionary<int, Material> s_accentMaterials = new();

        /// <summary>Shared unlit material for an accent colour (cached per colour). Public so
        /// toys that build their own meshes can use the same tint path as the primitive bodies.</summary>
        public static Material AccentMaterial(Color accent)
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

        // Prism-shader materials minted from a bare colour, cached per (colour, template). This
        // is the fallback that keeps "every switch is a prism" true where the live per-domain
        // sets are not available - the toybox before ThemeManager has built them. Nothing
        // mutates these after creation.
        static readonly System.Collections.Generic.Dictionary<(int, int), Material> s_prismShaderMaterials = new();
        static Shader s_prismShader;
        static bool s_prismShaderSearched;

        /// <summary>
        /// A material on the PRISM block shader tinted from <paramref name="rim"/>: the colour
        /// becomes the fresnel rim (<c>_BrightColor</c>) over a dark base face
        /// (<c>_DarkColor</c>), because in every tier on every domain the rim is brighter than
        /// the base (Docs/PALETTE.md section 4.0). Null when neither a template nor the shader
        /// is available - callers fall back to a flat accent tint.
        ///
        /// <para><b>Copy the shipped material when there is one.</b> A Shader Graph property's
        /// authored DEFAULT is not the value the shipped material carries, and on
        /// <c>BlockGraph</c> that difference is fatal: <c>_Alpha</c> defaults to <b>0</b> while
        /// <c>PrismMaterial.mat</c> sets 1, so a bare <c>new Material(Shader.Find(...))</c> is a
        /// correctly-tinted prism that alpha-clips to nothing. Cloning the base set's own
        /// <c>BlockMaterial</c> carries every render-state property AND its shader keywords
        /// (<c>_ALPHATEST_ON</c>) across; the <c>Shader.Find</c> path below has to restate them,
        /// and can only restate the ones we know about.</para>
        /// </summary>
        static Material PrismShaderMaterial(Color rim, Material template)
        {
            if (!template)
            {
                if (!s_prismShaderSearched)
                {
                    s_prismShaderSearched = true;
                    s_prismShader = Shader.Find("Shader Graphs/BlockGraph");
                }
                if (!s_prismShader) return null;
            }

            Color32 c = rim;
            var key = ((c.r << 24) | (c.g << 16) | (c.b << 8) | c.a,
                       template ? template.GetInstanceID() : 0);
            if (s_prismShaderMaterials.TryGetValue(key, out var cached) && cached) return cached;

            Material mat;
            if (template)
            {
                mat = new Material(template);
            }
            else
            {
                mat = new Material(s_prismShader);
                // Restate what the shipped prism material sets and the graph's defaults do not.
                mat.SetFloat("_Alpha", 1f);
                mat.SetFloat("_AlphaClip", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                // A switch ring is a static mesh, not a prism being grown, so it wants no
                // along-normal displacement (the shipped prism material's 0.1 is for prisms).
                mat.SetVector("_Spread", Vector4.zero);
                mat.SetVector("_GrowStartFrac", new Vector4(1f, 1f, 1f, 0f)); // as PrismMaterial.mat
            }
            mat.SetColor("_BrightColor", rim);
            mat.SetColor("_DarkColor", rim.ScaleRGB(0.22f));
            s_prismShaderMaterials[key] = mat;
            return mat;
        }

        static void Tint(GameObject body, Color accent)
        {
            if (!body.TryGetComponent(out MeshRenderer renderer)) return;
            var mat = AccentMaterial(accent);
            if (mat) renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material s_lineMaterial;

        /// <summary>
        /// One shared vertex-coloured material for every toy LineRenderer (ghost blueprints,
        /// miniature strokes) - per-line tint comes from startColor/endColor, so dozens of
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
            => DomainAccentColor(Theme(context), domain);

        /// <summary>
        /// The domain colour with NO theme available - the fixed fallback palette below. Its own
        /// overload because <c>DomainAccentColor(null, d)</c> is ambiguous between the two
        /// two-argument overloads, and a cast at the call site reads as an accident.
        /// </summary>
        public static Color DomainAccentColor(Domains domain)
            => DomainAccentColor((ThemeManagerDataContainerSO)null, domain);

        /// <inheritdoc cref="DomainAccentColor(ToyContext, Domains)"/>
        public static Color DomainAccentColor(ThemeManagerDataContainerSO theme, Domains domain)
        {
            if (theme) return theme.GetDomainUIColor(domain);
            return domain switch
            {
                Domains.Jade => new Color(0.15f, 0.95f, 0.55f),
                Domains.Ruby => new Color(1.00f, 0.20f, 0.45f),
                Domains.Gold => new Color(1.00f, 0.80f, 0.15f),
                // Blue is the platform's "no team" sentinel and therefore the NEUTRAL switch's
                // colour - a periwinkle matching the shipped BlueColors, never a flat gray.
                Domains.Blue => new Color(0.40f, 0.50f, 1.00f),
                _ => Color.gray,
            };
        }

        /// <summary>
        /// Adds a world-space TMP label above the body. Returns the text so callers can
        /// recolor/retext it. <paramref name="fontSize"/> defaults to the historic
        /// <c>0.75 x upOffset</c>; pass it explicitly when the height is set by something other
        /// than legibility (a label hung above a switch ring - see <see cref="AddRingedLabel"/>),
        /// or the text grows with the clearance it needed.
        /// </summary>
        public static TMP_Text AddLabel(Transform parent, string text, Color color, float upOffset,
            float fontSize = 0f)
        {
            // 3D TextMeshPro uses a RectTransform - create it up front so AddComponent is safe.
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * upOffset;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize > 0f ? fontSize : Mathf.Max(8f, upOffset * 0.75f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
            go.AddComponent<BillboardLabel>(); // every toy label reads from all sides
            return tmp;
        }
    }

    /// <summary>
    /// Faces its transform away from the main camera each LateUpdate so world-space toy text is
    /// readable from every approach direction. Cheap: one rotation write per frame; re-resolves
    /// the camera only when the cached one dies (scene loads).
    /// </summary>
    public class BillboardLabel : MonoBehaviour
    {
        Camera _cam;

        void LateUpdate()
        {
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;
            // Forward points AWAY from the camera - TextMeshPro's readable face looks at the viewer.
            Vector3 away = transform.position - _cam.transform.position;
            if (away.sqrMagnitude < 1e-6f) return;
            // Directly above/below a label, world-up is colinear with the view direction and
            // LookRotation's implicit up degenerates (the text rolls wildly) - use the camera's up.
            Vector3 up = Mathf.Abs(Vector3.Dot(away.normalized, Vector3.up)) > 0.98f
                ? _cam.transform.up
                : Vector3.up;
            transform.rotation = Quaternion.LookRotation(away, up);
        }
    }
}
