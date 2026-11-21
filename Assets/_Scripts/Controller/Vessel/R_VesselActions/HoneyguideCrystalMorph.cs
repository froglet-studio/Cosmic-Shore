using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY into the hive comb the same pickup lays — the Honeyguide's bespoke
    /// replacement for the shared husk spray ("the omnicrystal becomes the hive", <see cref="HoneyguideCrew"/>). Full
    /// record: <c>_Scripts/Controller/Vessel/R_VesselActions/HONEYGUIDE_CRYSTAL_MORPH.md</c>.
    ///
    /// ── The crystal breaks into the comb's HEART ──────────────────────────────────────────────
    /// The omni cage is 64 panels, eight octahedra's worth, and the Squirrel's panel census lands every panel on one
    /// octahedron face, 1:1. A comb is up to 37 super-shielded cells, so the crystal spreads onto the eight nearest the
    /// comb's middle (<see cref="HiveCombMorphGeometry.CentralCells"/>), each landing on the octahedron its stellation is
    /// built around (the two tetrahedra's intersection: the spike-tip cube's face centres). The rest of the comb blooms
    /// in around them as it always did.
    ///
    /// ── MECHANICALLY INSTANT, VISUALLY GRADUAL ─────────────────────────────────────────────────
    /// Only the eight cells' PHOTONS wait (<see cref="Prism.SetOwnerHidden"/>). The hive, its super shields and the raid
    /// run from the frame the comb is laid. At the hand-off the eight cells' own blooms are settled while still hidden
    /// (the Squirrel's rule: the morph IS their arrival), then they are revealed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoneyguideCrystalMorph : CrystalMorphRunner
    {
        IVesselStatus _status;
        Vector3 _collectPosition;
        readonly List<Prism> _held = new();
        Color _targetDark, _targetBright;
        bool _haveTargetColour;
        bool _subscribed;

        protected override string Owner => "Honeyguide";

        public static HoneyguideCrystalMorph Begin(in CrystalForgeOrigin origin, IVesselStatus status)
        {
            if (status == null) return null;
            if (!origin.Valid)
            {
                CSDebug.LogWarning("[HoneyguideCrystalMorph] the pickup carried no crystal origin, so there is no body " +
                                   "to morph — this pickup retires with no animation.");
                return null;
            }

            var crystal = ResolveCrystal(origin);
            if (crystal != null && !crystal.HasBody) return null;
            if (crystal == null)
            {
                CSDebug.LogWarning($"[HoneyguideCrystalMorph] no crystal with id {origin.CrystalId} on this peer, so " +
                                   "there is no body to carry into the hive — this pickup retires with no animation.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<HoneyguideCrystalMorph>();
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[HoneyguideCrystalMorph] '{crystal.name}' exposed no drawable body — nothing to " +
                                   "morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._status = status;
            morph._collectPosition = origin.Position;
            morph.BeginWaiting();
            HoneyguideCrew.CombLaid += morph.OnCombLaid;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Honeyguide: began on '{crystal.name}' at {origin.Position}, {morph.Census}; waiting " +
                $"up to {morph.Config.TargetGraceSeconds:F2}s for the hive comb.");
            return morph;
        }

        bool IsThisVessel(IVesselStatus other)
        {
            if (other == null || _status == null) return false;
            return ReferenceEquals(other, _status) || (other.Transform && other.Transform == _status.Transform);
        }

        void OnCombLaid(IVesselStatus status, IReadOnlyList<Prism> comb)
        {
            if (IsStamped || comb == null || !IsThisVessel(status)) return;

            var positions = new List<Vector3>(comb.Count);
            for (int i = 0; i < comb.Count; i++) positions.Add(comb[i] ? comb[i].transform.position : Vector3.zero);
            var picked = HiveCombMorphGeometry.CentralCells(positions, HiveCombMorphGeometry.CellsCrystalFills);
            if (picked.Count == 0)
            {
                CSDebug.LogWarning($"[HoneyguideCrystalMorph] the comb has {comb.Count} cells, fewer than the " +
                                   $"{HiveCombMorphGeometry.CellsCrystalFills} the crystal's panels fill, so the crystal " +
                                   "will fade instead; the hive is unaffected.");
                return;
            }

            // The comb is laid hiveOffset (40 u) behind where the crystal was; a later pickup's comb is not this one.
            Vector3 middle = Vector3.zero;
            foreach (int i in picked) middle += positions[i];
            middle /= picked.Count;
            float distance = Vector3.Distance(middle, _collectPosition);
            if (distance > Config.RingCaptureRadius)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] Honeyguide: skipped a comb {distance:F0}u from the collect " +
                    $"(capture radius {Config.RingCaptureRadius:F0}u).");
                return;
            }

            var cells = new List<Prism>(picked.Count);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget>(picked.Count);
            foreach (int i in picked)
            {
                if (!TryResolveCell(comb[i], out var target, out string refusal))
                {
                    CSDebug.LogWarning($"[HoneyguideCrystalMorph] cannot measure comb cell {i}: {refusal}. The " +
                                       "crystal will fade instead; the hive is unaffected.");
                    return;
                }
                cells.Add(comb[i]);
                targets.Add(target);
            }

            var mesh = CrystalMorphMeshBuilder.TryBuild(SourceMesh, targets, Config.FillerPhase,
                                                        Config.PanelPhaseStart,
                                                        Mathf.Max(Config.PanelPhaseStart, Config.PanelPhaseEnd),
                                                        out string diagnosis);
            if (mesh == null)
            {
                CSDebug.LogWarning($"[HoneyguideCrystalMorph] cannot morph this crystal onto the comb: {diagnosis}. " +
                                   "The crystal will fade instead; the hive is unaffected.");
                return;
            }

            Unsubscribe();
            CaptureTargetColour(cells);
            foreach (var cell in cells)
            {
                cell.SetOwnerHidden(true);
                _held.Add(cell);
            }
            Stamp(mesh);

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Honeyguide: took the comb {distance:F1}u from the collect — {_held.Count} of " +
                $"{comb.Count} cells held, {MorphVertexCount} morph vertices, colour target " +
                $"{(_haveTargetColour ? "read" : "NOT FOUND")}.");
        }

        /// <summary>
        /// One comb cell's landing octahedron, in the body's frame: the octahedron a super shield's stellation is built
        /// around (vertices at the spike-tip cube's face centres, so semi-axes equal to the cube's half-extents), at the
        /// prism's FINAL scale.
        /// </summary>
        bool TryResolveCell(Prism prism, out CrystalMorphMeshBuilder.OctahedronTarget target, out string refusal)
        {
            target = default;
            refusal = null;
            if (!prism) { refusal = "the prism is gone"; return false; }
            if (!prism.TryGetComponent<PrismStellatedOctahedronShield>(out var shield))
            {
                refusal = "no PrismStellatedOctahedronShield (its super shield had not engaged)";
                return false;
            }

            Vector3 scale = prism.TargetScale;
            if (scale == Vector3.zero) scale = prism.transform.localScale;
            var toLocal = BodyFrame.worldToLocalMatrix *
                          Matrix4x4.TRS(prism.transform.position, prism.transform.rotation, scale);
            target = CrystalMorphMeshBuilder.OctahedronTarget.FromSemiAxes(
                toLocal, shield.ShellCenterLocal, shield.ShellSemiAxesLocal);
            return true;
        }

        /// <summary>The pair the morph arrives wearing: the hull's own domain cell's, else the first held cell's. The comb
        /// is many domains, so the other cells change colour at the hand-off (the doc's §3).</summary>
        void CaptureTargetColour(List<Prism> cells)
        {
            Prism pick = null;
            foreach (var c in cells) if (c && c.Domain == _status.Domain) { pick = c; break; }
            if (!pick && cells.Count > 0) pick = cells[0];
            if (!pick || !pick.TryGetComponent<MeshRenderer>(out var r)) return;
            var mat = r.sharedMaterial;
            if (mat == null || !mat.HasProperty(DarkColorId) || !mat.HasProperty(BrightColorId)) return;
            _targetDark = mat.GetColor(DarkColorId);
            _targetBright = mat.GetColor(BrightColorId);
            _haveTargetColour = true;
        }

        protected override bool TryReadTargetColours(out Color dark, out Color bright)
        {
            dark = _targetDark;
            bright = _targetBright;
            return _haveTargetColour;
        }

        /// <summary>The plates have landed. Settle the held cells' own arrivals while they are still hidden (the crystal
        /// covers them), then reveal them; from here the comb draws itself.</summary>
        protected override void HandOff()
        {
            foreach (var prism in _held)
            {
                if (!prism || prism.destroyed || !prism.gameObject.activeInHierarchy) continue;
                if (!prism.IsCreationComplete) prism.CompleteCreationImmediately();
                prism.CompleteGrowthImmediately();
                if (prism.TryGetComponent<PrismStellatedOctahedronShield>(out var shield) && shield.IsShielded)
                    shield.Engage(instant: true);
            }
            ReleaseHold();
        }

        /// <summary>Gives the cells their photons back. Idempotent and called from teardown — an unreleased hold would
        /// leave invisible, invulnerable hive cells in the world.</summary>
        protected override void ReleaseHold()
        {
            foreach (var prism in _held) if (prism) prism.SetOwnerHidden(false);
            _held.Clear();
        }

        protected override void OnGaveUp()
        {
            Unsubscribe();
            // The comb is laid on the hull's own machine only, so every other peer's crystal fades: ordinary, not a fault.
            if (!_status.IsNetworkOwner)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    "[CrystalMorph] Honeyguide: the comb is laid on the pilot's machine; this peer's crystal fades.");
                return;
            }
            if (_status.Transform && _status.Transform.TryGetComponent(out HoneyguideCrew crew) && crew.HiveOpen)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    "[CrystalMorph] Honeyguide: a hive is already open, so this crystal opens none; it fades.");
                return;
            }
            CSDebug.LogWarning(
                $"[HoneyguideCrystalMorph] no hive comb from this Honeyguide was laid within " +
                $"{Config.TargetGraceSeconds:F2}s, so the crystal is fading out instead of morphing. The comb is laid by " +
                "HoneyguideCrew.EnterHive (from HoneyguideOmniCrystalEffect) once any pick of three is done, and never while " +
                "a hive is already open or hive mode is off. Trace with FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            HoneyguideCrew.CombLaid -= OnCombLaid;
            _subscribed = false;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }
    }
}
