using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY into the vessel that took it — the placeholder replacement for the shared
    /// husk spray on every hull whose omni ability puts nothing in the world to land on: the Brittle Star (a spare ring
    /// charge), the Sparrow (its ward), the Stoat (overclock), the Orca (its magazine) and the Wrasse (board motes). Full
    /// record: <c>_Scripts/Controller/Vessel/R_VesselActions/VESSEL_ABSORB_CRYSTAL_MORPH.md</c>.
    ///
    /// ── The vessel takes the crystal in ───────────────────────────────────────────────────────
    /// The cage folds onto a shell just inside the hull's own bounds (<see cref="VesselAbsorbMorphGeometry"/>), each
    /// vertex keeping its direction from the crystal's middle (<see cref="CrystalMorphRecast"/>), and dissolves there: the
    /// ability the pickup gave is now IN the vessel. A design placeholder, chosen so each hull can later be given its own
    /// target (a ward shell, a magazine round) the way the Squirrel and the Dolphin were; until then the pickup reads as
    /// one event instead of a shatter.
    ///
    /// ── It rides the hull ─────────────────────────────────────────────────────────────────────
    /// The target is known at the pickup, so the morph stamps at once and then follows the hull's pose every frame. It
    /// holds nothing: the vessel stays drawn throughout and nothing in the vessel kit changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VesselAbsorbCrystalMorph : CrystalMorphRunner
    {
        string _owner = "VesselAbsorb";
        Transform _hull;
        Matrix4x4 _morphInHull;
        Color _dark, _bright;
        bool _haveColour;

        protected override string Owner => _owner;

        public static VesselAbsorbCrystalMorph Begin(in CrystalForgeOrigin origin, IVesselStatus status, string owner)
        {
            if (status == null) return null;
            string tag = string.IsNullOrEmpty(owner) ? "VesselAbsorb" : owner;
            if (!origin.Valid)
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] the pickup carried no crystal origin, so there is no body to " +
                                   "morph — this pickup retires with no animation.");
                return null;
            }

            var crystal = ResolveCrystal(origin);
            if (crystal != null && !crystal.HasBody) return null;
            if (crystal == null)
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] no crystal with id {origin.CrystalId} on this peer, so there " +
                                   "is no body to carry into the vessel — this pickup retires with no animation.");
                return null;
            }

            var animation = status.VesselAnimation;
            var rig = animation ? CrystalHullFusion.FindHull(animation.transform) : null;
            if (rig == null || !rig.Space || !TryHullBounds(rig, out var bounds))
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] {status.VesselType} has no visible hull mesh under its " +
                                   "VesselAnimation, so there is nothing to fold into — this pickup retires with no animation.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<VesselAbsorbCrystalMorph>();
            morph._owner = tag;
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] '{crystal.name}' exposed no drawable body — nothing to morph, " +
                                   "so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }
            morph.ReadCrystalColours(crystal);

            if (!morph.TryBuildIntoHull(rig.Space, bounds, out var mesh, out string refusal))
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] cannot fold into the hull: {refusal}. This pickup retires " +
                                   "with no animation.");
                Destroy(go);
                return null;
            }

            morph._hull = rig.Space;
            morph.Stamp(mesh);
            morph._morphInHull = rig.Space.worldToLocalMatrix * go.transform.localToWorldMatrix;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] {tag}: folding '{crystal.name}' into '{rig.Name}' " +
                $"({Vector3.Distance(origin.Position, rig.Space.position):F1}u away), {morph.Census}, " +
                $"{morph.MorphVertexCount} morph vertices.");
            return morph;
        }

        /// <summary>The hull's bounds in its own frame: a skin's local bounds, or every static part's mesh bounds.</summary>
        static bool TryHullBounds(CrystalHullFusion.HullRig rig, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toSpace = rig.Space.worldToLocalMatrix;
            if (rig.Skinned)
            {
                var root = rig.Skinned.rootBone ? rig.Skinned.rootBone : rig.Skinned.transform;
                VesselAbsorbMorphGeometry.Encapsulate(ref bounds, ref any, rig.Skinned.localBounds,
                                                      toSpace * root.localToWorldMatrix);
            }
            else if (rig.Parts != null)
            {
                foreach (var part in rig.Parts)
                    if (part && part.sharedMesh)
                        VesselAbsorbMorphGeometry.Encapsulate(ref bounds, ref any, part.sharedMesh.bounds,
                                                              toSpace * part.transform.localToWorldMatrix);
            }
            return any;
        }

        bool TryBuildIntoHull(Transform space, Bounds hullBounds, out Mesh mesh, out string refusal)
        {
            mesh = null;
            refusal = null;
            var source = SourceMesh;
            if (source == null) { refusal = "the crystal's body exposed no mesh"; return false; }

            Matrix4x4 toLocal = BodyFrame.worldToLocalMatrix * space.localToWorldMatrix *
                                VesselAbsorbMorphGeometry.UnitSphereToShell(hullBounds);
            var sphere = IcosphereMeshGenerator.Generate(3, 1f, true);
            try
            {
                Vector3 centre = toLocal.MultiplyPoint3x4(Vector3.zero);
                if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(sphere, toLocal, centre, out var target, out refusal))
                    return false;
                mesh = CrystalMorphRecast.TryBuildAround(source, in target, source.bounds.center,
                                                         Config.PhaseNear, Config.PhaseFar, out refusal);
                return mesh != null;
            }
            finally
            {
                Destroy(sphere);
            }
        }

        /// <summary>The morph keeps the crystal's own colour pair: it sinks into the hull as itself.</summary>
        void ReadCrystalColours(Crystal crystal)
        {
            var models = crystal.CrystalModels;
            for (int i = 0; models != null && i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (!model || !model.TryGetComponent<Renderer>(out var r)) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                var mat = r.sharedMaterial;
                bool hasDark = block.HasColor(DarkColorId) || (mat && mat.HasProperty(DarkColorId));
                bool hasBright = block.HasColor(BrightColorId) || (mat && mat.HasProperty(BrightColorId));
                if (!hasDark || !hasBright) continue;
                _dark = block.HasColor(DarkColorId) ? block.GetColor(DarkColorId) : mat.GetColor(DarkColorId);
                _bright = block.HasColor(BrightColorId) ? block.GetColor(BrightColorId) : mat.GetColor(BrightColorId);
                _haveColour = true;
                return;
            }
        }

        protected override void LateUpdate()
        {
            if (_hull)
            {
                var m = _hull.localToWorldMatrix * _morphInHull;
                transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
            }
            base.LateUpdate();
        }

        protected override bool TryReadTargetColours(out Color dark, out Color bright)
        {
            dark = _dark;
            bright = _bright;
            return _haveColour;
        }

        // Nothing is held: the vessel is drawn throughout.
        protected override void HandOff() { }
        protected override void ReleaseHold() { }
    }
}
