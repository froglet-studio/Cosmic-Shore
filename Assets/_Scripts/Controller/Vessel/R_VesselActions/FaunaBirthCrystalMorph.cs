using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY into the HEART of the creature the same pickup is
    /// born as — the bespoke replacement for the shared husk spray on every hull whose omni-crystal
    /// ability is a creature: the Angler (the crystal morphs into a random creature that joins the
    /// school) and the Jacks (an omnivorous worm in the collector's domain). Full record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/FAUNA_BIRTH_CRYSTAL_MORPH.md</c>.
    ///
    /// ── The crystal becomes the creature's HEART ──────────────────────────────────────────────
    /// Every creature carries its element in one embedded crystal, its heart, sized to the one
    /// elemental crystal size. That is the target: crystal into crystal, about the same size. The
    /// body blooms around it exactly as it always did. A worm colony is heartless at its root and
    /// carries a heart per segment; the crystal becomes the one nearest where it was collected
    /// (<see cref="FaunaHeartMorphGeometry.NearestHeart"/>). The cage keeps each vertex's direction
    /// from the crystal's own middle (<see cref="CrystalMorphRecast"/>), so a heart born 30 u behind
    /// the hull (the Jacks') is reached without the plates piling onto one side.
    ///
    /// ── It FOLLOWS the heart ──────────────────────────────────────────────────────────────────
    /// A newborn swims from its first frame, so after the stamp the morph rides the heart's pose
    /// every frame (it is never parented under the creature, so a pooled or killed creature cannot
    /// take the morph, or the hold, with it).
    ///
    /// ── MECHANICALLY INSTANT, VISUALLY GRADUAL ─────────────────────────────────────────────────
    /// Only the heart's PHOTONS wait (<c>Renderer.forceRenderingOff</c> on the heart's own
    /// renderers). The creature is born, registered, leashed and simulated from the pickup frame as
    /// before; nothing in the ecology knows the morph exists (<see cref="CrystalFaunaBirth"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FaunaBirthCrystalMorph : CrystalMorphRunner
    {
        IVesselStatus _status;
        string _owner = "FaunaBirth";
        Vector3 _collectPosition;
        Fauna _pending;
        Crystal _heart;
        readonly List<Renderer> _held = new();
        Matrix4x4 _morphInHeart;
        bool _following;
        bool _subscribed;
        string _declined;

        protected override string Owner => _owner;

        public static FaunaBirthCrystalMorph Begin(in CrystalForgeOrigin origin, IVesselStatus status, string owner)
        {
            if (status == null) return null;
            string tag = string.IsNullOrEmpty(owner) ? "FaunaBirth" : owner;
            if (!origin.Valid)
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] the pickup carried no crystal origin, so there is no " +
                                   "body to morph — this pickup retires with no animation. " +
                                   "CrystalImpactData.FromCrystal stamps it on every collect.");
                return null;
            }

            var crystal = ResolveCrystal(origin);
            if (crystal != null && !crystal.HasBody) return null;
            if (crystal == null)
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] no crystal with id {origin.CrystalId} on this peer, so " +
                                   "there is no body to carry into the creature — this pickup retires with no " +
                                   "animation. A peer whose cell has not finished initialising cannot find it.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<FaunaBirthCrystalMorph>();
            morph._owner = tag;
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] '{crystal.name}' exposed no drawable body — nothing to " +
                                   "morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._status = status;
            morph._collectPosition = origin.Position;
            morph.BeginWaiting();
            CrystalFaunaBirth.Born += morph.OnBorn;
            CrystalFaunaBirth.Declined += morph.OnDeclined;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] {tag}: began on '{crystal.name}' at {origin.Position}, {morph.Census}; " +
                $"waiting up to {morph.Config.TargetGraceSeconds:F2}s for the creature.");
            return morph;
        }

        bool IsThisVessel(IVesselStatus other)
        {
            if (other == null || _status == null) return false;
            if (ReferenceEquals(other, _status)) return true;
            return other.Transform && other.Transform == _status.Transform;
        }

        void OnBorn(IVesselStatus status, Fauna creature)
        {
            if (IsStamped || _pending || !creature || !IsThisVessel(status)) return;

            // This pickup's creature is born where the crystal was (the Angler) or just behind the
            // hull (the Jacks). A morph still waiting out a declined birth must not claim a later one.
            float distance = Vector3.Distance(creature.transform.position, _collectPosition);
            if (distance > Config.RingCaptureRadius)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] {Owner}: skipped '{creature.name}' born {distance:F0}u from the collect " +
                    $"(capture radius {Config.RingCaptureRadius:F0}u).");
                return;
            }

            _pending = creature;
            TryLand();   // a colony grows its segments' hearts later: LateUpdate keeps trying
        }

        void OnDeclined(IVesselStatus status, string reason)
        {
            if (IsStamped || _pending || !IsThisVessel(status)) return;
            _declined = reason;
        }

        void TryLand()
        {
            if (IsStamped || !_pending) return;

            var heart = PickHeart(_pending);
            if (!heart) return;

            if (!TryBuildOntoHeart(heart, out var mesh, out string refusal))
            {
                CSDebug.LogWarning($"[{Owner}CrystalMorph] cannot land on the heart of '{_pending.name}': " +
                                   $"{refusal}. The crystal will fade instead; the creature is unaffected.");
                _pending = null;
                return;
            }

            Unsubscribe();
            _heart = heart;
            Hold(heart);
            Stamp(mesh);
            _morphInHeart = heart.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            _following = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] {Owner}: landing on the heart of '{_pending.name}' " +
                $"{Vector3.Distance(heart.transform.position, _collectPosition):F1}u from the collect — " +
                $"{MorphVertexCount} morph vertices, colour target {(HasTargetColour ? "read" : "NOT FOUND")}.");
        }

        readonly List<Crystal> _hearts = new();
        readonly List<Crystal> _drawnHearts = new();
        readonly List<Vector3> _heartPositions = new();
        MaterialPropertyBlock _heartBlock;

        Crystal PickHeart(Fauna creature)
        {
            creature.GetComponentsInChildren(false, _hearts);
            _drawnHearts.Clear();
            _heartPositions.Clear();
            foreach (var h in _hearts)
            {
                if (!h || !h.HasBody || !TryGetHeartBody(h, out _, out _)) continue;
                _drawnHearts.Add(h);
                _heartPositions.Add(h.transform.position);
            }
            int i = FaunaHeartMorphGeometry.NearestHeart(_heartPositions, _collectPosition);
            return i >= 0 ? _drawnHearts[i] : null;
        }

        /// <summary>The mesh the heart's body draws and its transform: a MeshFilter's, or a skin's at
        /// rest (the Space heart is skinned), the same choice the crystal's own adoption makes.</summary>
        static bool TryGetHeartBody(Crystal heart, out Mesh mesh, out Renderer renderer)
        {
            mesh = null;
            renderer = null;
            var models = heart.CrystalModels;
            for (int i = 0; models != null && i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (!model) continue;
                if (model.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh &&
                    model.TryGetComponent<MeshRenderer>(out var meshRenderer))
                {
                    mesh = filter.sharedMesh;
                    renderer = meshRenderer;
                    return true;
                }
                if (model.TryGetComponent<SkinnedMeshRenderer>(out var skin) && skin.sharedMesh)
                {
                    mesh = RhombusSkinBaker.SourceOf(skin.sharedMesh);
                    renderer = skin;
                    return true;
                }
            }
            return false;
        }

        bool TryBuildOntoHeart(Crystal heart, out Mesh mesh, out string refusal)
        {
            mesh = null;
            refusal = null;
            var source = SourceMesh;
            if (source == null) { refusal = "the crystal's body exposed no mesh"; return false; }
            if (!TryGetHeartBody(heart, out var heartMesh, out var heartRenderer))
            {
                refusal = "the heart draws no mesh";
                return false;
            }
            if (!heartMesh.isReadable)
            {
                refusal = $"the heart mesh '{heartMesh.name}' is not Read/Write enabled on import";
                return false;
            }

            Matrix4x4 toLocal = BodyFrame.worldToLocalMatrix * heartRenderer.transform.localToWorldMatrix;
            Vector3 centre = toLocal.MultiplyPoint3x4(heartMesh.bounds.center);

            if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    heartMesh, toLocal, centre, out var target, out string hullDiagnosis))
            {
                refusal = $"cannot read the heart: {hullDiagnosis}";
                return false;
            }

            mesh = CrystalMorphRecast.TryBuildAround(source, in target, source.bounds.center,
                                                     Config.PhaseNear, Config.PhaseFar, out string diagnosis);
            if (mesh == null) { refusal = diagnosis; return false; }
            return true;
        }

        void Hold(Crystal heart)
        {
            _held.Clear();
            foreach (var r in heart.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || r.forceRenderingOff) continue;   // already off for its own reason: not ours to restore
                r.forceRenderingOff = true;
                _held.Add(r);
            }
        }

        protected override void LateUpdate()
        {
            if (!IsStamped) TryLand();

            if (_following)
            {
                // Ride the heart while it is drawn; a heart that is gone or pooled releases its hold
                // and the plates finish where they are.
                if (_heart && _heart.gameObject.activeInHierarchy)
                {
                    var m = _heart.transform.localToWorldMatrix * _morphInHeart;
                    transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                }
                else
                {
                    ReleaseHold();
                    _following = false;
                }
            }

            base.LateUpdate();
        }

        protected override bool TryReadTargetColours(out Color dark, out Color bright)
        {
            dark = bright = default;
            if (!_heart || !TryGetHeartBody(_heart, out _, out var r)) return false;
            var block = _heartBlock ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            var mat = r.sharedMaterial;
            bool HasPair(int id) => block.HasColor(id) || (mat && mat.HasProperty(id));
            if (!HasPair(DarkColorId) || !HasPair(BrightColorId)) return false;
            dark = block.HasColor(DarkColorId) ? block.GetColor(DarkColorId) : mat.GetColor(DarkColorId);
            bright = block.HasColor(BrightColorId) ? block.GetColor(BrightColorId) : mat.GetColor(BrightColorId);
            return true;
        }

        protected override void HandOff() => ReleaseHold();

        /// <summary>Gives the heart its photons back. Idempotent and called from teardown — an
        /// unreleased hold would leave the creature heartless to look at for its whole life.</summary>
        protected override void ReleaseHold()
        {
            for (int i = 0; i < _held.Count; i++)
                if (_held[i]) _held[i].forceRenderingOff = false;
            _held.Clear();
        }

        protected override void OnGaveUp()
        {
            Unsubscribe();
            if (_declined != null)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] {Owner}: no creature was born ({_declined}); the crystal fades.");
                return;
            }
            if (_pending)
            {
                CSDebug.LogWarning(
                    $"[{Owner}CrystalMorph] '{_pending.name}' was born but showed no drawn heart within " +
                    $"{Config.TargetGraceSeconds:F2}s, so the crystal is fading out instead of morphing. Every " +
                    "creature carries its element in an embedded Crystal (Fauna.ProvisionHeart).");
                return;
            }
            CSDebug.LogWarning(
                $"[{Owner}CrystalMorph] no creature from this vessel was born within " +
                $"{Config.TargetGraceSeconds:F2}s, so the crystal is fading out instead of morphing. The creature " +
                "is born by a SIBLING effect in the vessel's VesselCrystalEffects (the Angler's " +
                "AnglerTameByCrystalEffect, the Jacks' VesselSpawnFaunaByCrystalEffect), which announces it " +
                "through CrystalFaunaBirth — check it is still wired. Trace with FrogletTools ▸ Toolbox ▸ " +
                "Logging ▸ CrystalMorph.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            CrystalFaunaBirth.Born -= OnBorn;
            CrystalFaunaBirth.Declined -= OnDeclined;
            _subscribed = false;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();   // releases the hold
        }
    }
}
