using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class Spindle : MonoBehaviour
    {
        private static readonly int PhaseOffsetID = Shader.PropertyToID("_Phase");
        private static readonly int SwayAmplitudeID = Shader.PropertyToID("_SwayAmplitude");
        private static readonly int SwayFrequencyID = Shader.PropertyToID("_SwayFrequency");
        private static readonly int DeathStartTimeID = Shader.PropertyToID("_DeathStartTime");
        private static readonly int DeathDurationID = Shader.PropertyToID("_DeathDuration");
        private static readonly int DeathDirectionID = Shader.PropertyToID("_DeathDirection");

        // Condense/evaporate fades stamp _DeathStartTime / _DeathDuration /
        // _DeathDirection ONCE through a shared scratch MaterialPropertyBlock.
        // The GPU runs the course off _PrismClock (PrismDeathClock in SpindleGraph
        // / AnimatedSpindleGraph). Trade-off, stated honestly: a renderer WITH an
        // MPB is still excluded from the SRP Batcher for the fade's ~1s — this
        // migration does NOT recover batching DURING the fade (unique staggered
        // StartTimes cannot share quantized fade materials without collapsing the
        // ecology-LOCKED wither order). What it recovers is (1) zero per-frame
        // CPU and (2) SRP Batcher AFTER settle via SetPropertyBlock(null). If a
        // capture shows the unbatched fade window regressing, bucket the fade
        // into a few shared quantized-fade materials like the phase variants —
        // that would be a look trade against ordered wither.
        static MaterialPropertyBlock s_fadeMpb;

        const float DeathFadeDuration = 1f;
        const float DeathDirectionEvaporate = 1f;
        const float DeathDirectionCondense = -1f;

        // Spindle sway is desynced with a small set of SHARED phase-variant materials chosen
        // by world position, NOT a per-renderer MaterialPropertyBlock. A per-renderer MPB
        // excludes the renderer from the SRP Batcher — that is why hundreds of spindles
        // (tadpole bodies, gyroid branches) each drew as their own draw call. Every spindle
        // that lands in the same phase bucket shares one material and batches into a single
        // draw. Purely cosmetic (animation phase only); cached per base material so the count
        // is bounded by the handful of distinct spindle materials.
        const int PhaseVariantCount = 8;
        static readonly Dictionary<Material, Material[]> PhaseVariants = new();

        public Renderer RenderedObject;

        // A spindle may carry MORE THAN ONE piece of branch geometry, and every piece has to
        // live and die on the same clock as the first: the gyroid's branch is a MIRRORED PAIR
        // of half-branches meeting at the prism (Docs/ECOSYSTEM.md §34.12), so a fade driven
        // through RenderedObject alone would condense one half in and evaporate one half out
        // while the other POPPED - a continuity-of-existence violation, on a spindle whose
        // whole point is symmetry. Listed explicitly rather than swept with
        // GetComponentsInChildren because the flora parents its HEALTH PRISM under the spindle
        // root, so a sweep would capture the prism's renderer and fade the mass along with the
        // branch. Empty on every single-renderer spindle; those behave exactly as before.
        // NOTE: attribute and declaration stay on ONE line - the repo's serialized-field parity
        // check is line-based, so a wrapped attribute hides the field from it (a silent false pass).
        [SerializeField, Tooltip("Extra branch renderers beyond RenderedObject. They share its sway phase bucket and take the same condense/evaporate fade, so a multi-part spindle can never half-pop. Leave empty for a single-part spindle.")] Renderer[] additionalRenderedObjects;

        [SerializeField] Spindle parentSpindle;
        public LifeForm LifeForm;
        [SerializeField] bool retainSpindle = false;

        HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();
        HashSet<Spindle> spindles = new HashSet<Spindle>();

        // RenderedObject + additionalRenderedObjects, flattened once. Parallel arrays, one
        // slot per renderer: its captured base material and the shared phase variant it draws
        // with. Every slot resolves its variant from the SPINDLE ROOT's position, so the parts
        // of one spindle always land in the same phase bucket and sway together - bucketing
        // per-renderer would desync a mirrored pair and tear it apart at the joint.
        Renderer[] _renderers;
        Material[] _phaseBaseMaterials;
        Material[] _phaseVariants;

        bool deregistered;
        bool dying = false;

        [SerializeField] bool permanentWither = true;
        bool isPermanentlyWithered = false;

        // ── Ordered death wither ─────────────────────────────────────────────
        // A dying lifeform spends its spindles ONE AT A TIME, in an order the death
        // itself dictates (Docs/ECOSYSTEM.md §26): outside-in along the spindle tree
        // (OrderOutsideIn), except the crystal joust, which unravels from the heart outward
        // (OrderHeartOutward, §26.10). Two couplings in the ordinary spindle lifecycle
        // fight that, and both are structural rather than cosmetic:
        //   • ForceWither RECURSES into child spindles, so withering an inner spindle
        //     first would collapse the whole creature in a single step.
        //   • Destroying a spindle GameObject destroys its child spindles with it.
        // Isolation breaks both up front - and suspends CheckForLife, so handing this
        // spindle's prisms to the skeleton cannot wither it out of turn.
        bool isolatedForOrderedWither;

        // The phase bucket this spindle's variant material was minted from, resolved in
        // Start alongside that material and cached so nothing can derive a DIFFERENT one
        // later. It has to be cached rather than re-hashed on demand because a spindle is
        // routinely Instantiated and only THEN posed (AssembledFlora), so the position the
        // bucket was chosen from is not the position it has a frame later — re-hashing
        // would hand a prism a phase its own limb is not using.
        float _swayPhase;
        bool _swayPhaseResolved;
        int _phaseBucket;

        // ── Distance LOD (SpindleLodSystem, Docs/SPINDLE_LOD.md) ─────────────────────────────
        // One handle per renderer the system took: a limb drawing a mesh the LOD profile knows is
        // tiered, and a creature's own spindle-material body wears a depth material (null where
        // neither applies; the whole array is null when none does). The system owns that
        // renderer's material (and a limb's mesh and enabled flag) while the handle is registered;
        // this class keeps owning life and death, and hands the renderer back before either.
        SpindleLodSystem.Handle[] _lodHandles;
        bool _started;
        // A condense/evaporate MPB stamp is live on the renderers. Such a limb must draw through
        // its own renderer, so the LOD never batches it.
        bool _fadeActive;

        void CleanupDeadRefs()
        {
            healthBlocks.RemoveWhere(h => !h);
            spindles.RemoveWhere(s => !s);
        }

        void Awake() => CacheRenderers();

        /// <summary>
        /// Flattens <see cref="RenderedObject"/> + <see cref="additionalRenderedObjects"/> into
        /// the array every visual path drives. Idempotent and called defensively from each entry
        /// point as well as from Awake, so no Awake/OnEnable ordering assumption is load-bearing.
        /// </summary>
        void CacheRenderers()
        {
            if (_renderers != null) return;

            // A creature whose BODY is an imported model cannot author this reference from
            // outside: the renderer lives inside a nested FBX PrefabInstance, so pointing at it
            // needs an fbx-internal fileID that only the importer knows. Rather than make that a
            // reason a species stays un-animated (the Clawfish was, for two years — see
            // Docs/ECOSYSTEM.md §45), resolve it from the spindle's own children.
            //
            // PRISMS ARE EXCLUDED, and that exclusion is the whole reason this is not just a
            // GetComponentsInChildren sweep: flora parents its HEALTH PRISM under the spindle
            // root, so adopting the first renderer found would hand conserved mass to the branch
            // animation and fade it with the branch. An authored RenderedObject always wins, so
            // every shipped spindle is bit-for-bit unchanged.
            if (RenderedObject == null) RenderedObject = ResolveRenderedObject();

            int extra = 0;
            if (additionalRenderedObjects != null)
                for (int i = 0; i < additionalRenderedObjects.Length; i++)
                    if (additionalRenderedObjects[i] && additionalRenderedObjects[i] != RenderedObject) extra++;

            _renderers = new Renderer[(RenderedObject ? 1 : 0) + extra];
            int n = 0;
            if (RenderedObject) _renderers[n++] = RenderedObject;
            if (additionalRenderedObjects != null)
                for (int i = 0; i < additionalRenderedObjects.Length && n < _renderers.Length; i++)
                {
                    var extraRenderer = additionalRenderedObjects[i];
                    if (extraRenderer && extraRenderer != RenderedObject) _renderers[n++] = extraRenderer;
                }

            _phaseBaseMaterials = new Material[_renderers.Length];
            _phaseVariants = new Material[_renderers.Length];
        }

        /// <summary>
        /// The first non-prism renderer under this spindle, or null. See
        /// <see cref="CacheRenderers"/> for why a prism can never be the answer.
        /// </summary>
        Renderer ResolveRenderedObject()
        {
            var candidates = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!candidate || candidate.sharedMaterial == null) continue;
                if (candidate.GetComponentInParent<Prism>(true)) continue;
                // ... and a HEART is not branch geometry either. A lifeform's crystal is drawn
                // by a SkinnedMeshRenderer under its own root, so a spindle that happened to
                // parent one would otherwise adopt it and fade the collectable with the limb.
                if (candidate.GetComponentInParent<Crystal>(true)) continue;
                return candidate;
            }

            return null;
        }

        void SetRenderersEnabled(bool value)
        {
            CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i]) _renderers[i].enabled = value;
        }

        void OnEnable()
        {
            // pooled spindles must be allowed to deregister again later
            if (!isPermanentlyWithered)
                deregistered = false;

            // A living limb re-enabled after Start (its plant was switched off and on) rejoins the
            // LOD; the first enable is Start's to handle, once the limb is posed.
            if (!isPermanentlyWithered && _started && _lodHandles == null)
                RegisterLod();

            if (!isPermanentlyWithered) return;

            SetRenderersEnabled(false);
            CancelFadeSettle();
        }

        void Start()
        {
            if (isPermanentlyWithered)
                return;

            // Before the guard, not after: CacheRenderers is what resolves an unauthored
            // RenderedObject, and it is idempotent, so calling it here costs nothing on the
            // ordinary Awake-first path and makes the guard correct on every other.
            CacheRenderers();

            if (RenderedObject == null || RenderedObject.sharedMaterial == null)
            {
                CSDebug.LogError($"{gameObject.name}: RenderedObject does not have a valid material at Start.");
                return;
            }

            // Desync the sway via a shared phase-variant material (see PhaseVariants) so the
            // spindle stays SRP-batchable — no per-renderer MaterialPropertyBlock. Capture each
            // base material once so pooled reuse never layers variants-on-variants, and bucket
            // EVERY part off the spindle root's position so a multi-part spindle sways as one.
            _phaseBucket = PhaseBucket(transform.position);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer) continue;
                if (_phaseBaseMaterials[i] == null) _phaseBaseMaterials[i] = partRenderer.sharedMaterial;
                _phaseVariants[i] = GetPhaseVariant(LeanedMaterial(_phaseBaseMaterials[i]), _phaseBucket);
                if (_phaseVariants[i]) partRenderer.sharedMaterial = _phaseVariants[i];
            }
            _swayPhase = PhaseForBucket(_phaseBucket);
            _swayPhaseResolved = true;
            // Any prism already bound to this limb finished creating before Start ran, so
            // its own creation stamp found no phase to ride. Stamp them now — the values
            // are pure functions of the attachment, so re-stamping is idempotent.
            RestampSway();

            if (!dying)
                StampCondense();

            if (LifeForm) LifeForm.AddSpindle(this);
            // Explicit Unity-null test: an unassigned serialized reference is a fake-null object in
            // the Editor, which `??=` treats as set (see HealthPrism.ResolveSpindle).
            if (!parentSpindle && transform.parent)
                parentSpindle = transform.parent.GetComponentInParent<Spindle>();
            if (parentSpindle) parentSpindle.AddSpindle(this);

            // Last: the limb is posed (a flora poses a spindle between Instantiate and its Start)
            // and wears its phase variant, which is the near tier the LOD derives its tiers from.
            _started = true;
            RegisterLod();
        }

        void RegisterLod()
        {
            CacheRenderers();
            // A Charge lifeform's limbs part hardest in the overt style (SpindleLodProfileSO.chargeStrength).
            // A flora limb names its LifeForm; a creature's spindles do not (the shark's, the
            // tadpole's), so the element is read off whatever lifeform this spindle is part of.
            ILifeFormEntity lifeform = LifeForm ? LifeForm : GetComponentInParent<ILifeFormEntity>();
            bool charge = lifeform != null && lifeform.Element == CosmicShore.Data.Element.Charge;
            SpindleLodSystem.Handle[] handles = null;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!_renderers[i] || !_phaseVariants[i]) continue;
                var handle = SpindleLodSystem.Register(_renderers[i], _phaseVariants[i], _fadeActive, charge);
                if (handle == null) continue;
                handles ??= new SpindleLodSystem.Handle[_renderers.Length];
                handles[i] = handle;
            }
            _lodHandles = handles;
        }

        /// <summary>Hands every renderer back to this limb: its authored mesh, its phase variant and
        /// enabled. <paramref name="restore"/> false when the renderers are being destroyed.</summary>
        void UnregisterLod(bool restore)
        {
            if (_lodHandles == null) return;
            for (int i = 0; i < _lodHandles.Length; i++)
                SpindleLodSystem.Unregister(_lodHandles[i], restore);
            _lodHandles = null;
        }

        void SetLodFading(bool fading)
        {
            _fadeActive = fading;
            if (_lodHandles == null) return;
            for (int i = 0; i < _lodHandles.Length; i++)
                SpindleLodSystem.SetFading(_lodHandles[i], fading);
        }

        // A shared material identical to baseMat but with a fixed _Phase, bucketed by world
        // position: same bucket -> same material -> one SRP batch. Created lazily and cached
        // per base material, so the total is bounded by the distinct spindle materials in play.
        static Material GetPhaseVariant(Material baseMat, int bucket)
        {
            if (baseMat == null) return null;

            // variants[0] == null catches destroyed (fake-null) materials: with Enter Play
            // Mode Options' domain reload disabled, the static dictionary survives play-mode
            // exit while its runtime-created materials are destroyed — rebuild in that case.
            if (!PhaseVariants.TryGetValue(baseMat, out var variants) || variants[0] == null)
            {
                variants = new Material[PhaseVariantCount];
                for (int i = 0; i < PhaseVariantCount; i++)
                {
                    variants[i] = new Material(baseMat) { name = $"{baseMat.name}_Phase{i}" };
                    variants[i].SetFloat(PhaseOffsetID, i / (float)PhaseVariantCount * Mathf.PI * 2f);
                }
                PhaseVariants[baseMat] = variants;
            }

            return variants[bucket];
        }

        /// Cheap position hash -> stable per-spindle bucket that scatters neighbours. Shared
        /// by the variant picker and by <see cref="TryGetSwayConstants"/>, so the phase a
        /// health prism is stamped with is the SAME number baked into the material its limb
        /// draws with — two copies of this hash is a desync nobody would look for.
        static int PhaseBucket(Vector3 worldPos)
        {
            float h = Mathf.Sin(worldPos.x * 12.9898f + worldPos.y * 78.233f + worldPos.z * 37.719f) * 43758.5453f;
            return Mathf.Clamp((int)((h - Mathf.Floor(h)) * PhaseVariantCount), 0, PhaseVariantCount - 1);
        }

        static float PhaseForBucket(int bucket) => bucket / (float)PhaseVariantCount * Mathf.PI * 2f;

        // ── Hue lean: a holder's mark on a tamed creature (Fauna.ApplyLeashMark) ─────────────────────
        // Garrett, 2026-10-21: "distinguish them by giving their spindles a slight hue shift in your domain". The
        // limb's colours lean toward a target hue; nothing else about it changes. Like the phase variants it is a SHARED
        // material per (base material, target, lean), minted under the phase variants and handed to the LOD as the near
        // material, so a marked limb still batches with every other limb wearing the same mark.

        static readonly int[] LeanColorIDs =
        {
            Shader.PropertyToID("_BrightColor"), Shader.PropertyToID("_DullColor"),   // SpindleGraph, SpindleDepthGraph
            Shader.PropertyToID("_Color1"), Shader.PropertyToID("_Color2"),           // AnimatedSpindleGraph
            Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_Color"),         // a creature's imported body
        };
        static readonly Dictionary<(Material, int, byte), Material> LeanedMaterials = new();   // (base, target RGBA, lean)

        Color _leanTarget;   // as given (HDR kept: clamping a bright colour to 8 bits can turn its hue)
        int _leanKey;        // the target as 8-bit RGBA, the cache key
        byte _lean;          // 0..255 of the way toward the target's hue; 0 = the limb's own colours

        /// <summary>
        /// Lean this limb's colours toward <paramref name="target"/>'s hue by <paramref name="lean"/> (0..1 of the way
        /// round the hue circle, the short way; saturation, value and HDR intensity kept). 0 restores its own colours.
        /// Presentation only. Safe before Start: Start mints the leaned material.
        /// </summary>
        public void SetHueLean(Color target, float lean)
        {
            byte amount = (byte)Mathf.RoundToInt(Mathf.Clamp01(lean) * 255f);
            Color32 rgba = amount > 0 ? (Color32)target : default;
            int key = rgba.r << 24 | rgba.g << 16 | rgba.b << 8 | rgba.a;
            if (amount == _lean && key == _leanKey) return;
            _lean = amount;
            _leanTarget = target;
            _leanKey = key;
            if (!_started || dying || isPermanentlyWithered) return;
            RemintVariants();
        }

        /// <summary>Put each renderer back on its (re-leaned) phase variant, through the LOD if it holds the renderer.</summary>
        void RemintVariants()
        {
            bool lod = _lodHandles != null;
            if (lod) UnregisterLod(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer || !_phaseBaseMaterials[i]) continue;
                _phaseVariants[i] = GetPhaseVariant(LeanedMaterial(_phaseBaseMaterials[i]), _phaseBucket);
                if (_phaseVariants[i]) partRenderer.sharedMaterial = _phaseVariants[i];
            }
            if (lod) RegisterLod();
        }

        Material LeanedMaterial(Material baseMat)
        {
            if (!baseMat || _lean == 0) return baseMat;
            var key = (baseMat, _leanKey, _lean);
            if (LeanedMaterials.TryGetValue(key, out var leaned) && leaned) return leaned;
            leaned = new Material(baseMat) { name = $"{baseMat.name}_Lean{_leanKey:X8}" };
            float amount = _lean / 255f;
            for (int i = 0; i < LeanColorIDs.Length; i++)
                if (leaned.HasProperty(LeanColorIDs[i]))
                    leaned.SetColor(LeanColorIDs[i], LeanHue(leaned.GetColor(LeanColorIDs[i]), _leanTarget, amount));
            LeanedMaterials[key] = leaned;
            return leaned;
        }

        /// <summary>
        /// <paramref name="color"/> with its hue turned <paramref name="lean"/> of the way toward
        /// <paramref name="target"/>'s, the short way round. Saturation, value (HDR included) and alpha are kept, so a
        /// grey stays grey and a grey target (no hue) changes nothing.
        /// </summary>
        public static Color LeanHue(Color color, Color target, float lean)
        {
            Color.RGBToHSV(target, out float th, out float ts, out _);
            if (ts < 0.05f || lean <= 0f) return color;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            float turn = Mathf.DeltaAngle(h * 360f, th * 360f) / 360f;
            var leaned = Color.HSVToRGB(Mathf.Repeat(h + turn * Mathf.Clamp01(lean), 1f), s, v, true);
            leaned.a = color.a;
            return leaned;
        }

        /// <summary>The transform whose OBJECT SPACE the sway actually happens in. This is
        /// the RENDERER's, not the spindle root's: `SpindleSway` shears `PositionOS`, and
        /// PositionOS is the rendered mesh's own space. The two coincide on a spindle whose
        /// geometry sits at local identity and do NOT on one whose mesh is posed under it —
        /// the Clawfish's body is a nested FBX instance carried at an offset — so baking a
        /// prism's basis off the root would shear it about an axis its limb is not using.</summary>
        internal Transform SwayFrame
        {
            get
            {
                CacheRenderers();
                return RenderedObject ? RenderedObject.transform : transform;
            }
        }

        /// <summary>The sway this limb is actually running, for anything BOLTED to it to
        /// ride (Docs/ECOSYSTEM.md §47). Amplitude and frequency are read off the material
        /// the spindle draws with — the phase variant is a clone of the base, so both carry
        /// the authored values either way — and the phase is the bucket that variant was
        /// minted from. Returns false until Start has resolved the bucket, and false for a
        /// material that authors no sway, which is the honest answer: a prism bolted to a
        /// motionless limb must not move.</summary>
        internal bool TryGetSwayConstants(out float amplitude, out float frequency, out float phase)
        {
            amplitude = frequency = phase = 0f;
            if (!_swayPhaseResolved) return false;
            CacheRenderers();
            var mat = RenderedObject ? RenderedObject.sharedMaterial : null;
            if (mat == null || !mat.HasProperty(SwayAmplitudeID)) return false;
            amplitude = mat.GetFloat(SwayAmplitudeID);
            if (Mathf.Approximately(amplitude, 0f)) return false;
            frequency = mat.HasProperty(SwayFrequencyID) ? mat.GetFloat(SwayFrequencyID) : 0f;
            phase = _swayPhase;
            return true;
        }

        public void AddHealthBlock(HealthPrism healthPrism)
        {
            if (isPermanentlyWithered) return;
            if (!healthPrism) return;

            healthBlocks.Add(healthPrism);
            healthPrism.LifeForm = LifeForm;
            PrismSway.TryStamp(healthPrism, this);
        }

        /// Re-applies the living-mass sway to every prism on this limb. Cheap and
        /// idempotent: every stamped value is a constant of the attachment, so this
        /// writes the same numbers it wrote last time.
        void RestampSway()
        {
            CleanupDeadRefs();
            foreach (var prism in healthBlocks)
                PrismSway.TryStamp(prism, this);
        }

        /// <summary>Drops a prism from this limb's list with NO life check - the prism is
        /// leaving for the pool (<see cref="HealthPrism.DetachForPool"/>), not dying.</summary>
        public void ForgetHealthBlock(HealthPrism healthPrism)
        {
            if (healthPrism) healthBlocks.Remove(healthPrism);
        }

        public void RemoveHealthBlock(HealthPrism healthPrism)
        {
            if (!healthPrism) return;
            healthBlocks.Remove(healthPrism);
            CheckForLife();
        }

        public void AddSpindle(Spindle spindle)
        {
            if (isPermanentlyWithered) return;
            if (!spindle) return;

            spindles.Add(spindle);
            spindle.parentSpindle = this;
        }

        public void RemoveSpindle(Spindle spindle)
        {
            if (!spindle) return;
            spindles.Remove(spindle);
            CheckForLife();
        }

        /// <summary>
        /// Hangs this limb off <paramref name="parent"/> in the spindle TREE (null = off the heart)
        /// without touching the transform hierarchy. For a species that keeps its limbs as flat
        /// siblings under the plant root and poses each one on a bond (<c>BorromeanFlora</c>,
        /// <c>MandelbulbFlora</c>) this is the only thing that makes the tree real - and the tree
        /// is what keeps a limb standing while anything still hangs off it, so a plant can never
        /// lose the limb between its crystal and its outer prisms (Docs/ECOSYSTEM.md §26.10).
        /// Re-linking releases the old parent, which then withers if nothing else holds it up.
        /// </summary>
        public void AttachToParent(Spindle parent)
        {
            if (parent == this || parentSpindle == parent) return;

            var previous = parentSpindle;
            parentSpindle = null;
            if (previous) previous.RemoveSpindle(this);

            if (parent) parent.AddSpindle(this);
        }

        /// <summary>
        /// This limb's parent in the spindle tree: the logical link when one is set, otherwise
        /// the nearest spindle above it in the transform hierarchy - which is what
        /// <see cref="Start"/> would link it to, read before Start has run.
        /// </summary>
        Spindle ResolveTreeParent()
        {
            if (parentSpindle) return parentSpindle;
            var up = transform.parent;
            return up ? up.GetComponentInParent<Spindle>() : null;
        }

        /// <summary>
        /// How many limbs stand between this one and the heart (0 = it grows straight out of the
        /// crystal). Bounded so a malformed link can never hang the death path.
        /// </summary>
        int TreeDepth()
        {
            int depth = 0;
            for (var p = ResolveTreeParent(); p && depth < 4096; p = p.ResolveTreeParent())
                depth++;
            return depth;
        }

        /// <summary>
        /// The order an ordered death wither spends a lifeform's limbs in: OUTSIDE-IN along the
        /// spindle tree, deepest first, so every limb still standing has an unbroken path of
        /// standing limbs back to the heart at every instant of the wither. Ties (and a
        /// lifeform whose limbs form no tree at all) fall back to farthest-from-the-heart first.
        /// Euclidean distance alone is NOT enough on a plant whose surface wraps back past its
        /// own heart (the Borromean membrane): a limb five hops out can sit closer to the crystal
        /// than its own grandparent.
        ///
        /// <para>Every ordered wither uses this EXCEPT the crystal joust, which unravels
        /// <see cref="OrderHeartOutward"/> from the hole the joust left (Docs/ECOSYSTEM.md
        /// §26.10).</para>
        ///
        /// <para>Must be called BEFORE <see cref="IsolateForOrderedWither"/>, which is what
        /// severs the links the depth is read from.</para>
        /// </summary>
        public static List<Spindle> OrderOutsideIn(IEnumerable<Spindle> spindles, Vector3 heart)
        {
            return spindles
                .Where(s => s)
                .Select(s => (spindle: s, depth: s.TreeDepth(),
                              distance: (s.transform.position - heart).sqrMagnitude))
                .OrderByDescending(e => e.depth)
                .ThenByDescending(e => e.distance)
                .Select(e => e.spindle)
                .ToList();
        }

        /// <summary>
        /// The crystal joust's EXCEPTION to <see cref="OrderOutsideIn"/>, and its exact reverse:
        /// FROM THE MISSING CRYSTAL TO THE LEAVES along the spindle tree, the limbs at the heart
        /// first. The jouster took the heart, so the body comes apart around the hole it left —
        /// the one death in which an outer limb is ALLOWED to stand after the limb joining it to
        /// the heart has gone (Docs/ECOSYSTEM.md §26.10). Read off the tree rather than distance
        /// so the unravel follows the plant's own growth order on a surface that folds back past
        /// its heart. Same call-before-isolation rule.
        /// </summary>
        public static List<Spindle> OrderHeartOutward(IEnumerable<Spindle> spindles, Vector3 heart)
        {
            var order = OrderOutsideIn(spindles, heart);
            order.Reverse();
            return order;
        }

        /// <summary>
        /// Sets this spindle aside for an ORDERED death wither (see the isolation notes):
        /// detaches it from its parent and children - logically AND in the hierarchy, so it
        /// can be destroyed without taking anything else with it - and suspends
        /// <see cref="CheckForLife"/> so losing its prisms to the skeleton doesn't evaporate
        /// it before its turn. The caller then walks the isolated spindles in whatever order
        /// the death dictates, calling <see cref="ForceWither(float)"/> on each with a
        /// start-time offset. Idempotent; a spindle already dying or withered is left alone.
        /// </summary>
        public void IsolateForOrderedWither(Transform detachedParent)
        {
            if (dying || isPermanentlyWithered || isolatedForOrderedWither) return;
            isolatedForOrderedWither = true;

            if (parentSpindle)
            {
                parentSpindle.spindles.Remove(this);
                parentSpindle = null;
            }

            foreach (var child in spindles)
                if (child) child.parentSpindle = null;
            spindles.Clear();

            if (transform.parent != detachedParent)
                transform.SetParent(detachedParent, true);
        }

        public void CheckForLife()
        {
            if (dying || isPermanentlyWithered || isolatedForOrderedWither) return;

            CleanupDeadRefs();

            if (healthBlocks.Count > 0 || spindles.Count > 0) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;
            EvaporateSpindle();
        }

        void EvaporateSpindle() => StampEvaporate(0f);

        void RestoreOriginalMaterial()
        {
            // Clearing the property block is what restores SRP batching; the shared
            // phase-variant material itself was never swapped out. The null guard on the
            // variant matters on the error path: Start bails before assigning one when the
            // material is invalid, and writing that null back would blank the renderer.
            CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer) continue;
                // A limb the LOD is drawing keeps its TIER's material (a clone of the same phase
                // variant on the mid/far graph); writing the near variant back would put the
                // braid's shader on the thread's mesh.
                var tierMaterial = _lodHandles != null ? SpindleLodSystem.CurrentMaterial(_lodHandles[i]) : null;
                if (tierMaterial) partRenderer.sharedMaterial = tierMaterial;
                else if (_phaseVariants[i]) partRenderer.sharedMaterial = _phaseVariants[i];
                partRenderer.SetPropertyBlock(null);
            }
        }

        void CancelFadeSettle() => PrismTimerManager.Instance?.CancelScheduledActions(this);

        void StampCondense()
        {
            StampDeathFade(PrismClock.Now, DeathFadeDuration, DeathDirectionCondense, OnCondenseSettled);
        }

        void StampEvaporate(float delay)
        {
            // One start time for both: the shard grows exactly while the limb fades.
            float start = PrismClock.Now + delay;
            StampDeathFade(start, DeathFadeDuration, DeathDirectionEvaporate, OnEvaporateSettled);
            MorphIntoShard(start);
        }

        /// <summary>
        /// A withering or dying limb becomes a shard of its cell's cytoplasm (Garrett, 2026-10-21;
        /// Docs/ECOSYSTEM.md, "A dying limb becomes a cytoplasm shard"): the shard joins the
        /// SnowChanger's shard list where the limb stood and condenses on the limb's own fade
        /// clock, so the limb fades as the shard grows and an ordered wither stays ordered. Every
        /// death of a spindle runs through <see cref="StampEvaporate"/> exactly once (both callers
        /// latch <c>dying</c>), so a limb makes at most one shard. No cell, a satellite (no
        /// cytoplasm), or a full field: the limb evaporates as it always did.
        /// </summary>
        void MorphIntoShard(float startTime)
        {
            if (!gameObject.scene.isLoaded) return;

            // The limb's middle, not its root: a limb is posed from its root along its forward.
            // Local bounds read the same whether or not the LOD has the renderer switched off.
            Vector3 at = transform.position;
            if (RenderedObject)
                at = RenderedObject.transform.TransformPoint(RenderedObject.localBounds.center);

            var cell = Cell.FindCellContaining(at);
            if (cell && cell.Cytoplasm)
                cell.Cytoplasm.AddMorphedShard(at, transform.rotation, startTime, DeathFadeDuration);
        }

        void StampDeathFade(float startTime, float duration, float direction, Action onSettle)
        {
            // Out of the instanced batch first: the fade is this renderer's own stamp.
            SetLodFading(true);
            CancelFadeSettle();
            CacheRenderers();
            s_fadeMpb ??= new MaterialPropertyBlock();
            s_fadeMpb.Clear();
            s_fadeMpb.SetFloat(DeathStartTimeID, startTime);
            s_fadeMpb.SetFloat(DeathDurationID, duration);
            s_fadeMpb.SetFloat(DeathDirectionID, direction);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer) continue;
                var mat = partRenderer.sharedMaterial;
                if (mat && !mat.HasProperty(DeathStartTimeID))
                    PrismClockDiagnostics.WarnUnwiredMaterial(mat, "_DeathStartTime", this);
                partRenderer.SetPropertyBlock(s_fadeMpb);
            }

            float delay = Mathf.Max(0f, startTime + duration - PrismClock.Now);
            PrismTimerManager.EnsureInstance()?.ScheduleAction(this, delay, onSettle);
        }

        void OnCondenseSettled()
        {
            RestoreOriginalMaterial();
            SetLodFading(false);
        }

        void OnEvaporateSettled()
        {
            // Must run even if the renderer is gone: bailing here leaves a dying=true
            // spindle registered forever and stalls LifeForm.DieCoroutine's empty-tracker wait.
            UnregisterLod(restore: true);
            RestoreOriginalMaterial();
            SetRenderersEnabled(false);
            DisableSpindle();

            if (retainSpindle)
                gameObject.SetActive(false);
            else
                Destroy(gameObject);
        }

        /// <param name="evaporateDelay">
        /// Seconds until this spindle's fade STARTS. Ordered wither stamps every
        /// spindle in one pass with <c>i * interval</c> so starvation stays outside-in
        /// (<see cref="OrderOutsideIn"/>) and a joust stays heart-outward
        /// (<see cref="OrderHeartOutward"/>) — never a per-frame cascade.
        /// </param>
        public void ForceWither(float evaporateDelay = 0f)
        {
            if (dying || isPermanentlyWithered) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;

            foreach (var child in spindles.ToArray())
            {
                if (child) child.ForceWither(evaporateDelay);
            }

            StampEvaporate(evaporateDelay);
        }

        void DisableSpindle()
        {
            RestoreOriginalMaterial();

            if (!gameObject.scene.isLoaded) return;

            if (parentSpindle)
            {
                parentSpindle.RemoveSpindle(this);
                parentSpindle.CheckForLife();
            }

            if (LifeForm)
            {
                LifeForm.RemoveSpindle(this);
                LifeForm.CheckIfDead();
            }
        }

        void OnDisable()
        {
            // Before anything else: whatever happens next to this limb, its renderers are its own
            // again (a pooled or deactivated limb must come back as the authored braid).
            UnregisterLod(restore: gameObject.scene.isLoaded);

            if (deregistered) return;

            // only deregister if we are truly gone (dying/perma-wither) or being unloaded
            if (!dying && !isPermanentlyWithered && gameObject.scene.isLoaded) return;

            deregistered = true;

            // During scene unload, only remove references - don't trigger the death
            // cascade (CheckForLife/CheckIfDead) which explodes prisms, accesses
            // disposed NativeArrays, and spawns new GameObjects during teardown.
            // RemoveSpindle itself runs CheckForLife/CheckIfDead, so on unload it must not be
            // called at all - it evaporated the parent (re-spawning [PrismTimerManager]) and
            // killed the LifeForm (its structure exploding into a fresh [PrismDebris]), both
            // leaked into the closing scene.
            bool sceneUnloading = !gameObject.scene.isLoaded;

            if (parentSpindle)
            {
                if (sceneUnloading)
                    parentSpindle.spindles.Remove(this);
                else
                {
                    parentSpindle.RemoveSpindle(this);
                    parentSpindle.CheckForLife();
                }
            }

            // The LifeForm goes down with the same scene, so there is nothing to deregister from.
            if (LifeForm && !sceneUnloading)
            {
                LifeForm.RemoveSpindle(this);
                LifeForm.CheckIfDead();
            }
        }

        void OnDestroy()
        {
            UnregisterLod(restore: false);
            CancelFadeSettle();
            if (deregistered) return;
            deregistered = true;
            DisableSpindle();
        }
    }
}