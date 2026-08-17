using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Charge level-5</b> half of the Dolphin's Echo Sight: while the trigger is held, every
    /// VESSEL standing inside the next crystal blast's volume is marked in its own domain's colour.
    ///
    /// <para><b>Brightening alone did not work, and the reason generalises.</b> The first version
    /// only raised each hull's <c>_ColorMultiplier</c>. That fails because the sight lights up the
    /// surrounding PRISMS at the same time — so in a dense arena (Rampage's cactus forest was the
    /// case that proved it) a brighter ship sits inside a brighter forest and reads as more of the
    /// same — and because a hull tint says nothing whatsoever about a pilot who is standing BEHIND
    /// mass. A highlight has to answer all three situations the ability serves: target in the open,
    /// target surrounded by mass, target fully occluded by mass.</para>
    ///
    /// So it marks a vessel two ways, and each one covers a case the other cannot:
    ///
    /// <list type="number">
    /// <item><b>The hull is driven to its own SATURATED domain colour</b> — <c>_Color1</c> and
    /// <c>_Color2</c> (the pair <c>VesselGraph.shadergraph</c> exposes) plus a
    /// <c>_ColorMultiplier</c> gain. Saturated rather than merely brighter, because the arena's mass
    /// is already bright when the sight is up and only HUE separates a ship from it.</item>
    /// <item><b>An additive halo drawn with <c>ZTest Always</c></b> — a soft disc with a hard ring at
    /// the hull's silhouette, in the same domain colour. This is the half that works through prisms
    /// and in empty space, and the ring is what stays legible among lit mass: a ring is a shape
    /// nothing in the arena has, whereas a glow can be mistaken for one more bright prism.
    /// (<c>_Graphics/Materials/Graphs/EchoSightHalo.shader</c>.)</item>
    /// </list>
    ///
    /// <para><b>Colour is the pilot's own domain, always.</b> Both layers are tinted from the
    /// target's domain, never from a fixed highlight colour, so two rivals caught in one cone are
    /// tellable apart and a Ruby pilot can never read as Jade. Domain identity is the palette's job
    /// and this must not borrow its space for something else (<c>Docs/PALETTE.md</c>).</para>
    ///
    /// <para><b>Why per-vessel CPU is fine here and would not be on prisms.</b> The prism half of
    /// this sight is a global uniform because there are tens of thousands of prisms
    /// (<c>Docs/PRISM_ANIMATION.md</c> §4.7). There are at most a dozen vessels, they are already
    /// individually simulated, and this runs only while a trigger is held — so a
    /// MaterialPropertyBlock per renderer plus one additive quad per target is the ordinary tool.
    /// No material is ever cloned (<c>renderer.material</c> is never touched) and the block is
    /// written per material INDEX, so the restore is exact.</para>
    ///
    /// <para><b>Continuity of existence applies.</b> Nothing pops: a vessel entering the volume
    /// blooms up over <c>fadeSeconds</c> and a vessel leaving it fades back down, so sweeping the
    /// nose across a target reads as a beam passing over it.</para>
    ///
    /// Owned by <see cref="EchoSightActionExecutor"/> — a plain C# object with the executor's
    /// lifetime, so it holds no statics and can never retain a destroyed vessel across a scene load.
    /// </summary>
    public sealed class EchoSightVesselHighlighter
    {
        static readonly int ColorMultiplierId = Shader.PropertyToID("_ColorMultiplier");
        static readonly int Color1Id = Shader.PropertyToID("_Color1");
        static readonly int Color2Id = Shader.PropertyToID("_Color2");

        static readonly int HaloColorId = Shader.PropertyToID("_HaloColor");
        static readonly int HaloIntensityId = Shader.PropertyToID("_Intensity");
        static readonly int HaloRadiusId = Shader.PropertyToID("_Radius");
        static readonly int HaloMinScreenRadiusId = Shader.PropertyToID("_MinScreenRadius");
        static readonly int HaloRingPosId = Shader.PropertyToID("_RingPos");

        const string HaloMaterialResourcePath = "EchoSightHalo";

        /// <summary>One renderer sub-mesh we are allowed to mark, and the colours it rests at.</summary>
        readonly struct Target
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex;
            public readonly float RestMultiplier;
            public readonly Color RestColor1;
            public readonly Color RestColor2;
            public readonly bool HasColorPair;

            public Target(Renderer renderer, int materialIndex, float restMultiplier,
                          Color restColor1, Color restColor2, bool hasColorPair)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                RestMultiplier = restMultiplier;
                RestColor1 = restColor1;
                RestColor2 = restColor2;
                HasColorPair = hasColorPair;
            }
        }

        sealed class Entry
        {
            public IVessel Vessel;
            public readonly List<Target> Targets = new();
            public float Blend;          // 0 = resting, 1 = fully marked
            public bool SeenThisFrame;
            public bool BlockApplied;    // so a released highlight clears exactly once
            public Color DomainColor = Color.white;
            public Renderer Halo;        // the additive disc; created lazily, destroyed on clear
            public float HullRadius;
        }

        readonly Dictionary<int, Entry> _entries = new();
        readonly List<int> _scratchRemove = new();

        // Vessels already reported as un-markable. Survives the entry being retired and rebuilt as a
        // target flies in and out of the cone, so a mis-painted hull is named ONCE, not on every
        // pass of the beam.
        readonly HashSet<int> _warned = new();

        MaterialPropertyBlock _block;
        Material _haloMaterial;
        Mesh _haloMesh;
        bool _haloUnavailableReported;

        readonly float _fadeSeconds;
        readonly float _gain;
        readonly float _saturation;
        readonly float _haloScale;
        readonly float _haloIntensity;
        readonly float _haloMinScreenRadius;
        readonly bool _haloEnabled;

        /// <param name="fadeSeconds">Seconds a vessel takes to bloom in / fade out of the highlight.</param>
        /// <param name="gain">Brightness multiplier applied to a marked vessel at full highlight.</param>
        /// <param name="saturation">How far the hull is driven to its saturated domain colour (0-1).</param>
        /// <param name="haloScale">Halo radius as a multiple of the target's own hull radius.</param>
        /// <param name="haloIntensity">Peak additive strength of the halo.</param>
        /// <param name="haloMinScreenRadius">
        /// Floor on the halo's on-screen size, as a fraction of half the screen height. This is what
        /// makes the mark distance-independent — see the shader's header note.
        /// </param>
        /// <param name="haloEnabled">False leaves the hull tint as the only mark.</param>
        public EchoSightVesselHighlighter(float fadeSeconds, float gain, float saturation,
                                          float haloScale, float haloIntensity,
                                          float haloMinScreenRadius, bool haloEnabled)
        {
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
            _gain = Mathf.Max(1f, gain);
            _saturation = Mathf.Clamp01(saturation);
            _haloScale = Mathf.Max(1.05f, haloScale);
            _haloIntensity = Mathf.Max(0f, haloIntensity);
            _haloMinScreenRadius = Mathf.Clamp(haloMinScreenRadius, 0f, 0.5f);
            _haloEnabled = haloEnabled;
        }

        /// <summary>
        /// Drive one frame of the highlight. <paramref name="strength01"/> is the sight's own fade,
        /// so the vessel marks and the prism highlight come up together.
        ///
        /// <paramref name="domainColorFor"/> resolves a vessel's saturated domain colour. Supplied by
        /// the caller rather than resolved here so the palette is read through the one path the rest
        /// of the HUD reads it through, and so this class stays free of theme dependencies.
        ///
        /// Pass <c>players == null</c> or an invalid volume to fade everything back down — the fade
        /// still runs, which is what keeps a released trigger from snapping the world dark.
        /// </summary>
        public void Tick(IReadOnlyList<IPlayer> players, IVessel self, in BlastVolume volume,
                         float strength01, float deltaTime,
                         System.Func<IVessel, Color> domainColorFor)
        {
            bool live = players != null && volume.IsValid && strength01 > 0.001f;

            foreach (var kv in _entries) kv.Value.SeenThisFrame = false;

            if (live)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var vessel = players[i]?.Vessel;
                    if (vessel == null || !vessel.Transform) continue;

                    // The apex sits on our own hull, so we are trivially inside our own blast.
                    // Lighting the ship the pilot is flying tells them nothing.
                    if (ReferenceEquals(vessel, self)) continue;

                    if (!volume.Contains(vessel.Transform.position, out float fill)) continue;

                    var entry = Resolve(vessel);
                    entry.SeenThisFrame = true;

                    // Re-read every frame it is seen: domain is not fixed for a match (the freestyle
                    // domain-changer toy re-picks it) and must never be snapshotted.
                    if (domainColorFor != null) entry.DomainColor = domainColorFor(vessel);

                    entry.Blend = Mathf.MoveTowards(entry.Blend, fill * strength01, deltaTime / _fadeSeconds);
                }
            }

            _scratchRemove.Clear();
            foreach (var kv in _entries)
            {
                var entry = kv.Value;

                if (!entry.SeenThisFrame)
                    entry.Blend = Mathf.MoveTowards(entry.Blend, 0f, deltaTime / _fadeSeconds);

                // A vessel destroyed mid-highlight takes its renderers with it; drop the entry
                // rather than writing into a dead reference.
                if (entry.Vessel == null || !entry.Vessel.Transform)
                {
                    _scratchRemove.Add(kv.Key);
                    continue;
                }

                Apply(entry);

                // Fully faded AND already restored: nothing left to drive.
                if (entry.Blend <= 0f && !entry.BlockApplied)
                    _scratchRemove.Add(kv.Key);
            }

            for (int i = 0; i < _scratchRemove.Count; i++)
            {
                if (_entries.TryGetValue(_scratchRemove[i], out var dead)) DestroyHalo(dead);
                _entries.Remove(_scratchRemove[i]);
            }
        }

        /// <summary>
        /// Drop every highlight IMMEDIATELY, restoring each renderer's authored colours and removing
        /// every halo. Called on release-to-zero, on disable and on a vessel swap — a faded highlight
        /// left behind would otherwise be a permanently recoloured vessel with a halo stuck to it and
        /// nothing in the game left to restore either.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kv in _entries)
            {
                var entry = kv.Value;
                entry.Blend = 0f;
                if (entry.Vessel != null && entry.Vessel.Transform) Restore(entry);
                DestroyHalo(entry);
            }
            _entries.Clear();
        }

        // ---------------- Internals ----------------

        Entry Resolve(IVessel vessel)
        {
            int id = vessel.Transform.GetInstanceID();
            if (_entries.TryGetValue(id, out var existing)) return existing;

            var entry = new Entry { Vessel = vessel };
            CollectTargets(vessel, entry.Targets);

            // Measured with the SAME helper the prism occlusion corridor sizes itself with: hull
            // only, rotation-invariant, skinned meshes measured in root-bone space. Reusing it means
            // the halo is ship-sized on a new vessel of any size with nothing authored, and it cannot
            // disagree with the corridor about how big a hull is.
            entry.HullRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(vessel.Transform);

            _entries[id] = entry;

            if (entry.Targets.Count == 0 && _warned.Add(id))
                CSDebug.LogWarning(
                    $"[EchoSightVesselHighlighter] '{vessel.Transform.name}' has no renderer material " +
                    "exposing _ColorMultiplier, so the Charge-5 pilot highlight cannot tint its hull. " +
                    "Its hull is not painted with a VesselGraph material - check ShipHelper.ApplyShipMaterial " +
                    "reached it. The halo still marks it.");

            return entry;
        }

        static void CollectTargets(IVessel vessel, List<Target> into)
        {
            var renderers = vessel.Transform.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (!renderer) continue;

                // sharedMaterials, never .materials — reading the live instances the vessel is
                // already drawing with, without cloning a single one of them.
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    if (!material || !material.HasFloat(ColorMultiplierId)) continue;

                    bool hasPair = material.HasColor(Color1Id) && material.HasColor(Color2Id);
                    into.Add(new Target(
                        renderer, m,
                        material.GetFloat(ColorMultiplierId),
                        hasPair ? material.GetColor(Color1Id) : Color.white,
                        hasPair ? material.GetColor(Color2Id) : Color.white,
                        hasPair));
                }
            }
        }

        void Apply(Entry entry)
        {
            if (entry.Blend <= 0f)
            {
                if (entry.BlockApplied) Restore(entry);
                UpdateHalo(entry);
                return;
            }

            _block ??= new MaterialPropertyBlock();

            var marked = entry.DomainColor;

            for (int i = 0; i < entry.Targets.Count; i++)
            {
                var target = entry.Targets[i];
                if (!target.Renderer) continue;

                // Brightness lerps from the material's OWN authored value, so an engine that rests at
                // 5 brightens from 5 and a hull that rests at 1 brightens from 1 — the gain is a
                // multiple of what the vessel already looks like, not a value it is forced to.
                float value = Mathf.LerpUnclamped(target.RestMultiplier,
                                                  target.RestMultiplier * _gain, entry.Blend);

                target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
                _block.SetFloat(ColorMultiplierId, value);

                // HUE is what separates a marked ship from the lit mass around it, so the colour pair
                // is driven toward the saturated domain colour rather than merely brightened. Scaled
                // by Blend as well as by _saturation so the recolour blooms in with everything else.
                if (target.HasColorPair)
                {
                    float t = _saturation * entry.Blend;
                    _block.SetColor(Color1Id, Color.Lerp(target.RestColor1, marked, t));
                    _block.SetColor(Color2Id, Color.Lerp(target.RestColor2, marked, t));
                }

                target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);
            }

            entry.BlockApplied = true;
            UpdateHalo(entry);
        }

        void Restore(Entry entry)
        {
            for (int i = 0; i < entry.Targets.Count; i++)
            {
                var target = entry.Targets[i];
                if (!target.Renderer) continue;
                target.Renderer.SetPropertyBlock(null, target.MaterialIndex);
            }
            entry.BlockApplied = false;
        }

        // ---------------- The halo ----------------

        void UpdateHalo(Entry entry)
        {
            if (!_haloEnabled) return;

            if (entry.Blend <= 0f)
            {
                if (entry.Halo) entry.Halo.enabled = false;
                return;
            }

            if (!entry.Halo && !TryCreateHalo(entry)) return;

            entry.Halo.enabled = true;

            _block ??= new MaterialPropertyBlock();
            entry.Halo.GetPropertyBlock(_block);
            _block.SetColor(HaloColorId, entry.DomainColor);
            _block.SetFloat(HaloIntensityId, _haloIntensity * entry.Blend);
            _block.SetFloat(HaloRadiusId, Mathf.Max(0.1f, entry.HullRadius * _haloScale));

            // The floor that stops the halo shrinking with distance. Applied in the shader rather
            // than here because it depends on the target's DEPTH this frame, which the vertex stage
            // already has and the CPU would have to recompute per camera.
            _block.SetFloat(HaloMinScreenRadiusId, _haloMinScreenRadius);

            // The ring sits ON the hull's silhouette while the world-sized disc is the larger of the
            // two, which is why the halo radius is expressed as a multiple of the hull radius: the
            // ring position is just its reciprocal. Once the screen floor takes over, the same
            // fraction makes the ring a reticle AROUND the ship instead - deliberate, so the glyph
            // looks identical at every distance (see the shader header).
            _block.SetFloat(HaloRingPosId, 1f / _haloScale);
            entry.Halo.SetPropertyBlock(_block);
        }

        bool TryCreateHalo(Entry entry)
        {
            if (!TryResolveHaloAssets()) return false;

            // Parented to the target at local zero, so it rides the vessel for free: the billboard
            // is done in the vertex shader, so neither position nor rotation needs a per-frame write.
            var go = new GameObject("EchoSightHalo")
            {
                // Not saved, and invisible in the hierarchy — this is a transient view effect on
                // someone else's vessel, and it must never look like part of that prefab.
                hideFlags = HideFlags.HideAndDontSave
            };
            go.transform.SetParent(entry.Vessel.Transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _haloMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _haloMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            entry.Halo = renderer;
            return true;
        }

        bool TryResolveHaloAssets()
        {
            if (_haloMaterial && _haloMesh) return true;

            if (!_haloMaterial)
            {
                // Loaded from Resources rather than serialized on a prefab: the highlighter is a
                // plain C# object with no inspector, and a Resources-loaded material also keeps the
                // shader out of the build stripper's reach (an unreferenced shader is stripped, and
                // Shader.Find would then return null in a player).
                _haloMaterial = Resources.Load<Material>(HaloMaterialResourcePath);
                if (!_haloMaterial)
                {
                    if (!_haloUnavailableReported)
                    {
                        _haloUnavailableReported = true;
                        CSDebug.LogWarning(
                            "[EchoSightVesselHighlighter] Resources/" + HaloMaterialResourcePath +
                            " is missing, so the Charge-5 pilot highlight has no halo and a marked " +
                            "pilot standing behind mass will be invisible. Restore the material.");
                    }
                    return false;
                }
            }

            _haloMesh ??= BuildUnitQuad();
            return _haloMaterial && _haloMesh;
        }

        /// <summary>
        /// A unit quad in [-0.5, 0.5]. The shader spreads its corners across the view plane about the
        /// object origin, so this one mesh serves every halo at every size — the radius is a shader
        /// property, never a transform scale, which is what keeps the disc perfectly circular
        /// regardless of the parent vessel's own scale.
        /// </summary>
        static Mesh BuildUnitQuad()
        {
            var mesh = new Mesh { name = "EchoSightHaloQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3>
            {
                new(-0.5f, -0.5f, 0f),
                new( 0.5f, -0.5f, 0f),
                new( 0.5f,  0.5f, 0f),
                new(-0.5f,  0.5f, 0f),
            });
            mesh.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0);
            // Generous bounds: the vertex shader moves the corners out to the halo radius in view
            // space, so culling against the authored unit quad would pop the halo off at glancing
            // angles. The renderer is one additive quad — there is nothing to save by culling it
            // tightly, and a highlight that blinks out is worse than one that is always submitted.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            return mesh;
        }

        static void DestroyHalo(Entry entry)
        {
            if (!entry.Halo) return;
            var go = entry.Halo.gameObject;
            entry.Halo = null;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
