using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The connecting screen's live window onto the arena being built — the same idea as the arcade
    /// card's preview, pointed at the world this scene is actually standing up.
    ///
    /// <para>It is the one thing on the panel that answers "is it doing anything?" honestly. The
    /// status line and the bar are readouts of counters; this is the arena itself, laying and
    /// blooming in, so a long load reads as a world being built rather than as a hang.</para>
    ///
    /// <para><b>It renders into a RenderTexture, never to the screen.</b> A camera with a
    /// <c>targetTexture</c> never draws to the display at all, so this one cannot fight the panel's
    /// own backdrop camera or the gameplay camera behind it — no depth ordering to get right.</para>
    ///
    /// <para><b>The camera is created at runtime when none is wired</b>, because the interesting
    /// half of this is WHERE it looks, not which prefab object it is: the cell does not exist yet
    /// when the panel comes up, so the aim has to be resolved every frame until one appears.
    /// Wiring one is still supported for a scene that wants a specific pose or culling mask.</para>
    /// </summary>
    public class ConnectingArenaPreview : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Tooltip("The RawImage the arena renders into. Without one this component " +
                                 "does nothing at all — the panel simply shows no preview.")]
        RawImage surface;

        [Header("Camera")]
        [SerializeField, Tooltip("Optional. Left empty, one is created at runtime and posed by " +
                                 "this component. Wire one only to pin a specific culling mask or " +
                                 "post-processing setup.")]
        Camera previewCamera;

        [SerializeField, Tooltip("Copied for culling mask, clear flags and background when the " +
                                 "camera is created at runtime. Normally the panel's own backdrop " +
                                 "camera, so the preview matches what the panel already shows.")]
        Camera settingsTemplate;

        [Header("Framing")]
        [SerializeField, Tooltip("Orbit radius as a fraction of the cell's membrane radius. Above " +
                                 "1 sits outside the membrane looking in, which is what shows the " +
                                 "arena as a whole rather than a wall of it.")]
        [Min(0.1f)] float radiusMembraneFraction = 1.35f;

        [SerializeField, Tooltip("Radius used before a cell exists, and when one has no membrane.")]
        [Min(1f)] float fallbackRadius = 900f;

        [SerializeField, Tooltip("Degrees per second around the cell. Slow: the subject is the " +
                                 "arena appearing, and a fast orbit competes with it.")]
        float orbitDegreesPerSecond = 6f;

        [SerializeField, Tooltip("Degrees above the cell's equator.")]
        [Range(-80f, 80f)] float pitchDegrees = 18f;

        [SerializeField, Min(20f)] float fieldOfView = 55f;

        [Header("Render texture")]
        [SerializeField, Tooltip("Render height in pixels. Deliberately modest — this runs during " +
                                 "the heaviest frames of the whole session, and the surface is a " +
                                 "small panel inset.")]
        [Range(120, 720)] int renderHeight = 320;

        RenderTexture _renderTexture;
        Camera _ownedCamera;
        float _angle;
        bool _running;

        /// <summary>Bring the preview up. Safe to call with nothing wired.</summary>
        public void Begin()
        {
            if (!surface) return;

            EnsureRenderTexture();
            var cam = EnsureCamera();
            if (!cam) return;

            cam.targetTexture = _renderTexture;
            cam.enabled = true;

            surface.texture = _renderTexture;
            surface.enabled = true;

            _running = true;
            AimAtArena(0f);
        }

        /// <summary>
        /// Take it down. Called when the panel hides, and from OnDisable/OnDestroy — a RenderTexture
        /// is a GPU allocation, and one left bound to a camera that outlives the panel is both a
        /// leak and a camera still rendering the world every frame of the match.
        /// </summary>
        public void End()
        {
            _running = false;

            if (previewCamera)
            {
                previewCamera.targetTexture = null;
                previewCamera.enabled = false;
            }

            if (surface)
            {
                surface.texture = null;
                // Disabled rather than cleared: a RawImage with a null texture draws its colour as
                // a solid rectangle, which is the "white box" a blank preview used to show.
                surface.enabled = false;
            }

            ReleaseRenderTexture();
        }

        void OnDisable() => End();

        void OnDestroy()
        {
            End();
            if (_ownedCamera) Destroy(_ownedCamera.gameObject);
        }

        void LateUpdate()
        {
            if (!_running) return;
            AimAtArena(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Point the camera at whatever cell exists RIGHT NOW. Re-resolved every frame rather than
        /// once at Begin, because the panel comes up before the cell does — that is the whole point
        /// of the panel — so a one-shot lookup would frame empty space for the entire load.
        /// </summary>
        void AimAtArena(float deltaTime)
        {
            var cam = previewCamera;
            if (!cam) return;

            _angle += orbitDegreesPerSecond * deltaTime;

            Vector3 centre = Vector3.zero;
            float radius = fallbackRadius;

            var cell = Cell.FindNearestActiveCell(Vector3.zero);
            if (cell)
            {
                centre = cell.transform.position;
                float membrane = cell.MembraneRadius;
                if (membrane > 1f) radius = membrane * radiusMembraneFraction;
            }

            var rotation = Quaternion.Euler(pitchDegrees, _angle, 0f);
            cam.transform.SetPositionAndRotation(centre + rotation * (Vector3.back * radius),
                                                 rotation);
            cam.fieldOfView = fieldOfView;
        }

        Camera EnsureCamera()
        {
            if (previewCamera) return previewCamera;

            var go = new GameObject("[ConnectingArenaPreviewCamera]");
            go.transform.SetParent(transform, worldPositionStays: false);

            _ownedCamera = go.AddComponent<Camera>();
            _ownedCamera.enabled = false;

            if (settingsTemplate)
            {
                _ownedCamera.cullingMask = settingsTemplate.cullingMask;
                _ownedCamera.clearFlags = settingsTemplate.clearFlags;
                _ownedCamera.backgroundColor = settingsTemplate.backgroundColor;
                _ownedCamera.nearClipPlane = settingsTemplate.nearClipPlane;
                _ownedCamera.farClipPlane = settingsTemplate.farClipPlane;
            }
            else
            {
                // Everything except UI: a preview that rendered the canvas would draw the panel
                // inside its own window, one frame stale, forever.
                int ui = LayerMask.NameToLayer("UI");
                _ownedCamera.cullingMask = ui >= 0 ? ~(1 << ui) : ~0;
                _ownedCamera.clearFlags = CameraClearFlags.Skybox;
                _ownedCamera.farClipPlane = 20000f;
            }

            previewCamera = _ownedCamera;
            return previewCamera;
        }

        void EnsureRenderTexture()
        {
            if (_renderTexture) return;

            float aspect = 16f / 9f;
            if (surface && surface.rectTransform.rect.height > 1f)
                aspect = Mathf.Clamp(
                    surface.rectTransform.rect.width / surface.rectTransform.rect.height, 0.25f, 4f);

            _renderTexture = new RenderTexture(
                Mathf.Max(64, Mathf.RoundToInt(renderHeight * aspect)), renderHeight, 24)
            {
                name = "ConnectingArenaPreviewRT",
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
            };
        }

        void ReleaseRenderTexture()
        {
            if (!_renderTexture) return;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
