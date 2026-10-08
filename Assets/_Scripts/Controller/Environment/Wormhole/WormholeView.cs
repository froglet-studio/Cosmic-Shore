using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The once-a-frame driver behind every <see cref="WormholeMouth"/>: it decides which mouths
    /// get an exact view this frame and which panoramas need refreshing, and it keeps the
    /// illusion intact at the one moment it is hardest — a ship half-way through.
    ///
    /// <para><b>The budget.</b> At most <see cref="MaxExactRendersPerFrame"/> exact views per
    /// frame (the nearest on-screen mouths inside their range), each costing one render of the
    /// world at the mouth's footprint on screen; and one panorama FACE per mouth whose partner is
    /// on screen and not already fully covered by its exact view — a 90° square at the authored
    /// face size. No shadows, no anti-aliasing, no post
    /// (the gameplay camera's own post runs over the composited sphere, once). A mouth the player
    /// cannot see costs nothing — and neither does a SEALED one (an unpaired mouth withering
    /// away), which shows no view through. This is the Butterfly's retired fold-gate window's
    /// cost model, widened to a sphere the player can see two of.</para>
    ///
    /// <para><b>Everyone may look through.</b> A view through is a promise you can go there, and
    /// every pilot can: a mouth carries anyone, and only CHARGES a pilot of another domain
    /// (<see cref="WormholeMouth.LevyToll"/>). So every viewer gets the view; the rim's domain hue
    /// is what says whose road it is.</para>
    ///
    /// <para><b>Which camera is looking.</b> The exact view is valid only for the camera it was
    /// rendered for, so the surface uses it only while THAT camera is drawing:
    /// <c>_WormholeMainView</c> is set to 1 in <c>beginCameraRendering</c> for the gameplay
    /// camera and to 0 for every other one — the mouths' own eyes, preview cameras, the editor's
    /// scene view — which see the panorama instead.</para>
    ///
    /// <para><b>A ship in the shared interior is seen through either mouth.</b> The two balls are
    /// one place, so a vessel inside (or straddling) ball M is, to a viewer looking through M, a
    /// vessel inside the partner's ball — and M's exact render draws every vessel that
    /// is in or cut by M at its mapped position on the far side. That is what shows a nose
    /// disappearing into a mouth in the window, and what shows a Butterfly that has just folded,
    /// sitting at the centre of the destination mouth laid around it, THROUGH that mouth rather
    /// than hidden behind it. After a transit the camera is still on the near side and the ship's
    /// TAIL still sticks out of the far ball's near face, so the gameplay camera's render draws the
    /// followed ship carried back. Each move brackets a single render and is undone before anything
    /// else runs.</para>
    /// </summary>
    public static class WormholeView
    {
        /// <summary>Exact views rendered per frame, nearest first. Two: you can see both mouths
        /// of one wormhole at once, and the second render is what keeps the far one exact.</summary>
        public const int MaxExactRendersPerFrame = 2;

        static readonly int MainViewId = Shader.PropertyToID("_WormholeMainView");
        static readonly int ClearanceId = Shader.PropertyToID("_WormholeClearance");
        static readonly Plane[] FrustumPlanes = new Plane[6];
        static readonly List<WormholeMouth> Candidates = new();
        static readonly List<WormholeMouth> OnScreen = new();
        static readonly List<bool> HiddenScratch = new();
        static readonly List<Transform> MovedRoots = new();
        static readonly List<Vector3> MovedSaved = new();
        static readonly Dictionary<Transform, float> HullRadii = new();
        static readonly List<Transform> DeadRadii = new();

        static GameObject _host;
        static Camera _mainView;

        static Transform _subjectKey;
        static Transform _subjectRoot;
        static float _subjectRadius;

        // The borrowed pose for the gameplay camera's render during a carry.
        static WormholeMouth _carriedMouth;
        static bool _mainMoved;
        static Vector3 _mainSaved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // Statics survive play-mode exit in the editor.
            _mainView = null;
            _subjectKey = null;
            _subjectRoot = null;
            _subjectRadius = 0f;
            HullRadii.Clear();
            _carriedMouth = null;
            _mainMoved = false;
            Shader.SetGlobalFloat(MainViewId, 0f);

            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;

            // HideInHierarchy, not HideAndDontSave (that exempts it from play-mode-exit cleanup).
            _host = new GameObject("[WormholeView]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<Driver>();
        }

        /// <summary>
        /// After every camera has posed (CustomCameraController poses in its LateUpdate), after
        /// the occlusion corridor publishes (10000) and after the fold gate window (10100), whose
        /// render this one must not interleave with.
        /// </summary>
        [DefaultExecutionOrder(10150)]
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Tick();
        }

        static void Tick()
        {
            // A borrowed pose never outlives the render it was borrowed for.
            RestoreMainPose();
            _carriedMouth = null;

            var live = WormholeMouth.Live;
            if (live.Count == 0) { _mainView = null; return; }

            var controller = ResolveController();
            var view = controller != null && controller.Camera ? controller.Camera : Camera.main;
            _mainView = view && view.isActiveAndEnabled ? view : null;

            for (int i = 0; i < live.Count; i++)
            {
                var m = live[i];
                if (!m) continue;
                m.ExactBlend = 0f;
                m.PanoramaWanted = false;
            }

            if (_mainView)
            {
                Shader.SetGlobalFloat(ClearanceId, WormholeGeometry.Clearance(_mainView.nearClipPlane));
                ResolveSubject(controller);
                SelectAndRender(_mainView, controller);
            }

            for (int i = 0; i < live.Count; i++)
                if (live[i]) live[i].ApplySurface();
        }

        static void SelectAndRender(Camera view, CustomCameraController controller)
        {
            var live = WormholeMouth.Live;
            GeometryUtility.CalculateFrustumPlanes(view, FrustumPlanes);
            Vector3 eye = view.transform.position;
            float clearance = WormholeGeometry.Clearance(view.nearClipPlane);

            // --- who is on screen ------------------------------------------------------------
            Candidates.Clear();
            OnScreen.Clear();
            for (int i = 0; i < live.Count; i++)
            {
                var m = live[i];
                if (!m || !m.Partner) continue;
                float r = m.Radius;
                if (r <= 0f) continue;
                if (!GeometryUtility.TestPlanesAABB(FrustumPlanes, new Bounds(m.Centre, Vector3.one * (2f * r))))
                    continue;

                OnScreen.Add(m);

                // A camera inside a mouth's clearance does not draw that mouth at all (the shader
                // drops it), so there is no view through it to render.
                float d = Vector3.Distance(eye, m.Centre);
                if (d <= r + clearance) continue;
                if (d > m.ExactRange + m.ExactFadeBand) continue;
                Candidates.Add(m);
            }

            // The mouth the camera is being carried through always gets its exact view: it is the
            // only place the pilot's ship can be seen until the camera follows it across.
            if (controller != null && controller.IsCarryingThroughPortal)
            {
                Vector3 mouth = controller.CarryMouthCentre;
                for (int i = 0; i < live.Count; i++)
                {
                    var m = live[i];
                    if (!m || !m.Partner || (m.Centre - mouth).sqrMagnitude > 1f) continue;
                    _carriedMouth = m;
                    if (!Candidates.Contains(m)) Candidates.Add(m);
                    break;
                }
            }

            Candidates.Sort((a, b) =>
            {
                if (a == b) return 0;
                if (a == _carriedMouth) return -1;
                if (b == _carriedMouth) return 1;
                return (a.Centre - eye).sqrMagnitude.CompareTo((b.Centre - eye).sqrMagnitude);
            });

            // --- the exact views ------------------------------------------------------------
            float tierScale = TierMaxRenderScale;
            int rendered = 0;
            for (int i = 0; i < Candidates.Count && rendered < MaxExactRendersPerFrame; i++)
            {
                var m = Candidates[i];
                if (!RenderExact(m, view, tierScale)) continue;
                rendered++;

                float d = Vector3.Distance(eye, m.Centre);
                m.ExactBlend = m == _carriedMouth
                    ? 1f
                    : 1f - Mathf.Clamp01((d - m.ExactRange) / Mathf.Max(1f, m.ExactFadeBand));
            }

            // --- the panoramas --------------------------------------------------------------
            // A mouth on screen shows its PARTNER's panorama wherever its exact view does not
            // cover it fully - and a fully exact mouth still keeps the capture warm until all six
            // faces exist, so stepping back out of range never fades into an empty picture.
            for (int i = 0; i < OnScreen.Count; i++)
            {
                var m = OnScreen[i];
                if (m.ExactBlend < 0.999f || !m.Partner.PanoramaReady) m.Partner.PanoramaWanted = true;
            }
            for (int i = 0; i < live.Count; i++)
            {
                var m = live[i];
                if (!m || !m.PanoramaWanted) continue;
                HideAllMouths();
                m.RenderNextPanoramaFace(view);
                RestoreMouths();
            }
        }

        static bool RenderExact(WormholeMouth m, Camera view, float tierScale)
        {
            var partner = m.Partner;
            Vector3 delta = partner.Centre - m.Centre;

            HideAllMouths();

            // The occlusion corridor opens onto the pilot's ship from the camera that is drawing;
            // for this render that camera is through the pair, so the ship is taken through too.
            if (PrismOcclusionCorridor.Target)
                PrismOcclusionCorridor.PublishTargetPosition(PrismOcclusionCorridor.ViewTargetPosition + delta);

            // Every vessel this mouth may carry that is in or cut by its ball is in the shared
            // interior, so for this render it is drawn where the far side has it.
            MoveInteriorVessels(m, delta);

            bool ok = m.RenderExact(view, tierScale);

            RestoreInteriorVessels();
            PrismOcclusionCorridor.RepublishTarget();
            RestoreMouths();
            return ok;
        }

        // ---- hiding the mouths inside a wormhole render ------------------------------------

        /// <summary>
        /// Every mouth's surface is switched off inside every wormhole render: a mouth samples the
        /// very targets those renders draw into, and the far one sits on the exact view's clip
        /// plane where it would flicker.
        /// </summary>
        static void HideAllMouths()
        {
            var live = WormholeMouth.Live;
            HiddenScratch.Clear();
            for (int i = 0; i < live.Count; i++)
            {
                var surface = live[i] ? live[i].Surface : null;
                HiddenScratch.Add(surface && surface.forceRenderingOff);
                if (surface) surface.forceRenderingOff = true;
            }
        }

        static void RestoreMouths()
        {
            var live = WormholeMouth.Live;
            for (int i = 0; i < live.Count && i < HiddenScratch.Count; i++)
            {
                var surface = live[i] ? live[i].Surface : null;
                if (surface) surface.forceRenderingOff = HiddenScratch[i];
            }
        }

        // ---- the straddling ship -----------------------------------------------------------

        /// <summary>The followed ship's root (the transform a teleport moves) and its hull radius.</summary>
        static void ResolveSubject(CustomCameraController controller)
        {
            var target = controller != null ? controller.FollowTarget : null;
            if (target != _subjectKey)
            {
                _subjectKey = target;
                var root = target ? target.GetComponentInParent<VesselTransformer>() : null;
                _subjectRoot = root ? root.transform : null;
                _subjectRadius = 0f;
            }
            // Re-asked while it reads zero: a hull measured before its art is on answers 0.
            if (_subjectRoot && _subjectRadius <= 0f)
                _subjectRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(_subjectRoot);
        }

        static void MoveInteriorVessels(WormholeMouth m, Vector3 delta)
        {
            MovedRoots.Clear();
            MovedSaved.Clear();
            var players = m.Players;
            if (players == null) return;
            Vector3 c = m.Centre;
            float r = m.Radius;
            for (int i = 0; i < players.Count; i++)
            {
                var vessel = players[i]?.Vessel;
                if (vessel == null) continue;
                var root = vessel.Transform;
                if (!root || MovedRoots.Contains(root)) continue;
                float reach = r + HullRadius(root);
                Vector3 p = root.position;
                if ((p - c).sqrMagnitude >= reach * reach) continue;
                MovedRoots.Add(root);
                MovedSaved.Add(p);
                root.position = p + delta;
            }
        }

        static void RestoreInteriorVessels()
        {
            for (int i = 0; i < MovedRoots.Count; i++)
                if (MovedRoots[i]) MovedRoots[i].position = MovedSaved[i];
            MovedRoots.Clear();
            MovedSaved.Clear();
        }

        /// <summary>A vessel's hull radius (the occlusion corridor's own measurement), cached;
        /// re-asked while it reads zero, since a hull measured before its art is on answers 0.</summary>
        static float HullRadius(Transform root)
        {
            if (HullRadii.TryGetValue(root, out var r) && r > 0f) return r;
            r = PrismOcclusionCorridor.MeasureCircumscribedRadius(root);
            if (HullRadii.Count > 64) PruneRadii();
            HullRadii[root] = r;
            return r;
        }

        static void PruneRadii()
        {
            DeadRadii.Clear();
            foreach (var key in HullRadii.Keys) if (!key) DeadRadii.Add(key);
            for (int i = 0; i < DeadRadii.Count; i++) HullRadii.Remove(DeadRadii[i]);
        }

        /// <summary>Is the followed ship's hull cut by this mouth's sphere?</summary>
        static bool Straddles(WormholeMouth m)
        {
            if (!_subjectRoot || _subjectRadius <= 0f || !m) return false;
            float d = Vector3.Distance(_subjectRoot.position, m.Centre);
            return Mathf.Abs(d - m.Radius) < _subjectRadius;
        }

        /// <summary>
        /// The gameplay camera is about to draw from the near side of a mouth its ship has already
        /// gone through. If the hull is still cut by the FAR sphere, draw it at the near mouth for
        /// this one render, so its tail is still in front of the sphere it is flying into.
        /// </summary>
        static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            bool main = cam != null && cam == _mainView;
            Shader.SetGlobalFloat(MainViewId, main ? 1f : 0f);
            if (!main || _mainMoved) return;

            var near = _carriedMouth;
            if (!near) return;
            var far = near.Partner;
            if (!far || !Straddles(far)) return;

            _mainSaved = _subjectRoot.position;
            _subjectRoot.position = _mainSaved - (far.Centre - near.Centre);
            _mainMoved = true;
        }

        static void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != null && cam == _mainView) RestoreMainPose();
            // Anything drawn after the gameplay camera (an overlay, a preview) is not it.
            Shader.SetGlobalFloat(MainViewId, 0f);
        }

        static void RestoreMainPose()
        {
            if (!_mainMoved) return;
            _mainMoved = false;
            if (_subjectRoot) _subjectRoot.position = _mainSaved;
        }

        static CustomCameraController ResolveController()
        {
            var manager = CameraManager.Instance;
            if (manager == null) return null;
            return manager.GetActiveController() as CustomCameraController;
        }

        /// <summary>The device tier's ceiling on an exact view's render scale —
        /// <c>PlatformProfileSO.FoldGateWindowMaxRenderScale</c> (1 = none).</summary>
        static float TierMaxRenderScale
        {
            get
            {
                var profile = CosmicShore.Core.PlatformProfile.Current;
                return profile ? profile.FoldGateWindowMaxRenderScale : 1f;
            }
        }
    }
}
